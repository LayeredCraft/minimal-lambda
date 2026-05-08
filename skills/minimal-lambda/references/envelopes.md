# Envelopes

Read when task touches SQS, SNS, API Gateway, Kinesis, Kinesis Firehose, Kafka/MSK, CloudWatch Logs, ALB, event bodies, typed payloads, or AWS trigger-specific request/response types.

## Portability note

This reference is self-contained for client-project use. Do not assume envelope package source or tests exist in the current workspace. If the task is a repo contribution, switch to `repo-workflow.md` before inspecting local source.

## Mental model

Envelope packages wrap official AWS Lambda event classes and add type-safe payload access, commonly `BodyContent<T>`, so client code avoids manual JSON parsing of strings.

Benefits:

- typed payload contracts
- trigger-specific envelope support
- AOT-friendly serializer context paths
- reuse AWS event shape while adding generic body content

## Package selection

Use only package(s) matching trigger:

| Trigger                         | Package                                           |
| ------------------------------- | ------------------------------------------------- |
| SQS                             | `MinimalLambda.Envelopes.Sqs`                     |
| SNS                             | `MinimalLambda.Envelopes.Sns`                     |
| SNS-to-SQS                      | `MinimalLambda.Envelopes.Sqs` SNS-to-SQS envelope |
| API Gateway REST/HTTP/WebSocket | `MinimalLambda.Envelopes.ApiGateway`              |
| Kinesis Data Streams            | `MinimalLambda.Envelopes.Kinesis`                 |
| Kinesis Firehose transform      | `MinimalLambda.Envelopes.KinesisFirehose`         |
| Kafka/MSK/self-managed          | `MinimalLambda.Envelopes.Kafka`                   |
| CloudWatch Logs                 | `MinimalLambda.Envelopes.CloudWatchLogs`          |
| ALB                             | `MinimalLambda.Envelopes.Alb`                     |

## Exact common types

| Trigger                                | Request/event type                                      | Response type                                             |
| -------------------------------------- | ------------------------------------------------------- | --------------------------------------------------------- |
| API Gateway REST/HTTP v1/WebSocket     | `ApiGatewayRequestEnvelope<T>`                          | `ApiGatewayResponseEnvelope<T>` or `ApiGatewayResult`     |
| API Gateway HTTP API v2 / Function URL | `ApiGatewayV2RequestEnvelope<T>`                        | `ApiGatewayV2ResponseEnvelope<T>` or `ApiGatewayV2Result` |
| ALB                                    | `AlbRequestEnvelope<T>`                                 | `AlbResponseEnvelope<T>` or `AlbResult`                   |
| SQS                                    | `SqsEnvelope<T>`                                        | usually none                                              |
| SNS                                    | `SnsEnvelope<T>`                                        | usually none                                              |
| SNS-to-SQS                             | `SqsSnsEnvelope<T>`                                     | usually none                                              |
| Kinesis Data Streams                   | `KinesisEnvelope<T>`                                    | usually none                                              |
| Kinesis Firehose transform             | `KinesisFirehoseEventEnvelope<T>`                       | `KinesisFirehoseResponseEnvelope<T>`                      |
| Kafka/MSK/self-managed                 | `KafkaEnvelope<T>`                                      | usually none                                              |
| CloudWatch Logs                        | `CloudWatchLogsEnvelope<T>` or `CloudWatchLogsEnvelope` | usually none                                              |

## Handler shape

Envelope types still enter through `[FromEvent]`:

```csharp
lambda.MapHandler(async ([FromEvent] SqsEnvelope<OrderMessage> envelope, IOrderService service, CancellationToken ct) =>
{
    foreach (var message in envelope.Records)
    {
        if (message.BodyContent is not null)
            await service.ProcessAsync(message.BodyContent, ct);
    }
});
```

When exact type/member names matter, inspect target package README and source. Names vary by trigger.

## AOT / serialization guidance

- Prefer explicit records/classes with predictable JSON contracts.
- For Native AOT, add envelope and payload/response types to `JsonSerializerContext`.
- Register `builder.Services.AddLambdaSerializerWithContext<SerializerContext>()`.
- For nested envelope payloads, also call `builder.Services.ConfigureEnvelopeOptions(options => options.JsonOptions.TypeInfoResolver = SerializerContext.Default)`.
- Avoid ad-hoc `JsonSerializer.Deserialize<object>` and reflection-based polymorphism.
- Match request and response envelope types for API Gateway/ALB-style triggers.

See `patterns/aot-and-envelopes.md` for complete snippets.

## Agent workflow for envelope questions

1. Identify AWS event source.
2. Read matching envelope README.
3. Inspect source/tests for exact type/member names.
4. Propose minimal package references and handler signature.
5. Add/adjust tests using `MinimalLambda.Testing` or existing envelope unit test style.
