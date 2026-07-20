# MinimalLambda.Testing

In-memory Lambda runtime for end-to-end and integration testing with the MinimalLambda framework.

> 📚 **[View Full Documentation](https://layeredcraft.github.io/minimal-lambda/)**

## Overview

MinimalLambda.Testing lets you run Lambda functions entirely in memory, exercising the same runtime
API that AWS provides without deploying or opening network ports. It follows the familiar ASP.NET
Core `WebApplicationFactory` pattern: reusing your real `Program` entry point via
`LambdaApplicationFactory<TEntryPoint>` and driving it through a `LambdaTestServer` that mimics the
Lambda Runtime API (init, invocation, and shutdown).

Use it to:

- **Boot real apps**: Spin up your Lambda entry point with `LambdaApplicationFactory` just like
  ASP.NET's `WebApplicationFactory`
- **Simulate Runtime API**: `LambdaTestServer` feeds events and receives responses/errors over the
  Lambda Runtime HTTP contract—no mocks or stubs
- **Typed Invocations**: `InvokeAsync<TEvent, TResponse>` sends strongly typed events and returns
  typed responses, including structured error details
- **Lifecycle Coverage**: Exercise `OnInit` and `OnShutdown` hooks and verify cold-start logic
- **Host Customization**: Override configuration and services for tests with `WithHostBuilder`

## Installation

This package extends [MinimalLambda](../MinimalLambda/README.md); install both:

```bash
dotnet add package MinimalLambda
dotnet add package MinimalLambda.Testing
```

Ensure your project uses C# 11 or later:

```xml

<PropertyGroup>
  <LangVersion>11</LangVersion>
  <!-- or <LangVersion>latest</LangVersion> -->
</PropertyGroup>
```

## Quick Start

Write an end-to-end test that drives your Lambda through the in-memory runtime:

```csharp
using MinimalLambda.Testing;
using Xunit;

public class LambdaTests
{
    [Fact]
    public async Task HelloWorldHandler_ReturnsGreeting()
    {
        await using var factory = new LambdaApplicationFactory<Program>();

        await factory.TestServer.StartAsync();

        var response = await factory.TestServer.InvokeAsync<string, string>("World");

        Assert.True(response.WasSuccess);
        Assert.Equal("Hello World!", response.Response);
    }
}
```

Customize the host configuration for a specific test:

```csharp
await using var factory = new LambdaApplicationFactory<Program>().WithHostBuilder(builder =>
{
    builder.ConfigureServices((_, services) =>
    {
        // Override registrations or configuration for this test run
    });
});
```

Use `LambdaServerOptions` to tweak runtime details such as timeouts, ARN, or custom headers returned
by the simulated Runtime API.

## Key Features

- **Runtime-accurate simulation** – Emulates the Lambda Runtime API (init, `/invocation/next`,
  response/error posts) over an in-memory message channel
- **End-to-end coverage** – Drives source-generated handlers, middleware, envelopes, DI scopes, and
  lifecycle hooks exactly as they run in production
- **Typed invocation helpers** – `InvokeAsync<TEvent, TResponse>` returns structured
  `InvocationResponse` objects with success flags and error payloads
- **Host customization** – `WithHostBuilder` and `LambdaApplicationFactoryContentRootAttribute`
  mirror ASP.NET testing patterns for overriding configuration and locating content roots
- **Concurrency safe** – Handles multiple pending invocations FIFO with per-request correlation

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
