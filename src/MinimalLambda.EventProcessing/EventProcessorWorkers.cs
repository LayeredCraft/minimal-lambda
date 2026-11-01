using Microsoft.Extensions.DependencyInjection;

namespace MinimalLambda.EventProcessing;

internal readonly struct ItemOutcome<TValue>
{
    private ItemOutcome(EventItemStatus status, bool hasValue, TValue? value, Exception? error)
    {
        Status = status;
        HasValue = hasValue;
        Value = value;
        Exception = error;
    }

    internal EventItemStatus Status { get; }
    internal bool HasValue { get; }
    internal TValue? Value { get; }
    internal Exception? Exception { get; }

    internal static ItemOutcome<TValue> FromValue(TValue? value) =>
        new(EventItemStatus.Succeeded, true, value, null);

    internal static ItemOutcome<TValue> FromError(TValue? value, Exception error) =>
        new(EventItemStatus.MappedError, true, value, error);

    internal static ItemOutcome<TValue> Cancelled =>
        new(EventItemStatus.Cancelled, false, default, null);
}

internal static class EventProcessorWorkers
{
    internal static async Task<EventItemOutcome<TItem, TValue>[]?> RunAsync<TItem, TValue>(
        List<TItem> items,
        int effectiveConcurrency,
        IServiceScopeFactory scopeFactory,
        EventProcessingState state,
        Func<TItem, IServiceProvider, EventProcessingState, Task<ItemOutcome<TValue>>> adapter,
        bool collectValues)
    {
        if (items.Count == 0)
            state.TryCloseHandling();

        var outcomes =
            collectValues
                ? items
                    .Select((item, index) =>
                        new EventItemOutcome<TItem, TValue>(index, item, EventItemStatus.Skipped))
                    .ToArray()
                : null;
        var workerCount = Math.Min(items.Count, Math.Max(1, effectiveConcurrency));
        var workers = new Task[workerCount];

        for (var index = 0; index < workerCount; index++)
            workers[index] = RunWorkerAsync();

        await Task.WhenAll(workers).ConfigureAwait(false);
        await state.JoinAsync().ConfigureAwait(false);

        return outcomes;

        async Task RunWorkerAsync()
        {
            while (state.TryClaim(out var index))
            {
                if (state.IsInvocationCancellationLatched)
                {
                    state.CompleteAdmission();
                    continue;
                }

                AsyncServiceScope scope;
                try
                {
                    scope = scopeFactory.CreateAsyncScope();
                }
                catch (Exception ex)
                {
                    state.RecordFatal(ex);
                    state.CompleteAdmission();
                    continue;
                }

                ItemOutcome<TValue> outcome = default;

                try
                {
                    try
                    {
                        outcome = await adapter(items[index], scope.ServiceProvider, state)
                            .ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        if (!state.TryLatchInvocationCancellation())
                            state.RecordFatal(ex);
                    }

                    try
                    {
                        await scope.DisposeAsync().ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        state.RecordFatal(ex);
                    }
                }
                finally
                {
                    state.CompleteAdmission();
                }

                if (collectValues)
                    outcomes![index] = new EventItemOutcome<TItem, TValue>(
                        index,
                        items[index],
                        outcome.Status,
                        outcome.HasValue,
                        outcome.Value,
                        outcome.Exception);
            }
        }
    }
}
