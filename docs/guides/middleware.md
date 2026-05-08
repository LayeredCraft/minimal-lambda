# Middleware

`minimal-lambda` uses a middleware model similar to ASP.NET Core: each component gets a context
object, runs code before/after the next component, and can short-circuit the pipeline. If you're new
to the pattern, the
[ASP.NET Core middleware overview](https://learn.microsoft.com/aspnet/core/fundamentals/middleware/)
is a helpful primer. This guide focuses on Lambda-specific behavior: invocation scopes, feature
access, and composition tips that keep middleware and handlers decoupled without extra DI plumbing.

## Pipeline Basics

Register middleware before calling `MapHandler`. Components execute in registration order and unwind
in
reverse order:

```csharp title="Program.cs"
var builder = LambdaApplication.CreateBuilder();
var lambda = builder.Build();

lambda.UseMiddleware(async (context, next) =>
{
    Console.WriteLine("[Logging] Before handler");
    await next(context);
    Console.WriteLine("[Logging] After handler");
});

lambda.UseMiddleware(async (context, next) =>
{
    Console.WriteLine("[Metrics] Before handler");
    await next(context);
    Console.WriteLine("[Metrics] After handler");
});

lambda.MapHandler(([FromEvent] Request request) => new Response("ok"));
await lambda.RunAsync();
```

Output:

```
[Logging] Before handler
[Metrics] Before handler
[Metrics] After handler
[Logging] After handler
```

## `ILambdaInvocationContext`

Every middleware receives the same `ILambdaInvocationContext`, which is scoped to the invocation.

```csharp title="Program.cs"
lambda.UseMiddleware(async (context, next) =>
{
    var logger = context.ServiceProvider.GetRequiredService<ILogger<Program>>();

    if (context.CancellationToken.IsCancellationRequested)
    {
        logger.LogWarning("Invocation cancelled before handler");
        return;
    }

    context.Items["RequestId"] = Guid.NewGuid().ToString();
    context.Items["Start"] = DateTimeOffset.UtcNow;
    context.Properties["Version"] ??= "1.0.0"; // safe cross-invocation value

    await next(context);

    var started = (DateTimeOffset)context.Items["Start"];
    logger.LogInformation("Completed in {Duration}ms", (DateTimeOffset.UtcNow - started).TotalMilliseconds);
});
```

Key members:

- `ServiceProvider` – resolve scoped services for the invocation.
- `CancellationToken` – fires before Lambda termination (buffer controlled by
    `LambdaHostOptions.InvocationCancellationBuffer`). Pass it to downstream async work.
- `Items` – per-invocation storage shared by middleware/handler.
- `Properties` – cross-invocation storage.
- `Features` – typed capabilities such as `IEventFeature<T>` and `IResponseFeature<T>` that let
    middleware collaborate without injecting each other.

## Middleware Approaches

`MinimalLambda` supports two middleware styles:

**Inline delegates** – Best for simple, application-specific middleware that orchestrates services.
Quick to write and keeps logic visible in the pipeline configuration.

**Class-based middleware** – Best for complex, reusable middleware with dependencies, state
management,
or disposal needs. Easier to test and share across projects.

Most applications use both: inline delegates for orchestration, class-based for heavy lifting.

### Inline Middleware

Inline middleware uses delegates registered directly in `Program.cs`. Despite the inline syntax,
you have full access to the invocation context and all its capabilities:

```csharp title="Program.cs"
lambda.UseMiddleware(async (context, next) =>
{
    // Resolve services from DI
    var cache = context.ServiceProvider.GetRequiredService<ICache>();
    var logger = context.ServiceProvider.GetRequiredService<ILogger<Program>>();

    // Access event data using type-safe helpers
    if (context.TryGetEvent<OrderRequest>(out var request))
    {
        logger.LogInformation("Processing order {OrderId}", request.OrderId);

        // Check cache before continuing
        if (cache.TryGet(request.OrderId, out OrderResponse cached))
        {
            context.Features.Get<IResponseFeature<OrderResponse>>()!.Response = cached;
            return; // Short-circuit
        }
    }

    // Store per-invocation data
    context.Items["RequestId"] = Guid.NewGuid().ToString();
    context.Items["StartTime"] = DateTimeOffset.UtcNow;

    await next(context);

    // Access response after handler executes
    var response = context.GetResponse<OrderResponse>();
    if (response is not null && request is not null)
    {
        await cache.SetAsync(request.OrderId, response);
    }
});
```

**What inline middleware can access:**

- **Dependency Injection** - `context.ServiceProvider.GetRequiredService<T>()`
- **Event/Response Data** - `GetEvent<T>()`, `GetResponse<T>()`, `TryGetEvent<T>()` (
    see [Type-Safe Feature Access](#type-safe-feature-access))
- **Features** - `context.Features.Get<IEventFeature<T>>()` (
    see [Working with Features](#working-with-features))
- **Per-Invocation State** - `context.Items` for temporary data within the request
- **Cross-Invocation State** - `context.Properties` for data shared across Lambda invocations
- **Cancellation** - `context.CancellationToken` for cooperative cancellation
- **AWS Context** - All standard `ILambdaContext` properties (`AwsRequestId`, `RemainingTime`, etc.)

**When to use inline middleware:**

- Application-specific orchestration logic
- Simple logging, metrics, or tracing
- One-off middleware that won't be reused
- Quick prototyping before extracting to a class
- Gluing together services without needing a separate file

**Best practice:** Keep inline middleware thin. Push complex logic into services registered in DI so
the middleware stays readable and testable. Treat inline middleware as the glue between services.

### Class-Based Middleware

Class-based middleware promotes reusability, testability, and clean separation of concerns. Define a
class implementing `ILambdaMiddleware`, then register it with `UseMiddleware<T>()`.

```csharp title="LoggingMiddleware.cs" linenums="1"
using System.Diagnostics;
using MinimalLambda;

internal sealed class LoggingMiddleware : ILambdaMiddleware
{
    private readonly ILogger<LoggingMiddleware> _logger;

    public LoggingMiddleware(ILogger<LoggingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(ILambdaInvocationContext context, LambdaInvocationDelegate next)
    {
        _logger.LogInformation("Invocation starting");

        var stopwatch = Stopwatch.StartNew();

        await next(context);

        _logger.LogInformation("Invocation completed in {Duration}ms", stopwatch.ElapsedMilliseconds);
    }
}
```

```csharp title="Program.cs"
var builder = LambdaApplication.CreateBuilder();
var lambda = builder.Build();

lambda.UseMiddleware<LoggingMiddleware>();

lambda.MapHandler(([FromEvent] Request req) => new Response("OK"));

await lambda.RunAsync();
```

**How it works:** Source generators intercept `UseMiddleware<T>()` at compile-time, generating code
that instantiates your middleware and resolves constructor parameters automatically. No reflection,
no runtime overhead.

!!! tip "Reusable packages"

    Class-based middleware is a good fit for shared packages: ship the middleware type and attributes,
    and the consuming app's build generates the wiring code. The generated code lives in the
    application's build output, not in your package.

#### Dependency Injection

Constructor parameters are automatically resolved from the DI container:

```csharp title="ValidationMiddleware.cs" linenums="1"
using MinimalLambda;

internal sealed class ValidationMiddleware : ILambdaMiddleware
{
    private readonly IValidator _validator;
    private readonly ILogger<ValidationMiddleware> _logger;
    private readonly IMetrics _metrics;

    public ValidationMiddleware(
        IValidator validator,
        ILogger<ValidationMiddleware> logger,
        IMetrics metrics)
    {
        _validator = validator;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task InvokeAsync(ILambdaInvocationContext context, LambdaInvocationDelegate next)
    {
        if (!context.TryGetEvent<OrderRequest>(out var request))
        {
            _logger.LogWarning("No event found for validation");
            return;
        }

        var result = await _validator.ValidateAsync(request);

        if (!result.IsValid)
        {
            _metrics.IncrementCounter("validation.failed");
            context.Features.Get<IResponseFeature<ErrorResponse>>()!.Response
                = new ErrorResponse(result.Errors);
            return; // short-circuit
        }

        await next(context);
    }
}
```

**Default resolution behavior:**

- Parameters without attributes first check args passed to `UseMiddleware<T>()`, then fall back to
    DI
- Services must be registered in `builder.Services` before calling `builder.Build()`
- Use `[FromServices]` to skip args and resolve directly from DI (
    see [Parameter Sources](#parameter-sources))

For more on service lifetimes and DI patterns, see [Dependency Injection](dependency-injection.md).

#### Factory-Based Middleware

When middleware construction needs to be customized or deferred, register a factory that implements
`ILambdaMiddlewareFactory` and use `UseMiddleware<TFactory>()`. The factory is resolved from the
invocation's `ServiceProvider` and executed per invocation. If the created middleware implements
`IDisposable` or `IAsyncDisposable`, it is disposed after the invocation completes.

```csharp title="CachingMiddlewareFactory.cs" linenums="1"
using MinimalLambda;

internal sealed class CachingMiddlewareFactory(ICache cache, ILogger<CachingMiddleware> logger)
    : ILambdaMiddlewareFactory
{
    public ILambdaMiddleware Create() => new CachingMiddleware(cache, logger);
}
```

```csharp title="Program.cs"
var builder = LambdaApplication.CreateBuilder();
builder.Services.AddSingleton<ICache, RedisCache>();
builder.Services.AddSingleton<ILambdaMiddlewareFactory, CachingMiddlewareFactory>();

var lambda = builder.Build();
lambda.UseMiddleware<CachingMiddlewareFactory>();

lambda.MapHandler(([FromEvent] OrderRequest req) => ProcessOrder(req));
await lambda.RunAsync();
```

#### Parameter Sources

Control how constructor parameters are resolved using attributes:

| Attribute             | Source        | Behavior                                                |
| --------------------- | ------------- | ------------------------------------------------------- |
| (none)                | Args, then DI | Try args first, fall back to DI if no match             |
| `[FromServices]`      | DI only       | Resolve from DI container, skip args                    |
| `[FromKeyedServices]` | Keyed DI      | Resolve keyed service from DI (e.g., `"primary"` cache) |
| `[FromArguments]`     | Args only     | Require value from args; throw if not found             |

**Example: Mixed Parameter Sources**

```csharp title="CachingMiddleware.cs" linenums="1"
using Microsoft.Extensions.DependencyInjection;
using MinimalLambda.Builder;

internal sealed class CachingMiddleware : ILambdaMiddleware
{
    private readonly string _cacheKey;
    private readonly ICache _primaryCache;
    private readonly ICache _fallbackCache;
    private readonly ILogger<CachingMiddleware> _logger;
    private readonly IMetrics? _metrics;

    public CachingMiddleware(
        [FromArguments] string cacheKey,                    // Required from args
        [FromKeyedServices("primary")] ICache primaryCache, // Keyed service
        [FromKeyedServices("fallback")] ICache fallbackCache,
        [FromServices] ILogger<CachingMiddleware> logger,   // Explicit DI
        IMetrics? metrics)                                  // Optional: args or DI
    {
        _cacheKey = cacheKey;
        _primaryCache = primaryCache;
        _fallbackCache = fallbackCache;
        _logger = logger;
        _metrics = metrics;
    }

    public async Task InvokeAsync(ILambdaInvocationContext context, LambdaInvocationDelegate next)
    {
        if (await _primaryCache.TryGetAsync(_cacheKey, out var cached))
        {
            context.Features.Get<IResponseFeature<Response>>()!.Response = cached;
            _metrics?.IncrementCounter("cache.hit");
            return;
        }

        await next(context);

        // Cache the response
        var response = context.GetResponse<Response>();
        if (response is not null)
        {
            await _primaryCache.SetAsync(_cacheKey, response);
        }
    }
}
```

```csharp title="Program.cs" linenums="1"
var builder = LambdaApplication.CreateBuilder();

builder.Services.AddKeyedSingleton<ICache, RedisCache>("primary");
builder.Services.AddKeyedSingleton<ICache, MemoryCache>("fallback");
builder.Services.AddSingleton<ILogger<CachingMiddleware>, Logger>();
builder.Services.AddSingleton<IMetrics, CloudWatchMetrics>(); // Optional

var lambda = builder.Build();

lambda.UseMiddleware<CachingMiddleware>("order-cache"); // Pass cacheKey as arg

lambda.MapHandler(([FromEvent] OrderRequest req) => ProcessOrder(req));

await lambda.RunAsync();
```

!!! tip "When to use `[FromArguments]`"

    Use `[FromArguments]` for configuration values that vary per middleware registration (like
    cache keys, API endpoints, or feature flags). This makes the middleware reusable with
    different configurations.

#### Multiple Constructors

When a middleware class has multiple constructors, the source generator selects the one with the
**most parameters** by default. Override this behavior with `[MiddlewareConstructor]`:

```csharp title="AuthMiddleware.cs" linenums="1"
using MinimalLambda.Builder;

internal sealed class AuthMiddleware : ILambdaMiddleware
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthMiddleware> _logger;
    private readonly bool _allowAnonymous;

    // This constructor has more parameters, so it would be selected by default
    public AuthMiddleware(
        IAuthService authService,
        ILogger<AuthMiddleware> logger,
        bool allowAnonymous)
    {
        _authService = authService;
        _logger = logger;
        _allowAnonymous = allowAnonymous;
    }

    // Explicitly select this simpler constructor instead
    [MiddlewareConstructor]
    public AuthMiddleware(IAuthService authService)
    {
        _authService = authService;
        _logger = null!;
        _allowAnonymous = false;
    }

    public async Task InvokeAsync(ILambdaInvocationContext context, LambdaInvocationDelegate next)
    {
        var token = context.Items["AuthToken"] as string;

        if (token is null && !_allowAnonymous)
        {
            _logger?.LogWarning("Missing authentication token");
            return;
        }

        if (token is not null && !await _authService.ValidateAsync(token))
        {
            _logger?.LogWarning("Invalid authentication token");
            return;
        }

        await next(context);
    }
}
```

!!! warning

    Only one constructor can have `[MiddlewareConstructor]`. Applying it to multiple constructors
    triggers compile-time diagnostic **LH0005**.

#### Lifecycle and Disposal

Middleware instances are created **per invocation**. Each Lambda invocation gets a fresh instance,
which is disposed after the invocation completes.

**IDisposable and IAsyncDisposable:**

```csharp title="TracingMiddleware.cs" linenums="1"
using System.Diagnostics;

internal sealed class TracingMiddleware : ILambdaMiddleware, IAsyncDisposable
{
    private readonly ITracer _tracer;
    private ISpan? _span;

    public TracingMiddleware(ITracer tracer)
    {
        _tracer = tracer;
    }

    public async Task InvokeAsync(ILambdaInvocationContext context, LambdaInvocationDelegate next)
    {
        _span = _tracer.StartSpan("lambda.invocation");
        _span.SetAttribute("requestId", context.Items["RequestId"]?.ToString() ?? "unknown");

        try
        {
            await next(context);
            _span.SetStatus(SpanStatus.Ok);
        }
        catch (Exception ex)
        {
            _span.SetStatus(SpanStatus.Error);
            _span.RecordException(ex);
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_span is not null)
        {
            await _span.EndAsync();
            await _tracer.FlushAsync();
        }
    }
}
```

**Disposal timing:**

- `Dispose()` or `DisposeAsync()` is called after `InvokeAsync()` completes
- Even if an exception occurs, disposal is guaranteed (wrapped in try/finally)
- The generated code prefers `IAsyncDisposable` over `IDisposable` if both are implemented

!!! info "Singleton vs. Per-Invocation"

    Unlike services registered in DI (which can be singleton, scoped, or transient), middleware
    instances are **always per-invocation**. For shared state, inject singleton services into the
    middleware constructor.

For more on lifecycle hooks, see [Lifecycle Management](lifecycle-management.md).

#### Testing Class-Based Middleware

Class-based middleware is straightforward to test: create instances directly and verify behavior.

```csharp title="LoggingMiddlewareTests.cs" linenums="1"
[Theory]
[AutoNSubstituteData]
internal async Task InvokeAsync_LogsStartAndCompletion(
    [Frozen] ILogger<LoggingMiddleware> logger,
    LoggingMiddleware middleware,
    ILambdaInvocationContext context,
    LambdaInvocationDelegate next)
{
    // Act
    await middleware.InvokeAsync(context, next);

    // Assert
    logger.Received(1).Log(
        LogLevel.Information,
        Arg.Any<EventId>(),
        Arg.Is<object>(o => o.ToString()!.Contains("starting")),
        null,
        Arg.Any<Func<object, Exception?, string>>());

    logger.Received(1).Log(
        LogLevel.Information,
        Arg.Any<EventId>(),
        Arg.Is<object>(o => o.ToString()!.Contains("completed")),
        null,
        Arg.Any<Func<object, Exception?, string>>());

    await next.Received(1).Invoke(context);
}
```

**Testing with parameter resolution:**

```csharp title="CachingMiddlewareTests.cs" linenums="1"
[Fact]
internal async Task InvokeAsync_ReturnsCachedResult_WhenCacheHit()
{
    // Arrange
    var primaryCache = Substitute.For<ICache>();
    var fallbackCache = Substitute.For<ICache>();
    var logger = Substitute.For<ILogger<CachingMiddleware>>();
    var cachedResponse = new Response("cached");

    primaryCache.TryGetAsync("test-key", out Arg.Any<Response>())
        .Returns(x =>
        {
            x[1] = cachedResponse;
            return true;
        });

    var middleware = new CachingMiddleware(
        "test-key",
        primaryCache,
        fallbackCache,
        logger,
        null);

    var context = Substitute.For<ILambdaInvocationContext>();
    var responseFeature = Substitute.For<IResponseFeature<Response>>();
    context.Features.Get<IResponseFeature<Response>>().Returns(responseFeature);

    var next = Substitute.For<LambdaInvocationDelegate>();

    // Act
    await middleware.InvokeAsync(context, next);

    // Assert
    responseFeature.Response.Should().Be(cachedResponse);
    await next.DidNotReceive().Invoke(Arg.Any<ILambdaInvocationContext>());
}
```

!!! tip "Testing Strategy"

    Test middleware in isolation by mocking `ILambdaInvocationContext` and the `next` delegate.
    This keeps tests fast and focused on middleware behavior without spinning up the entire pipeline.

For more testing patterns, see [Testing](testing.md).

#### Real-World Examples

**JWT Authentication:**

```csharp title="JwtAuthMiddleware.cs" linenums="1"
internal sealed class JwtAuthMiddleware : ILambdaMiddleware
{
    private readonly IJwtValidator _jwtValidator;
    private readonly ILogger<JwtAuthMiddleware> _logger;

    public JwtAuthMiddleware(
        IJwtValidator jwtValidator,
        ILogger<JwtAuthMiddleware> logger)
    {
        _jwtValidator = jwtValidator;
        _logger = logger;
    }

    public async Task InvokeAsync(ILambdaInvocationContext context, LambdaInvocationDelegate next)
    {
        // Extract JWT from event (e.g., API Gateway authorizer context)
        var request = context.GetEvent<ApiGatewayProxyRequest>();
        if (request?.Headers is null ||
            !request.Headers.TryGetValue("Authorization", out var authHeader))
        {
            _logger.LogWarning("Missing Authorization header");
            SetUnauthorizedResponse(context);
            return;
        }

        var token = authHeader.Replace("Bearer ", string.Empty);

        var validationResult = await _jwtValidator.ValidateAsync(token, context.CancellationToken);
        if (!validationResult.IsValid)
        {
            _logger.LogWarning("Invalid JWT token: {Reason}", validationResult.FailureReason);
            SetUnauthorizedResponse(context);
            return;
        }

        // Store claims for downstream handlers
        context.Items["User"] = validationResult.User;
        context.Items["Claims"] = validationResult.Claims;

        await next(context);
    }

    private static void SetUnauthorizedResponse(ILambdaInvocationContext context)
    {
        var responseFeature = context.Features.Get<IResponseFeature<ApiGatewayProxyResponse>>();
        if (responseFeature is not null)
        {
            responseFeature.Response = new ApiGatewayProxyResponse
            {
                StatusCode = 401,
                Body = "{\"error\":\"Unauthorized\"}"
            };
        }
    }
}
```

**Request/Response Transformation (Envelopes):**

```csharp title="EnvelopeMiddleware.cs" linenums="1"
internal sealed class EnvelopeMiddleware : ILambdaMiddleware
{
    private readonly ILogger<EnvelopeMiddleware> _logger;

    public EnvelopeMiddleware(ILogger<EnvelopeMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task InvokeAsync(ILambdaInvocationContext context, LambdaInvocationDelegate next)
    {
        // Unwrap envelope before handler
        var rawEvent = context.GetEvent<EnvelopedEvent>();
        if (rawEvent?.Payload is not null)
        {
            var innerEvent = JsonSerializer.Deserialize<OrderRequest>(rawEvent.Payload);
            context.Features.Get<IEventFeature<OrderRequest>>()!.Event = innerEvent;
        }

        await next(context);

        // Wrap response in envelope
        var rawResponse = context.GetResponse<OrderResponse>();
        if (rawResponse is not null)
        {
            var envelope = new EnvelopedResponse
            {
                Timestamp = DateTimeOffset.UtcNow,
                CorrelationId = rawEvent?.CorrelationId ?? Guid.NewGuid().ToString(),
                Payload = JsonSerializer.Serialize(rawResponse)
            };

            context.Features.Get<IResponseFeature<EnvelopedResponse>>()!.Response = envelope;
        }
    }
}
```

**Distributed Tracing (OpenTelemetry):**

```csharp title="OpenTelemetryMiddleware.cs" linenums="1"
using System.Diagnostics;

internal sealed class OpenTelemetryMiddleware : ILambdaMiddleware, IDisposable
{
    private readonly ActivitySource _activitySource;
    private Activity? _activity;

    public OpenTelemetryMiddleware([FromArguments] string serviceName)
    {
        _activitySource = new ActivitySource(serviceName);
    }

    public async Task InvokeAsync(ILambdaInvocationContext context, LambdaInvocationDelegate next)
    {
        _activity = _activitySource.StartActivity("lambda.invocation", ActivityKind.Server);

        if (_activity is not null)
        {
            _activity.SetTag("aws.request_id", context.AwsRequestId);
            _activity.SetTag("service.name", _activitySource.Name);
        }

        try
        {
            await next(context);
            _activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (Exception ex)
        {
            _activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            _activity?.RecordException(ex);
            throw;
        }
    }

    public void Dispose()
    {
        _activity?.Dispose();
    }
}
```

```csharp title="Program.cs"
lambda.UseMiddleware<OpenTelemetryMiddleware>("my-lambda-service");
```

#### Common Patterns

**Pattern: Conditional Middleware**

Run middleware only for specific event types:

```csharp title="ConditionalValidationMiddleware.cs"
internal sealed class ConditionalValidationMiddleware : ILambdaMiddleware
{
    private readonly IValidator<OrderRequest> _validator;

    public ConditionalValidationMiddleware(IValidator<OrderRequest> validator)
    {
        _validator = validator;
    }

    public async Task InvokeAsync(ILambdaInvocationContext context, LambdaInvocationDelegate next)
    {
        // Only validate if event is OrderRequest
        if (context.TryGetEvent<OrderRequest>(out var order))
        {
            var result = await _validator.ValidateAsync(order);
            if (!result.IsValid)
            {
                // Set error response and short-circuit
                context.Features.Get<IResponseFeature<ErrorResponse>>()!.Response
                    = new ErrorResponse(result.Errors);
                return;
            }
        }

        await next(context);
    }
}
```

**Pattern: Shared State via DI**

Share state across invocations using singleton services:

```csharp title="RateLimitMiddleware.cs"
internal sealed class RateLimitMiddleware : ILambdaMiddleware
{
    private readonly IRateLimiter _rateLimiter; // Singleton service

    public RateLimitMiddleware(IRateLimiter rateLimiter)
    {
        _rateLimiter = rateLimiter; // Shared across invocations
    }

    public async Task InvokeAsync(ILambdaInvocationContext context, LambdaInvocationDelegate next)
    {
        var clientId = context.Items["ClientId"]?.ToString() ?? "unknown";

        if (!await _rateLimiter.AllowRequestAsync(clientId))
        {
            // Rate limit exceeded
            context.Features.Get<IResponseFeature<ErrorResponse>>()!.Response
                = new ErrorResponse("Rate limit exceeded");
            return;
        }

        await next(context);
    }
}
```

```csharp title="Program.cs"
builder.Services.AddSingleton<IRateLimiter, MemoryRateLimiter>(); // Singleton!

var lambda = builder.Build();
lambda.UseMiddleware<RateLimitMiddleware>();
```

**Pattern: Early Response**

Set a response and skip the handler:

```csharp title="MaintenanceModeMiddleware.cs"
internal sealed class MaintenanceModeMiddleware : ILambdaMiddleware
{
    private readonly IFeatureFlagService _featureFlags;

    public MaintenanceModeMiddleware(IFeatureFlagService featureFlags)
    {
        _featureFlags = featureFlags;
    }

    public async Task InvokeAsync(ILambdaInvocationContext context, LambdaInvocationDelegate next)
    {
        if (await _featureFlags.IsEnabledAsync("maintenance-mode"))
        {
            context.Features.Get<IResponseFeature<MaintenanceResponse>>()!.Response
                = new MaintenanceResponse("Service unavailable during maintenance");
            return; // Don't call next
        }

        await next(context);
    }
}
```

#### Diagnostics

The source generator validates middleware at compile-time:

| Diagnostic | Severity | Description                                                             |
| ---------- | -------- | ----------------------------------------------------------------------- |
| **LH0005** | Error    | Multiple constructors have `[MiddlewareConstructor]` (only one allowed) |
| **LH0006** | Error    | Middleware type must be a concrete class (not interface/abstract)       |

**Example: LH0006**

```csharp
// ❌ This triggers LH0006
lambda.UseMiddleware<ILambdaMiddleware>(); // Interface, not allowed

// ❌ This triggers LH0006
lambda.UseMiddleware<AbstractMiddleware>(); // Abstract class, not allowed

// ✅ This is correct
lambda.UseMiddleware<ConcreteMiddleware>(); // Concrete class
```

!!! info "Compile-Time Safety"

    These diagnostics catch configuration errors during build, not at runtime. This prevents
    deployment of misconfigured middleware.

## Working with Features

Features are type-keyed adapters stored inside `ILambdaInvocationContext.Features` (an
`IFeatureCollection`). They decouple middleware from handlers: a handler (or the framework)
populates a
feature, middleware reads or mutates it, and nobody needs to inject each other through DI. The
collection lazily creates features by asking every registered `IFeatureProvider` to build them when
first requested.

```csharp title="Program.cs"
using MinimalLambda.Abstractions.Features;

lambda.UseMiddleware(async (context, next) =>
{
    var eventFeature = context.Features.Get<IEventFeature<OrderRequest>>();
    if (eventFeature is { Event: { } request })
        Console.WriteLine($"Processing {request.OrderId}");

    await next(context);

    var responseFeature = context.Features.Get<IResponseFeature<OrderResponse>>();
    if (responseFeature?.Response is { } response)
        Console.WriteLine($"Result: {response.Status}");
});
```

Common features:

| Feature                       | Purpose                                                      |
| ----------------------------- | ------------------------------------------------------------ |
| `IEventFeature<TEvent>`       | Access the deserialized event payload                        |
| `IResponseFeature<TResponse>` | Inspect or replace the handler response before serialization |
| `IInvocationDataFeature`      | Access raw event/response streams for envelopes              |

**Why features matter:**

- Middleware can extract values set by handlers (or other middleware) without DI fan-out.
- Handlers remain free of middleware-specific dependencies; they just work with the event/response
    types.
- Custom features are easy to add—register an implementation of `IFeatureProvider` and it becomes
    available to all middleware.

### Type-Safe Feature Access

The framework provides convenient extension methods on `ILambdaInvocationContext` for type-safe
event and response access, simplifying the feature access pattern shown above:

```csharp title="Program.cs"
lambda.UseMiddleware(async (context, next) =>
{
    // Nullable access - returns null if not found
    var request = context.GetEvent<OrderRequest>();
    if (request is not null)
        Console.WriteLine($"Processing order {request.OrderId}");

    // Try pattern - safe null checking
    if (context.TryGetEvent<OrderRequest>(out var order))
    {
        // Use order safely without additional null checks
        Console.WriteLine($"Order {order.OrderId} has {order.Items.Count} items");
    }

    await next(context);

    // Required access - throws if not found
    var response = context.GetRequiredResponse<OrderResponse>();
    Console.WriteLine($"Status: {response.Status}");
});
```

**Available Methods:**

| Method                     | Description                             | Returns                                  |
| -------------------------- | --------------------------------------- | ---------------------------------------- |
| `GetEvent<T>()`            | Returns event or `null` if not found    | `T?`                                     |
| `GetResponse<T>()`         | Returns response or `null` if not found | `T?`                                     |
| `TryGetEvent<T>(out T)`    | Try-pattern for safe event access       | `bool`                                   |
| `TryGetResponse<T>(out T)` | Try-pattern for safe response access    | `bool`                                   |
| `GetRequiredEvent<T>()`    | Returns event or throws                 | `T` (throws `InvalidOperationException`) |
| `GetRequiredResponse<T>()` | Returns response or throws              | `T` (throws `InvalidOperationException`) |

**When to use each:**

- **Nullable methods** (`GetEvent<T>()`) – When the event/response might not exist and you'll handle
    null gracefully
- **Try pattern** (`TryGetEvent<T>()`) – When you want explicit null checking without additional
    conditionals
- **Required methods** (`GetRequiredEvent<T>()`) – When the event/response must exist and missing it
    is an error condition

These methods are equivalent to calling `context.Features.Get<IEventFeature<T>>()` and accessing the
event/response, but provide cleaner syntax and better null-safety annotations.

### Feature Providers in Practice

When `context.Features.Get<T>()` runs, `MinimalLambda` walks through every registered
`IFeatureProvider`
until one returns the requested feature. Built-in providers handle common cases such as response
serialization. Use the same pattern for your features.

```csharp title="DefaultResponseFeatureProvider.cs" linenums="1"
using Amazon.Lambda.Core;

namespace MinimalLambda;

/// <summary>
///     Provides a default implementation of <see cref="IResponseFeature" /> for Lambda response
///     serialization. This provider is instantiated by source-generated code to handle Lambda response
///     processing using the specified <see cref="ILambdaSerializer" />.
/// </summary>
public class DefaultResponseFeatureProvider<T>(ILambdaSerializer lambdaSerializer)
    : IFeatureProvider
{
    // ReSharper disable once StaticMemberInGenericType
    private static readonly Type FeatureType = typeof(IResponseFeature);

    /// <inheritdoc />
    public bool TryCreate(Type type, out object? feature)
    {
        feature = type == FeatureType ? new DefaultResponseFeature<T>(lambdaSerializer) : null;

        return feature is not null;
    }
}
```

Registering a provider is just another DI call:

```csharp title="Program.cs"
builder.Services.AddSingleton<IFeatureProvider, MyCorrelationFeatureProvider>(); // implements IFeatureProvider
```

Your provider can return singleton instances (for stateless metadata) or create fresh objects per
invocation.

## Short-Circuiting and Error Handling

Middleware can stop the pipeline early:

```csharp title="Caching"
lambda.UseMiddleware(async (context, next) =>
{
    var cache = context.ServiceProvider.GetRequiredService<ICache>();
    var request = context.Features.Get<IEventFeature<OrderRequest>>()?.Event;

    if (request is not null && cache.TryGet(request.OrderId, out OrderResponse cached))
    {
        context.Features.Get<IResponseFeature<OrderResponse>>()!.Response = cached;
        return; // skip handler
    }

    await next(context);
});
```

Wrap the pipeline to catch and translate exceptions:

```csharp title="Error Handling"
lambda.UseMiddleware(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (ValidationException ex)
    {
        var response = context.Features.Get<IResponseFeature<OrderResponse>>();
        if (response is not null)
            response.Response = new("invalid", ex.Message);
        return; // handled
    }
});
```

## Ordering Strategy

Register middleware from outermost to innermost. Mix inline delegates and class-based middleware
freely:

```csharp title="Order"
lambda.UseMiddleware<ErrorHandlingMiddleware>();   // Class-based: catches everything
lambda.UseMiddleware<LoggingMiddleware>();         // Class-based: logs every request

lambda.UseMiddleware(async (context, next) =>      // Inline: quick metric
{
    var sw = Stopwatch.StartNew();
    await next(context);
    Console.WriteLine($"Duration: {sw.ElapsedMilliseconds}ms");
});

lambda.UseMiddleware<AuthenticationMiddleware>();  // Class-based: auth first
lambda.UseMiddleware<AuthorizationMiddleware>();   // Class-based: then authorization
lambda.UseMiddleware<ValidationMiddleware>();      // Class-based: validate payloads
lambda.MapHandler(/* handler */);
```

Guidelines:

- Error/diagnostics (logging, metrics) go first so they see every request.
- Authentication/authorization should wrap validation and business logic.
- Response caching happens late so only valid, authorized responses are stored.
- Inline and class-based middleware execute in registration order - no difference in behavior.

## Configuration and Options

**Inline middleware** resolves services via `context.ServiceProvider`:

```csharp
lambda.UseMiddleware(async (context, next) =>
{
    var options = context.ServiceProvider.GetRequiredService<IOptions<MyOptions>>().Value;
    // Use options...
    await next(context);
});
```

**Class-based middleware** injects services via constructor:

```csharp
internal sealed class ConfiguredMiddleware : ILambdaMiddleware
{
    private readonly MyOptions _options;

    public ConfiguredMiddleware(IOptions<MyOptions> options)
    {
        _options = options.Value;
    }

    public async Task InvokeAsync(ILambdaInvocationContext context, LambdaInvocationDelegate next)
    {
        // Use _options...
        await next(context);
    }
}
```

Both approaches access the same options registered in `builder.Services.Configure<MyOptions>(...)`.

## Best Practices

**General:**

- **Keep middleware focused.** One responsibility per component (logging, metrics, caching, etc.).
- **Always call `await next(context)`** unless you intentionally short-circuit; forgetting it
    prevents the
    handler from running.
- **Never swallow exceptions silently.** If you handle an error, set a response or log it so Lambda
    doesn't
    report success unintentionally.
- **Use per-invocation state wisely.** `Items` is cleared after each request; `Properties` live for
    the life
    of the container and must be thread-safe.
- **Make cancellation cooperative.** Honor `context.CancellationToken` in middleware and pass it to
    downstream I/O.

**Inline Middleware:**

- Push complex logic into services so inline middleware stays thin and readable
- Use inline for orchestration, not implementation
- Great for prototyping before extracting to a class

**Class-Based Middleware:**

- Implement `IDisposable` or `IAsyncDisposable` if you acquire resources (connections, spans, etc.)
- Use `[FromArguments]` for configuration that varies per registration
- Inject dependencies via constructor for testability
- Share state across invocations via singleton services, not middleware fields
- Write unit tests by mocking `ILambdaInvocationContext` and the `next` delegate

**Choosing Between Inline and Class-Based:**

| Use Inline When...                         | Use Class-Based When...                           |
| ------------------------------------------ | ------------------------------------------------- |
| Middleware is application-specific         | Middleware will be reused across projects         |
| Logic is simple orchestration              | Logic is complex or has multiple responsibilities |
| No disposal or lifecycle management needed | Need `IDisposable` or `IAsyncDisposable` support  |
| Quickly prototyping or experimenting       | Ready to formalize and test thoroughly            |
| Tight coupling to app logic is acceptable  | Clean separation of concerns is important         |

With these patterns, you can build rich, testable pipelines around your Lambda handlers while
keeping
business logic small and focused.
