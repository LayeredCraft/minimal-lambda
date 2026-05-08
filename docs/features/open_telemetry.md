# OpenTelemetry

## What is OpenTelemetry Integration?

The `MinimalLambda.OpenTelemetry` package provides seamless integration with the official [`OpenTelemetry.Instrumentation.AWSLambda`](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AWSLambda) package. It acts as a smart adapter layer for `MinimalLambda`, using **C# 12 interceptors and source generation** to automatically instrument your Lambda handlers with minimal overhead.

At compile time, it wraps your handler invocation in a root trace span, providing reflection-free, high-performance distributed tracing.

______________________________________________________________________

## Key Benefits

- **Zero-Overhead Instrumentation**: Uses source generation to create trace spans for your handler at compile time, avoiding runtime reflection.
- **Distributed Tracing**: Propagates trace context across services, giving you end-to-end visibility in a microservices architecture.
- **Lifecycle Integration**: Provides an explicit shutdown hook to ensure all buffered telemetry is flushed before the Lambda execution environment is frozen.
- **Vendor-Neutral**: Fully compatible with any OpenTelemetry-compliant backend, including Jaeger, Datadog, New Relic, Honeycomb, and AWS X-Ray.
- **Standard APIs**: Leverages the standard OpenTelemetry .NET SDK, so you can use familiar configuration and custom instrumentation APIs.
- **Custom Instrumentation**: Easily create custom spans (`Activity`) and metrics (`Meter`) to capture application-specific logic.

______________________________________________________________________

## Quick Start

### Installation

Install the OpenTelemetry integration package and any required exporter packages.

```bash
# Core integration package
dotnet add package MinimalLambda.OpenTelemetry

# Common packages for OTLP export
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add package OpenTelemetry.Extensions.Hosting

# Packages for X-Ray integration
dotnet add package OpenTelemetry.Contrib.Extensions.AWSXRay
```

### MVP Code Example

This example demonstrates how to add basic OpenTelemetry instrumentation to your Lambda function.

```csharp title="Program.cs" linenums="1"
using MinimalLambda.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = LambdaApplication.CreateBuilder();

builder
    .Services.AddOpenTelemetry()// (1)!
    .WithTracing(tracing =>
    {
        tracing.AddAWSLambdaConfigurations();// (2)!
        tracing.AddSource("MyService");// (3)!
        tracing.SetResourceBuilder(
            ResourceBuilder.CreateDefault().AddService("MyService", serviceVersion: "1.0.0")
        );
        tracing.AddConsoleExporter();// (4)!
    })
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("MyService");// (5)!
        metrics.AddConsoleExporter();// (6)!
    });

await using var lambda = builder.Build();

lambda.UseOpenTelemetryTracing();// (7)!

lambda.OnShutdownFlushOpenTelemetry();// (8)!

lambda.MapHandler(// (9)!
    async ([FromEvent] Request request, ILogger<Program> logger, CancellationToken cancellationToken) =>
    {
        logger.LogInformation("Responding to {Name}", request.Name);

        await Task.Delay(100, cancellationToken); // Simulate work

        return new Response($"Hello {request.Name}!");
    }
);

await lambda.RunAsync();

internal record Request(string Name);

internal record Response(string Message);
```

1. Add OpenTelemetry tracing to the DI container
2. Enable AWS Lambda configurations
3. Add a custom source for tracing
4. Export traces to a collector, in this case the console
5. Register a named meter so custom metrics (like `NameMetrics`) can emit data
6. Export metrics alongside traces
7. Enable OpenTelemetry tracing in the Lambda host through middleware.
8. (Optional) Flush the OpenTelemetry traces at the end of the Lambda execution.
9. Write your Lambda handler like normal.

