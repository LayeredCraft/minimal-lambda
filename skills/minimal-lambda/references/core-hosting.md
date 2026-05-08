# Core hosting, handlers, DI, lifecycle, middleware

Read when task touches `LambdaApplication`, `MapHandler`, `[FromEvent]`, DI, lifecycle hooks, middleware, features, configuration, or source-generated handler behavior.

## Portability note

This reference is self-contained for client-project use. Do not assume the MinimalLambda source tree exists in the current workspace. If the task is a repo contribution, switch to `repo-workflow.md` for local source landmarks.

## Builder shape

Typical app:

```csharp
var builder = LambdaApplication.CreateBuilder();
builder.Services.AddScoped<IOrderService, OrderService>();

await using var lambda = builder.Build();

lambda.UseMiddleware(async (context, next) =>
{
    await next(context);
});

lambda.MapHandler(async ([FromEvent] OrderRequest request, IOrderService service, CancellationToken ct) =>
    await service.ProcessAsync(request, ct));

await lambda.RunAsync();
```

`CreateBuilder()` wires standard .NET configuration/logging/DI defaults unless `LambdaApplicationOptions.DisableDefaults = true`.

Configuration provider order from docs:

1. `AWS_` env vars
2. `DOTNET_` env vars
3. `appsettings.json`
4. `appsettings.{Environment}.json`
5. user secrets in Development
6. all env vars

Framework options bind from `LambdaHost` section, not old `AwsLambdaHost`.

## Handler registration rules

- `MapHandler` is source-generated/intercepted. Avoid dynamic delegates/reflection workarounds.
- Multiple `MapHandler` calls may exist in code, but only one can execute at runtime.
- Handler with payload: exactly one `[FromEvent]` parameter.
- Handler with no payload: omit event parameter and omit `[FromEvent]`.
- Other parameters can be services, `[FromKeyedServices(...)]`, `ILambdaInvocationContext`, raw AWS `ILambdaContext`, or `CancellationToken`.
- Return values can be `T`, `Task<T>`, `ValueTask<T>`, `Task`, `ValueTask`; serializer/envelope handles response.

Good handler style:

```csharp
lambda.MapHandler(MyHandlers.HandleAsync);

internal static class MyHandlers
{
    public static Task<OrderResponse> HandleAsync(
        [FromEvent] OrderRequest request,
        IOrderService service,
        CancellationToken ct) =>
        service.ProcessAsync(request, ct);
}
```

Method groups keep handler logic unit-testable.

## DI and lifetimes

- Singleton: reused across warm invocations. Good for `HttpClient`, AWS SDK clients, caches, config.
- Scoped: new per invocation. Good default for repositories, DbContexts, per-request state.
- Transient: new per resolve. Good for lightweight helpers.
- Never store scoped service on singleton.
- Prefer constructor/parameter injection over manual `IServiceProvider` resolution.

## Context and features

`ILambdaInvocationContext` resembles `HttpContext` for Lambda:

- `ServiceProvider` scoped to invocation
- `CancellationToken` cancels before hard timeout using configured buffer
- `Items` per-invocation bag
- `Properties` shared cross-invocation dictionary; use thread-safe values
- `Features` typed feature collection
- also exposes AWS Lambda context members

Useful feature helpers from docs:

```csharp
if (context.TryGetEvent<OrderRequest>(out var request)) { }
if (context.TryGetResponse<OrderResponse>(out var response))
    context.Features.Get<IResponseFeature<OrderResponse>>()!.SetResponse(response);
```

Use features in middleware to avoid coupling middleware directly to handlers.

## Middleware

Register before `MapHandler`. Execution order follows registration order and unwinds in reverse.

Inline middleware: quick app-specific glue.

```csharp
lambda.UseMiddleware(async (context, next) =>
{
    var logger = context.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Before");
    await next(context);
    logger.LogInformation("After");
});
```

Class middleware: reusable/testable.

```csharp
internal sealed class LoggingMiddleware(ILogger<LoggingMiddleware> logger) : ILambdaMiddleware
{
    public async Task InvokeAsync(ILambdaInvocationContext context, LambdaInvocationDelegate next)
    {
        logger.LogInformation("Invocation starting");
        await next(context);
    }
}

lambda.UseMiddleware<LoggingMiddleware>();
```

Order guidance:

- diagnostics first so they wrap all work
- auth before validation/business logic
- short-circuit/caching near handler when it depends on final event/response type

## Lifecycle

`OnInit`:

- cold start once per execution environment
- each handler gets fresh scope
- handlers run concurrently (`Task.WhenAll` per docs)
- `bool`/`Task<bool>` can abort startup on `false`; no return implies success
- exceptions aggregate and bubble so container does not serve traffic

`OnShutdown`:

- runs once on teardown/SIGTERM
- fresh scope per handler
- bounded by `ShutdownDuration - ShutdownDurationBuffer`
- use to flush telemetry/dispose external resources

Example:

```csharp
lambda.OnInit(async (ICache cache, CancellationToken ct) =>
{
    await cache.WarmAsync(ct);
    return true;
});

lambda.OnShutdown(async (ITelemetrySink sink, CancellationToken ct) =>
{
    await sink.FlushAsync(ct);
});
```

## Options

Use `builder.Services.ConfigureLambdaHostOptions(options => { ... })`.

Important options:

- `InitTimeout` default 5s
- `InvocationCancellationBuffer` default 500ms
- `ShutdownDuration` default external extension window (500ms)
- `ShutdownDurationBuffer` default 50ms
- `ClearLambdaOutputFormatting`
- `BootstrapHttpClient`
- `BootstrapOptions`

## Common pitfalls

- Missing `[FromEvent]` for payload handler → generator diagnostic.
- Duplicate `[FromEvent]` → generator diagnostic.
- Registering middleware after `MapHandler` likely means it will not wrap as intended.
- Multiple `MapHandler` runtime calls → `InvalidOperationException`.
- Manual service resolution everywhere → less testable; prefer injected params.
- Ignoring cancellation token → bad Lambda timeout behavior.
