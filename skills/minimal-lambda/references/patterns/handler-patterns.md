# Handler patterns

Read when implementing or reviewing handler shape.

## Thin method-group handler

Best default for client projects.

```csharp
lambda.MapHandler(OrderHandlers.HandleAsync);

internal static class OrderHandlers
{
    public static Task<OrderResponse> HandleAsync(
        [FromEvent] OrderRequest request,
        IOrderService orders,
        CancellationToken ct) =>
        orders.ProcessAsync(request, ct);
}
```

Why:

- source generator gets explicit signature
- handler can be unit-tested directly
- business logic stays in service

## No-event handler

Use for scheduled/heartbeat style Lambda where payload not needed.

```csharp
lambda.MapHandler(async (IJobRunner jobs, CancellationToken ct) =>
{
    await jobs.RunAsync(ct);
});
```

No `[FromEvent]`; no fake unused event parameter.

## Context-aware handler

Use `ILambdaInvocationContext` when handler needs AWS request metadata, per-invocation bag, or features.

```csharp
lambda.MapHandler(async (
    [FromEvent] OrderRequest request,
    ILambdaInvocationContext context,
    IOrderService orders,
    CancellationToken ct) =>
{
    context.Items["OrderId"] = request.OrderId;
    return await orders.ProcessAsync(request, context.AwsRequestId, ct);
});
```

Use context sparingly. Prefer services for business operations.

## Keyed service handler

Use .NET keyed services for explicit variant selection.

```csharp
builder.Services.AddKeyedScoped<IOrderProcessor, PrimaryOrderProcessor>("primary");

lambda.MapHandler((
    [FromEvent] OrderRequest request,
    [FromKeyedServices("primary")] IOrderProcessor processor,
    CancellationToken ct) =>
    processor.ProcessAsync(request, ct));
```

Keep keys simple constants.

## Unit-testable handler method

```csharp
[Fact]
public async Task HandleAsync_ReturnsAcceptedOrder()
{
    var orders = Substitute.For<IOrderService>();
    var request = new OrderRequest("order-123");
    var expected = new OrderResponse("order-123", Accepted: true);

    orders.ProcessAsync(request, Arg.Any<CancellationToken>()).Returns(expected);

    var actual = await OrderHandlers.HandleAsync(request, orders, TestContext.Current.CancellationToken);

    actual.Should().Be(expected);
}
```

Use integration tests for source-generated binding; direct unit tests for business behavior.

## Anti-pattern: routing many event shapes in one handler

Avoid big `object`/JSON switch dispatch when separate Lambda functions or explicit envelope types fit. It hides contracts from source generation, tests, and AOT serializer metadata.