!!! tip

    OpenTelemetry tracing can be configured in multiple ways, including manually creating a trace provider using the [OpenTelemetry](https://www.nuget.org/packages/OpenTelemetry), or through registering OpenTelemetry services in your DI container using [OpenTelemetry.Extensions.Hosting](https://www.nuget.org/packages/OpenTelemetry.Extensions.Hosting).

    When working with `MinimalLambda`, its recommended to the latter approach and as such, this documentation focuses on it.

______________________________________________________________________

## How It Works: Feature-Based Middleware

The `UseOpenTelemetryTracing()` method adds middleware to the Lambda invocation pipeline that automatically instruments your handlers with distributed tracing.

Here's the step-by-step process:

1. The `UseOpenTelemetryTracing()` middleware is registered in the invocation pipeline when you call the method on your Lambda application.
2. During each Lambda invocation, the middleware executes before your handler runs.
3. The middleware dynamically detects the event and response types by reading from the feature collection:
    - It checks for an `IEventFeature` to get the incoming event object
    - It checks for an `IResponseFeature` to get the outgoing response object
4. These event and response objects are passed to the official `OpenTelemetry.Instrumentation.AWSLambda` package, which wraps the handler execution in a root trace span.
5. The OpenTelemetry instrumentation automatically extracts trace context from supported event types (like API Gateway requests), ensuring proper distributed trace propagation across services.

The shutdown helpers (`OnShutdownFlushOpenTelemetry`, `OnShutdownFlushTracer`, and `OnShutdownFlushMeter`) are regular extension methods that execute at runtime and use the registered `TracerProvider`/`MeterProvider` instances to force-flush telemetry before Lambda freezes the environment.

______________________________________________________________________

## Working With `MinimalLambda.OpenTelemetry`

### Configuration

Configuration is done using the standard OpenTelemetry .NET SDK extension methods on `IServiceCollection`. Official documentation for these methods can be found [here](https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry.Extensions.Hosting/README.md).

`MinimalLambda.OpenTelemetry` expectes for an instance of `TracerProvider` to be registered in the DI container as this provider is used by `OpenTelemetry.Instrumentation.AWSLambda`. As such, it is your responsibility to configure the OpenTelemetry provider and ensure it is registered in the DI container. If it is not, an exception will be thrown at startup.

### Instrumenting The Invocation Pipeline

After configuring the OpenTelemetry services, you need to add the tracing middleware to the Lambda invocation pipeline. This is done by calling the `UseOpenTelemetryTracing()` extension method on the `LambdaApplication` instance.

```csharp
await using var lambda = builder.Build();

// This method call enables the tracing middleware
lambda.UseOpenTelemetryTracing();

// ... MapHandler, OnShutdown, etc. ...
```

This method registers middleware in the invocation pipeline that wraps each handler execution in a root trace span. The middleware is responsible for:

- Retrieving the event object from the `IEventFeature` in the feature collection (if present)
- Executing the handler and capturing the response from `IResponseFeature` (if present)
- Passing both the event and response to the underlying `OpenTelemetry.Instrumentation.AWSLambda` package

Because the middleware reads event and response types dynamically from the feature collection, it works seamlessly with any Lambda event source type. The OpenTelemetry instrumentation can correctly extract context and attributes from strongly-typed event objects (such as trace parent headers from an API Gateway request), ensuring proper distributed trace propagation across your services.

### Gracefully Shutdown & Cleaning Up

The OpenTelemetry `TracerProvider` and `MeterProvider` services both implement `IDisposable`. When the dependency injection container is disposed of during a normal application shutdown, it should trigger these providers to automatically flush any buffered telemetry. However, in a serverless environment where the lifecycle can be abrupt, this disposal is not always guaranteed to complete before the execution environment is frozen.

For situations where you notice data being dropped, or if you want to guarantee a flush attempt is made, `MinimalLambda.OpenTelemetry` provides the following explicit helper methods. They register a function during the application's shutdown phase to manually force-flush pending telemetry.

The following methods are available to be called on the `LambdaApplication` instance:

| Method                           | Description                                                                                                                                                                                                      |
| -------------------------------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `OnShutdownFlushOpenTelemetry()` | A convenience method that registers shutdown hooks to flush both traces and metrics. It calls both `OnShutdownFlushTracer` and `OnShutdownFlushMeter` internally. This is the recommended method for most users. |
| `OnShutdownFlushTracer()`        | Registers a shutdown hook to force-flush only the `TracerProvider`. Use this if you are only tracing and not collecting metrics, or if you need separate control over flushing traces.                           |
| `OnShutdownFlushMeter()`         | Registers a shutdown hook to force-flush only the `MeterProvider`. Use this if you are only collecting metrics and not tracing.                                                                                  |

For most applications, calling `lambda.OnShutdownFlushOpenTelemetry()` is sufficient to ensure all telemetry is flushed. If your application only uses tracing or metrics, but not both, you can use the more specific methods for clarity.

!!! note

    These methods call `GetRequiredService<TracerProvider>()` and `GetRequiredService<MeterProvider>()`. Make sure those providers are registered (via `.AddOpenTelemetry().WithTracing(...)` / `.WithMetrics(...)`) before invoking the shutdown helpers, otherwise the application will throw during startup.

All three methods also accept an optional `timeoutMilliseconds` parameter. This allows you to specify a maximum duration for the flush operation. Importantly, these flush operations are non-blocking and respect the provided `CancellationToken`, ensuring they can gracefully exit if the Lambda execution environment signals a shutdown before the timeout elapses. This combined approach offers robust control over the flush duration within the limited time available during a Lambda shutdown.

!!! Warning

    When shutting down, Lambda only allocates up to 500ms of time for the execution environment to shut down. As such, it is important to make sure that shutdown logic such as flushing traces is executed as quickly as possible. More information about the Lambda execution environment lifecycle can be found [here](https://docs.aws.amazon.com/lambda/latest/dg/lambda-runtime-environment.html).

______________________________________________________________________

## Manual Instrumentation

`MinimalLambda.OpenTelemetry` helps you instrement your Lambda handlers with [OpenTelemetry.Instrumentation.AWSLambda](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AWSLambda), but to get the most out of observability, you should add custom instrumentation to your application code. In this section we cover how this can be done easily with the Dependancy Injection support provided by `MinimalLambda`.

!!! note

    This code is not specific to `MinimalLambda.OpenTelemetry` and follows the guidlines provided by Microsoft's [.NET distributed tracing documetation](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing).

A full working example of an instrumented Lambda application can be found [on GitHub](https://github.com/j-d-ha/minimal-lambda/tree/main/examples/MinimalLambda.Example.OpenTelemetry).

### Custom Instrumentation Class

The [.NET distributed tracing guidance](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing) recommends creating a dedicated `ActivitySource` per service or bounded context, then sharing it through dependency injection. This keeps source names consistent and avoids collisions when multiple libraries emit spans.

```csharp title="Instrumentation.cs" linenums="1"
using System.Diagnostics;

namespace MinimalLambda.Example.OpenTelemetry;

/// <summary>
///     It is recommended to use a custom type to hold references for ActivitySource. This avoids
///     possible type collisions with other components in the DI container.
/// </summary>
internal class Instrumentation : IDisposable
{
    internal const string ActivitySourceName = "MyLambda";
    internal const string ActivitySourceVersion = "1.0.0";

    internal ActivitySource ActivitySource { get; } =
        new(ActivitySourceName, ActivitySourceVersion);

    public void Dispose() => ActivitySource.Dispose();
}
```

Register this class (typically as a singleton) so that services can request an `Instrumentation` instance and start spans with a stable `ActivitySourceName`. Keeping the type disposable mirrors the official walkthrough for manual instrumentation and ensures underlying event listeners are released when the Lambda host shuts down.

### Custom Metrics Class

Metrics follow a similar pattern: create a class that receives an `IMeterFactory`, then build strongly typed instruments. This matches the [instrumentation walkthroughs](https://learn.microsoft.com/en-us/dotnet/core/diagnostics/distributed-tracing-instrumentation-walkthroughs) where meters and counters are grouped by concern.

```csharp title="NameMetrics.cs" linenums="1"
using System.Diagnostics.Metrics;

namespace MinimalLambda.Example.OpenTelemetry;

public class NameMetrics
{
    private readonly Counter<int> _namesProcessed;

    public NameMetrics(IMeterFactory meterFactory)
    {
        var meter = meterFactory.Create("MyLambda.Service");
        _namesProcessed = meter.CreateCounter<int>("MyLambda.Service.Processed");
    }

    public void ProcessName(string name) =>
        _namesProcessed.Add(1, new KeyValuePair<string, object?>("name", name));
}
```

By injecting `NameMetrics` into your services you can increment counters, attach semantic tags (in this case the processed `name`), and have the values exported alongside traces through whatever OTLP or X-Ray exporter you registered earlier.

### Instrument A Service

Once the reusable helpers exist, wrap service logic in spans to capture timing, tags, and exceptions. The following `NameService` starts a child activity every time it generates a value, enriching it with both input and output information.

```csharp title="NameService.cs" linenums="1"
using System.Diagnostics;

namespace MinimalLambda.Example.OpenTelemetry;

internal class NameService(Instrumentation instrumentation, NameMetrics nameMetrics)
{
    private readonly ActivitySource _activitySource = instrumentation.ActivitySource;

    public async Task<string> GetFullName(string name, CancellationToken cancellationToken = default)
    {
        using var activity = _activitySource.StartActivity("GetFullName");
        activity?.SetTag("input.name", name);

        await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);

        var fullName = $"{name} Smith";
        activity?.SetTag("output.fullname", fullName);

        nameMetrics.ProcessName(name);

        return fullName;
    }
}
```

The `using var activity = ...` pattern mirrors the BCL samples and guarantees spans finish even when exceptions are thrown. Because the service receives both `Instrumentation` and `NameMetrics` through DI, every span and counter entry shares the same source name and tags, keeping traces and metrics correlated.

### Instrument A Handler

Finally, surface the custom instrumentation inside your Lambda handler. `MinimalLambda` injects any registered services, so you can receive both `IService` and `Instrumentation` directly in the handler signature.

```csharp title="Function.cs" linenums="1"
using MinimalLambda.Builder;

namespace MinimalLambda.Example.OpenTelemetry;

internal static class Function
{
    internal static async Task<Response> Handler(
        [FromEvent] Request request,
        IService service,
        Instrumentation instrumentation,
        CancellationToken cancellationToken
    )
    {
        using var activity = instrumentation.ActivitySource.StartActivity();

        var message = await service.GetMessage(request.Name, cancellationToken);

        return new Response(message, DateTime.UtcNow);
    }
}
```

The handler starts a root `Activity` before invoking downstream services, ensuring any spans produced inside `Service.GetMessage` automatically nest beneath it. Because `UseOpenTelemetryTracing()` already wires up the Lambda envelope instrumentation, your manual spans flow into the same trace, giving full visibility from the trigger event through your business logic and custom metrics.
