# Getting Started

**`MinimalLambda`** brings ASP.NET Core–style hosting, dependency injection, and middleware to AWS Lambda. Instead of wiring up serialization and context handling manually, you configure a Lambda-specific host that manages scopes, middleware, and strongly typed handlers at compile time.

### Why `MinimalLambda`?

- **Familiar patterns** – Builder APIs, DI, and middleware mirror ASP.NET Core.
- **Source-generated handlers** – Avoid reflection while staying AOT ready.
- **Lambda-focused lifecycle** – Startup, invocation, and shutdown hooks map to the Lambda runtime model.
- **Type-safe envelopes** – Request/response contracts are validated at compile time.

## Prerequisites

Before you begin, ensure you have:

- **.NET 8 SDK or later** – [Download here](https://dotnet.microsoft.com/download)
- **C# 11 or later** – Required for source generators and language features
- **Basic AWS Lambda knowledge** – Understanding of Lambda concepts (functions, events, execution model)
- **AWS Account** – For deploying and testing (optional for local development)

!!! tip "IDE Recommendations"

    - Visual Studio 2022 (17.8+)
    - JetBrains Rider 2023.3+
    - Visual Studio Code with C# Dev Kit

## Start Here

- **[Installation](installation.md)** – Add the NuGet packages and configure your csproj.
- **[Your First Lambda](first-lambda.md)** – Walk through a handler, DI setup, and local testing.
- **[Core Concepts](core-concepts.md)** – Learn about the host lifecycle, middleware, and source generation.
- **Testing** – For in-memory, `WebApplicationFactory`-style integration tests, see
    [MinimalLambda.Testing](../guides/testing.md).

Prefer to explore? Head directly to **[Guides](../guides/index.md)** or **[Examples](../examples/index.md)** for deeper dives.

## Framework Highlights

- **Async-first runtime** – Cancellation tokens, timeout awareness, and scoped services work as expected.
- **Middleware pipeline** – Implement interception logic such as logging, validation, or OpenTelemetry spans in one place.
- **Envelope integrations** – Map SQS, SNS, API Gateway, and custom payloads using the envelope abstractions.
- **Observability-ready** – First-class OpenTelemetry integration for traces and metrics.

## Explore Features

- **[Envelopes](../features/envelopes.md)** – Type-safe event source integration (SQS, SNS, API Gateway, etc.)
- **[OpenTelemetry](../features/open_telemetry.md)** – Distributed tracing and observability
- **[AOT Compilation](../advanced/index.md)** – Optimize for fastest cold starts (coming soon)
- **[Source Generators](../advanced/index.md)** – Understand compile-time optimizations (coming soon)

## Getting Help

If you run into issues or have questions:

- **[GitHub Issues](https://github.com/j-d-ha/minimal-lambda/issues)** – Report bugs or request features
- **[GitHub Discussions](https://github.com/j-d-ha/minimal-lambda/discussions)** – Ask questions and share ideas

Continue with **[Installation](installation.md)** to configure your project.
