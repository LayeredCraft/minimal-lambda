# Testing patterns

Read when adding client-project tests with `MinimalLambda.Testing`.

## End-to-end happy path

```csharp
public sealed class OrderLambdaTests
{
    [Fact]
    public async Task InvokeAsync_ReturnsAcceptedOrder()
    {
        await using var factory = new LambdaApplicationFactory<Program>()
            .WithCancellationToken(TestContext.Current.CancellationToken);

        var response = await factory.TestServer.InvokeAsync<OrderRequest, OrderResponse>(
            new OrderRequest("order-123"),
            TestContext.Current.CancellationToken);

        response.WasSuccess.Should().BeTrue();
        response.Response.Should().Be(new OrderResponse("order-123", Accepted: true));
    }
}
```

`InvokeAsync` starts host on demand. Call `StartAsync` explicitly when init status matters.

## Assert startup/init behavior

```csharp
await using var factory = new LambdaApplicationFactory<Program>()
    .WithCancellationToken(TestContext.Current.CancellationToken);

var init = await factory.TestServer.StartAsync(TestContext.Current.CancellationToken);

init.InitStatus.Should().Be(InitStatus.InitCompleted);
```

Use fresh factory per test when checking lifecycle hooks.

## Override services

```csharp
await using var factory = new LambdaApplicationFactory<Program>()
    .WithHostBuilder(builder =>
    {
        builder.ConfigureServices((_, services) =>
        {
            services.RemoveAll<IOrderService>();
            services.AddScoped(_ => Substitute.For<IOrderService>());
        });
    });
```

Use this for external dependencies. Do not mock MinimalLambda runtime when runtime behavior is under test.

## No-event handler

```csharp
var response = await factory.TestServer.InvokeNoEventAsync<JobResponse>(
    TestContext.Current.CancellationToken);

response.WasSuccess.Should().BeTrue();
```

## No-response handler

```csharp
var response = await factory.TestServer.InvokeNoResponseAsync<JobRequest>(
    new JobRequest("sync"),
    TestContext.Current.CancellationToken);

response.WasSuccess.Should().BeTrue();
```

## Error assertion

```csharp
var response = await factory.TestServer.InvokeAsync<OrderRequest, OrderResponse>(
    new OrderRequest("bad"),
    TestContext.Current.CancellationToken);

response.WasSuccess.Should().BeFalse();
response.Error.Should().NotBeNull();
```

Assert structured Lambda-style error payload, not local exception type, for invocation failures.

## Shared factory fixture

Use `IClassFixture<LambdaApplicationFactory<Program>>` for speed only when shared singletons and one-time `OnInit` are acceptable.

Avoid shared factory when tests mutate configuration, singleton state, or lifecycle assertions.
