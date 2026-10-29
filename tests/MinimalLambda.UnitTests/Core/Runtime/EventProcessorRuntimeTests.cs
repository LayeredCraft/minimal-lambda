using System.Collections.ObjectModel;
using Microsoft.Extensions.DependencyInjection;

namespace MinimalLambda.UnitTests.Core.Runtime;

public sealed class EventProcessorRuntimeTests
{
    [Fact]
    public async Task CollectsMaterializedContributionsInSourceOrder()
    {
        // Arrange
        await using var services = CreateServices();
        var runtime = new EventProcessorRuntime(services);
        var context = CreateContext(services, new Batch([new Item(3), new Item(1), new Item(2)]));
        var completions =
            Enumerable
                .Range(1, 3)
                .ToDictionary(
                    id => id,
                    _ => new TaskCompletionSource<int>(
                        TaskCreationOptions.RunContinuationsAsynchronously));
        IEnumerable<int>? captured = null;

        // Act
        var run = runtime.RunAsync<Batch, Item, int>(
            context,
            static (batch, _) => batch.Items,
            (item, _, _) => () => completions[item.Id].Task,
            static (_, task) => ((Task<int>)task).GetAwaiter().GetResult(),
            onError: null,
            complete: values => captured = values);
        completions[2].SetResult(2);
        completions[1].SetResult(1);
        completions[3].SetResult(3);
        await run;

        // Assert
        captured.Should().BeOfType<ReadOnlyCollection<int>>();
        captured.Should().Equal(3, 1, 2);
    }

    [Fact]
    public async Task DisposesEachItemScopeBeforeCompleting()
    {
        // Arrange
        var tracker = new ScopeTracker();
        await using var services = CreateServices(serviceCollection =>
        {
            serviceCollection.AddSingleton(tracker);
            serviceCollection.AddScoped<ScopeProbe>();
        });
        var runtime = new EventProcessorRuntime(services);
        var context = CreateContext(services, new Batch([new Item(1), new Item(2), new Item(3)]));
        var scopeIds = new List<int>();
        var allScopesDisposedAtCompletion = false;

        // Act
        await runtime.RunAsync<Batch, Item, int>(
            context,
            static (batch, _) => batch.Items,
            (item, itemServices, _) =>
            {
                scopeIds.Add(itemServices.GetRequiredService<ScopeProbe>().Id);
                return static () => Task.CompletedTask;
            },
            static (item, _) => item.Id,
            onError: null,
            complete: _ => allScopesDisposedAtCompletion =
                tracker.Created == 3 && tracker.Disposed == 3);

        // Assert
        scopeIds.Should().OnlyHaveUniqueItems();
        allScopesDisposedAtCompletion.Should().BeTrue();
    }

    [Fact]
    public async Task BindingExceptionFailsInvocationWithoutCallingErrorMapper()
    {
        // Arrange
        await using var services = CreateServices();
        var runtime = new EventProcessorRuntime(services);
        var context = CreateContext(services, new Batch([new Item(1)]));
        var expected = new InvalidOperationException("binding failed");
        var errorCalls = 0;

        // Act
        var act = () => runtime.RunAsync<Batch, Item, int>(
            context,
            static (batch, _) => batch.Items,
            (_, _, _) => throw expected,
            static (item, _) => item.Id,
            (_, _, _) =>
            {
                errorCalls++;
                return -1;
            },
            complete: _ => { });

        // Assert
        var exception = await act.Should().ThrowAsync<InvalidOperationException>();
        exception.Which.Should().BeSameAs(expected);
        errorCalls.Should().Be(0);
    }

    [Fact]
    public async Task SynchronousHandlerExceptionIsMapped()
    {
        // Arrange
        await using var services = CreateServices();
        var runtime = new EventProcessorRuntime(services);
        var context = CreateContext(services, new Batch([new Item(4)]));
        var expected = new InvalidOperationException("handler failed");
        var contributions = new List<int>();

        // Act
        await runtime.RunAsync<Batch, Item, int>(
            context,
            static (batch, _) => batch.Items,
            (_, _, _) => () => throw expected,
            static (item, _) => item.Id,
            (item, exception, _) =>
            {
                exception.Should().BeSameAs(expected);
                return -item.Id;
            },
            values => contributions.AddRange(values));

        // Assert
        contributions.Should().Equal(-4);
    }

    [Fact]
    public async Task NullHandlerTaskFailsInvocationWithoutCallingErrorMapper()
    {
        // Arrange
        await using var services = CreateServices();
        var runtime = new EventProcessorRuntime(services);
        var context = CreateContext(services, new Batch([new Item(1)]));
        var errorCalls = 0;

        // Act
        var act = () => runtime.RunAsync<Batch, Item, int>(
            context,
            static (batch, _) => batch.Items,
            static (_, _, _) => static () => null!,
            static (item, _) => item.Id,
            (_, _, _) =>
            {
                errorCalls++;
                return -1;
            },
            complete: _ => { });

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*null task*");
        errorCalls.Should().Be(0);
    }

    private static ServiceProvider CreateServices(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddOptions<EventHandlerOptions>();
        configure?.Invoke(services);
        return services.BuildServiceProvider();
    }

    private static ILambdaInvocationContext CreateContext<TEvent>(
        IServiceProvider services,
        TEvent value)
    {
        var features = new DefaultFeatureCollection([]);
        features.Set<IEventFeature>(new TestEventFeature<TEvent>(value));
        var context = Substitute.For<ILambdaInvocationContext>();
        context.Features.Returns(features);
        context.ServiceProvider.Returns(services);
        context.CancellationToken.Returns(CancellationToken.None);
        context.Items.Returns(new Dictionary<object, object?>());
        return context;
    }

    private sealed record Batch(Item[] Items);

    private sealed record Item(int Id);

    private sealed class TestEventFeature<T>(T value) : IEventFeature<T>
    {
        public T GetEvent(ILambdaInvocationContext context) => value;

        object? IEventFeature.GetEvent(ILambdaInvocationContext context) => value;
    }

    private sealed class ScopeTracker
    {
        public int Created;

        public int Disposed;
    }

    private sealed class ScopeProbe(ScopeTracker tracker) : IDisposable
    {
        public int Id { get; } = Interlocked.Increment(ref tracker.Created);

        public void Dispose() => Interlocked.Increment(ref tracker.Disposed);
    }
}
