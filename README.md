# MinimalLambda

[![Main Build](https://github.com/j-d-ha/minimal-lambda/actions/workflows/main-build.yaml/badge.svg)](https://github.com/j-d-ha/minimal-lambda/actions/workflows/main-build.yaml)
[![codecov](https://codecov.io/gh/j-d-ha/minimal-lambda/graph/badge.svg?token=BWORPTQ0UK)](https://codecov.io/gh/j-d-ha/minimal-lambda)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

**Lambda-first hosting with Minimal API-inspired patterns** – Familiar .NET ergonomics with
handlers, DI, and middleware, purpose-built for AWS Lambda triggers.

> 📚 **[View Full Documentation](https://j-d-ha.github.io/minimal-lambda/)**

## Overview

MinimalLambda brings the clean, declarative style of ASP.NET Core Minimal APIs to AWS Lambda while
staying grounded in Lambda’s execution model. Use the same mental model (builder, DI, middleware,
handler mapping) while leaning on Lambda-specific features like strongly-typed envelopes, lifecycle
hooks, scoped invocations, and source generation.

- **Familiar builder flow**: `LambdaApplication.CreateBuilder()` → `Build()` → `RunAsync()`
- **.NET DI you already use**: `builder.Services.AddScoped<IMyService, MyService>()` with
  Lambda-safe lifetimes
- **Handler mapping, Lambda edition**: `lambda.MapHandler(...)` instead of crafting raw handlers
- **Middleware for cross-cutting concerns**: `lambda.UseMiddleware(...)` to wrap your pipeline
- **Lambda-first runtime**: Lifecycle hooks, cancellation token management, and strongly typed
  envelope models for event triggers

Instead of writing boilerplate Lambda handlers, you keep familiar .NET patterns while the framework
handles event envelopes, dependency injection, scoped lifetimes, middleware, and compile-time code
generation for zero reflection overhead.

## Key Features

- **Minimal API Pattern** – Map handlers with `lambda.MapHandler(...)` just like `app.MapGet()` in
  ASP.NET Core – clean, declarative, and intuitive
- **Dependency Injection** – Constructor and parameter injection using
  `Microsoft.Extensions.DependencyInjection` with proper scoped lifetimes per invocation
- **Middleware Pipeline** – Familiar `Use()` pattern for cross-cutting concerns like logging,
  validation, and error handling
- **Source Generated** – Compile-time code generation for zero reflection overhead and optimal
  performance
- **AOT Ready** – Native AOT compilation support for fast cold starts
- **Built-in Observability** – OpenTelemetry integration for distributed tracing and metrics
- **Type-Safe Envelopes** – Strongly-typed event wrappers for SQS, SNS, API Gateway, Kinesis, and
  more
- **Minimal Runtime Overhead** – Lightweight abstraction layer built on the same foundation as
  ASP.NET Core

## Packages

The framework is divided into multiple NuGet packages:

### Core Packages

The core packages provide the fundamental hosting framework, abstractions, and observability support
for building AWS Lambda functions.

| Package                                                                        | NuGet                                                                                                                                  | Downloads                                                                                                                                    |
| ------------------------------------------------------------------------------ | -------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------- |
| [**MinimalLambda**](./src/MinimalLambda/README.md)                             | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.svg)](https://www.nuget.org/packages/MinimalLambda)                             | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.svg)](https://www.nuget.org/packages/MinimalLambda/)                             |
| [**MinimalLambda.Abstractions**](./src/MinimalLambda.Abstractions/README.md)   | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Abstractions.svg)](https://www.nuget.org/packages/MinimalLambda.Abstractions)   | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Abstractions.svg)](https://www.nuget.org/packages/MinimalLambda.Abstractions/)   |
| [**MinimalLambda.OpenTelemetry**](./src/MinimalLambda.OpenTelemetry/README.md) | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.OpenTelemetry.svg)](https://www.nuget.org/packages/MinimalLambda.OpenTelemetry) | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.OpenTelemetry.svg)](https://www.nuget.org/packages/MinimalLambda.OpenTelemetry/) |
| [**MinimalLambda.Testing**](./src/MinimalLambda.Testing/README.md)             | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Testing.svg)](https://www.nuget.org/packages/MinimalLambda.Testing)             | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Testing.svg)](https://www.nuget.org/packages/MinimalLambda.Testing/)             |

### Envelopes Packages

Envelope packages provide specialized support for handling different AWS Lambda event sources,
including SQS, SNS, API Gateway, Kinesis, and more.

| Package                                                                                            | NuGet                                                                                                                                                          | Downloads                                                                                                                                                            |
| -------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [**MinimalLambda.Envelopes**](./src/Envelopes/MinimalLambda.Envelopes/README.md)                   | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes)                                 | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes/)                                 |
| [**MinimalLambda.Envelopes.Sqs**](./MinimalLambda.Envelopes.Sqs/README.md)                         | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Sqs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sqs)                         | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Sqs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sqs/)                         |
| [**MinimalLambda.Envelopes.ApiGateway**](./MinimalLambda.Envelopes.ApiGateway/README.md)           | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.ApiGateway.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.ApiGateway)           | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.ApiGateway.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.ApiGateway/)           |
| [**MinimalLambda.Envelopes.Sns**](./MinimalLambda.Envelopes.Sns/README.md)                         | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Sns.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sns)                         | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Sns.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sns/)                         |
| [**MinimalLambda.Envelopes.Kinesis**](./MinimalLambda.Envelopes.Kinesis/README.md)                 | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Kinesis.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kinesis)                 | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Kinesis.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kinesis/)                 |
| [**MinimalLambda.Envelopes.KinesisFirehose**](./MinimalLambda.Envelopes.KinesisFirehose/README.md) | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.KinesisFirehose.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.KinesisFirehose) | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.KinesisFirehose.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.KinesisFirehose/) |
| [**MinimalLambda.Envelopes.Kafka**](./MinimalLambda.Envelopes.Kafka/README.md)                     | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Kafka.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kafka)                     | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Kafka.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kafka/)                     |
| [**MinimalLambda.Envelopes.CloudWatchLogs**](./MinimalLambda.Envelopes.CloudWatchLogs/README.md)   | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.CloudWatchLogs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.CloudWatchLogs)   | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.CloudWatchLogs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.CloudWatchLogs/)   |
| [**MinimalLambda.Envelopes.Alb**](./MinimalLambda.Envelopes.Alb/README.md)                         | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Alb.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Alb)                         | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Alb.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Alb/)                         |

