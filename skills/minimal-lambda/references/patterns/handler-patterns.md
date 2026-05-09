# Handler patterns

Read when implementing or reviewing handler shape.

## Default: inline Lambda adapter in `Program.cs`

Prefer mapping an inline arrow function in `Program.cs` for normal MinimalLambda apps.

```csharp
lambda.MapHandler(([FromEvent] OrderRequest request, IOrderService orders, CancellationToken ct) =>
    orders.ProcessAsync(request, ct));
```

Handler job: Lambda edge only.

- Bind payload with `[FromEvent]`.
- Accept Lambda/context parameters only when needed.
- Keep Lambda context objects at edge; pass services domain values, not `ILambdaInvocationContext`/
  `ILambdaContext`.
- Accept injected services explicitly.
- Pass `CancellationToken` through.
- Return response shape.

Business job: service/helper.

- validation workflows
- authorization/business policy decisions
- persistence
- external API orchestration
- transformations large enough to need names/tests

Why: readers see whole Lambda entry point in one place, source generator gets analyzable signature,
business code remains independent of Lambda.

## Complex business logic: delegate immediately

```csharp
var builder = LambdaApplication.CreateBuilder();

builder.Services.AddScoped<IOrderService, OrderService>();

await using var lambda = builder.Build();

lambda.MapHandler(([FromEvent] OrderRequest request, IOrderService orders, CancellationToken ct) =>
    orders.ProcessAsync(request, ct));

await lambda.RunAsync();
```

Do not move complex logic into a named handler class just to hide it. Move it into app
services/domain helpers because that code is not Lambda-specific.

## Tiny logic: keep it inline

Small, obvious logic is fine directly in `Program.cs`.

```csharp
lambda.MapHandler(([FromEvent] PingRequest request) =>
    new PingResponse(request.Message.Trim(), DateTimeOffset.UtcNow));
```

Keep inline when it is easier to read than a service, has no persistence/external orchestration, and
probably does not need isolated unit tests.

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
    context.Items["AwsRequestId"] = context.AwsRequestId;
    return await orders.ProcessAsync(request, ct);
});
```

Use context sparingly. Prefer services for business operations. Do not pass
`ILambdaInvocationContext`, raw AWS `ILambdaContext`, features, or Lambda wrappers into
domain/application services. Extract needed values (`AwsRequestId`, deadline, tenant id, claims,
headers) in handler/middleware and pass those values instead. Only isolate a service behind Lambda
context when boundary cannot be expressed otherwise.

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

## Testing guidance

Unit-test services/helpers for business behavior. Use integration tests for source-generated
binding, DI, middleware, envelopes, and serialization.

Extract a named static handler only when the adapter itself has enough Lambda-specific branching to
deserve direct unit tests. That should be uncommon.

## Anti-pattern: routing many event shapes in one handler

Avoid big `object`/JSON switch dispatch when separate Lambda functions or explicit envelope types fit. It hides contracts from source generation, tests, and AOT serializer metadata.
