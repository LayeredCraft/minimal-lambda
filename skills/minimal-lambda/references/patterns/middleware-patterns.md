# Middleware patterns

Read when adding logging, metrics, validation, auth, idempotency, response mapping, or feature access.

Default: inline app-local middleware in `Program.cs`. Middleware should mostly be Lambda pipeline
glue: read context/features, set scopes/items/responses, call `next`, or delegate to services.
Inline middleware receives only `context` and `next`; it does not support direct service injection.
Resolve simple dependencies from `context.ServiceProvider`, use `UseMiddleware<T>()` class
middleware when constructor DI is cleaner, or use `UseMiddleware<TFactory>()` when construction must
be custom/deferred per invocation. Class middleware constructor parameters can come from DI and/or
explicit `UseMiddleware<T>(args)` values; use `[FromServices]` or `[FromArguments]` to remove
ambiguity. Keep Lambda context objects at this edge; services should receive domain values, options,
and `CancellationToken`, not `ILambdaInvocationContext`/`ILambdaContext`. Extract class middleware
when logic is complex, reusable, stateful, or needs direct unit tests.

## Inline logging/correlation

```csharp
lambda.UseMiddleware(async (context, next) =>
{
    var logger = context.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var correlationId = context.AwsRequestId;

    using var scope = logger.BeginScope(new Dictionary<string, object>
    {
        ["AwsRequestId"] = correlationId,
    });

    context.Items["CorrelationId"] = correlationId;

    await next(context);
});
```

Good for app-local glue. Keep heavy logic in services.

## Feature-based validation

```csharp
lambda.UseMiddleware(async (context, next) =>
{
    if (!context.TryGetEvent<OrderRequest>(out var request))
    {
        await next(context);
        return;
    }

    if (string.IsNullOrWhiteSpace(request.OrderId))
    {
        context.Features.Get<IResponseFeature<OrderResponse>>()!
            .SetResponse(new OrderResponse("", Accepted: false));
        return;
    }

    await next(context);
});
```

Features let middleware work with typed event/response without coupling to handler implementation.

## Class-based middleware

```csharp
internal sealed class TimingMiddleware(ILogger<TimingMiddleware> logger) : ILambdaMiddleware
{
    public async Task InvokeAsync(ILambdaInvocationContext context, LambdaInvocationDelegate next)
    {
        var started = Stopwatch.GetTimestamp();

        try
        {
            await next(context);
        }
        finally
        {
            var elapsed = Stopwatch.GetElapsedTime(started);
            logger.LogInformation("Invocation completed in {ElapsedMs} ms", elapsed.TotalMilliseconds);
        }
    }
}

lambda.UseMiddleware<TimingMiddleware>();
```

Use class middleware for reusable code, complex pipeline behavior, stateful middleware, or easier
unit tests. Do not extract a class just to hide a small inline delegate.

## Short-circuit cache

```csharp
lambda.UseMiddleware(async (context, next) =>
{
    var cache = context.ServiceProvider.GetRequiredService<IOrderCache>();

    if (context.TryGetEvent<OrderRequest>(out var request)
        && await cache.TryGetAsync(request.OrderId, context.CancellationToken) is { } cached)
    {
        context.Features.Get<IResponseFeature<OrderResponse>>()!.SetResponse(cached);
        return;
    }

    await next(context);

    if (request is not null && context.GetResponse<OrderResponse>() is { } response)
        await cache.SetAsync(request.OrderId, response, context.CancellationToken);
});
```

Place short-circuit middleware after auth/validation and before handler. If cache policy grows
beyond simple Lambda pipeline glue, delegate policy decisions to an injected service and pass
request ids/keys/values, not the whole Lambda context.

## Error boundary pattern

```csharp
lambda.UseMiddleware(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (ValidationException ex)
    {
        var logger = context.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Validation failed");
        context.Features.Get<IResponseFeature<ErrorResponse>>()!.SetResponse(new ErrorResponse(ex.Message));
    }
});
```

Use trigger-specific HTTP result/envelope for API Gateway/ALB when mapping errors to status codes.
