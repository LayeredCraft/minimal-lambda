# Durable Execution

Use `MapDurableHandler` to build typed workflows on [AWS Lambda Durable Execution](https://docs.aws.amazon.com/durable-execution/getting-started/key-concepts/).

!!! warning "Experimental"

    `MinimalLambda.DurableExecution` and its NativeAOT support are experimental. APIs may change before a stable release.

## Install packages

Target `net10.0`. Reference all three packages directly:

```bash
dotnet add package MinimalLambda --version 2.6.0-beta.2
dotnet add package MinimalLambda.DurableExecution --prerelease
dotnet add package Amazon.Lambda.DurableExecution --version 1.0.0
```

`MinimalLambda.DurableExecution` releases independently from `MinimalLambda`. Install latest durable prerelease or stable package compatible with selected core version. Its package dependency enforces `2.6.0-beta.2` as current minimum compatible core version; do not require matching core and durable package versions. Direct `MinimalLambda` reference supplies source generator for `MapHandler` and `MapDurableHandler`. Direct AWS package reference supplies DE001-DE004 analyzers, which do not flow through transitive dependencies.

NuGet fallback may select an asset for another target framework, but only `net10.0` is supported.

## Deploy durable function

`MapDurableHandler` does not configure AWS infrastructure. Deployment must configure Lambda `DurableConfig`, durable IAM permissions, and a qualified target. Use sample [package recipe](https://github.com/LayeredCraft/minimal-lambda/tree/main/examples/MinimalLambda.Example.DurableExecution#package-for-deployment); it produces Lambda ZIP and deploys with Amazon.Lambda.Tools durable defaults. Custom roles require equivalent durable permissions plus application-specific permissions. Versions, aliases, and `$LATEST` are supported qualified targets; prefer immutable versions or aliases for stable routing.

No managed-service deployment was run. Consult current [AWS Durable Execution deployment documentation](https://docs.aws.amazon.com/lambda/latest/dg/durable-getting-started.html) before production use.

## Build typed workflow

Following complete `Program.cs` is maintained as [canonical Durable Execution sample](https://github.com/LayeredCraft/minimal-lambda/tree/main/examples/MinimalLambda.Example.DurableExecution):

```csharp title="Program.cs"
using System.Text.Json.Serialization;
using Amazon.Lambda.DurableExecution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalLambda;
using MinimalLambda.Builder;

var builder = LambdaApplication.CreateBuilder();

builder.Services.AddLambdaSerializerWithContext<DurableExampleJsonContext>();
builder.Services.AddSingleton<IOrderService, OrderService>();

await using var lambda = builder.Build();

lambda.MapDurableHandler(async (
    [FromEvent] OrderRequest request,
    IDurableContext durable,
    [FromServices] IOrderService orders) =>
{
    var step = await durable.StepAsync(
        (_, cancellationToken) => orders.ProcessAsync(request.OrderId, cancellationToken),
        name: "process-order");

    return new OrderResult(
        step.Message,
        durable.ExecutionContext.DurableExecutionArn,
        durable.LambdaContext.AwsRequestId);
});

await lambda.RunAsync();

internal sealed record OrderRequest(string OrderId);

internal sealed record OrderResult(string Message, string ExecutionArn, string AwsRequestId);

internal sealed record ProcessOrderStepResult(string Message);

internal interface IOrderService
{
    Task<ProcessOrderStepResult> ProcessAsync(string orderId, CancellationToken cancellationToken);
}

internal sealed class OrderService : IOrderService
{
    public Task<ProcessOrderStepResult> ProcessAsync(
        string orderId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ProcessOrderStepResult($"Order {orderId} processed"));
    }
}

[JsonSerializable(typeof(DurableExecutionInvocationInput))]
[JsonSerializable(typeof(DurableExecutionInvocationOutput))]
[JsonSerializable(typeof(OrderRequest))]
[JsonSerializable(typeof(OrderResult))]
[JsonSerializable(typeof(ProcessOrderStepResult))]
internal partial class DurableExampleJsonContext : JsonSerializerContext;
```

`StepAsync` checkpoints result. Step body can still be retried, so external side effects must be idempotent. Checkpointing does not guarantee exactly-once execution.

## Handler contract

Durable handlers support two return forms:

| Form            | Purpose                       |
| --------------- | ----------------------------- |
| `Task`          | Workflow with no typed result |
| `Task<TOutput>` | Workflow with typed result    |

`[FromEvent] TInput` and AWS `IDurableContext` are optional; a handler may use either, both, or neither. Each can occur at most once. Parameter order is unrestricted. Other parameters use normal handler binding rules, but `ref`, `in`, and `out` parameters are unsupported. Types referenced by handler parameters or output must be accessible from generated code and cannot contain unbound type parameters. See [Dependency Injection](../guides/dependency-injection.md) for service lifetimes.

Do not expose `DurableExecutionInvocationInput`, `DurableExecutionInvocationOutput`, streams, or AWS client in high-level handler. MinimalLambda directly deserializes and serializes hidden outer envelopes through its configured Lambda serializer; durable envelopes are not available through `IEventFeature` or `IResponseFeature`. It also owns raw-stream hosting, middleware, DI scope, and physical invocation context. AWS runtime owns workflow payloads, `IDurableContext`, checkpoints, replay, suspension, waits, and durable status/result mapping.

### Cancellation

AWS operation callbacks receive SDK-linked cancellation tokens. A durable handler that explicitly declares `CancellationToken` receives `ILambdaInvocationContext.CancellationToken`; it owns resulting physical-invocation failure and retry behavior. It is not a durable-operation token.

### Execution and invocation metadata

Logical durable execution can span many physical Lambda invocations. Use:

- `durable.ExecutionContext.DurableExecutionArn` for logical execution identity.
- `durable.LambdaContext` for AWS metadata from current physical invocation.
- injected `ILambdaInvocationContext` for MinimalLambda metadata and services from current physical invocation.

When only `IDurableContext` is available, recover exact MinimalLambda context:

```csharp
using MinimalLambda.DurableExecution;

ILambdaInvocationContext invocation = durable.GetInvocationContext();
```

Invocation context and scoped DI belong to one physical invocation. Replay creates new invocation and scope. Never store logical workflow state in scoped service.

## Serialization and AOT checklist

One registered `ILambdaSerializer` handles MinimalLambda outer envelopes and AWS inner values. For source-generated JSON and AOT:

- [ ] Register context with `AddLambdaSerializerWithContext<TContext>()`.
- [ ] Add `DurableExecutionInvocationInput` root.
- [ ] Add `DurableExecutionInvocationOutput` root.
- [ ] Add the event type if the handler declares one.
- [ ] For event-less handlers, add `[JsonSerializable(typeof(object))]`; generated adapters use it for ignored workflow input.
- [ ] For `Task<TOutput>`, add `TOutput` when required by the configured serializer.
- [ ] Add every operation payload, result, and state root used by steps, callbacks, invokes, child workflows, waits, maps, or parallel branches.
- [ ] Publish intended runtime and architecture with NativeAOT enabled; restore/build alone does not compile native code.

The generator does not inspect serializer contexts. Ensure that the context registered for the Lambda serializer includes the outer envelopes and every payload, result, and state type your workflow uses.

Local source-based `net10.0` NativeAOT publish passes. This proves local publishing only. Managed cloud integration remains unverified, and Durable Execution NativeAOT support remains experimental. Successful local publish is not evidence of deployment, IAM, replay, or managed-service behavior. See [AWS NativeAOT guidance](https://docs.aws.amazon.com/lambda/latest/dg/dotnet-native-aot.html) and project [support matrix](https://github.com/LayeredCraft/minimal-lambda/blob/main/decisions/durable-dependency-support-matrix.md).

## Make replay safe

AWS rebuilds workflow state by replaying code around checkpointed operations.

- Keep step and callback side effects idempotent.
- Use `durable.Logger` or operation context logger for workflow logs. AWS logger suppresses messages while re-deriving checkpointed operations; ambient loggers and `Console.WriteLine` repeat on replay.
- Do not use invocation-scoped memory or DI service as durable state.
- Expect middleware, scope construction, and invocation-level telemetry to run once per physical invocation, including replay.

Durable middleware should call and await `next` once, preserve exceptions, and avoid response fabrication or short-circuiting. MinimalLambda does not enforce these rules: skipped `next` can produce an empty response, repeated `next` reruns the adapter, and swallowed failures can change AWS-visible behavior. Replay-safe logging, metrics, tracing, and invocation-scoped observation fit. Apply these constraints when using [Middleware](../guides/middleware.md) or [OpenTelemetry](open_telemetry.md).

## Test locally

Build canonical sample:

```bash
dotnet build examples/MinimalLambda.Example.DurableExecution/MinimalLambda.Example.DurableExecution.csproj
```

Split tests by ownership:

- Use [MinimalLambda host/integration tests](../guides/testing.md) for generated adapter, middleware, DI, serializer identity, and outer stream roundtrip. Durable envelope feature access is intentionally unsupported.
- Use `Amazon.Lambda.DurableExecution.Testing` for workflow operations, suspension, waits, and replay. Follow [AWS durable testing guide](https://docs.aws.amazon.com/lambda/latest/dg/durable-testing.html).

`dotnet run` is not local workflow runner; executable expects Lambda Runtime API. Local engine and integration tests do not prove IAM, deployment, managed-runtime behavior, retention, or cloud service integration.

## Troubleshooting

| Symptom                                                    | Fix                                                                                                                                                   |
| ---------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------- |
| `LH0007`                                                   | Use `Task` or `Task<TOutput>`, value parameters, and closed types accessible to generated adapter code.                                               |
| Runtime `InvalidOperationException` at `MapDurableHandler` | Compile-time interceptor did not replace fallback stub. Keep direct `MinimalLambda` reference and project interceptor/source-generator configuration. |
| AWS DE001-DE004 absent                                     | Add direct `Amazon.Lambda.DurableExecution` reference; analyzer assets are not transitive.                                                            |
| Duplicate logs, metrics, or DI work                        | Replay caused another physical invocation. Use AWS replay-aware logger for workflow logs and make invocation observation replay-safe.                 |
| `dotnet run` cannot execute workflow                       | Use builds/tests or deploy configured durable function; process expects Lambda Runtime API.                                                           |

## Raw-envelope escape hatch

For raw envelope access, custom `IAmazonLambda`, protocol diagnostics, or new AWS overloads, replace `MapDurableHandler` with low-level `MapHandler` plus `DurableFunction.WrapAsync`. Never register both paths. See [package raw-envelope guide](https://github.com/LayeredCraft/minimal-lambda/tree/main/src/MinimalLambda.DurableExecution) for full code and serializer requirements.

Related: [Handler Registration](../guides/handler-registration.md), [Middleware](../guides/middleware.md), [Dependency Injection](../guides/dependency-injection.md), and [Testing](../guides/testing.md).
