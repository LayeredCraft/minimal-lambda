# Troubleshooting MinimalLambda usage

Read when user reports compile errors, source generator diagnostics, runtime startup failures, handler not running, serialization issues, middleware not firing, or test failures.

## Source generator / compile-time issues

### `MapHandler` call not intercepted

Symptoms:

- runtime exception: `This method is replaced at compile time.`
- generated handler missing

Check:

- project uses supported C# language version
- package references include `MinimalLambda`
- `MapHandler` call shape is a static, analyzable delegate/method group
- source generator diagnostics in build output

### Missing or duplicate `[FromEvent]`

Payload handler needs exactly one `[FromEvent]` parameter. No-payload handler should have no event parameter.

Bad:

```csharp
lambda.MapHandler((OrderRequest request, IOrderService service) => service.Process(request));
```

Good:

```csharp
lambda.MapHandler(([FromEvent] OrderRequest request, IOrderService service) => service.Process(request));
```

### Keyed service diagnostic

`[FromKeyedServices(...)]` keys must be supported constants. If generator reports unsupported metadata, replace complex key object with string/int/enum-style supported key.

## Runtime issues

### Multiple handlers registered

Only one handler can be registered per Lambda execution. Conditional mapping is fine; executing multiple mappings is not.

### DI service missing

Handler parameters not marked `[FromEvent]` resolve from DI/context. If a custom service parameter fails, register it before `builder.Build()`.

### Scoped service leak

Warm Lambda containers reuse singletons. If per-invocation state appears in later invocations, check singleton fields and static state for captured scoped services/data.

### Middleware not running

Check registration order. Middleware should be registered before `MapHandler`.

## Serialization/envelope issues

### Body content null

For API Gateway/ALB/SQS/etc. envelope types, inspect raw event shape and matching package README. Common causes:

- wrong envelope type for trigger version
- request body absent or not JSON
- AOT serializer context missing payload/envelope type
- custom content type unsupported by default JSON envelope

### Native AOT serialization failure

Add all event, envelope, payload, and response types to `JsonSerializerContext`. Register both Lambda serializer and envelope options when envelopes deserialize nested payloads.

```csharp
builder.Services.AddLambdaSerializerWithContext<SerializerContext>();
builder.Services.ConfigureEnvelopeOptions(options =>
{
    options.JsonOptions.TypeInfoResolver = SerializerContext.Default;
});
```

## Testing issues

### `LambdaApplicationFactory<Program>` cannot access `Program`

Add public partial class marker at bottom of top-level `Program.cs`:

```csharp
public partial class Program;
```

### Test passes alone, fails in class fixture

Shared factory reuses host/singletons and runs `OnInit` once. Use fresh factory when test needs isolation.

### Invocation times out

Check `factory.ServerOptions.FunctionTimeout`, long-running middleware, and cancellation token propagation.

## Debug workflow

1. Reproduce with `dotnet build` first for generator diagnostics.
2. Run a focused integration test with `MinimalLambda.Testing`.
3. Log event/response feature types in middleware if binding unclear.
4. Compare handler/envelope type with matching package README and source.
5. For AOT, publish the app; normal build is insufficient.
