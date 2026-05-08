# Guides

Comprehensive guides for building production Lambda functions with `MinimalLambda`. Each guide provides in-depth coverage of a specific framework feature with complete examples, best practices, and troubleshooting.

## Core Framework Guides

Master the essential framework features that power your Lambda functions.

### [Dependency Injection](dependency-injection.md)

Learn service registration patterns, understand Singleton vs Scoped lifetimes, and master dependency injection in handlers and lifecycle methods.

**Topics covered:**

- Service lifetime management (Singleton, Scoped)
- Service registration patterns
- Injectable parameter types
- Options pattern and configuration
- Best practices and anti-patterns

### [Middleware](middleware.md)

Build middleware pipelines for cross-cutting concerns like logging, metrics, validation, and error handling.

**Topics covered:**

- Middleware pipeline composition
- Common middleware patterns
- Context and state management
- Execution order and control flow
- Reusable middleware components

### [Lifecycle Management](lifecycle-management.md)

Understand and control the Lambda lifecycle phases: OnInit, Invocation, and OnShutdown.

**Topics covered:**

- OnInit phase for cold start setup
- OnShutdown phase for cleanup
- Multiple lifecycle handlers
- Timeout configuration
- Error handling in lifecycle

### [Handler Registration](handler-registration.md)

Register type-safe Lambda handlers with automatic dependency injection and source generation.

**Topics covered:**

- MapHandler method usage
- `[FromEvent]` attribute requirements
- Injectable parameter types
- Return type handling
- Source generation benefits
- Handler patterns

### [Hosting & Builder](hosting.md)

Understand what `LambdaApplication.CreateBuilder()` configures, how the runtime composes middleware,
and how to customize the host for advanced scenarios.

**Topics covered:**

- Builder defaults (configuration, logging, DI)
- LambdaApplicationOptions customization
- LambdaHostedService orchestration
- Default serializers and cancellation factories
- Troubleshooting host setup

### [Configuration](configuration.md)

Configure framework behavior with LambdaHostOptions and application settings.

**Topics covered:**

- LambdaHostOptions reference
- Timeout and cancellation configuration
- Application configuration patterns
- JSON serialization options
- Environment variables
- Best practices

## Development Guides

Build robust, testable, and deployable Lambda functions.

### [Error Handling](error-handling.md)

Implement resilient error handling with retries, graceful degradation, and proper exception management.

**Topics covered:**

- Exception handling in handlers
- Middleware error handling
- Cancellation token usage
- Retry patterns and strategies
- Dead Letter Queue integration
- Best practices

### [Testing](testing.md)

Write comprehensive tests for your Lambda functions using xUnit, NSubstitute, and AutoFixture.

**Topics covered:**

- Testing framework setup
- Unit testing services
- AutoNSubstituteData pattern
- Testing handlers and middleware
- Integration testing
- Test naming conventions
- In-memory end-to-end testing with `MinimalLambda.Testing` (`WebApplicationFactory`-style runtime shim)

## Learning Path

### New to `MinimalLambda`?

Start with [Getting Started](../getting-started/index.md) to build your first Lambda function, then return here for deeper coverage of specific features.

### Building Production Lambda Functions?

Use these guides as reference documentation when implementing specific features. Each guide is self-contained and can be read independently.

### Optimizing Performance?

After mastering the guides, explore [Advanced Topics](../advanced/index.md) for AOT compilation, source generators, and performance optimization.

## Additional Resources

- **[Examples (Coming Soon)](../examples/index.md)** – Guided sample apps covering middleware, envelopes, and DI wiring.
- **[Features](../features/index.md)** – Envelope packages and OpenTelemetry add-ons.
- **[Advanced Topics (Coming Soon)](../advanced/index.md)** – Native AOT, generator internals, and performance deep dives.

## Getting Help

If you encounter issues not covered in these guides:

- Search or ask in [GitHub Discussions](https://github.com/j-d-ha/minimal-lambda/discussions)
- Report bugs in [GitHub Issues](https://github.com/j-d-ha/minimal-lambda/issues)

______________________________________________________________________

Ready to dive in? Choose a guide above or start with [Dependency Injection](dependency-injection.md).
