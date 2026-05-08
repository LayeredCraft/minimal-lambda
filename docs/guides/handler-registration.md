# Handler Registration

`MapHandler` is the entry point for telling `MinimalLambda` which delegate should process events. The call looks like an ordinary lambda registration, but source generators intercept it at compile time to wire up serialization, dependency injection, and middleware without reflection.

## Registering a Handler

```csharp title="Program.cs" linenums="1"
using MinimalLambda;
using Microsoft.Extensions.DependencyInjection;

var builder = LambdaApplication.CreateBuilder();

builder.Services.AddScoped<IGreetingService, GreetingService>();

var lambda = builder.Build();

lambda.MapHandler(([FromEvent] string name, IGreetingService greetings) =>
    greetings.Greet(name)
);

await lambda.RunAsync();

sealed record GreetingResponse(string Message);

sealed class GreetingService : IGreetingService
{
    public GreetingResponse Greet(string name) => new($"Hello, {name}!");
}

interface IGreetingService
{
    GreetingResponse Greet(string name);
}
```

- While you can have multiple `MapHandler` calls in your code, only one can be registered at runtime (see [Multiple MapHandler Calls](#multiple-maphandler-calls) below).
- The generated handler feeds into the normal invocation builder, so all middleware, features, and diagnostics apply equally to handlers created via `Handle` or `MapHandler`.

## Multiple MapHandler Calls

You can have multiple `MapHandler` calls in your code, but **only one handler can be registered per Lambda execution**. If more than one `MapHandler` is called at runtime, an exception will be thrown.

This design enables several useful patterns:

### Use Cases

**Library-Provided Handlers**

Libraries can expose pre-configured handlers that applications can opt into:

```csharp title="MyLibrary.cs" linenums="1"
namespace MyLibrary;

public static class OrderHandlers
{
    public static void MapOrderHandler(this ILambdaInvocationBuilder app)
    {
        app.MapHandler(([FromEvent] OrderRequest req, IOrderService orders) =>
            orders.ProcessAsync(req)
        );
    }
}
```

```csharp title="Program.cs" linenums="1"
using MyLibrary;

var builder = LambdaApplication.CreateBuilder();
builder.Services.AddOrderServices(); // Library-provided services

var lambda = builder.Build();

// Use the library's handler
lambda.MapOrderHandler();

await lambda.RunAsync();
```

### Important Notes

- Only **one** `MapHandler` can execute at runtime—calling multiple will throw an `InvalidOperationException`
- Feature providers are registered when `MapHandler` is called, so ensure the registered handler matches your expected event/response types
- This pattern is designed for **conditional registration**, not for handling multiple event types in a single Lambda (which requires a custom routing solution)

## Handler Signatures and the `[FromEvent]` Parameter

Handlers that receive an incoming payload must identify exactly one parameter with `[FromEvent]`. The generator uses that marker to synthesize deserialization logic (JSON by default, or whatever envelope/serializer is active). If your Lambda does **not** expect input (e.g., scheduled jobs, health checks, etc.), you can omit the `[FromEvent]` attribute entirely—just define a handler with no payload parameter and `MinimalLambda` skips the event binding phase.

- `[FromEvent]` may appear on reference types, structs, records, collection types, or envelope types such as `ApiGatewayRequestEnvelope<T>`.
- Handlers without payloads can simply omit `[FromEvent]` by not declaring an event parameter at all.
- When you do accept a payload, exactly one parameter must be annotated. Missing or duplicate `[FromEvent]` attributes trigger compile-time diagnostics so you catch signature issues early.

```csharp title="Program.cs" linenums="1"
// No incoming event required
lambda.MapHandler((ILogger<Program> logger) =>
{
    logger.LogInformation("Heartbeat fired at {Timestamp}", DateTimeOffset.UtcNow);
});
```

### Parameter Sources

Handlers can mix lambda events with services, context objects, and cancellation tokens. This table shows what the generator knows how to supply:

| Parameter                                       | Source                                                                                                                                                        |
| ----------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `[FromEvent] T event`                           | Deserialized from the Lambda payload (or envelope). Optional—only include when the handler expects an input.                                                  |
| `IServiceType service`                          | Resolved from the DI container using the invocation scope.                                                                                                    |
| `[FromKeyedServices("key")] IServiceType keyed` | Resolves a keyed service registered with `AddKeyed*`. Keys must be constants supported by the BCL.                                                            |
| `ILambdaInvocationContext context`              | Framework context that extends `ILambdaContext`, exposes scoped `ServiceProvider`, `Items`, `Features`, `Properties`, and the invocation `CancellationToken`. |
| `ILambdaContext lambdaContext`                  | Raw AWS Lambda context for folks that prefer the SDK contract.                                                                                                |
| `CancellationToken cancellationToken`           | Cancels when `InvocationCancellationBuffer` elapses before the Lambda timeout.                                                                                |

```csharp title="Program.cs" linenums="1"
lambda.MapHandler(async (
    [FromEvent] OrderRequest request,
    [FromKeyedServices("primary")] IOrderProcessor orderProcessor,
    ILambdaInvocationContext context,
    CancellationToken ct
) =>
{
    context.Items["RequestId"] = context.AwsRequestId;

    var response = await orderProcessor.ProcessAsync(request, ct);
    context.Properties["OrdersProcessed"] = (int)(context.Properties["OrdersProcessed"] ?? 0) + 1;

    return response;
});
```

`ILambdaInvocationContext.ServiceProvider` is lazily created for each invocation. Prefer constructor- or parameter-injected services because they participate in disposal automatically, but the scoped provider is available for advanced scenarios.

## Return Values and Serialization

The generator also emits serialization code for the delegate's return value. Supported shapes include:

- Plain values (`T`), including records, arrays, `Stream`, or envelope types.
- `Task<T>` and `ValueTask<T>` for asynchronous responses.
- `Task` or `ValueTask` when no result should be written (Lambda receives `null`).

By default responses are serialized through the configured `ILambdaSerializer` (System.Text.Json unless you swap it). Envelope packages often provide specialized features that capture the response inside an `IResponseFeature`, so the `ILambdaInvocationContext` can retrieve or mutate it later.

## Invocation Scope and Context

Each invocation receives its own dependency injection scope and `ILambdaInvocationContext`. Use it to share data across middleware and handlers without introducing service-locator patterns.

```csharp title="Program.cs" linenums="1"
lambda.MapHandler(async (
    [FromEvent] ApiGatewayRequestEnvelope<Order> request,
    ILambdaInvocationContext context,
    ILogger<Program> logger,
    CancellationToken ct
) =>
{
    var order = request.Body ?? throw new InvalidOperationException("Missing body.");

    if (context.TryGetEvent<ApiGatewayRequestEnvelope<Order>>(out var originalEnvelope))
        logger.LogDebug("HTTP Method {Method}", originalEnvelope.RequestContext.Http.Method);

    var serviceScope = context.ServiceProvider;
    var metrics = serviceScope.GetRequiredService<IMetrics>();
    await metrics.AddInvocationAsync(order.Id, ct);

    return new ApiGatewayResponseEnvelope<OrderResponse>
    {
        StatusCode = 200,
        Body = new(order.Id, approved: true)
    };
});
```

- `context.Items` is a per-invocation bag for ad-hoc data.
- `context.Properties` mirrors `lambda.Properties` so you can stash long-lived configuration at startup and read it later.
- `context.Features` exposes the ASP.NET-style feature system documented in the middleware guide; features enable decoupled access to the raw event/request/response without direct DI coupling.

## Source Generation and Diagnostics

`MapHandler` is decorated as a C# 12 interceptor target. During compilation the generator:

1. Ensures the project is built with C# 11+ so interceptors are available (otherwise `LH0004`).
2. When a payload parameter exists, verifies exactly one `[FromEvent]` annotation is present.
3. Validates keyed service metadata so the requested key matches the DI container's capabilities (`LH0003` when the key uses an unsupported type such as arrays).
4. Emits a strongly typed `Handle` call that deserializes the payload (if any), resolves services via generated code, sets up features, and serializes the response.

At runtime:

- The stub `MapHandler` method would throw if invoked, but the interception step guarantees that never happens
- Only one handler can be registered—calling `MapHandler` multiple times in the same execution throws an `InvalidOperationException`
- You get ahead-of-time compatible, reflection-free code with compile-time errors if the signature is invalid

## Patterns and Best Practices

- Keep handlers thin. Delegate business logic to services so you can test them outside Lambda and reuse them across handlers.
- Respect the provided `CancellationToken`; `MinimalLambda` fires it `InvocationCancellationBuffer` before the hard Lambda timeout.
- Prefer strongly typed responses or envelopes instead of anonymous objects—serialization contracts stay predictable and versionable.
- Use `ILambdaInvocationContext.Features` (e.g., `context.GetEvent<T>()`) to decouple middleware from handlers when you need shared metadata.
- Avoid resolving services manually from `IServiceProvider` unless absolutely necessary. Let the generator inject what you need, or expose a dedicated facade service.
- Prefer referencing a static method on a static class when you want to exercise the handler logic outside of the Lambda host. Mapping a method group (`lambda.MapHandler(MyHandler.HandleAsync);`) makes it trivial to unit test the handler by invoking it directly.

=== "Program.cs"

    ```csharp linenums="1"
    lambda.MapHandler(Handlers.HandleAsync);
    ```

=== "Handlers.cs"

    ```csharp linenums="1"
    namespace MyLambda;

    static class Handlers
    {
        public static async Task<Response> HandleAsync(
            [FromEvent] Request request,
            IService service,
            CancellationToken ct
        )
        {
            return await service.ProcessAsync(request, ct);
        }
    }
    ```

## Troubleshooting

**`LH0002: No parameter marked with [FromEvent]`**

Add a `[FromEvent]` attribute when your handler accepts an input payload. This diagnostic does **not** appear for payload-less handlers because no event parameter is required in that case.

**`InvalidOperationException: No service for type ... has been registered`**

Register dependencies before building the application:

```csharp linenums="1"
var builder = LambdaApplication.CreateBuilder();
builder.Services.AddScoped<IOrderProcessor, OrderProcessor>();
var lambda = builder.Build();
```

**`InvalidOperationException: Unable to resolve service referenced by FromKeyedServicesAttribute`**

Keyed services require .NET 8+ DI. Ensure you registered the keyed instance using `AddKeyed*` and that the key is a supported primitive, enum, `string`, `Type`, or `null`.

## Next Steps

- [Dependency Injection](dependency-injection.md) – Learn how scopes, keyed services, and context injection work under the hood.
- [Middleware](middleware.md) – Compose reusable components that run before and after your handler.
- [Lifecycle Management](lifecycle-management.md) – Initialize resources before the first invocation and dispose them during shutdown.
- [Features](../features/envelopes.md) – Understand envelopes and the feature pipeline when you need event-specific helpers.
