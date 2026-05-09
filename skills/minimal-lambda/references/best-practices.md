# Best practices and decision guide

Read when designing client-project code, reviewing MinimalLambda usage, or deciding between handler/middleware/lifecycle/testing/envelope patterns.

## Default architecture

Use `Program.cs` as Lambda edge:

1. `Program.cs` wires host, configuration, services, inline middleware, lifecycle hooks, and one
   inline `MapHandler` arrow function.
2. Handler/middleware/hooks adapt Lambda concerns: bind payload, access invocation context, set
   scopes/features/responses, call application service/helper, return Lambda response.
3. Application services contain complex business logic and can be unit-tested without Lambda host.

Good shape for real business logic:

```csharp
var builder = LambdaApplication.CreateBuilder();

builder.Services.AddScoped<IOrderService, OrderService>();

await using var lambda = builder.Build();

lambda.MapHandler(([FromEvent] OrderRequest request, IOrderService orders, CancellationToken ct) =>
    orders.ProcessAsync(request, ct));

await lambda.RunAsync();
```

Why: handler remains visibly Lambda-shaped and local to startup, while non-Lambda decisions live
behind services/helpers.

## Handler best practices

Prefer:

- one clear `[FromEvent]` payload parameter when event exists
- explicit typed request/response records
- inline `MapHandler` arrow functions for Lambda adapter code
- `CancellationToken` in async handlers
- injected services instead of resolving from `IServiceProvider`
- extracting needed values from `ILambdaInvocationContext`/`ILambdaContext` at the edge before
  calling services
- throwing meaningful exceptions for unrecoverable invalid state

Avoid:

- complex business rules, orchestration, validation workflows, or persistence logic inside the
  handler
- extracting named handler classes just to hold a one-line adapter
- anonymous response contracts in public APIs
- multiple runtime `MapHandler` calls
- manually parsing event JSON when envelope package exists
- reflection-heavy dispatch/routing inside one Lambda unless absolutely needed
- passing `ILambdaInvocationContext`, raw AWS `ILambdaContext`, lifecycle context, feature
  collections, or Lambda context wrappers into application services
- injecting Lambda context into services; this should be almost never and treated as a
  layer-boundary smell unless explicitly isolated and justified
- storing `ILambdaInvocationContext` or scoped services beyond invocation

## DI lifetime choices

| Need                                             | Lifetime  |
| ------------------------------------------------ | --------- |
| AWS SDK client, `HttpClient`, config cache       | singleton |
| per-invocation repository/unit of work/DbContext | scoped    |
| stateless lightweight helper                     | transient |

Never capture scoped services in singleton state. Lambda warm reuse makes leaks harder to notice.

## Middleware best practices

Use middleware for cross-cutting invocation concerns. Prefer inline middleware in `Program.cs` when
it is app-local Lambda glue. Inline `UseMiddleware(async (context, next) => ...)` receives only
`ILambdaInvocationContext` and `next`; it does not support handler-style direct parameter injection.
Resolve simple dependencies from `context.ServiceProvider`, use `UseMiddleware<T>()` class
middleware for constructor DI, or use `UseMiddleware<TFactory>()` when construction must be
custom/deferred per invocation. Services called from middleware should receive normal domain values,
not Lambda context objects:

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

Keep inline middleware thin. If logic becomes complex, reusable, stateful, or needs direct unit
tests, extract an `ILambdaMiddleware` class or delegate business work to an injected service.

## Lifecycle best practices

Use `OnInit` for cold-start work:

- warm caches
- validate required configuration/secrets
- pre-create expensive singleton clients only when needed

Use `OnShutdown` for bounded cleanup:

- flush telemetry
- drain buffers
- release external leases

Keep both cancellation-aware. Inline Lambda-specific or tiny hook logic in `Program.cs`; delegate
complex warmup/flush/validation to DI services. Hook delegate overloads support direct DI
parameters, but pass services only the data they need, not lifecycle/context objects. `OnInit`
failures should be intentional because failed init prevents serving invocations.

## Event source decision guide

- Plain JSON event: `MinimalLambda` only, `[FromEvent] MyEvent`.
- HTTP API/API Gateway/ALB with JSON body: use matching envelope package and response/result type.
- SQS/SNS/Kinesis/Kafka/Firehose/CloudWatch Logs: use matching envelope package to avoid hand-parsing records.
- Native AOT + envelopes: add `JsonSerializerContext`, `AddLambdaSerializerWithContext<TContext>()`, and `ConfigureEnvelopeOptions`.

## Testing strategy

- Unit-test services/helpers directly; avoid unit-testing generated binding through handler
  extraction.
- Use `MinimalLambda.Testing` for pipeline behavior: source-generated binding, middleware, DI scopes, lifecycle, envelopes, serialization, error payloads.
- Share `LambdaApplicationFactory` only when singleton/lifecycle sharing is acceptable.

## AOT and trimming

Prefer:

- source-generated JSON contexts
- inline, analyzable handler delegates
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
- [ ] Handler, middleware, and hooks contain Lambda adapter/glue work only, unless logic is tiny
  enough to stay readable inline.
- [ ] Complex business logic lives in injected services/helpers, not handler/middleware/hook bodies.
- [ ] Lambda context objects stay at the edge; services receive domain values/options/cancellation
  tokens instead.
- [ ] Async work accepts and propagates cancellation token.
- [ ] DI lifetimes match Lambda warm-container reuse.
- [ ] Envelope package matches AWS trigger.
- [ ] Native AOT path has serializer context and envelope options.
- [ ] Integration tests cover real pipeline when framework behavior matters.
