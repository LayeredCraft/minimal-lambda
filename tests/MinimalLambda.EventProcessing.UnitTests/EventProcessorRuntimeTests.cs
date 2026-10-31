using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MinimalLambda.EventProcessing.UnitTests;

public sealed class EventProcessorRuntimeTests
{
    [Fact]
    public async Task OutcomesRetainDuplicatesNullValuesAndSkippedPositions()
    {
        var builder = LambdaApplication.CreateBuilder();
        builder.Services.Configure<EventHandlerOptions>(o => o.MaxConcurrentExecutions = 1);
        await using var app = builder.Build();
        IReadOnlyList<EventItemOutcome<string?, string?>>? captured = null;
        app.MapEventHandler(
            split: (string?[] batch, ILambdaInvocationContext _) => batch,
            handle: (string? item, IEventProcessingContext processing) =>
            {
                if (item == "stop")
                {
                    processing.StopProcessing();
                    throw new Exception("failed");
                }

                return Task.FromResult<string?>(null);
            },
            onError: (item, error, processing) => "mapped",
            createResponse: (outcomes, _) =>
            {
                captured = outcomes;
                return "response";
            });
        await app.Build()(Context(app, new string?[] { "a", "a", null, "stop", "later" }));
        captured.Should().NotBeNull();
        captured!.Select(x => x.Index).Should().Equal(0, 1, 2, 3, 4);
        captured.Select(x => x.Item).Should().Equal("a", "a", null, "stop", "later");
        captured
            .Select(x => x.Status)
            .Should()
            .Equal(
                EventItemStatus.Succeeded,
                EventItemStatus.Succeeded,
                EventItemStatus.Succeeded,
                EventItemStatus.MappedError,
                EventItemStatus.Skipped);
        captured[0].HasValue.Should().BeTrue();
        captured[0].Value.Should().BeNull();
        captured[3].Exception!.Message.Should().Be("failed");
        captured[4].HasValue.Should().BeFalse();
        captured.ToArray().Should().Equal(captured.ToArray());
        ((ICollection<EventItemOutcome<string?, string?>>)captured).IsReadOnly.Should().BeTrue();
    }

    [Fact]
    public async Task EmptyBatchBuildsResponseOnce()
    {
        await using var app = LambdaApplication.CreateBuilder().Build();
        var calls = 0;
        app.MapEventHandler(
            split: (string[] batch, ILambdaInvocationContext _) => batch,
            handle: (string _) => Task.CompletedTask,
            createResponse: (outcomes, _) =>
            {
                outcomes.Should().BeEmpty();
                calls++;
                return "empty";
            });
        await app.Build()(Context(app, Array.Empty<string>()));
        calls.Should().Be(1);
    }

    [Fact]
    public async Task ProcessingCancellationRetainsCancelledAndUnstartedItems()
    {
        var builder = LambdaApplication.CreateBuilder();
        builder.Services.Configure<EventHandlerOptions>(o => o.MaxConcurrentExecutions = 1);
        await using var app = builder.Build();
        IReadOnlyList<EventItemOutcome<string, string>>? captured = null;
        app.MapEventHandler(
            split: (string[] batch, ILambdaInvocationContext _) => batch,
            handle: (string _, IEventProcessingContext processing) =>
            {
                processing.CancelProcessing();
                return Task.FromCanceled(processing.CancellationToken);
            },
            createResponse: (outcomes, _) =>
            {
                captured = outcomes;
                return "cancelled";
            });
        await app.Build()(Context(app, new[] { "first", "second" }));
        captured!
            .Select(x => x.Status)
            .Should()
            .Equal(EventItemStatus.Cancelled, EventItemStatus.Skipped);
    }

