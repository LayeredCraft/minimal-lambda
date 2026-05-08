# Best practices and decision guide

Read when designing client-project code, reviewing MinimalLambda usage, or deciding between handler/middleware/lifecycle/testing/envelope patterns.

## Default architecture

Use three layers:

1. `Program.cs` wires host, configuration, services, middleware, lifecycle hooks, and one handler.
2. Handler method adapts Lambda event to application service call.
3. Application services contain business logic and can be unit-tested without Lambda host.

Good shape:

```csharp
var builder = LambdaApplication.CreateBuilder();

builder.Services.AddScoped<IOrderService, OrderService>();

await using var lambda = builder.Build();

lambda.MapHandler(OrderHandlers.HandleAsync);

await lambda.RunAsync();

internal static class OrderHandlers
{
    public static Task<OrderResponse> HandleAsync(
        [FromEvent] OrderRequest request,
        IOrderService orders,
        CancellationToken ct) =>
        orders.ProcessAsync(request, ct);
}
```

Why: source generator sees stable handler signature, handler stays testable, Lambda-specific code stays at edge.

## Handler best practices

Prefer:

- one clear `[FromEvent]` payload parameter when event exists
- explicit typed request/response records
- method-group handlers for direct unit tests
- `CancellationToken` in async handlers
- injected services instead of resolving from `IServiceProvider`
- throwing meaningful exceptions for unrecoverable invalid state

Avoid:

- anonymous response contracts in public APIs
- multiple runtime `MapHandler` calls
- manually parsing event JSON when envelope package exists
- reflection-heavy dispatch/routing inside one Lambda unless absolutely needed
- storing `ILambdaInvocationContext` or scoped services beyond invocation

## DI lifetime choices

| Need                                             | Lifetime  |
| ------------------------------------------------ | --------- |
| AWS SDK client, `HttpClient`, config cache       | singleton |
| per-invocation repository/unit of work/DbContext | scoped    |
| stateless lightweight helper                     | transient |

Never capture scoped services in singleton state. Lambda warm reuse makes leaks harder to notice.

## Middleware best practices

Use middleware for cross-cutting invocation concerns:

- logging scopes/correlation
- auth/authz
- validation
- metrics/tracing
- idempotency/cache short-circuiting
- error mapping

Ordering:

1. diagnostics/tracing/logging
2. auth/authz
3. validation
4. idempotency/caching
5. handler

Keep inline middleware thin. Extract reusable or stateful logic to `ILambdaMiddleware` classes.

## Lifecycle best practices

Use `OnInit` for cold-start work:

- warm caches
- validate required configuration/secrets
- pre-create expensive singleton clients only when needed

Use `OnShutdown` for bounded cleanup:

- flush telemetry
- drain buffers
- release external leases

Keep both cancellation-aware. `OnInit` failures should be intentional because failed init prevents serving invocations.

## Event source decision guide

- Plain JSON event: `MinimalLambda` only, `[FromEvent] MyEvent`.
- HTTP API/API Gateway/ALB with JSON body: use matching envelope package and response/result type.
- SQS/SNS/Kinesis/Kafka/Firehose/CloudWatch Logs: use matching envelope package to avoid hand-parsing records.
- Native AOT + envelopes: add `JsonSerializerContext`, `AddLambdaSerializerWithContext<TContext>()`, and `ConfigureEnvelopeOptions`.

## Testing strategy

- Unit-test services and static handler methods directly.
- Use `MinimalLambda.Testing` for pipeline behavior: source-generated binding, middleware, DI scopes, lifecycle, envelopes, serialization, error payloads.
- Share `LambdaApplicationFactory` only when singleton/lifecycle sharing is acceptable.

## AOT and trimming

Prefer:

- source-generated JSON contexts
- static handler methods
- explicit contracts
- package APIs built for source generation

Avoid:

- runtime reflection over handler signatures
- dynamic serialization polymorphism without source-gen metadata
- broad service locator patterns that hide dependencies from code review

## Code review checklist

- [ ] One runtime handler mapping.
- [ ] Payload parameter has exactly one `[FromEvent]` or no payload at all.
- [ ] Middleware registered before handler mapping.
- [ ] Async work accepts and propagates cancellation token.
- [ ] DI lifetimes match Lambda warm-container reuse.
- [ ] Envelope package matches AWS trigger.
- [ ] Native AOT path has serializer context and envelope options.
- [ ] Integration tests cover real pipeline when framework behavior matters.
