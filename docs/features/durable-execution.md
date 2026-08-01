# Durable Execution

Use `MapDurableHandler` to build typed workflows on [AWS Lambda Durable Execution](https://docs.aws.amazon.com/durable-execution/getting-started/key-concepts/).

!!! warning "Experimental"

    `MinimalLambda.DurableExecution` and its NativeAOT support are experimental. APIs may change before a stable release.

## Install packages

Target `net8.0` or `net10.0`. Reference all three packages directly:

```bash
dotnet add package MinimalLambda --version 2.6.0-beta.2
dotnet add package MinimalLambda.DurableExecution --version 2.6.0-beta.2
dotnet add package Amazon.Lambda.DurableExecution --version 1.0.0
```

`MinimalLambda.DurableExecution` and `MinimalLambda` are versioned independently; do not assume matching versions are always required. `2.6.0-beta.2` is current minimum compatible core version. Direct `MinimalLambda` reference supplies source generator for `MapHandler` and `MapDurableHandler`. Direct AWS package reference supplies DE001-DE004 analyzers, which do not flow through transitive dependencies.

NuGet fallback may select an asset for another target framework, but only `net8.0` and `net10.0` are supported.

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

lambda.MapDurableHandler(HandleOrderAsync);

await lambda.RunAsync();

static async Task<OrderResult> HandleOrderAsync(
    [FromEvent] OrderRequest request,
    IDurableContext durable,
    [FromServices] IOrderService orders)
{
    var step = await durable.StepAsync(
        (_, cancellationToken) => orders.ProcessAsync(request.OrderId, cancellationToken),
        name: "process-order");

    return new OrderResult(
        step.Message,
        durable.ExecutionContext.DurableExecutionArn,
        durable.LambdaContext.AwsRequestId);
}

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

Durable handlers support two exact return forms:

| Form            | Purpose                       | Required explicit serializer roots             |
| --------------- | ----------------------------- | ---------------------------------------------- |
| `Task`          | Workflow with no typed result | outer input, outer output, `TInput`            |
| `Task<TOutput>` | Workflow with typed result    | outer input, outer output, `TInput`, `TOutput` |

Both forms require exactly one `[FromEvent] TInput` and exactly one exact AWS `IDurableContext`. Input is never inferred, and parameter order is unrestricted. Additional parameters may be `ILambdaContext`, `ILambdaInvocationContext`, ordinary DI, keyed DI, or optional DI. See [Dependency Injection](../guides/dependency-injection.md) for service lifetimes.

Do not expose `DurableExecutionInvocationInput`, `DurableExecutionInvocationOutput`, streams, or AWS client in high-level handler. MinimalLambda owns hidden outer envelope, raw-stream host, serializer boundary, middleware, DI scope, physical invocation context, and terminal lifecycle. AWS runtime owns workflow payloads, `IDurableContext`, checkpoints, replay, suspension, waits, and durable status/result mapping.

### Cancellation

Never add root `CancellationToken` to durable handler. Generator reports `LH0009` because near-timeout root cancellation could turn retryable physical invocation into terminal `FAILED` workflow. Use AWS-supplied token inside step, callback, child workflow, map, parallel, and other operation callbacks, as sample does.

Reading `ILambdaInvocationContext.CancellationToken` explicitly means accepting root failure/retry consequences.

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
- [ ] Add workflow `TInput` root.
- [ ] For `Task<TOutput>`, add `TOutput` root. `Task` handlers therefore need three signature roots; `Task<TOutput>` handlers need four.
- [ ] Add every operation payload, result, and state root used by steps, callbacks, invokes, child workflows, waits, maps, or parallel branches.
- [ ] Publish intended runtime and architecture with NativeAOT enabled; restore/build alone does not compile native code.

Generator cannot discover operation types hidden inside method bodies or referenced libraries. `LH0011` checks only explicit outer input/output and handler input/output declarations. It does not inspect member graphs or hidden operation types, and warning absence does not prove serializer completeness. `LH0011` does not suppress generation, so missing runtime metadata can still fail later.

Local package-only `net8.0` and source-based `net10.0` NativeAOT publishes pass. This proves local publishing only. Managed cloud integration remains unverified, and Durable Execution NativeAOT support remains experimental. Successful local publish is not evidence of deployment, IAM, replay, or managed-service behavior. See [AWS NativeAOT guidance](https://docs.aws.amazon.com/lambda/latest/dg/dotnet-native-aot.html) and project [support matrix](https://github.com/LayeredCraft/minimal-lambda/blob/main/decisions/durable-dependency-support-matrix.md).

## Make replay safe

AWS rebuilds workflow state by replaying code around checkpointed operations.

- Keep step and callback side effects idempotent.
- Use `durable.Logger` or operation context logger for workflow logs. AWS logger suppresses messages while re-deriving checkpointed operations; ambient loggers and `Console.WriteLine` repeat on replay.
- Do not use invocation-scoped memory or DI service as durable state.
- Expect middleware, scope construction, and invocation-level telemetry to run once per physical invocation, including replay.

Durable middleware must call and await `next` exactly once. It must not skip or double `next`, fabricate ordinary response, short-circuit through response cache, translate terminal exception into response, or swallow terminal exception. Replay-safe logging, metrics, tracing, and invocation-scoped observation fit. Apply these constraints when using [Middleware](../guides/middleware.md) or [OpenTelemetry](open_telemetry.md).

## Test locally

Build canonical sample:

```bash
dotnet build examples/MinimalLambda.Example.DurableExecution/MinimalLambda.Example.DurableExecution.csproj
```

Split tests by ownership:

- Use [MinimalLambda host/integration tests](../guides/testing.md) for generated adapter, middleware, DI, serializer identity, terminal lifecycle, and outer stream roundtrip.
- Use `Amazon.Lambda.DurableExecution.Testing` for workflow operations, suspension, waits, and replay. Follow [AWS durable testing guide](https://docs.aws.amazon.com/lambda/latest/dg/durable-testing.html).

`dotnet run` is not local workflow runner; executable expects Lambda Runtime API. Local engine and integration tests do not prove IAM, deployment, managed-runtime behavior, retention, or cloud service integration.

## Deploy and invoke

Before claiming cloud support:

- [ ] Create function with Durable Execution configuration; existing ordinary function cannot be converted by update alone.
- [ ] Configure execution timeout, retention, IAM, runtime, architecture, and serializer roots.
- [ ] Publish and invoke qualified version or alias, not unqualified function ARN.
- [ ] Cloud-test create, qualified invoke, checkpoint/replay, wait/suspension, callback, failure, retention, and IAM paths.
- [ ] Repeat cloud tests for every claimed managed runtime and NativeAOT combination.

Follow [Durable Execution Deployment](../guides/durable-execution-deployment.md) for locally validated package/template assets, direct CLI and SAM syntax, IAM boundaries, qualified invocation, and version rollback safety. Use [AWS infrastructure configuration](https://docs.aws.amazon.com/lambda/latest/dg/durable-getting-started-iac.html) and [supported runtimes](https://docs.aws.amazon.com/lambda/latest/dg/durable-supported-runtimes.html) as current service references. Package restore, sample build, template validation, local workflow tests, and local AOT publish are not cloud evidence.

## Troubleshooting

| Symptom                                                    | Fix                                                                                                                                                                                                     |
| ---------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `LH0007`                                                   | Declare exactly one `[FromEvent] TInput`; remove missing or duplicate event inputs.                                                                                                                     |
| `LH0008`                                                   | Declare exactly one exact `Amazon.Lambda.DurableExecution.IDurableContext`.                                                                                                                             |
| `LH0009`                                                   | Remove unsupported parameter. Common cases: root `CancellationToken`, stream/outer envelope, conflicting binding attribute, or `ref`/`in`/`out`. Use operation callback token or raw path where needed. |
| `LH0010`                                                   | Return exact `Task` or `Task<TOutput>`. Sync, `ValueTask`, custom awaitable, stream, and envelope outputs are unsupported.                                                                              |
| `LH0011`                                                   | Add explicit `[JsonSerializable(typeof(...))]` signature root. Then audit hidden operation roots manually; warning checks only signature roots and does not block emission.                             |
| Runtime `InvalidOperationException` at `MapDurableHandler` | Compile-time interceptor did not replace fallback stub. Keep direct `MinimalLambda` reference and project interceptor/source-generator configuration.                                                   |
| AWS DE001-DE004 absent                                     | Add direct `Amazon.Lambda.DurableExecution` reference; analyzer assets are not transitive.                                                                                                              |
| Duplicate logs, metrics, or DI work                        | Replay caused another physical invocation. Use AWS replay-aware logger for workflow logs and make invocation observation replay-safe.                                                                   |
| Empty/fabricated response or swallowed failure             | Middleware violated durable terminal lifecycle. Call/await `next` once and preserve terminal exception.                                                                                                 |
| `dotnet run` cannot execute workflow                       | Use builds/tests or deploy configured durable function; process expects Lambda Runtime API.                                                                                                             |

## Raw-envelope escape hatch

For raw envelope access, custom `IAmazonLambda`, protocol diagnostics, or new AWS overloads, replace `MapDurableHandler` with low-level `MapHandler` plus `DurableFunction.WrapAsync`. Never register both paths. See [package raw-envelope guide](https://github.com/LayeredCraft/minimal-lambda/tree/main/src/MinimalLambda.DurableExecution) for full code and serializer requirements.

Related: [Handler Registration](../guides/handler-registration.md), [Middleware](../guides/middleware.md), [Dependency Injection](../guides/dependency-injection.md), and [Testing](../guides/testing.md).
