# Dependency Injection

`minimal-lambda` uses the same dependency injection container as ASP.NET Core
(`Microsoft.Extensions.DependencyInjection`). If you're new to DI in .NET, start with the
[official documentation](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection)
and then come back for Lambda-specific guidance. This guide focuses on what changes (and what stays the
same) when you run inside AWS Lambda.

## How the Container Is Created

```csharp title="Program.cs"
var builder = LambdaApplication.CreateBuilder();

builder.Services.AddSingleton<ICache, MemoryCache>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();
builder.Services.AddTransient<IValidator<OrderRequest>, OrderValidator>();

var lambda = builder.Build();
```

- `builder.Services` is the same `IServiceCollection` you use everywhere else in .NET.
- All registrations must happen **before** `builder.Build()`.
- Keep supporting types (records, services, options classes) at the bottom of `Program.cs`; keep the
    pipeline (DI, middleware, handlers, run) at the top so cold-start work stays easy to read.

## Service Lifetimes in Lambda

Lambda containers live across multiple invocations. Map the standard lifetimes to Lambda's lifecycle:

| Lifetime  | When it's created                | When it's disposed                        | Use for                                    |
| --------- | -------------------------------- | ----------------------------------------- | ------------------------------------------ |
| Singleton | During OnInit (first cold start) | When the execution environment shuts down | HttpClient, caches, telemetry, config      |
| Scoped    | Once per invocation              | After the invocation completes            | DbContext, repositories, per-request state |
| Transient | Every time it's requested        | After the requesting scope is disposed    | Lightweight helpers, pure functions        |

**Tips:**

- Scoped services are the default choice for anything that shouldn't leak state between invocations.
- Transients work the same as in ASP.NET Core, but prefer Scoped unless you truly need a new instance
    every time a constructor runs.

## Invocation Scope and `ILambdaInvocationContext`

Every invocation gets its own scope. You can access it via the `ILambdaInvocationContext` and it is shared
across middleware and handlers:

```csharp title="Handlers"
lambda.MapHandler(async (
    [FromEvent] OrderRequest request,
    IOrderService orders,          // scoped service
    ILambdaInvocationContext context,    // framework context
    CancellationToken cancellation // host-managed token
) =>
{
    context.Items["RequestId"] = request.Id;
    return await orders.ProcessAsync(request, cancellation);
});
```

`ILambdaInvocationContext` exposes:

- `ServiceProvider` – the scoped service provider for the invocation
- `CancellationToken` – automatically linked to Lambda remaining time
- `Items` – per-invocation storage shared by middleware/handlers
- `Properties` – cross-invocation storage backed by the singleton container
- `Features` – typed feature collections (advanced scenarios)

If your handler doesn't need the Lambda payload, omit the `[FromEvent]` parameter entirely and inject only services.

!!! tip "Cancellation buffers"

    The cancellation token fires slightly **before** AWS kills the process:

    - The runtime subtracts `LambdaHostOptions.InvocationCancellationBuffer` (default 500ms) from the
        remaining time when creating the token.
    - Always pass it down to outbound SDK calls and database queries so you can stop work cleanly.

## Middleware and Lifecycle Hooks: Source-Generated DI

- Middleware receives the invocation scope via the `ILambdaInvocationContext` argument. Resolve services with
    `context.ServiceProvider` or create reusable middleware classes with constructor injection.
- `OnInit` and `OnShutdown` handlers now use the same source-generated dependency injection as your main
    handlers. Each executes inside its own scoped service provider so you can warm caches, seed connections,
    or flush telemetry without leaking per-invocation services.

OnInit and OnShutdown handlers support multiple dependency injection patterns:

```csharp title="Pattern 1: Direct DI (Recommended)"
lambda.OnInit(async (ICache cache, ILogger<Program> logger, CancellationToken ct) =>
{
    logger.LogInformation("Warming cache during cold start");
    await cache.WarmUpAsync(ct);
    return true;
});
```

Each handler runs in its own scoped service provider, so you can safely resolve scoped services even
outside the invocation pipeline.

