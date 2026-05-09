# AOT and serializer patterns

Read when user targets Native AOT, trimming, or envelope body deserialization.

## Plain event/response context

```csharp
using System.Text.Json.Serialization;

[JsonSerializable(typeof(OrderRequest))]
[JsonSerializable(typeof(OrderResponse))]
internal partial class SerializerContext : JsonSerializerContext;

builder.Services.AddLambdaSerializerWithContext<SerializerContext>();
```

`AddLambdaSerializerWithContext<TContext>()` registers AWS `ILambdaSerializer` backed by source-generated metadata.

## Envelope context

Envelope packages deserialize in two steps:

1. Lambda serializer deserializes raw AWS event/envelope.
2. Envelope code deserializes nested body/message/record payload.

So register both Lambda serializer and envelope options.

```csharp
using System.Text.Json.Serialization;
using MinimalLambda.Envelopes.ApiGateway;

[JsonSerializable(typeof(ApiGatewayRequestEnvelope<CreateOrderRequest>))]
[JsonSerializable(typeof(ApiGatewayResponseEnvelope<CreateOrderResponse>))]
[JsonSerializable(typeof(CreateOrderRequest))]
[JsonSerializable(typeof(CreateOrderResponse))]
internal partial class SerializerContext : JsonSerializerContext;

builder.Services.AddLambdaSerializerWithContext<SerializerContext>();

builder.Services.ConfigureEnvelopeOptions(options =>
{
    options.JsonOptions.TypeInfoResolver = SerializerContext.Default;
});
```

## Kinesis example

Docs show this pattern:

```csharp
[JsonSerializable(typeof(KinesisEnvelope<StreamRecord>))]
[JsonSerializable(typeof(StreamRecord))]
internal partial class SerializerContext : JsonSerializerContext;

builder.Services.AddLambdaSerializerWithContext<SerializerContext>();

builder.Services.ConfigureEnvelopeOptions(options =>
{
    options.JsonOptions.TypeInfoResolver = SerializerContext.Default;
});
```

## AOT review checklist

- [ ] Every event/envelope/response/payload type appears in `JsonSerializerContext`.
- [ ] `AddLambdaSerializerWithContext<TContext>()` is registered before `Build()`.
- [ ] Envelope payloads also configure `ConfigureEnvelopeOptions`.
- [ ] No runtime reflection over handlers/contracts.
- [ ] Publish has been tested; build alone is not enough.

## When not to overdo it

If client project is not AOT/trimming-sensitive, normal `System.Text.Json` fallback may be acceptable. Still prefer explicit contracts and avoid dynamic serialization for Lambda cold-start performance and reliability.
