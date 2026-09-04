# MinimalLambda

**Minimal API-style ergonomics, Lambda-first runtime** – Build Lambda functions for any trigger
using familiar .NET patterns without sacrificing Lambda-specific capabilities.

> 📚 **[View Full Documentation](https://layeredcraft.github.io/minimal-lambda/)**

## Overview

Write Lambda functions with the familiar minimal API pattern from ASP.NET Core, adapted for Lambda's
execution model:

```csharp
var builder = LambdaApplication.CreateBuilder();
builder.Services.AddScoped<IMyService, MyService>();

var lambda = builder.Build();
lambda.MapHandler(([FromEvent] string input, IMyService service) =>
    service.Process(input));

await lambda.RunAsync();
```

The framework provides:

- **Minimal API Pattern**: `lambda.MapHandler(...)` in the same declarative style as `app.MapGet()`
- **Dependency Injection**: The ASP.NET Core container with scoped lifetimes tailored to Lambda
  invocations
- **Middleware Pipeline**: Familiar `Use()` pattern for cross-cutting concerns
- **Source Generated**: Compile-time code generation for zero reflection overhead
- **Native AOT Ready**: Full AOT support for sub-100ms cold starts
- **Lambda-Optimized**: Envelopes, lifecycle hooks, automatic cancellation tokens, and timeout
  handling

## Installation

Install the NuGet package:

```bash
dotnet add package MinimalLambda
```

Ensure your project uses C# 11 or later:

```xml

<PropertyGroup>
  <LangVersion>11</LangVersion>
  <!-- or <LangVersion>latest</LangVersion> -->
</PropertyGroup>
```

## Quick Start

Create a simple Lambda handler:

```csharp
using MinimalLambda.Builder;
using Microsoft.Extensions.Hosting;

var builder = LambdaApplication.CreateBuilder();
var lambda = builder.Build();

// The [FromEvent] attribute marks the parameter that receives the deserialized Lambda event
lambda.MapHandler(([FromEvent] string input) => $"Hello {input}!");

await lambda.RunAsync();
```

The `[FromEvent]` attribute tells the framework which parameter receives the deserialized event. You
can
also inject dependencies:

```csharp
lambda.MapHandler(([FromEvent] Order order, IOrderService service) =>
    service.Process(order)
);
```

Add middleware for cross-cutting concerns:

```csharp
lambda.UseMiddleware(async (context, next) =>
{
    Console.WriteLine("Before handler");
    await next(context);
    Console.WriteLine("After handler");
});
```

Use `OnInit()` for setup and `OnShutdown()` for cleanup:

```csharp
// Service can be injected into the Init handler
lambda.OnInit(ICache cache =>
{
    // Runs once at startup - perfect for setting up resources
    cache.Warm();
});

// Handlers can also control if the Init phase should be continued or not
lambda.OnInit(async (services, token) =>
{
    // Returns false to abort startup
    return true;
});

// Runs once at shutdown - cleanup resources
lambda.OnShutdown(async (services, token) =>
{
    // ...
});

// Service can be injected into the shutdown handler too handler
lambda.OnShutdown(ITelemetryService telemetryService =>
{
    // Runs once at shutdown - great for cleaning up resources
    telemetryService.ForceFlush();
});
```

## Key Features

- **Source Generators** – Compile-time code generation eliminates reflection; zero runtime overhead
- **Interceptors** – Handler parameters resolved at compile time, not runtime
- **Dependency Injection** – Built-in scoped lifetime management per invocation
- **Middleware Pipeline** – Familiar ASP.NET Core-style middleware for cross-cutting concerns
- **AOT Ready** – Full support for .NET Native AOT compilation
- **Lambda Lifecycle** – Explicit control over Init, Invocation, and Shutdown phases
- **Automatic Cancellation** – Cancellation tokens respect Lambda timeout with configurable buffer

## Core Concepts

### Handlers & Middleware

Register your Lambda handler with the builder. The framework uses source generation to analyze your
handler signature:

- The `[FromEvent]` attribute marks the input parameter type
- The return type determines the response type
- Source generation handles serialization/deserialization automatically

Handlers can inject dependencies alongside the event:

```csharp
lambda.MapHandler(([FromEvent] Order order, IOrderService service) =>
    service.Process(order)  // Return type automatically serialized to JSON
);
```

Middleware wraps the handler for cross-cutting concerns. Add as many middlewares as needed—they
compose into a pipeline:

```csharp
lambda.UseMiddleware(async (context, next) =>
{
    // Pre-handler logic
    await next(context);
    // Post-handler logic
});

lambda.UseMiddleware(async (context, next) =>
{
    // Another middleware layer
    await next(context);
});
```

To log the raw event payload from middleware, call `EnableEventBuffering()` first—by default the
underlying stream may not be seekable and is meant to be read once by event deserialization:

```csharp
lambda.UseMiddleware(async (context, next) =>
{
    context.EnableEventBuffering();
    var invocationData = context.Features.GetRequired<IInvocationDataFeature>();

    using (var reader = new StreamReader(invocationData.EventStream, leaveOpen: true))
        logger.LogInformation("Request: {Raw}", await reader.ReadToEndAsync());
    invocationData.EventStream.Position = 0; // reset for event deserialization

    await next(context);
});
```

The response stream can always be replaced (for example, with a `Stream` wrapper that tees writes
to a logger) since `IInvocationDataFeature.ResponseStream` is serialized to after the middleware
pipeline completes.

### Lambda Lifecycle

The framework manages initialization and shutdown phases automatically. Add as many callbacks as
needed—they execute in order and then all awaited:

- **OnInit** – Runs once when the function initializes; ideal for setting up resources like database
  connections
- **OnShutdown** – Runs once before Lambda terminates; cleanup and resource release

Both run asynchronously and should be kept as short as possible to minimize startup/shutdown time.

```csharp
lambda.OnInit(async (services, token) =>
{
    // One-time setup (runs once, reused across invocations)
    return true; // or false to abort startup
});

lambda.OnShutdown(async (services, token) =>
{
    // Cleanup before shutdown
});
```

### Dependency Injection

Register services in the builder; they're available in handlers, middleware, and lifecycle methods:

```csharp
builder.Services.AddSingleton<ICache, MemoryCache>();      // Reused across invocations
builder.Services.AddScoped<IRepository, Repository>();    // New per invocation
```

Each invocation receives its own scope—scoped services are isolated per request. `OnInit()` and
`OnShutdown()` handlers receive their own scopes as well. You can also request the
`ILambdaInvocationContext` or `CancellationToken` in any handler, and they're automatically
injected.

### Source Generation & Interceptors

The framework uses C# source generators and compile-time interceptors to:

- Analyze handler signatures at compile time
- Generate optimized dependency injection code
- Resolve handler parameters without reflection

Result: **Zero runtime reflection, zero performance cost.**

### AOT Support

To use .NET Native AOT, define a JSON serializer context and annotate with types to serialize:

```csharp
using System.Text.Json.Serialization;

[JsonSerializable(typeof(string))]
public partial class SerializerContext : JsonSerializerContext;
```

Register the serializer context with the application:

```csharp
using MinimalLambda;

var builder = LambdaApplication.CreateBuilder();

builder.Services.AddLambdaSerializerWithContext<SerializerContext>();

var lambda = builder.Build();
```

The `AddLambdaSerializerWithContext<TContext>()` method registers a source-generated JSON serializer
that uses your context for all Lambda event and response serialization, providing compile-time
serialization metadata and eliminating runtime reflection.

Enable AOT in your project file:

```xml
<PublishAot>true</PublishAot>
<PublishTrimmed>true</PublishTrimmed>
<TrimMode>full</TrimMode>
<JsonSerializerIsReflectionEnabledByDefault>false</JsonSerializerIsReflectionEnabledByDefault>
```

See [AOT documentation](https://docs.microsoft.com/en-us/dotnet/fundamentals/aot/overview) for
details.

## Configuration

The framework supports configuration through `LambdaHostOptions`:

```csharp
builder.Services.ConfigureLambdaHostOptions(options =>
{
    options.InitTimeout = TimeSpan.FromSeconds(10);
    options.InvocationCancellationBuffer = TimeSpan.FromSeconds(5);
    options.ShutdownDuration = ShutdownDuration.ExternalExtensions;
    options.ShutdownDurationBuffer = TimeSpan.FromMilliseconds(100);
    options.ClearLambdaOutputFormatting = true;
});
```

Available options include timeout control, shutdown duration, output formatting, and JSON
serialization customization. The framework automatically registers `DefaultLambdaHostJsonSerializer`
which uses `JsonSerializerOptions` and `JsonWriterOptions` for all Lambda serialization. See
the [configuration guide](https://github.com/LayeredCraft/minimal-lambda/wiki/Configuration) for details.

## Other Packages

Additional packages in the minimal-lambda framework for abstractions, observability, and event
source handling.

| Package                                                                                                       | NuGet                                                                                                                                                          | Downloads                                                                                                                                                            |
|---------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| [**MinimalLambda**](../MinimalLambda/README.md)                                                               | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.svg)](https://www.nuget.org/packages/MinimalLambda)                                                     | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.svg)](https://www.nuget.org/packages/MinimalLambda/)                                                     |
| [**MinimalLambda.Abstractions**](../MinimalLambda.Abstractions/README.md)                                     | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Abstractions.svg)](https://www.nuget.org/packages/MinimalLambda.Abstractions)                           | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Abstractions.svg)](https://www.nuget.org/packages/MinimalLambda.Abstractions/)                           |
| [**MinimalLambda.OpenTelemetry**](../MinimalLambda.OpenTelemetry/README.md)                                   | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.OpenTelemetry.svg)](https://www.nuget.org/packages/MinimalLambda.OpenTelemetry)                         | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.OpenTelemetry.svg)](https://www.nuget.org/packages/MinimalLambda.OpenTelemetry/)                         |
| [**MinimalLambda.Testing**](../MinimalLambda.Testing/README.md)                                               | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Testing.svg)](https://www.nuget.org/packages/MinimalLambda.Testing)                                     | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Testing.svg)](https://www.nuget.org/packages/MinimalLambda.Testing/)                                     |
| [**MinimalLambda.Envelopes**](../Envelopes/MinimalLambda.Envelopes/README.md)                                 | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes)                                 | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes/)                                 |
| [**MinimalLambda.Envelopes.Sqs**](../Envelopes/MinimalLambda.Envelopes.Sqs/README.md)                         | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Sqs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sqs)                         | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Sqs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sqs/)                         |
| [**MinimalLambda.Envelopes.ApiGateway**](../Envelopes/MinimalLambda.Envelopes.ApiGateway/README.md)           | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.ApiGateway.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.ApiGateway)           | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.ApiGateway.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.ApiGateway/)           |
| [**MinimalLambda.Envelopes.Sns**](../Envelopes/MinimalLambda.Envelopes.Sns/README.md)                         | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Sns.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sns)                         | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Sns.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sns/)                         |
| [**MinimalLambda.Envelopes.Kinesis**](../Envelopes/MinimalLambda.Envelopes.Kinesis/README.md)                 | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Kinesis.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kinesis)                 | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Kinesis.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kinesis/)                 |
| [**MinimalLambda.Envelopes.KinesisFirehose**](../Envelopes/MinimalLambda.Envelopes.KinesisFirehose/README.md) | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.KinesisFirehose.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.KinesisFirehose) | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.KinesisFirehose.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.KinesisFirehose/) |
| [**MinimalLambda.Envelopes.Kafka**](../Envelopes/MinimalLambda.Envelopes.Kafka/README.md)                     | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Kafka.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kafka)                     | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Kafka.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kafka/)                     |
| [**MinimalLambda.Envelopes.CloudWatchLogs**](../Envelopes/MinimalLambda.Envelopes.CloudWatchLogs/README.md)   | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.CloudWatchLogs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.CloudWatchLogs)   | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.CloudWatchLogs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.CloudWatchLogs/)   |
| [**MinimalLambda.Envelopes.Alb**](../Envelopes/MinimalLambda.Envelopes.Alb/README.md)                         | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Alb.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Alb)                         | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Alb.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Alb/)                         |

## License

This project is licensed under the MIT License. See [LICENSE](../../LICENSE) for details.
