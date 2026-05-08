---
title: ""
---

# MinimalLambda: ASP.NET Core Patterns for AWS Lambda

[![Main Build](https://github.com/j-d-ha/minimal-lambda/actions/workflows/main-build.yaml/badge.svg)](https://github.com/j-d-ha/minimal-lambda/actions/workflows/main-build.yaml)
[![codecov](https://codecov.io/gh/j-d-ha/minimal-lambda/graph/badge.svg?token=BWORPTQ0UK)](https://codecov.io/gh/j-d-ha/minimal-lambda)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://github.com/j-d-ha/minimal-lambda/blob/main/LICENSE)

**Minimal API-inspired, Lambda-first hosting.**

MinimalLambda keeps the builder, DI, middleware, and handler mapping patterns you know from ASP.NET
Core, but is built from the ground up for **any Lambda event source** (SQS, SNS, API Gateway,
Kinesis, S3, EventBridge, etc.). Strongly-typed envelopes, lifecycle hooks, scoped invocations,
source generation, and cancellation-token handling shape those familiar patterns to Lambda’s
execution model—so you get type-safe events, per-invocation scopes, predictable timeouts, and a
smoother developer experience when iterating locally or in CI.

[Get Started](getting-started/index.md){ .md-button .md-button--primary }
[Guides](guides/index.md){ .md-button }
[Examples (Coming Soon)](examples/index.md){ .md-button }

---

## Why MinimalLambda?

Stop wiring up DI scopes, serializers, and cancellation tokens by hand. Ship features with patterns
you
already know, while still embracing Lambda’s execution model.

=== "Traditional Lambda"

    ```csharp
    // ❌ Manual DI container setup
    var services = new ServiceCollection();
    services.AddScoped<IGreetingService, GreetingService>();

    await using var rootProvider = services.BuildServiceProvider();

    await LambdaBootstrapBuilder
        .Create<APIGatewayProxyRequest, APIGatewayProxyResponse>(
            async (request, context) =>
            {
                // ❌ Manual scope creation for each invocation
                using var scope = rootProvider.CreateScope();

                // ❌ Manual service resolution from scope
                var service = scope.ServiceProvider.GetRequiredService<IGreetingService>();

                // ❌ Manual cancellation token creation from context
                using var cts = new CancellationTokenSource(
                    context.RemainingTime - TimeSpan.FromMilliseconds(500)
                );

                // ❌ Manual JSON deserialization
                var requestContent = JsonSerializer.Deserialize<GreetingRequest>(request.Body);

                var greeting = await service.GreetAsync(requestContent.Name, cts.Token);

                // ❌ Manual JSON serialization
                var responseJson = JsonSerializer.Serialize(
                    new GreetingResponse(greeting, DateTime.UtcNow)
                );

                return new APIGatewayProxyResponse
                {
                    Body = responseJson,
                    StatusCode = 200,
                    Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
                };
            },
            new DefaultLambdaJsonSerializer()
        )
        .Build()
        .RunAsync();

    ```

=== "`MinimalLambda`"

    ```csharp
    // ✅ Familiar .NET Core builder pattern
    var builder = LambdaApplication.CreateBuilder();

    builder.Services.AddScoped<IGreetingService, GreetingService>();

    await using var lambda = builder.Build();

    lambda.MapHandler(
        async (
            // ✅ Automatic envelope deserialization with strong typing
            [FromEvent] ApiGatewayRequestEnvelope<GreetingRequest> request,
            // ✅ Automatic DI injection - proper scoped lifetime per invocation
            IGreetingService service,
            // ✅ Automatic cancellation token - framework manages timeout
            CancellationToken cancellationToken
        ) =>
        {
            var greeting = await service.GreetAsync(request.BodyContent.Name, cancellationToken);

            // ✅ Type-safe response - automatic JSON serialization
            return ApiGatewayResult.Ok(new GreetingResponse(greeting, DateTime.UtcNow));
        }
    );

    await lambda.RunAsync();
    ```

---

## Key Features

### :material-view-dashboard-outline: .NET Hosting Patterns

Use middleware, the builder pattern, and dependency injection just like ASP.NET Core—with proper
scoped lifetime management per invocation.

[Learn about DI](guides/dependency-injection.md){ .md-button }

### :material-calendar-sync-outline: Lifecycle Management

Run OnInit/OnShutdown hooks alongside your handler pipeline to warm resources, clear Lambda log
formatting, and flush telemetry with host-managed cancellation tokens.

[Lifecycle management](guides/lifecycle-management.md){ .md-button }

### :material-code-braces: Source-Generated Handlers

Compile-time interception validates handler signatures, injects dependencies, and avoids reflection.

[Handler registration](guides/handler-registration.md){ .md-button }

### :material-rocket-launch-outline: AOT Friendly

Source generation plus System.Text.Json contexts keep handlers ready for Native AOT publishing.

[Advanced topics (Coming Soon)](advanced/index.md){ .md-button }

### :material-chart-line: Built-in Observability

OpenTelemetry integration emits traces and metrics without bolting on custom shims.

[OpenTelemetry setup](features/open_telemetry.md){ .md-button }

### :material-code-json: Flexible Handler Registration

Map strongly typed handlers to envelopes or raw events with compile-time validation.

[Handler registration](guides/handler-registration.md){ .md-button }

### :material-speedometer: Minimal Runtime Overhead

Small abstraction surface area keeps CPU and memory usage predictable inside Lambda’s sandbox.

[Advanced topics (Coming Soon)](advanced/index.md){ .md-button }

---

## Quick Start

Install the NuGet package:

```bash
dotnet add package MinimalLambda
```

Create your first Lambda handler:

```csharp
using MinimalLambda.Builder;

var builder = LambdaApplication.CreateBuilder();
var lambda = builder.Build();

lambda.MapHandler(([FromEvent] string name) => $"Hello {name}!");

await lambda.RunAsync();
```

!!! tip "Next Steps"
Ready to dive deeper? Check out the [Getting Started Guide](getting-started/index.md) for a complete
tutorial, or explore the [Examples](examples/index.md) to see real-world applications.

## AI Agent Skill

Use the bundled `minimal-lambda` agent skill to give coding agents focused MinimalLambda guidance
for handlers, envelopes, middleware, AOT, testing, and repo workflow.

Install it with [skills.sh](https://skills.sh):

```bash
npx skills add j-d-ha/minimal-lambda --skill minimal-lambda
```

For a global Claude Code install:

```bash
npx skills add j-d-ha/minimal-lambda --skill minimal-lambda --global --agent claude-code
```

---

## Packages

### Core Packages

The core packages provide the fundamental hosting framework, abstractions, and observability support
for building AWS Lambda functions.

| Package                                                                                                             | Description                                   | NuGet                                                                                                                                  | Downloads                                                                                                                                    |
|---------------------------------------------------------------------------------------------------------------------|-----------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------|
| [**MinimalLambda**](https://github.com/j-d-ha/minimal-lambda/tree/main/src/MinimalLambda)                           | Core hosting framework with middleware and DI | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.svg)](https://www.nuget.org/packages/MinimalLambda)                             | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.svg)](https://www.nuget.org/packages/MinimalLambda/)                             |
| [**MinimalLambda.Abstractions**](https://github.com/j-d-ha/minimal-lambda/tree/main/src/MinimalLambda.Abstractions) | Core interfaces and contracts                 | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Abstractions.svg)](https://www.nuget.org/packages/MinimalLambda.Abstractions)   | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Abstractions.svg)](https://www.nuget.org/packages/MinimalLambda.Abstractions/)   |
| [**MinimalLambda.OpenTelemetry**](features/open_telemetry.md)                                                       | Distributed tracing and observability         | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.OpenTelemetry.svg)](https://www.nuget.org/packages/MinimalLambda.OpenTelemetry) | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.OpenTelemetry.svg)](https://www.nuget.org/packages/MinimalLambda.OpenTelemetry/) |

### Envelope Packages

Envelope packages provide type-safe handling of AWS Lambda event sources with automatic payload
deserialization.

!!! info "What are Envelopes?"
Envelopes wrap AWS Lambda events with strongly-typed payload handling, giving you compile-time type
safety and automatic deserialization of message bodies from SQS, SNS, Kinesis, and other event
sources.

    [Learn more about envelopes](features/envelopes.md){ .md-button }

| Package                                     | Description                                           | NuGet                                                                                                                                                          | Downloads                                                                                                                                                            |
|---------------------------------------------|-------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| **MinimalLambda.Envelopes**                 | Infrastructure package for HTTP response builders     | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes)                                 | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes/)                                 |
| **MinimalLambda.Envelopes.Sqs**             | Simple Queue Service events with typed message bodies | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Sqs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sqs)                         | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Sqs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sqs/)                         |
| **MinimalLambda.Envelopes.Sns**             | Simple Notification Service messages                  | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Sns.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sns)                         | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Sns.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sns/)                         |
| **MinimalLambda.Envelopes.ApiGateway**      | REST, HTTP, and WebSocket APIs                        | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.ApiGateway.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.ApiGateway)           | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.ApiGateway.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.ApiGateway/)           |
| **MinimalLambda.Envelopes.Kinesis**         | Data Streams with typed records                       | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Kinesis.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kinesis)                 | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Kinesis.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kinesis/)                 |
| **MinimalLambda.Envelopes.KinesisFirehose** | Data transformation                                   | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.KinesisFirehose.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.KinesisFirehose) | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.KinesisFirehose.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.KinesisFirehose/) |
| **MinimalLambda.Envelopes.Kafka**           | MSK and self-managed Kafka                            | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Kafka.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kafka)                     | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Kafka.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kafka/)                     |
| **MinimalLambda.Envelopes.CloudWatchLogs**  | Log subscriptions                                     | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.CloudWatchLogs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.CloudWatchLogs)   | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.CloudWatchLogs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.CloudWatchLogs/)   |
| **MinimalLambda.Envelopes.Alb**             | Application Load Balancer requests                    | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Alb.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Alb)                         | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Alb.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Alb/)                         |

