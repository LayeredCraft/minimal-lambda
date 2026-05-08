# Envelopes

Envelope packages extend the official AWS Lambda event types (SQS, SNS, API Gateway, etc.) with
strongly typed payload accessors. Instead of deserializing JSON manually, you work with
`BodyContent<T>`, `MessageContent<T>`, or similar properties that the framework populates before your
handler executes.

Behind the scenes, minimal-lambda injects the `UseExtractAndPackEnvelope` middleware at the end of
every pipeline. That middleware automatically calls `IRequestEnvelope.ExtractPayload` before your
handler runs and `IResponseEnvelope.PackPayload` after it finishes, guaranteeing consistent
serialization for both built-in envelopes and your own custom envelope types.

## Why Envelopes?

- **Strong typing** – `SqsEnvelope<Foo>` ensures handlers only run when payloads deserialize into
    `Foo`.
- **Zero boilerplate** – No more `JsonSerializer.Deserialize` calls sprinkled through handlers.
- **Consistent serialization** – `EnvelopeOptions` applies globally, including Native AOT
    `JsonSerializerContext` support.
- **Extensible** – Implement `IRequestEnvelope`/`IResponseEnvelope` for proprietary event shapes or
    alternative serialization formats (XML, Protobuf, etc.).

## Provided Envelopes

Install only the envelopes you need; each one lives in its own NuGet package.