    [Fact]
    public async Task ScopeCreationFailureIsFatalAndDoesNotMapItemErrors()
    {
        await using var app = LambdaApplication.CreateBuilder().Build();
        var original = new InvalidOperationException("scope creation");
        var scopes = Substitute.For<IServiceScopeFactory>();
        scopes.CreateScope().Returns(_ => throw original);
        var runtime = new EventProcessorRuntime(1, scopes, null);
        var mapped = false;
        var run = () => runtime.RunAsync<string[], string, string>(
            Context(app, new[] { "item" }),
            (batch, _) => batch,
            (_, _, _) => Task.CompletedTask,
            null,
            (_, _, _) =>
            {
                mapped = true;
                return "mapped";
            },
            null);
        (await run.Should().ThrowAsync<InvalidOperationException>())
            .Which
            .Should()
            .BeSameAs(original);
        mapped.Should().BeFalse();
    }

    [Fact]
    public async Task ConstructorFailureCanBeMappedWhileScopeIsAlive()
    {
        var builder = LambdaApplication.CreateBuilder();
        var original = new InvalidOperationException("constructor");
        builder.Services.AddScoped<Broken>(_ => throw original);
        await using var app = builder.Build();
        Exception? mapped = null;
        app.MapEventHandler(
            split: (string[] batch, ILambdaInvocationContext _) => batch,
            handle: (string _, Broken _) => Task.CompletedTask,
            onError: (_, error, _) =>
            {
                mapped = error;
                return "mapped";
            },
            createResponse: (outcomes, _) => outcomes.Single().Value!);
        await app.Build()(Context(app, new[] { "item" }));
        mapped.Should().BeSameAs(original);
    }

    [Fact]
    public async Task MissingKeyedDependencyIsMapped()
    {
        await using var app = LambdaApplication.CreateBuilder().Build();
        Exception? mapped = null;
        app.MapEventHandler(
            split: (string[] batch, ILambdaInvocationContext _) => batch,
            handle: (string _, [FromKeyedServices("missing")] Broken _) => Task.CompletedTask,
            onError: (_, error, _) =>
            {
                mapped = error;
                return "mapped";
            },
            createResponse: (outcomes, _) => outcomes.Single().Value!);
        await app.Build()(Context(app, new[] { "item" }));
        mapped.Should().BeOfType<InvalidOperationException>();
    }

    [Fact]
    public async Task DisposalAfterMappedFailureStillFailsInvocation()
    {
        var builder = LambdaApplication.CreateBuilder();
        builder.Services.AddScoped<DisposalFailure>();
        await using var app = builder.Build();
        var responseCalls = 0;
        app.MapEventHandler(
            split: (string[] batch, ILambdaInvocationContext _) => batch,
            handle: (string _, DisposalFailure _) => Task.FromException(new Exception("handler")),
            onError: (_, _, _) => "mapped",
            createResponse: (outcomes, _) =>
            {
                responseCalls++;
                return "response";
            });
        var run = () => app.Build()(Context(app, new[] { "item" }));
        await run.Should().ThrowAsync<InvalidOperationException>().WithMessage("disposal");
        responseCalls.Should().Be(0);
    }

    internal sealed class DisposalFailure : IAsyncDisposable
    {
        public ValueTask DisposeAsync() =>
            ValueTask.FromException(new InvalidOperationException("disposal"));
    }

    internal sealed class Broken;

