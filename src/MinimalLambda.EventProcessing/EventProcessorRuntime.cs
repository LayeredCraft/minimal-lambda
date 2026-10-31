using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace MinimalLambda.EventProcessing;

/// <summary>Provides the runtime used by generated event-handler registrations.</summary>
internal sealed class EventProcessorRuntime
{
    private readonly int _concurrency;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger? _logger;

    internal EventProcessorRuntime(
        int concurrency,
        IServiceScopeFactory scopeFactory,
        ILogger? logger)
    {
        _concurrency = concurrency;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Runs a generated event adapter with per-item scopes and ordered response contributions.</summary>
    /// <typeparam name="TEvent">The invocation event type.</typeparam>
    /// <typeparam name="TItem">The source item type.</typeparam>
    /// <typeparam name="TValue">The response contribution type.</typeparam>
    /// <param name="context">The original invocation context.</param>
    /// <param name="split">The source item splitter.</param>
    /// <param name="handle">Resolves arguments and runs the item handler.</param>
    /// <param name="selectSuccess">Selects a successful contribution, or null to discard successes.</param>
    /// <param name="onError">Maps handler failures, or null to fail the invocation.</param>
    /// <param name="complete">Builds and stores the response, or null for handlers without a response.</param>
    internal async Task RunAsync<TEvent, TItem, TValue>(
        ILambdaInvocationContext context,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, IServiceProvider, IEventProcessingContext, Task> handle,
        Func<TItem, Task, TValue>? selectSuccess,
        Func<TItem, Exception, IEventProcessingContext, TValue>? onError,
        Action<IReadOnlyList<EventItemOutcome<TItem, TValue>>>? complete)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        _ = context.ServiceProvider;
        var splitItems = split(context.GetRequiredEvent<TEvent>(), context);
        if (splitItems is null)
            throw new InvalidOperationException("The event splitter 'split' returned null.");

        var items = new List<TItem>();
        using (var enumerator = splitItems.GetEnumerator())
            while (enumerator.MoveNext())
            {
                context.CancellationToken.ThrowIfCancellationRequested();
                items.Add(enumerator.Current);
            }

        var itemArray = items.ToArray();
        context.CancellationToken.ThrowIfCancellationRequested();
        using var state = new EventProcessingState(context, _logger, itemArray.Length);
        var outcomes =
            await EventProcessorWorkers
                .RunAsync(
                    itemArray,
                    _concurrency,
                    _scopeFactory,
                    state,
                    Adapt,
                    complete is not null)
                .ConfigureAwait(false);

        context.CancellationToken.ThrowIfCancellationRequested();
        if (complete is not null)
        {
            complete(Array.AsReadOnly(outcomes!));
        }

        async Task<ItemOutcome<TValue>> Adapt(
            TItem item,
            IServiceProvider services,
            EventProcessingState processing)
        {
            var invocationToken = processing.Invocation.CancellationToken;
            invocationToken.ThrowIfCancellationRequested();
            if (processing.CancellationToken.IsCancellationRequested)
                return ItemOutcome<TValue>.Cancelled;

            Task? handlerTask = null;
            try
            {
                handlerTask = handle(item, services, processing);
                if (handlerTask is null)
                    throw new InvalidOperationException(
                        "The event handler 'handle' returned a null task.");
                await handlerTask.ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                if (invocationToken.IsCancellationRequested)
                    throw new OperationCanceledException(
                        "The Lambda invocation was cancelled while processing an item.",
                        ex,
                        invocationToken);
                if (ex is OperationCanceledException
                    && processing.CancellationToken.IsCancellationRequested)
                    return ItemOutcome<TValue>.Cancelled;
                if (onError is null)
                    throw;

                invocationToken.ThrowIfCancellationRequested();
                var mapped = onError(item, ex, processing);
                if (invocationToken.IsCancellationRequested)
                    throw new OperationCanceledException(
                        "The Lambda invocation was cancelled while mapping an item error.",
                        invocationToken);
                return ItemOutcome<TValue>.FromError(mapped, ex);
            }

            if (invocationToken.IsCancellationRequested)
                throw new OperationCanceledException(
                    "The Lambda invocation was cancelled while processing an item.",
                    invocationToken);
            return selectSuccess is null
                ? default
                : ItemOutcome<TValue>.FromValue(selectSuccess(item, handlerTask!));
        }
    }
}