```csharp title="Pattern 2: Using ILambdaLifecycleContext"
lambda.OnInit(async (ILambdaLifecycleContext context, ICache cache) =>
{
    var logger = context.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation(
        "Init type: {Type}, Function: {Name}, Memory: {MB}MB",
        context.InitializationType,
        context.FunctionName,
        context.FunctionMemorySize
    );

    await cache.WarmUpAsync(context.CancellationToken);
    return true;
});
```

Use `ILambdaLifecycleContext` when you need AWS environment metadata (region, function name, memory size,
initialization type) or want to share data between handlers via the `Properties` dictionary.

```csharp title="Pattern 3: Keyed Services"
builder.Services.AddKeyedSingleton<ICache, RedisCache>("redis");
builder.Services.AddKeyedSingleton<ICache, MemoryCache>("memory");

lambda.OnInit(async (
    [FromKeyedServices("redis")] ICache primaryCache,
    [FromKeyedServices("memory")] ICache fallbackCache,
    CancellationToken ct
) =>
{
    await primaryCache.WarmUpAsync(ct);
    await fallbackCache.WarmUpAsync(ct);
    return true;
});
```

## Configuration and Options

Use the standard options pattern for configuration:

```csharp title="Program.cs"
builder.Services.Configure<OrderProcessingOptions>(
    builder.Configuration.GetSection("OrderProcessing"));
```

Prefer `IOptions<T>` inside handlers/services so the value is captured once per cold start. Snapshot/Monitor
variants work but rarely matter in Lambda because configuration usually ships with the deployment package.

## Patterns That Work Well

- **Constructor injection everywhere** – middleware, handlers, lifecycle hooks can all resolve services
    directly. Avoid service locator patterns unless you truly need dynamic lookups.
- **Decorator pattern** – use `builder.Services.Decorate<TService, TDecorator>()` (from Scrutor) to add
    caching, logging, or retry behavior without touching core services.
- **Keyed services** – register multiple implementations with `AddKeyed{Lifetime}` and inject the
    one you need via `[FromKeyedServices]`.

### Keyed Services in Practice

```csharp title="Program.cs"
builder.Services.AddKeyedSingleton<INotifier, EmailNotifier>("email");
builder.Services.AddKeyedSingleton<INotifier, SmsNotifier>("sms");

lambda.MapHandler((
    [FromEvent] Order order,
    [FromKeyedServices("sms")] INotifier notifier
) => notifier.NotifyAsync(order));
```

- Keys can be strings, enums, numeric types, or even `Type` instances.
- Optional services are supported by making the parameter nullable.
- The generated code throws a descriptive exception if the service provider doesn't support keyed
    services (e.g., if you run on an older DI container).

## Host-Specific Pitfalls

| Pitfall                               | Impact                                              | Fix                                                                                     |
| ------------------------------------- | --------------------------------------------------- | --------------------------------------------------------------------------------------- |
| Singleton depends on a scoped service | Scoped instance from first invocation leaks forever | Inject `IServiceProvider`, create a scope, resolve the scoped service inside the method |
| Storing scoped services in singletons | `ObjectDisposedException` on later invocations      | Keep scoped dependencies scoped; pass data instead of services                          |
| Over-injecting handlers               | Hard-to-test functions with 8+ services             | Move orchestration into services; keep handlers thin                                    |
| Forgetting cancellation tokens        | Lambda kills the environment mid-work               | Always inject `CancellationToken` and pass it down                                      |

## Key Takeaways

- Register everything before `builder.Build()` so the container is ready for cold starts.
- Map lifetimes to the Lambda lifecycle: singleton for shared resources, scoped for per-invocation work, transient only when necessary.
- Always pass the host-provided `CancellationToken`; adjust `InvocationCancellationBuffer` when you need more time to wind down.
- Prefer constructor injection in handlers/middleware/lifecycle hooks—avoid service locator patterns.
- Use the options pattern for config and keyed services for multiple implementations.
- For fundamentals, refer back to the [official DI docs](https://learn.microsoft.com/en-us/dotnet/core/extensions/dependency-injection).

With these patterns, `MinimalLambda` feels just like ASP.NET Core, but tuned for the Lambda lifecycle.