    [Fact]
    public async Task CancellationDuringSlowDisposalWaitsForCleanupAndSuppressesResponse()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        var builder = LambdaApplication.CreateBuilder();
        builder.Services.AddScoped(_ => new GatedDisposal(entered, release.Task));
        await using var app = builder.Build();
        var responseCalls = 0;
        app.MapEventHandler(
            split: (string[] batch, ILambdaInvocationContext _) => batch,
            handle: (string _, GatedDisposal _) => Task.CompletedTask,
            createResponse: (outcomes, _) =>
            {
                responseCalls++;
                return "response";
            });
        var context = Context(app, new[] { "item" });
        context.CancellationToken.Returns(cancellation.Token);
        var invocation = app.Build()(context);
        await entered.Task.WaitAsync(
            TimeSpan.FromSeconds(10),
            TestContext.Current.CancellationToken);
        cancellation.Cancel();
        invocation.IsCompleted.Should().BeFalse();
        release.SetResult();
        var run = () => invocation;
        await run.Should().ThrowAsync<OperationCanceledException>();
        responseCalls.Should().Be(0);
    }

    [Fact]
    public async Task InvocationCancellationTakesPrecedenceOverResolutionError()
    {
        using var cancellation = new CancellationTokenSource();
        var builder = LambdaApplication.CreateBuilder();
        builder.Services.AddScoped<Broken>(_ =>
        {
            cancellation.Cancel();
            throw new Exception("resolution");
        });
        await using var app = builder.Build();
        var mapped = false;
        app.MapEventHandler(
            split: (string[] batch, ILambdaInvocationContext _) => batch,
            handle: (string _, Broken _) => Task.CompletedTask,
            onError: (_, _, _) =>
            {
                mapped = true;
                return "mapped";
            },
            createResponse: (outcomes, _) => "response");
        var context = Context(app, new[] { "item" });
        context.CancellationToken.Returns(cancellation.Token);
        var run = () => app.Build()(context);
        await run.Should().ThrowAsync<OperationCanceledException>();
        mapped.Should().BeFalse();
    }

    internal sealed class GatedDisposal(TaskCompletionSource entered, Task release)
        : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            entered.SetResult();
            await release.ConfigureAwait(false);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OptionalAndKeyedDefaultsDistinguishMissingAndDefaultValues(bool register)
    {
        var builder = LambdaApplication.CreateBuilder();
        if (register)
        {
            builder.Services.AddSingleton(typeof(int), 0);
            builder.Services.AddKeyedSingleton<string>("label", "");
        }

        await using var app = builder.Build();
        int? actualAmount = null;
        string? actualLabel = null;

        Task Handle(
            string _,
            int amount = 42,
            [FromKeyedServices("label")] string label = "fallback")
        {
            actualAmount = amount;
            actualLabel = label;
            return Task.CompletedTask;
        }

        Func<string, int, string, Task> handle = Handle;
        app.MapEventHandler(
            split: (string[] batch, ILambdaInvocationContext _) => batch,
            handle: handle);
        await app.Build()(Context(app, new[] { "item" }));
        actualAmount.Should().Be(register ? 0 : 42);
        actualLabel.Should().Be(register ? "" : "fallback");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DecimalAndDateTimeDefaultsPreserveExactValues(bool register)
    {
        var builder = LambdaApplication.CreateBuilder();
        if (register)
        {
            builder.Services.AddSingleton(typeof(decimal), 0m);
            builder.Services.AddKeyedSingleton(typeof(DateTime), "timestamp", default(DateTime));
        }

        await using var app = builder.Build();
        decimal? actualAmount = null;
        DateTime? actualTimestamp = null;

        Task Handle(
            string _,
            [FromKeyedServices("timestamp"), Optional, DateTimeConstant(637000000000000000L)]
            DateTime timestamp,
            decimal amount = 1.2345678901234567890123456789m)
        {
            actualAmount = amount;
            actualTimestamp = timestamp;
            return Task.CompletedTask;
        }

        Func<string, DateTime, decimal, Task> handle = Handle;
        app.MapEventHandler(
            split: (string[] batch, ILambdaInvocationContext _) => batch,
            handle: handle);
        await app.Build()(Context(app, new[] { "item" }));
        actualAmount.Should().Be(register ? 0m : 1.2345678901234567890123456789m);
        actualTimestamp.Should().Be(register ? default : new DateTime(637000000000000000L));
    }

    private static ILambdaInvocationContext Context<T>(LambdaApplication app, T batch)
    {
        var features = Substitute.For<IFeatureCollection>();
        features.Get<IEventFeature>().Returns(new EventFeature<T>(batch));
        features.Get<IResponseFeature>().Returns(Substitute.For<IResponseFeature<string>>());
        var context = Substitute.For<ILambdaInvocationContext>();
        context.Features.Returns(features);
        context.ServiceProvider.Returns(app.Services);
        return context;
    }

    private sealed class EventFeature<T>(T batch) : IEventFeature<T>
    {
        public T GetEvent(ILambdaInvocationContext context) => batch;
        object? IEventFeature.GetEvent(ILambdaInvocationContext context) => batch;
    }
}