| Event Source                    | Package                                                                                                                                             | NuGet                                                                                                                                                          |
| ------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Infrastructure / Base           | [MinimalLambda.Envelopes](https://github.com/j-d-ha/minimal-lambda/tree/main/src/Envelopes/MinimalLambda.Envelopes)                                 | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes)                                 |
| SQS                             | [MinimalLambda.Envelopes.Sqs](https://github.com/j-d-ha/minimal-lambda/tree/main/src/Envelopes/MinimalLambda.Envelopes.Sqs)                         | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Sqs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sqs)                         |
| SNS                             | [MinimalLambda.Envelopes.Sns](https://github.com/j-d-ha/minimal-lambda/tree/main/src/Envelopes/MinimalLambda.Envelopes.Sns)                         | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Sns.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sns)                         |
| API Gateway / HTTP API          | [MinimalLambda.Envelopes.ApiGateway](https://github.com/j-d-ha/minimal-lambda/tree/main/src/Envelopes/MinimalLambda.Envelopes.ApiGateway)           | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.ApiGateway.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.ApiGateway)           |
| Kinesis Data Streams            | [MinimalLambda.Envelopes.Kinesis](https://github.com/j-d-ha/minimal-lambda/tree/main/src/Envelopes/MinimalLambda.Envelopes.Kinesis)                 | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Kinesis.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kinesis)                 |
| Kinesis Data Firehose           | [MinimalLambda.Envelopes.KinesisFirehose](https://github.com/j-d-ha/minimal-lambda/tree/main/src/Envelopes/MinimalLambda.Envelopes.KinesisFirehose) | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.KinesisFirehose.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.KinesisFirehose) |
| Kafka (MSK / self-managed)      | [MinimalLambda.Envelopes.Kafka](https://github.com/j-d-ha/minimal-lambda/tree/main/src/Envelopes/MinimalLambda.Envelopes.Kafka)                     | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Kafka.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kafka)                     |
| CloudWatch Logs                 | [MinimalLambda.Envelopes.CloudWatchLogs](https://github.com/j-d-ha/minimal-lambda/tree/main/src/Envelopes/MinimalLambda.Envelopes.CloudWatchLogs)   | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.CloudWatchLogs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.CloudWatchLogs)   |
| Application Load Balancer (ALB) | [MinimalLambda.Envelopes.Alb](https://github.com/j-d-ha/minimal-lambda/tree/main/src/Envelopes/MinimalLambda.Envelopes.Alb)                         | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Alb.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Alb)                         |

!!! note "Infrastructure Package"

    `MinimalLambda.Envelopes` is automatically referenced by ALB and API Gateway packages. It provides
    `IHttpResult<TSelf>` and extension methods for the response builder API. You don't need to install
    it directly.

Each package ships with README examples in the repository if you need event-specific guidance.

## Quick Start

Install the envelope package that matches your event source, then use the envelope type in your
handler. This SQS example demonstrates the pattern; swap `SqsEnvelope<T>` with another envelope type
to handle SNS, API Gateway, etc.

```bash
dotnet add package MinimalLambda.Envelopes.Sqs
```

```csharp title="Program.cs" linenums="1"
using Amazon.Lambda.SQSEvents;
using MinimalLambda.Builder;
using MinimalLambda.Envelopes.Sqs;

var builder = LambdaApplication.CreateBuilder();
var lambda = builder.Build();

lambda.MapHandler(
    ([FromEvent] SqsEnvelope<OrderMessage> envelope, ILogger<Program> logger) =>
    {
        foreach (var record in envelope.Records)
        {
            if (record.BodyContent is null)
                continue;

            logger.LogInformation("Processing order {OrderId}", record.BodyContent.OrderId);
        }

        return new SQSBatchResponse(); // optional when you want to signal per-message failures
    }
);

await lambda.RunAsync();

internal sealed record OrderMessage(string OrderId, decimal Amount);
```

## Response Builder API

For HTTP-based event sources (API Gateway, ALB), result classes provide a fluent API for building
responses. **Key benefit**: Return multiple strongly typed models from the same handler—for example,
different success and error response types.

```csharp title="API Gateway Example" linenums="1"
using MinimalLambda.Envelopes.ApiGateway;

lambda.MapHandler(([FromEvent] ApiGatewayRequestEnvelope<LoginRequest> request) =>
{
    // Each return statement uses a different strongly typed model
    if (string.IsNullOrEmpty(request.BodyContent?.Username))
        return ApiGatewayResult.BadRequest(new ValidationError("Username required"));

    if (!authService.Authenticate(request.BodyContent))
        return ApiGatewayResult.Unauthorized(new AuthError("Invalid credentials"));

    return ApiGatewayResult.Ok(new LoginSuccess(token, expiresAt));
});

internal record LoginRequest(string Username, string Password);
internal record ValidationError(string Message);
internal record AuthError(string Message);
internal record LoginSuccess(string Token, DateTime ExpiresAt);
```

### Available Result Classes

| Class                | Package                            | Use Case                            |
| -------------------- | ---------------------------------- | ----------------------------------- |
| `AlbResult`          | MinimalLambda.Envelopes.Alb        | Application Load Balancer responses |
| `ApiGatewayResult`   | MinimalLambda.Envelopes.ApiGateway | REST API / HTTP API v1 / WebSocket  |
| `ApiGatewayV2Result` | MinimalLambda.Envelopes.ApiGateway | HTTP API v2 responses               |

Common methods: `Ok()`, `Created()`, `NoContent()`, `BadRequest()`, `Unauthorized()`, `NotFound()`,
`Conflict()`, `UnprocessableEntity()`, `InternalServerError()`, `StatusCode(int)`, `Text(int, string)`, `Json<T>(int, T)`. All methods have overloads with and without body content.

### When to Use Results vs. Envelopes

**Use result classes** when you need to return multiple strongly typed models from the same handler.
Provides convenient methods for common HTTP status codes.

**Use envelope classes directly** when you need custom serialization (e.g., XML) or want to extend
envelope base classes for custom behavior.

!!! tip "Complete API Reference"

    For detailed method documentation, AOT configuration, and advanced usage, see the package README
    files:

    - [ALB Package README](https://github.com/j-d-ha/minimal-lambda/tree/main/src/Envelopes/MinimalLambda.Envelopes.Alb)
    - [API Gateway Package README](https://github.com/j-d-ha/minimal-lambda/tree/main/src/Envelopes/MinimalLambda.Envelopes.ApiGateway)

!!! note

    Result classes use their respective envelope classes internally (`AlbResponseEnvelope<T>`,
    `ApiGatewayResponseEnvelope<T>`, etc.). They're a convenience layer over the envelope
    infrastructure.

## AOT Support

When using .NET Native AOT, register all envelope and payload types in your `JsonSerializerContext`.

!!! tip "Register Both Envelope and Payload Types"

    You must register **both** the envelope type (e.g., `ApiGatewayRequestEnvelope<LoginRequest>`)
    **and** the inner payload type (e.g., `LoginRequest`). The envelope wraps the AWS event
    structure, while the payload is your business type inside the envelope.

### Basic Envelope Setup

```csharp title="Program.cs" linenums="1"
using System.Text.Json.Serialization;

[JsonSerializable(typeof(ApiGatewayRequestEnvelope<LoginRequest>))]  // Envelope wrapper
[JsonSerializable(typeof(ApiGatewayResponseEnvelope<LoginSuccess>))] // Envelope wrapper
[JsonSerializable(typeof(LoginRequest))]                             // Inner payload type
[JsonSerializable(typeof(LoginSuccess))]                             // Inner payload type
internal partial class SerializerContext : JsonSerializerContext;
```

### Result Classes with Multiple Return Types

When using result classes (`AlbResult`, `ApiGatewayResult`, `ApiGatewayV2Result`), register each
response type separately:

```csharp title="Program.cs" linenums="1"
using System.Text.Json.Serialization;

[JsonSerializable(typeof(ApiGatewayRequestEnvelope<LoginRequest>))]
[JsonSerializable(typeof(ApiGatewayResult))]
[JsonSerializable(typeof(LoginRequest))]
[JsonSerializable(typeof(ValidationError))]
[JsonSerializable(typeof(AuthError))]
[JsonSerializable(typeof(LoginSuccess))]
internal partial class SerializerContext : JsonSerializerContext;
```

### Registering the Serializer Context

Register the serializer and configure envelope options to use the context:

```csharp title="Program.cs" linenums="1"
var builder = LambdaApplication.CreateBuilder();

builder.Services.AddLambdaSerializerWithContext<SerializerContext>();

builder.Services.ConfigureEnvelopeOptions(options =>
{
    options.JsonOptions.TypeInfoResolver = SerializerContext.Default;
});
```

!!! important "Why Register in Two Places?"

    The context must be registered as the type resolver for **both** the envelope options and the
    Lambda serializer because deserialization happens at different steps:

    1. **Lambda serializer** deserializes the raw AWS event (e.g., API Gateway event structure)
    2. **Envelope options** deserialize the envelope content into your payload types

## Custom Serialization & EnvelopeOptions

All envelope packages respect the global `EnvelopeOptions` configuration. Call
`builder.Services.ConfigureEnvelopeOptions` to tweak `System.Text.Json` behavior, register
`JsonSerializerContext` instances for Native AOT, or enable XML readers/writers for custom envelopes.

```csharp title="Program.cs" linenums="1"
using System.Text.Json;
using System.Text.Json.Serialization;

builder.Services.ConfigureEnvelopeOptions(options =>
{
    options.JsonOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.JsonOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.JsonOptions.TypeInfoResolver = MyEnvelopeJsonContext.Default; // Native AOT
});
```

The framework automatically copies `JsonOptions.TypeInfoResolver` into the internal
`LambdaDefaultJsonOptions`, so every envelope (including complex ones like CloudWatch Logs) gains the
same serialization metadata.

Need XML or another format? Set `options.XmlReaderSettings` / `XmlWriterSettings` and implement your
envelope using `System.Xml`. See the SQS README in the repo for a complete XML sample.

### Advanced Configuration

- **`LambdaDefaultJsonOptions`** – minimal-lambda maintains a second `JsonSerializerOptions`
    instance for Lambda-specific envelopes (e.g., SNS→SQS fan-out). Most apps shouldn’t touch it; the
    host copies your `JsonOptions.TypeInfoResolver` automatically. Only override it when you need
    different converters for those hybrid envelopes.

- **`Items` dictionary** – Store arbitrary context for custom envelopes:

    ```csharp title="Program.cs" linenums="1"
    builder.Services.ConfigureEnvelopeOptions(options =>
    {
        options.Items["SchemaVersion"] = "v2";
        options.Items["Validator"] = new PayloadValidator();
    });
    ```

    Inside your envelope implementation, read `options.Items` to control validation or routing logic.

## Creating Custom Envelopes

Implement `IRequestEnvelope` and/or `IResponseEnvelope` when you control the event schema or need a
non-standard payload format. The middleware automatically invokes these interfaces, so you only write
the extraction logic.

```csharp title="CustomRequestEnvelope.cs" linenums="1"
using System.Text.Json;
using System.Text.Json.Serialization;
using MinimalLambda.Envelopes;
using MinimalLambda.Options;

public sealed class CustomRequestEnvelope : IRequestEnvelope
{
    [JsonPropertyName("payload")]
    public required string Payload { get; set; }

    [JsonIgnore]
    public MyPayload? PayloadContent { get; private set; }

    public void ExtractPayload(EnvelopeOptions options)
    {
        PayloadContent = JsonSerializer.Deserialize<MyPayload>(Payload, options.JsonOptions);
    }
}
```

In a handler:

```csharp title="Program.cs" linenums="1"
lambda.MapHandler(([FromEvent] CustomRequestEnvelope envelope) =>
{
    if (envelope.PayloadContent is null)
        return new { Error = "Invalid payload" };

    return new { Success = true, envelope.PayloadContent.Name };
});
```

Response envelopes work the same way—implement `IResponseEnvelope` and serialize into a string
property inside `PackPayload`.

### Batch Envelopes

If your custom event contains multiple records (similar to SQS), deserialize each entry inside
`ExtractPayload`. Keep the original serialized string plus a `[JsonIgnore]` property for the strongly
typed object.

## Best Practices

- **Check for null** – Always guard against `BodyContent`/`PayloadContent` being `null`. Set it to
    `null` if deserialization fails instead of throwing.
- **Use `[JsonIgnore]`** – Keep serialized strings (`Body`, `Payload`, etc.) separate from the
    deserialized object to avoid recursive serialization.
- **Return `SQSBatchResponse` when required** – For SQS/SNS to SQS fan-out scenarios, populate
    `BatchItemFailures` to signal per-message errors.
- **Centralize configuration** – Prefer `ConfigureEnvelopeOptions` or configuration binding over
    ad-hoc serializer tweaks.
- **Log deserialization issues** – Logging helps diagnose malformed payloads without crashing the
    Lambda.

## When to Use Envelopes

Choose envelopes whenever:

- You want compile-time type checking for Lambda payloads.
- Your event contains nested JSON/XML that you’d otherwise deserialize manually.
- You need consistent serialization between request and response envelopes (API Gateway, ALB).
- You’re preparing for Native AOT and want the serialization metadata defined in one place.

Not every Lambda needs an envelope—handlers that already consume strongly typed SDK models can rely
on the base event types. Mix and match envelopes based on the triggers your project uses.
