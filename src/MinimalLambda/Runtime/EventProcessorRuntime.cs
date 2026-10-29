using System.Collections.ObjectModel;
using System.Runtime.ExceptionServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MinimalLambda.Runtime;

/// <summary>Provides the runtime used by generated event-handler registrations.</summary>
internal sealed class EventProcessorRuntime
{
    private readonly int _concurrency;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger? _logger;

    /// <summary>Captures event-handler options and services at registration time.</summary>
    /// <param name="services">The application's service provider.</param>
    internal EventProcessorRuntime(IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(services);
        var options = services.GetRequiredService<IOptions<EventHandlerOptions>>().Value;
        _concurrency = options.EnableConcurrency ? options.MaxConcurrentExecutions : 1;
        _scopeFactory = services.GetRequiredService<IServiceScopeFactory>();
        _logger = services
            .GetService<ILoggerFactory>()
            ?.CreateLogger("MinimalLambda.EventProcessor");
    }

    /// <summary>Runs a generated event adapter with per-item scopes and ordered response contributions.</summary>
    /// <typeparam name="TEvent">The invocation event type.</typeparam>
    /// <typeparam name="TItem">The source item type.</typeparam>
    /// <typeparam name="TValue">The response contribution type.</typeparam>
    /// <param name="context">The original invocation context.</param>
    /// <param name="split">The source item splitter.</param>
    /// <param name="bind">Resolves handler arguments and returns the bound handler.</param>
    /// <param name="selectSuccess">Selects a successful contribution, or null to discard successes.</param>
    /// <param name="onError">Maps handler failures, or null to fail the invocation.</param>
    /// <param name="complete">Builds and stores the response, or null for handlers without a response.</param>
    internal async Task RunAsync<TEvent, TItem, TValue>(
        ILambdaInvocationContext context,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, IServiceProvider, IEventProcessingContext, Func<Task>> bind,
        Func<TItem, Task, TValue>? selectSuccess,
        Func<TItem, Exception, IEventProcessingContext, TValue>? onError,
        Action<IEnumerable<TValue>>? complete)
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

        context.CancellationToken.ThrowIfCancellationRequested();
        using var state = new EventProcessingState(context, _logger, items.Count);
        var outcomes =
            await EventProcessorWorkers
                .RunAsync(items, _concurrency, _scopeFactory, state, Adapt, complete is not null)
                .ConfigureAwait(false);

        if (complete is not null)
        {
            var contributions = new List<TValue>(outcomes!.Length);
            foreach (var outcome in outcomes)
                if (outcome.HasValue)
                    contributions.Add(outcome.Value!);
            complete(new ReadOnlyCollection<TValue>(contributions));
        }

        async Task<ItemOutcome<TValue>> Adapt(
            TItem item,
            IServiceProvider services,
            EventProcessingState processing)
        {
            // Binding failures are fatal and must not be passed to the handler error mapper.
            var handle = bind(item, services, processing);
            var invocationToken = processing.Invocation.CancellationToken;
            invocationToken.ThrowIfCancellationRequested();
            if (processing.CancellationToken.IsCancellationRequested)
                return default;

            Task? handlerTask = null;
            Exception? synchronousFault = null;
            try
            {
                handlerTask = handle();
            }
            catch (Exception ex)
            {
                synchronousFault = ex;
            }

            if (synchronousFault is null && handlerTask is null)
                throw new InvalidOperationException(
                    "The event handler 'handle' returned a null task.");

            try
            {
                if (synchronousFault is not null)
                    ExceptionDispatchInfo.Capture(synchronousFault).Throw();
                await handlerTask!.ConfigureAwait(false);
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
                    return default;
                if (onError is null)
                    throw;

                invocationToken.ThrowIfCancellationRequested();
                var mapped = onError(item, ex, processing);
                if (invocationToken.IsCancellationRequested)
                    throw new OperationCanceledException(
                        "The Lambda invocation was cancelled while mapping an item error.",
                        invocationToken);
                return ItemOutcome<TValue>.FromValue(mapped);
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
