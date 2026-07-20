# Envelopes

Envelopes extend AWS Lambda event types with strongly-typed payload handling, making it easier to
work with JSON (or other formats) in Lambda function handlers.

> 📚 **[View Full Documentation](https://layeredcraft.github.io/minimal-lambda/)**

## Overview

Envelope packages wrap official AWS Lambda event classes (like `SQSEvent`, `APIGatewayProxyRequest`)
and add a `BodyContent<T>` property that provides type-safe access to deserialized message payloads.
Instead of manually parsing JSON strings from event bodies, you get strongly-typed objects with full
IDE support and compile-time type checking.

**Key benefits:**

- **Type Safety** Generic type parameter `<T>` ensures compile-time type checking for payloads
- **Extensibility** Abstract base classes allow custom serialization formats (JSON, XML, etc.)
- **Zero Overhead** Envelopes extend official AWS event types, adding no runtime cost
- **AOT Ready** Support for Native AOT compilation via JsonSerializerContext registration
- **Familiar API** Works seamlessly with existing AWS Lambda event patterns

## Packages

| Lambda Event Type                    | Package                                                                                                                     | NuGet                                                                                                                                                          | Downloads                                                                                                                                                            |
|--------------------------------------|-----------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------------------------------------------------|
| SQS                                  | [**MinimalLambda.Envelopes.Sqs**](./MinimalLambda.Envelopes.Sqs/README.md)                                                  | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Sqs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sqs)                         | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Sqs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sqs/)                         |
| SQS (SNS-to-SQS Subscription)        | [**MinimalLambda.Envelopes.Sqs**](./MinimalLambda.Envelopes.Sqs/README.md#sqssnsenvelope---sns-to-sqs-subscription-pattern) | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Sqs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sqs)                         | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Sqs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sqs/)                         |
| API Gateway (REST/HTTP/WebSocket)    | [**MinimalLambda.Envelopes.ApiGateway**](./MinimalLambda.Envelopes.ApiGateway/README.md)                                    | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.ApiGateway.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.ApiGateway)           | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.ApiGateway.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.ApiGateway/)           |
| SNS                                  | [**MinimalLambda.Envelopes.Sns**](./MinimalLambda.Envelopes.Sns/README.md)                                                  | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Sns.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sns)                         | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Sns.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sns/)                         |
| Kinesis Data Streams                 | [**MinimalLambda.Envelopes.Kinesis**](./MinimalLambda.Envelopes.Kinesis/README.md)                                          | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Kinesis.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kinesis)                 | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Kinesis.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kinesis/)                 |
| Kinesis Data Firehose Transformation | [**MinimalLambda.Envelopes.KinesisFirehose**](./MinimalLambda.Envelopes.KinesisFirehose/README.md)                          | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.KinesisFirehose.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.KinesisFirehose) | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.KinesisFirehose.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.KinesisFirehose/) |
| Kafka (MSK or self-managed)          | [**MinimalLambda.Envelopes.Kafka**](./MinimalLambda.Envelopes.Kafka/README.md)                                              | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Kafka.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kafka)                     | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Kafka.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kafka/)                     |
| CloudWatch Logs                      | [**MinimalLambda.Envelopes.CloudWatchLogs**](./MinimalLambda.Envelopes.CloudWatchLogs/README.md)                            | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.CloudWatchLogs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.CloudWatchLogs)   | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.CloudWatchLogs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.CloudWatchLogs/)   |
| Application Load Balancer            | [**MinimalLambda.Envelopes.Alb**](./MinimalLambda.Envelopes.Alb/README.md)                                                  | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Alb.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Alb)                         | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Alb.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Alb/)                         |

Each package has detailed documentation in its own README file.
