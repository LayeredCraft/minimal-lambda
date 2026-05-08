# Envelope patterns

Read when selecting or implementing strongly typed AWS trigger envelopes.

## Exact envelope types

| Trigger                                | Request/event type                                      | Response type                                             |
| -------------------------------------- | ------------------------------------------------------- | --------------------------------------------------------- |
| API Gateway REST/HTTP v1/WebSocket     | `ApiGatewayRequestEnvelope<T>`                          | `ApiGatewayResponseEnvelope<T>` or `ApiGatewayResult`     |
| API Gateway HTTP API v2 / Function URL | `ApiGatewayV2RequestEnvelope<T>`                        | `ApiGatewayV2ResponseEnvelope<T>` or `ApiGatewayV2Result` |
| ALB                                    | `AlbRequestEnvelope<T>`                                 | `AlbResponseEnvelope<T>` or `AlbResult`                   |
| SQS                                    | `SqsEnvelope<T>`                                        | usually no response                                       |
| SNS                                    | `SnsEnvelope<T>`                                        | usually no response                                       |
| SNS-to-SQS                             | `SqsSnsEnvelope<T>`                                     | usually no response                                       |
| Kinesis Data Streams                   | `KinesisEnvelope<T>`                                    | usually no response                                       |
| Kinesis Firehose transform             | `KinesisFirehoseEventEnvelope<T>`                       | `KinesisFirehoseResponseEnvelope<T>`                      |
| Kafka/MSK                              | `KafkaEnvelope<T>`                                      | usually no response                                       |
| CloudWatch Logs                        | `CloudWatchLogsEnvelope<T>` or `CloudWatchLogsEnvelope` | usually no response                                       |

Always inspect matching README/source for current property names and special cases.

## API Gateway result pattern

Use result builders when handler can return different response body types.

```csharp
lambda.MapHandler(([FromEvent] ApiGatewayRequestEnvelope<CreateOrderRequest> request) =>
{
    if (request.BodyContent is null)
        return ApiGatewayResult.BadRequest(new ErrorResponse("Missing body"));

    return ApiGatewayResult.Created(new CreateOrderResponse(request.BodyContent.OrderId));
});
```

Use envelope response when response type is stable and you need full control.

```csharp
lambda.MapHandler(([FromEvent] ApiGatewayRequestEnvelope<CreateOrderRequest> request) =>
    new ApiGatewayResponseEnvelope<CreateOrderResponse>
    {
        StatusCode = 200,
        BodyContent = new CreateOrderResponse(request.BodyContent!.OrderId),
        Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" },
    });
```

## SQS batch pattern

```csharp
lambda.MapHandler(async ([FromEvent] SqsEnvelope<OrderMessage> envelope, IOrderService orders, CancellationToken ct) =>
{
    foreach (var message in envelope.Records)
    {
        if (message.BodyContent is null)
            continue;

        await orders.ProcessAsync(message.BodyContent, ct);
    }
});
```

Production code often needs partial-batch failure support depending on AWS integration. Check package/docs/current support before promising behavior.

## Kinesis pattern

```csharp
lambda.MapHandler(async ([FromEvent] KinesisEnvelope<StreamRecord> envelope, IStreamProcessor processor, CancellationToken ct) =>
{
    foreach (var record in envelope.Records)
    {
        if (record.Kinesis.DataContent is not null)
            await processor.ProcessAsync(record.Kinesis.DataContent, ct);
    }
});
```

Kinesis payload appears on `record.Kinesis.DataContent`.

## Firehose transform pattern

```csharp
lambda.MapHandler(([FromEvent] KinesisFirehoseEventEnvelope<InputRecord> envelope) =>
{
    var response = new KinesisFirehoseResponseEnvelope<OutputRecord>();

    // Inspect package README/source for current record-construction API.
    // Preserve record IDs and set transform result per AWS Firehose contract.

    return response;
});
```

Firehose response contracts are easy to get wrong; validate against package tests/source.

## AOT envelope pattern

See `aot-and-envelopes.md`. Register both Lambda serializer and envelope options because raw event and nested body content deserialize at different layers.
