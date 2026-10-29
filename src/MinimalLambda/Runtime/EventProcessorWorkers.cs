using Microsoft.Extensions.DependencyInjection;

namespace MinimalLambda.Runtime;

internal readonly struct ItemOutcome<TValue>
{
    private ItemOutcome(TValue? value)
    {
        HasValue = true;
        Value = value;
    }

    internal bool HasValue { get; }

    internal TValue? Value { get; }

    internal static ItemOutcome<TValue> FromValue(TValue? value) => new(value);
}

internal static class EventProcessorWorkers
{
    internal static async Task<ItemOutcome<TValue>[]?> RunAsync<TItem, TValue>(
        List<TItem> items,
        int effectiveConcurrency,
        IServiceScopeFactory scopeFactory,
        EventProcessingState state,
        Func<TItem, IServiceProvider, EventProcessingState, Task<ItemOutcome<TValue>>> adapter,
        bool collectValues)
    {
        if (items.Count == 0)
            state.TryCloseHandling();

        var outcomes = collectValues ? new ItemOutcome<TValue>[items.Count] : null;
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

                if (collectValues && outcome.HasValue)
                    outcomes![index] = outcome;
            }
        }
    }
}
