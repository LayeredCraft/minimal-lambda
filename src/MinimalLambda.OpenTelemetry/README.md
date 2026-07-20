# MinimalLambda.OpenTelemetry

OpenTelemetry integration for distributed tracing and observability in AWS Lambda functions.

> 📚 **[View Full Documentation](https://layeredcraft.github.io/minimal-lambda/)**

## Overview

An extension package for the [MinimalLambda](../MinimalLambda/README.md) framework that provides
comprehensive observability integration. This package enables:

- **Distributed Tracing**: Automatic span creation and context propagation for Lambda invocations
- **Metrics Collection**: Performance and business metrics exportable to standard observability
  backends
- **OpenTelemetry Integration**: Built on the OpenTelemetry SDK for vendor-neutral instrumentation
- **AWS Lambda Instrumentation**:
  Wraps [OpenTelemetry.Instrumentation.AWSLambda](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AWSLambda)
  for Lambda-specific insights
- **Lifecycle Integration**: Seamless integration with Lambda cold starts, warm invocations, and
  error tracking

> [!NOTE]
> Requires MinimalLambda – this package extends that framework and cannot be used
> standalone. Configure exporters to send traces and metrics to your observability backend (e.g.,
> Datadog, New Relic, Jaeger, CloudWatch).

## Installation

**This package requires [MinimalLambda](../MinimalLambda/README.md) to be installed and working
in your project.** It is an extension package and cannot function standalone.

First, install the core framework:

```bash
dotnet add package MinimalLambda
```

Then install this OpenTelemetry extension:

```bash
dotnet add package MinimalLambda.OpenTelemetry
```

Ensure your project uses C# 11 or later:

```xml

<PropertyGroup>
  <LangVersion>11</LangVersion>
  <!-- or <LangVersion>latest</LangVersion> -->
</PropertyGroup>
```

You'll also need additional OpenTelemetry packages depending on your use case:

```bash
dotnet add package OpenTelemetry
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
```

Additional packages may include exporters (e.g., Jaeger, Datadog, AWS X-Ray), instrumentation
libraries (e.g., for HTTP, database calls), and other extensions. See the
[AWS OTel Lambda .NET guide](https://aws-otel.github.io/docs/getting-started/lambda/lambda-dotnet)
and [OpenTelemetry.io .NET documentation](https://opentelemetry.io/docs/languages/dotnet/)
for your specific observability backend and instrumentation needs.

## Quick Start

Set up OpenTelemetry with the AWS Lambda instrumentation:

```csharp
using MinimalLambda.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Instrumentation.AWSLambda;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = LambdaApplication.CreateBuilder();

// Configure OpenTelemetry with tracing
builder
    .Services.AddOpenTelemetry()
    .WithTracing(configure =>
        configure
            .AddAWSLambdaConfigurations()
            .SetResourceBuilder(
                ResourceBuilder.CreateDefault().AddService("MyLambda", serviceVersion: "1.0.0")
            )
            .AddOtlpExporter(options =>
            {
                options.Endpoint = new Uri("http://localhost:4317");
            })
    );

var lambda = builder.Build();

// Enable automatic tracing for Lambda invocations
lambda.UseOpenTelemetryTracing();

lambda.MapHandler(([FromEvent] string input) => $"Hello {input}!");

// Flush traces on Lambda shutdown
lambda.OnShutdownFlushTracer();

await lambda.RunAsync();
```

## Key Features

- **Automatic Root Span** – Wraps Lambda invocations with OpenTelemetry spans via source
  generation and compile-time interceptors
- **AWS Lambda Context** – Captures Lambda context information in spans (request IDs, function
  name, etc.)
- **Custom Instrumentation** – Inject `ActivitySource` to create spans for your business logic
- **Multiple Exporters** – OTLP, Jaeger, AWS X-Ray, Datadog, and more
- **AOT Compatible** – Works with .NET Native AOT compilation
- **Graceful Shutdown** – Ensures traces export before Lambda terminates

## Core Concepts

### Automatic Root Span Creation

When you call `UseOpenTelemetryTracing()`, the framework uses source generators and compile-time
interceptors to inject tracing middleware into your handler pipeline. This middleware delegates to
the [OpenTelemetry.Instrumentation.AWSLambda](https://www.nuget.org/packages/OpenTelemetry.Instrumentation.AWSLambda)
wrapper functions to create **root spans for each Lambda invocation**. These root spans capture
AWS Lambda context (request IDs, function name, etc.) and measure the entire invocation duration.

How it works:

- **Compile Time:** Source generators analyze your handler signature and create a compile-time
  interceptor that injects middleware into the pipeline
- **Startup:** The middleware extracts a `TracerProvider` from the dependency injection container
- **Per Invocation:** The middleware calls the appropriate AWS Lambda instrumentation wrapper
  function with the correct type information (event and response types), which uses the
  `TracerProvider` to create the root span

This happens at compile time with zero runtime reflection overhead. The actual span creation is
delegated to the AWS Lambda OpenTelemetry instrumentation package.

> [!IMPORTANT]
> A `TracerProvider` must be registered in the dependency injection container
> before calling `UseOpenTelemetryTracing()`. If it's missing, an `InvalidOperationException` is
> thrown at startup. See the Quick Start section above for configuration details.

> [!NOTE]
> This package creates the root invocation span automatically via the AWS instrumentation.
> If you want to instrument specific handlers, functions, or business logic within your Lambda, you
> create and manage those spans yourself using a custom `ActivitySource` (see below).

### Custom Instrumentation with ActivitySource

To add traces for specific operations within your handler (database queries, API calls, business
logic), create a custom `ActivitySource`. See the
[OpenTelemetry.io guide on setting up an ActivitySource](https://opentelemetry.io/docs/languages/dotnet/instrumentation/#setting-up-an-activitysource)
for detailed information.

```csharp
using System.Diagnostics;

internal class Instrumentation : IDisposable
{
    public const string ActivitySourceName = "MyLambda";
    public const string ActivitySourceVersion = "1.0.0";

    public ActivitySource ActivitySource { get; } =
        new(ActivitySourceName, ActivitySourceVersion);

    public void Dispose() => ActivitySource.Dispose();
}
```

Register it with the `TracerProvider` and inject it into your handler:

```csharp
builder.Services.AddSingleton<Instrumentation>();

var lambda = builder.Build();

// In your handler:
lambda.MapHandler(([FromEvent] Request request, Instrumentation instrumentation) =>
{
    using var activity = instrumentation.ActivitySource.StartActivity("ProcessRequest");
    activity?.SetAttribute("request.name", request.Name);

    return ProcessRequest(request);
});
```

Custom spans created with your `ActivitySource` automatically link to the root Lambda invocation
span, creating a complete trace of your function's execution. This is your responsibility—this
package only provides the root invocation span.

### Graceful Shutdown

Ensure all traces and metrics are exported before Lambda terminates:

```csharp
lambda.OnShutdownFlushOpenTelemetry();
```

This registers shutdown handlers that force flush both the `TracerProvider` and `MeterProvider`
with a configurable timeout (default: infinite):

```csharp
lambda.OnShutdownFlushOpenTelemetry(timeoutMilliseconds: 5000);
```

You can also flush individually:

```csharp
lambda.OnShutdownFlushTracer();
lambda.OnShutdownFlushMeter();
```

## Example Project

A complete, runnable example with Docker Compose setup is available in
[examples/MinimalLambda.Example.OpenTelemetry](../../examples/MinimalLambda.Example.OpenTelemetry/).

The example demonstrates:

- Full OpenTelemetry configuration with OTLP export
- Custom instrumentation and metrics in a real handler
- Jaeger tracing backend setup via Docker Compose
- Running locally with AWS Lambda Test Tool
- Viewing traces and metrics in the Jaeger UI

## Documentation

- [AWS OTel Lambda Guide](https://aws-otel.github.io/docs/getting-started/lambda/lambda-dotnet)
  – Official AWS documentation for OpenTelemetry on Lambda with .NET

- [OpenTelemetry.io](https://opentelemetry.io/) – OpenTelemetry specification, APIs, and best
  practices

- [OpenTelemetry Instrumentation AWSLambda](https://github.com/open-telemetry/opentelemetry-dotnet-instrumentation/tree/main/src/OpenTelemetry.Instrumentation.AWSLambda)
  – Source for the AWSLambda instrumentation

- [Full Project Documentation](https://github.com/LayeredCraft/minimal-lambda/wiki) – Comprehensive
  guides and patterns

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