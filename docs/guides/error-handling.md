# Error Handling

`MinimalLambda` does not hide exceptions—it embraces the standard .NET model so you can decide where
to intercept failures. This guide explains how errors flow through handlers, middleware, and lifecycle
hooks so you can add the right amount of protection without fighting the framework.

## Invocation Errors: What Happens by Default

- Handlers run inside the middleware pipeline. If a handler throws and nothing catches the exception,
    `MinimalLambda` lets it bubble back through the pipeline.
- Once the exception leaves the outermost middleware, the AWS .NET Lambda runtime records the error,
    writes it to CloudWatch Logs, and returns a failed invocation (with retries governed by the event
    source).
- Because the runtime already reports unhandled errors, you only need to add custom handling when you
    want different logging, metrics, or response shaping.

```csharp title="Program.cs" linenums="1"
lambda.MapHandler(([FromEvent] OrderRequest request, IOrderService service) =>
    service.ProcessAsync(request) // unhandled exception flows to Lambda runtime
);
```

## Prefer Middleware for Error Handling

Wrapping the pipeline once keeps error handling consistent across every trigger. The snippet below
demonstrates a simple pattern: capture known exceptions, map them to responses (or metrics), and
allow everything else to bubble so Lambda reports the failure.

```csharp title="Program.cs" linenums="1"
lambda.UseMiddleware(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (ValidationException ex)
    {
        var logger = context.ServiceProvider.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Validation failed for {RequestId}", context.AwsRequestId);

        // Optional: set a response via feature/envelope APIs instead of rethrowing.
        throw;
    }
    catch (OperationCanceledException) when (context.CancellationToken.IsCancellationRequested)
    {
        // Lambda is about to time out; log and rethrow so the runtime records the failure.
        throw;
    }
});
```

**Recommendations**

- Register error-handling middleware first so it wraps every other component.
- Use the helper extensions (`context.GetResponse<T>()`, `context.GetEvent<T>()`, etc.) from
    `FeatureLambdaInvocationContextExtensions` (they wrap `ILambdaInvocationContext.Features`) when you need to read
    or replace the outgoing payload instead of throwing.
- Still rethrow fatal errors so the runtime produces accurate CloudWatch metrics and DLQ/SQS retries.

## Handler-Level Try/Catch

Keep handlers thin, but do catch exceptions when you want a different payload or domain-specific
logic. Leave everything else to your middleware/global policy.

```csharp title="Program.cs" linenums="1"
lambda.MapHandler(async ([FromEvent] CheckoutRequest request, ICheckoutService service) =>
{
    try
    {
        return await service.RunAsync(request);
    }
    catch (PaymentDeclinedException ex)
    {
        return new CheckoutResponse("Declined", ex.Message);
    }
});
```

## Lifecycle Hooks (OnInit / OnShutdown)

`LambdaApplication` runs OnInit and OnShutdown handlers outside the invocation pipeline, but each
handler executes in its own DI scope and errors are aggregated:

- **OnInit** – All handlers run concurrently with a token derived from
    `LambdaHostOptions.InitTimeout`. Each handler can optionally return `bool`. `MinimalLambda` collects
    every exception and throws an `AggregateException("Encountered errors while running OnInit handlers", …)`
    if any fail. If a handler returns `false`, initialization aborts even when no exception occurred.
- **OnShutdown** – Handlers also run concurrently. Any exception is captured and rethrown as an
    `AggregateException("Encountered errors while running OnShutdown handlers", …)` after every handler
    finishes (or faults). Use the provided `CancellationToken` to respect the remaining shutdown window.

```csharp title="Program.cs" linenums="1"
lambda.OnInit(async (ICache cache, CancellationToken ct) =>
{
    await cache.WarmAsync(ct);
    return true; // omit or return false to abort startup
});

lambda.OnShutdown(async (ITelemetry telemetry, CancellationToken ct) =>
{
    await telemetry.FlushAsync(ct); // exceptions are aggregated and rethrown
});
```

Because the host rethrows aggregate exceptions, CloudWatch logs clearly show every failing handler.

## Timeouts and Cancellation

`MinimalLambda` links invocation tokens to the Lambda timeout using
`LambdaHostOptions.InvocationCancellationBuffer`. Honor that token in services and middleware—if your
code catches `OperationCanceledException`, log and rethrow so the runtime still marks the invocation
as failed rather than silently succeeding.

```csharp title="Program.cs" linenums="1"
lambda.MapHandler(async (CancellationToken ct) =>
{
    await DoWorkAsync(ct);
    // do not swallow OperationCanceledException unless you intentionally handled the timeout
});
```

## Checklist

- Install error-handling middleware as the outermost component.
- Keep handler try/catch blocks targeted—handle what you expect, rethrow everything else.
- Respect `CancellationToken` in handlers, middleware, and lifecycle hooks.
- Remember that OnInit/OnShutdown aggregate exceptions and rethrow them; log inside the handler for context.
- Rely on AWS retry/DLQ behavior instead of suppressing errors unless you have a compelling reason.
