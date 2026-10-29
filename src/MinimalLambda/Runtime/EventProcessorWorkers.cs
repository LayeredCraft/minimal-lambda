using Microsoft.Extensions.DependencyInjection;

namespace MinimalLambda.Runtime;

internal enum ItemExecutionState
{
    Unstarted,
    Succeeded,
    MappedError,
    Cancelled,
}

internal readonly struct ItemOutcome<TValue>
{
    private ItemOutcome(ItemExecutionState state, bool hasValue, TValue? value)
    {
        State = state;
        HasValue = hasValue;
        Value = value;
    }

    internal ItemExecutionState State { get; }

    internal bool HasValue { get; }

    internal TValue? Value { get; }

    internal static ItemOutcome<TValue> FromValue(TValue? value) =>
        new(ItemExecutionState.Succeeded, true, value);

    internal static ItemOutcome<TValue> FromMappedValue(TValue? value) =>
        new(ItemExecutionState.MappedError, true, value);

    internal static ItemOutcome<TValue> NoValue =>
        new(ItemExecutionState.Succeeded, false, default);

    internal static ItemOutcome<TValue> Cancelled =>
        new(ItemExecutionState.Cancelled, false, default);
}

internal static class EventProcessorWorkers
{
    internal static async Task<ItemOutcome<TValue>[]?> RunAsync<TItem, TValue>(
        TItem[] items,
        int effectiveConcurrency,
        IServiceScopeFactory scopeFactory,
        EventProcessingState state,
        Func<TItem, IServiceProvider, EventProcessingState, Task<ItemOutcome<TValue>>> adapter,
        bool collectValues)
    {
        if (items.Length == 0)
            state.TryCloseHandling();

        var outcomes = collectValues ? new ItemOutcome<TValue>[items.Length] : null;
        var workerCount = Math.Min(items.Length, Math.Max(1, effectiveConcurrency));
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
                var hasOutcome = false;

                try
                {
                    try
                    {
                        outcome = await adapter(items[index], scope.ServiceProvider, state)
                            .ConfigureAwait(false);
                        hasOutcome = true;
                    }
                    catch (Exception ex)
                    {
                        if (!state.TryLatchInvocationCancellation())
                            state.RecordFatal(ex);
                    }
                }
                finally
                {
                    state.CompleteAdmission();
                }

                try
                {
                    await scope.DisposeAsync().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    state.RecordFatal(ex);
                }

                if (collectValues && hasOutcome && outcome.HasValue)
                    outcomes![index] = outcome;
            }
        }
    }
}