Each package has detailed documentation in its own README file.

## Quick Start

Create a new Lambda project from the templates:

```bash
dotnet new install MinimalLambda.Templates
dotnet new mlambda -n MyLambda
```

For Native AOT:

```bash
dotnet new mlambda-aot -n MyAotLambda
```

Or add the NuGet package to an existing project:

```bash
dotnet add package MinimalLambda
```

Create a simple Lambda handler with dependency injection:

```csharp
using MinimalLambda.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// Configure and run
var builder = LambdaApplication.CreateBuilder();
builder.Services.AddScoped<IGreetingService, GreetingService>();

var lambda = builder.Build();

// Inject the service directly into the handler
lambda.MapHandler(([FromEvent] string input, IGreetingService greeting) => greeting.Greet(input));

await lambda.RunAsync();

// Define a service interface
public interface IGreetingService
{
    string Greet(string name);
}

// Implement the service
public class GreetingService : IGreetingService
{
    public string Greet(string name) => $"Hello {name}!";
}
```

See the [examples directory](./examples/) for more complete examples, including middleware and
OpenTelemetry integration. For in-memory integration tests, use
[MinimalLambda.Testing](./docs/guides/testing.md) (a `WebApplicationFactory`-style runtime shim).

## AI Agent Skill

This repository includes a `minimal-lambda` agent skill with focused guidance for building,
debugging, testing, and reviewing MinimalLambda code without loading the full repository into agent
context.

Install it with [skills.sh](https://skills.sh):

```bash
npx skills add j-d-ha/minimal-lambda --skill minimal-lambda
```

Install globally for Claude Code:

```bash
npx skills add j-d-ha/minimal-lambda --skill minimal-lambda --global --agent claude-code
```

Update later with:

```bash
npx skills update minimal-lambda
```

## Documentation

- [MinimalLambda](./src/MinimalLambda/README.md) – Core framework documentation
- [MinimalLambda.Abstractions](./src/MinimalLambda.Abstractions/README.md) – Interfaces and
  abstractions
- [MinimalLambda.OpenTelemetry](./src/MinimalLambda.OpenTelemetry/README.md) – Observability
  documentation
- [Examples](./examples/) – Sample Lambda functions demonstrating framework features

## Contributing

Contributions are welcome! Bug reports, suggestions, and pull requests are all appreciated. Please
check the GitHub repository for guidelines.

## License

This project is licensed under the MIT License. See [LICENSE](./LICENSE) for details.
