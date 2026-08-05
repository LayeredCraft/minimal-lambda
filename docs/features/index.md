# Features

The `MinimalLambda` framework provides a rich ecosystem of features and extension packages that enhance AWS Lambda development beyond the core framework capabilities. These features are designed to be modular, type-safe, and performant, integrating seamlessly with the core hosting patterns.

This section provides an overview of the available features.

______________________________________________________________________

## Feature Categories

### [Envelope Pattern](./envelopes.md)

The Envelope pattern provides type-safe wrappers for various AWS event sources like SQS, SNS, and API Gateway. Instead of manually parsing JSON, you can work with strongly-typed objects, improving code quality and developer productivity.

### [Durable Execution](./durable-execution.md)

Build typed, checkpointed AWS Lambda Durable Execution workflows with source-generated handler binding, dependency injection, and replay-aware guidance.

### [Observability (OpenTelemetry)](./open_telemetry.md)

This feature provides comprehensive observability through OpenTelemetry integration. It enables distributed tracing and metrics collection, offering deep insights into your Lambda function's performance and behavior.
