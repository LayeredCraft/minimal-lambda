using System.Runtime.ExceptionServices;
using Microsoft.Extensions.Logging;

namespace MinimalLambda.Runtime;

internal sealed class EventProcessingState : IEventProcessingContext, IDisposable
{
    private readonly object _gate = new();
    private readonly ILambdaInvocationContext _invocation;
    private readonly ILogger? _logger;
    private readonly int _itemCount;
    private readonly CancellationTokenSource _processingSource = new();
    private readonly CancellationTokenRegistration _invocationRegistration;

    private int _nextIndex;
    private int _admitted;
    private bool _stopRequested;
    private bool _cancelRequested;
    private bool _handlingClosed;
    private bool _completed;
    private bool _invocationCancelled;
    private bool _disposed;
    private ExceptionDispatchInfo? _firstFatal;
    private List<Exception>? _additionalFatal;
    private TaskCompletionSource? _dispatchCompletion;

    internal EventProcessingState(
        ILambdaInvocationContext invocation,
        ILogger? logger,
        int itemCount)
    {
        _invocation = invocation;
        _logger = logger;
        _itemCount = itemCount;
        _invocationRegistration = invocation.CancellationToken.Register(
            static state => ((EventProcessingState)state!).OnInvocationTokenCancelled(),
            this);
    }

    public ILambdaInvocationContext Invocation => _invocation;

    public CancellationToken CancellationToken => _processingSource.Token;

    public void StopProcessing()
    {
        lock (_gate)
        {
            if (_completed || _handlingClosed)
                return;

            _stopRequested = true;
            TryCloseHandlingCore();
        }
    }

    public void CancelProcessing() => RequestProcessingCancellation(false);

    internal bool TryClaim(out int index)
    {
        TaskCompletionSource? dispatch = null;
        var claimed = false;
        index = -1;

        lock (_gate)
        {
            if (_completed)
                return false;

            ObserveInvocationCancellationCore(ref dispatch);

            if (_handlingClosed
                || _stopRequested
                || _firstFatal is not null
                || _nextIndex >= _itemCount)
            {
                TryCloseHandlingCore();
            }
            else
            {
                index = _nextIndex++;
                _admitted++;
                claimed = true;
            }
        }

        if (dispatch is not null)
            StartDispatch(dispatch);

        return claimed;
    }

    internal bool IsInvocationCancellationLatched
    {
        get
        {
            lock (_gate)
                return _invocationCancelled;
        }
    }

    internal void CompleteAdmission()
    {
        lock (_gate)
        {
            if (_admitted > 0)
                _admitted--;

            TryCloseHandlingCore();
        }
    }

    internal bool TryLatchInvocationCancellation()
    {
        if (!_invocation.CancellationToken.IsCancellationRequested)
            return false;

        RequestProcessingCancellation(true);

        lock (_gate)
            return _invocationCancelled;
    }

    internal void RecordFatal(Exception exception)
    {
        lock (_gate)
        {
            if (_completed)
                return;

            _stopRequested = true;
            var fatal = ExceptionDispatchInfo.Capture(exception);
            if (_firstFatal is null)
                _firstFatal = fatal;
            else
                (_additionalFatal ??= []).Add(exception);

            TryCloseHandlingCore();
        }
    }

    internal void TryCloseHandling()
    {
        lock (_gate)
            TryCloseHandlingCore();
    }

    internal async Task JoinAsync()
    {
        Task? dispatch;
        lock (_gate)
        {
            TryCloseHandlingCore();
            dispatch = _dispatchCompletion?.Task;
        }

        if (dispatch is not null)
            await dispatch.ConfigureAwait(false);

        Completion completion;
        lock (_gate)
        {
            TryCloseHandlingCore();
            _completed = true;
            completion = new Completion(_invocationCancelled, _firstFatal, _additionalFatal);
        }

        completion.LogAdditional(_logger);

        if (completion.InvocationCancelled)
            throw new OperationCanceledException(
                "The Lambda invocation was cancelled during item processing.",
                _invocation.CancellationToken);

        if (completion.FirstFatal is not null)
        {
            completion.FirstFatal.Throw();
            throw new InvalidOperationException("Unreachable");
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _invocationRegistration.Dispose();
        _processingSource.Dispose();
    }

    private void OnInvocationTokenCancelled() => RequestProcessingCancellation(true);

    private void RequestProcessingCancellation(bool invocationCancelled)
    {
        TaskCompletionSource? dispatch = null;
        lock (_gate)
        {
            if (_completed || _handlingClosed)
                return;

            if (invocationCancelled)
                _invocationCancelled = true;

            _stopRequested = true;

            if (!_cancelRequested)
            {
                _cancelRequested = true;
                _dispatchCompletion = new TaskCompletionSource(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                dispatch = _dispatchCompletion;
            }

            TryCloseHandlingCore();
        }

        if (dispatch is not null)
            StartDispatch(dispatch);
    }

    private void ObserveInvocationCancellationCore(ref TaskCompletionSource? dispatch)
    {
        if (_handlingClosed || !_invocation.CancellationToken.IsCancellationRequested)
            return;

        _invocationCancelled = true;
        _stopRequested = true;

        if (_cancelRequested)
            return;

        _cancelRequested = true;
        _dispatchCompletion = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        dispatch = _dispatchCompletion;
    }

    private void TryCloseHandlingCore()
    {
        if (_handlingClosed)
            return;

        if (_admitted > 0)
            return;

        if (_nextIndex < _itemCount
            && !_stopRequested
            && _firstFatal is null
            && !_invocation.CancellationToken.IsCancellationRequested)
            return;

        if (_invocation.CancellationToken.IsCancellationRequested)
        {
            _invocationCancelled = true;
            _stopRequested = true;
        }

        _handlingClosed = true;
    }

    private void StartDispatch(TaskCompletionSource completion) =>
        _ = DispatchCancellationAsync(completion);

    private async Task DispatchCancellationAsync(TaskCompletionSource completion)
    {
        try
        {
            await _processingSource.CancelAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            RecordFatal(ex);
        }
        finally
        {
            completion.TrySetResult();
        }
    }

    private readonly struct Completion(
        bool invocationCancelled,
        ExceptionDispatchInfo? firstFatal,
        List<Exception>? additionalFatal)
    {
        internal bool InvocationCancelled { get; } = invocationCancelled;

        internal ExceptionDispatchInfo? FirstFatal { get; } = firstFatal;

        internal void LogAdditional(ILogger? logger)
        {
            if (logger is null || additionalFatal is null)
                return;

            foreach (var exception in additionalFatal)
                try
                {
                    logger.LogError(
                        exception,
                        "Event processor observed an additional failure while draining item work.");
                }
                catch
                {
                    // Logging must not replace the selected invocation error.
                }
        }
    }
}
