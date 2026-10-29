using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MinimalLambda.UnitTests.Core.Runtime;

public class EventHandlerRegistrationTests
{
    [Fact]
    public async Task N1RegistersOrdinaryHandlerAndRunsMiddleware()
    {
        // Arrange
        var calls = new List<string>();
        await using var app = LambdaApplication.CreateBuilder().Build();
        Func<Item, Task> handle = _ =>
        {
            calls.Add("handle");
            return Task.CompletedTask;
        };

        EventHandlerRegistration.Register<Batch, Item>(
            app,
            (batch, _) => batch.Items,
            handle,
            (item, _, _) => () => handle(item));
        app.UseMiddleware(async (context, next) =>
        {
            calls.Add("before");
            await next(context);
            calls.Add("after");
        });

        var harness = CreateHarness<object>(app, new Batch([new Item(1)]));
        var handler = app.Build();

        // Act
        await handler(harness.Context);

        // Assert
        calls.Should().Equal("before", "handle", "after");
    }

    [Fact]
    public async Task N2BuildsResponseFromSuccessfulSourceItems()
    {
        // Arrange
        Response? captured = null;
        await using var app = LambdaApplication.CreateBuilder().Build();
        Func<Item, Task> handle = _ => Task.CompletedTask;
        EventHandlerRegistration.Register<Batch, Item, Response>(
            app,
            (batch, _) => batch.Items,
            handle,
            (item, _, _) => () => handle(item),
            (items, _) => captured = new Response(items.Select(item => item.Id).ToArray()));

        var responseFeature = Substitute.For<IResponseFeature<Response>>();
        var harness = CreateHarness<Response>(
            app,
            new Batch([new Item(1), new Item(2)]),
            responseFeature);

        // Act
        await app.Build()(harness.Context);

        // Assert
        captured!.Ids.Should().Equal(1, 2);
        harness.ResponseFeature.Received(1).SetResponse(captured);
    }

    [Fact]
    public async Task N3BuildsResponseFromMappedFailures()
    {
        // Arrange
        Response? captured = null;
        await using var app = LambdaApplication.CreateBuilder().Build();
        Func<Item, Task> handle = item => item.Id == 2
            ? Task.FromException(new InvalidOperationException("item failed"))
            : Task.CompletedTask;
        EventHandlerRegistration.Register<Batch, Item, int, Response>(
            app,
            (batch, _) => batch.Items,
            handle,
            (item, _, _) => () => handle(item),
            (item, exception, _) => item.Id + exception.Message.Length,
            (failures, _) => captured = new Response(failures.ToArray()));

        var responseFeature = Substitute.For<IResponseFeature<Response>>();
        var harness = CreateHarness<Response>(
            app,
            new Batch([new Item(1), new Item(2)]),
            responseFeature);

        // Act
        await app.Build()(harness.Context);

        // Assert
        captured!.Ids.Should().Equal(2 + "item failed".Length);
        harness.ResponseFeature.Received(1).SetResponse(captured);
    }

    [Fact]
    public async Task R1BuildsResponseFromSuccessfulHandlerResults()
    {
        // Arrange
        Response? captured = null;
        await using var app = LambdaApplication.CreateBuilder().Build();
        Func<Item, Task<int>> handle = item => Task.FromResult(item.Id * 2);
        EventHandlerRegistration.Register<Batch, Item, int, Response>(
            app,
            (batch, _) => batch.Items,
            handle,
            (item, _, _) => () => handle(item),
            (results, _) => captured = new Response(results.ToArray()));

        var responseFeature = Substitute.For<IResponseFeature<Response>>();
        var harness = CreateHarness<Response>(
            app,
            new Batch([new Item(3), new Item(4)]),
            responseFeature);

        // Act
        await app.Build()(harness.Context);

        // Assert
        captured!.Ids.Should().Equal(6, 8);
        harness.ResponseFeature.Received(1).SetResponse(captured);
    }

    [Fact]
    public async Task R2BuildsResponseFromSuccessfulAndMappedResults()
    {
        // Arrange
        Response? captured = null;
        await using var app = LambdaApplication.CreateBuilder().Build();
        Func<Item, Task<int>> handle = item => item.Id == 2
            ? Task.FromException<int>(new InvalidOperationException("item failed"))
            : Task.FromResult(item.Id * 2);
        EventHandlerRegistration.Register<Batch, Item, int, Response>(
            app,
            (batch, _) => batch.Items,
            handle,
            (item, _, _) => () => handle(item),
            (item, exception, _) => item.Id + exception.Message.Length,
            (results, _) => captured = new Response(results.ToArray()));

        var responseFeature = Substitute.For<IResponseFeature<Response>>();
        var harness = CreateHarness<Response>(
            app,
            new Batch([new Item(1), new Item(2), new Item(3)]),
            responseFeature);

        // Act
        await app.Build()(harness.Context);

        // Assert
        captured!.Ids.Should().Equal(2, 2 + "item failed".Length, 6);
        harness.ResponseFeature.Received(1).SetResponse(captured);
    }

