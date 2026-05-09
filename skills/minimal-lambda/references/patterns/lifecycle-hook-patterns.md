# Lifecycle hook patterns

Read when adding or reviewing `OnInit` and `OnShutdown` hooks.

## Default: inline lifecycle glue in `Program.cs`

Use hooks for Lambda lifecycle concerns:

- cold-start cache warmup
- required configuration/secrets validation
- expensive singleton preflight
- telemetry/buffer flush during shutdown
- bounded external cleanup

Keep hooks inline when logic is small or Lambda-specific.

```csharp
lambda.OnInit(async (ICacheWarmer warmer, CancellationToken ct) =>
{
    await warmer.WarmAsync(ct);
    return true;
});

lambda.OnShutdown(async (ITelemetrySink telemetry, CancellationToken ct) =>
{
    await telemetry.FlushAsync(ct);
});
```

Unlike inline middleware, lifecycle hook delegate overloads support direct DI parameters. Prefer DI
parameters over manual service resolution.

## Delegate complex work to services

Hooks should coordinate lifecycle; services should do real work.

```csharp
lambda.OnInit(async (IStartupChecks checks, CancellationToken ct) =>
    await checks.ValidateAsync(ct));
```

Good service inputs:

- `CancellationToken`
- options/config values
- primitive/domain values
- typed abstractions like `ICacheWarmer`, `IStartupChecks`, `ITelemetrySink`

Avoid service inputs:

- `ILambdaLifecycleContext`
- `ILambdaInvocationContext`
- raw AWS `ILambdaContext`
- feature collections or Lambda wrappers

Passing lifecycle/context objects into services is almost always a layer-boundary smell. Extract
values at the Lambda edge if a service needs them.

## `OnInit` return values

Return `bool` only when startup should be able to abort.

```csharp
lambda.OnInit(async (IStartupChecks checks, CancellationToken ct) =>
{
    var ok = await checks.ValidateAsync(ct);
    return ok;
});
```

- `true` continues startup.
- `false` aborts startup and prevents serving invocations.
- no return value implies success.

Use `false` intentionally; failed init means Lambda should not process events.

## Cancellation and time bounds

Always accept/pass `CancellationToken` for async hook work.

- `OnInit` token is bounded by `LambdaHostOptions.InitTimeout`.
- `OnShutdown` token is bounded by shutdown duration/buffer.
- Downstream services should honor cancellation promptly.

## Shared state

Lifecycle hooks run outside normal invocation flow.

- `OnInit` runs once per execution environment.
- `OnShutdown` runs once during teardown.
- Each hook handler gets fresh scope.
- Warm containers reuse singleton state after init.

Do not use lifecycle hooks for per-invocation state. Use scoped services or middleware for
invocation data.