[Browse all envelope packages](features/envelopes.md){ .md-button }

---

## Examples & Use Cases

Explore the repository’s `examples/` folder and the docs’ [Examples](examples/index.md) page (
content coming
soon) for end-to-end Lambda samples that wire up middleware, envelopes, and DI.

[Examples (Coming Soon)](examples/index.md){ .md-button }

---

## Community & Resources

### Get Involved

- **[GitHub Repository](https://github.com/j-d-ha/minimal-lambda)** – Source code, issues, and
  discussions.
- **[License](https://github.com/j-d-ha/minimal-lambda/blob/main/LICENSE)** – MIT License.

### Documentation

- **[Getting Started](getting-started/index.md)** – Installation and first Lambda tutorial.
- **[Guides](guides/index.md)** – In-depth docs on DI, middleware, lifecycle, configuration, and
  more.
- **[Features](features/index.md)** – Envelopes, OpenTelemetry integration, and other add-ons.
- **[Advanced Topics](advanced/index.md)** – Coming soon: AOT, source generators, performance
  tuning.

### Support

- Ask or search in [GitHub Discussions](https://github.com/j-d-ha/minimal-lambda/discussions).
- File bugs or feature requests
  via [GitHub Issues](https://github.com/j-d-ha/minimal-lambda/issues).

---

**Ready to modernize your Lambda development?** [Get started now](getting-started/index.md){
.md-button .md-button--primary }