    [Fact]
    public void NullArgumentsFailBeforeReadingBuilderServices()
    {
        // Arrange
        var split = (Batch batch, ILambdaInvocationContext _) => batch.Items;
        Func<Item, Task> handle = _ => Task.CompletedTask;
        Func<Item, IServiceProvider, IEventProcessingContext, Func<Task>> bind = (item, _, _) =>
            () => handle(item);
        Func<Item, Exception, IEventProcessingContext, int> onError = (_, _, _) => 0;
        Func<IEnumerable<int>, ILambdaInvocationContext, Response> valueResponse = (values, _) =>
            new Response(values.ToArray());
        var builder = Substitute.For<ILambdaInvocationBuilder>();

        // Act / Assert
        AssertNullArgument(
            () => EventHandlerRegistration.Register<Batch, Item>(null!, split, handle, bind),
            "application");
        AssertNullArgument(
            () => EventHandlerRegistration.Register<Batch, Item>(builder, null!, handle, bind),
            "split");
        AssertNullArgument(
            () => EventHandlerRegistration.Register<Batch, Item>(builder, split, null!, bind),
            "handle");
        AssertNullArgument(
            () => EventHandlerRegistration.Register<Batch, Item>(builder, split, handle, null!),
            "bind");
        AssertNullArgument(
            () => EventHandlerRegistration.Register<Batch, Item, Response>(
                builder,
                split,
                handle,
                bind,
                null!),
            "createResponse");
        AssertNullArgument(
            () => EventHandlerRegistration.Register<Batch, Item, int, Response>(
                builder,
                split,
                handle,
                bind,
                null!,
                valueResponse),
            "onError");
        AssertNullArgument(
            () => EventHandlerRegistration.Register<Batch, Item, int, Response>(
                builder,
                split,
                handle,
                bind,
                onError,
                null!),
            "createResponse");
        Func<Item, IServiceProvider, IEventProcessingContext, Func<Task<int>>> resultBind =
            (item, _, _) => () => Task.FromResult(item.Id);
        Func<Item, Task<int>> resultHandle = item => Task.FromResult(item.Id);
        AssertNullArgument(
            () => EventHandlerRegistration.Register<Batch, Item, int, Response>(
                builder,
                split,
                resultHandle,
                resultBind,
                null!),
            "createResponse");
        AssertNullArgument(
            () => EventHandlerRegistration.Register<Batch, Item, int, Response>(
                builder,
                split,
                resultHandle,
                resultBind,
                null!,
                valueResponse),
            "onError");
        AssertNullArgument(
            () => EventHandlerRegistration.Register<Batch, Item, int, Response>(
                builder,
                split,
                resultHandle,
                resultBind,
                onError,
                null!),
            "createResponse");

        builder.ReceivedCalls().Should().BeEmpty();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ResponseFeatureErrorsOccurAfterResponseConstruction(bool useWrongFeature)
    {
        // Arrange
        var responseCalls = 0;
        await using var app = LambdaApplication.CreateBuilder().Build();
        Func<Item, Task> handle = _ => Task.CompletedTask;
        EventHandlerRegistration.Register<Batch, Item, Response>(
            app,
            (batch, _) => batch.Items,
            handle,
            (item, _, _) => () => handle(item),
            (items, _) =>
            {
                responseCalls++;
                return new Response(items.Select(item => item.Id).ToArray());
            });

        var context = useWrongFeature
            ? CreateHarness<OtherResponse>(
                    app,
                    new Batch([new Item(1)]),
                    Substitute.For<IResponseFeature<OtherResponse>>())
                .Context
            : CreateHarness<Response>(app, new Batch([new Item(1)])).Context;

        // Act
        var act = () => app.Build()(context);

        // Assert
        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage("*Response*feature*");
        responseCalls.Should().Be(1);
    }

    [Fact]
    public async Task ReadsOptionsOnceWhenRegistrationCapturesRuntime()
    {
        // Arrange
        var options = new CountingOptions(new EventHandlerOptions());
        var builder = LambdaApplication.CreateBuilder();
        builder.Services.AddSingleton<IOptions<EventHandlerOptions>>(options);
        await using var app = builder.Build();
        Func<Item, Task> handle = _ => Task.CompletedTask;
        EventHandlerRegistration.Register<Batch, Item>(
            app,
            (batch, _) => batch.Items,
            handle,
            (item, _, _) => () => handle(item));
        options.Reads.Should().Be(1);

        // Act
        var handler = app.Build();
        await handler(CreateHarness<object>(app, new Batch([new Item(1)])).Context);
        await handler(CreateHarness<object>(app, new Batch([new Item(2)])).Context);

        // Assert
        options.Reads.Should().Be(1);
    }

    private static void AssertNullArgument(
        Func<ILambdaInvocationBuilder> action,
        string parameterName) =>
        action.Should().Throw<ArgumentNullException>().WithParameterName(parameterName);

    private static Harness<TResponse> CreateHarness<TResponse>(
        LambdaApplication app,
        Batch batch,
        IResponseFeature<TResponse>? responseFeature = null)
    {
        var features = new DefaultFeatureCollection([]);
        features.Set<IEventFeature>(new TestEventFeature<Batch>(batch));
        if (responseFeature is not null)
            features.Set<IResponseFeature>(responseFeature);

        var context = Substitute.For<ILambdaInvocationContext>();
        context.Features.Returns(features);
        context.ServiceProvider.Returns(app.Services);
        context.CancellationToken.Returns(CancellationToken.None);
        context.Items.Returns(new Dictionary<object, object?>());

        return new Harness<TResponse>(context, responseFeature!);
    }

    private sealed record Batch(Item[] Items);

    private sealed record Item(int Id);

    public sealed record Response(int[] Ids);

    public sealed record OtherResponse(string Value);

    private sealed record Harness<TResponse>(
        ILambdaInvocationContext Context,
        IResponseFeature<TResponse> ResponseFeature);

    private sealed class TestEventFeature<T>(T value) : IEventFeature<T>
    {
        public T GetEvent(ILambdaInvocationContext context) => value;

        object? IEventFeature.GetEvent(ILambdaInvocationContext context) => value;
    }

    private sealed class CountingOptions(EventHandlerOptions value) : IOptions<EventHandlerOptions>
    {
        private int _reads;

        public int Reads => _reads;

        public EventHandlerOptions Value
        {
            get
            {
                Interlocked.Increment(ref _reads);
                return value;
            }
        }
    }
}
