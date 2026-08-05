# MinimalLambda.DurableExecution

Typed AWS Lambda Durable Execution handlers for MinimalLambda.

> This package is experimental. APIs may change before stable release.

## Compatibility and installation

Package ships assets for exactly `net10.0`. NuGet may select a compatible asset for other TFMs, but
those combinations are not supported.

`MinimalLambda.DurableExecution` releases with `MinimalLambda`; use matching package versions.

Reference all three packages directly:

```bash
dotnet add package MinimalLambda --version 2.6.0-beta.2
dotnet add package MinimalLambda.DurableExecution --version 2.6.0-beta.2
dotnet add package Amazon.Lambda.DurableExecution --version 1.0.0
```

Direct `MinimalLambda` reference supplies one source generator for both `MapHandler` and
`MapDurableHandler`; no durable generator package exists. Wrapper already depends on AWS runtime,
but NuGet does not flow analyzer assets through transitive dependencies. Direct AWS reference enables
its DE001-DE004 analyzers.

## Complete typed handler

```csharp
using System.Text.Json.Serialization;
using Amazon.Lambda.DurableExecution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalLambda;
using MinimalLambda.Builder;

var builder = LambdaApplication.CreateBuilder();

builder.Services.AddLambdaSerializerWithContext<DurableJsonContext>();
builder.Services.AddSingleton<IOrderService, OrderService>();

await using var lambda = builder.Build();

lambda.MapDurableHandler(async (
    [FromEvent] OrderRequest request,
    IDurableContext durable,
    ILambdaInvocationContext invocation,
    [FromServices] IOrderService orders) =>
{
    var step = await durable.StepAsync(
        (_, cancellationToken) =>
            orders.ProcessAsync(request.OrderId, cancellationToken),
        name: "process-order");

    return new OrderResult(
        step.Message,
        durable.ExecutionContext.DurableExecutionArn,
        invocation.AwsRequestId);
});

await lambda.RunAsync();

internal sealed record OrderRequest(string OrderId);
internal sealed record OrderResult(string Message, string ExecutionArn, string AwsRequestId);
internal sealed record ProcessOrderStepResult(string Message);

internal interface IOrderService
{
    Task<ProcessOrderStepResult> ProcessAsync(
        string orderId,
        CancellationToken cancellationToken);
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
internal partial class DurableJsonContext : JsonSerializerContext;
```

Register the serializer roots required by your configured serializer, including the outer input/output
envelopes and every payload, result, or state type used by steps, callbacks, invokes,
child workflows, waits, maps, and parallel branches; `ProcessOrderStepResult` is one representative
step root. Generator cannot discover types hidden inside operation bodies or referenced libraries.
Same registered `ILambdaSerializer` handles MinimalLambda outer envelopes and AWS inner values.
Step bodies can be retried, so keep external side effects idempotent; checkpointed results do not
provide exactly-once execution.

## Handler, cancellation, and context contract

Durable handlers return `Task` or `Task<TOutput>`. `[FromEvent] TInput` and AWS `IDurableContext`
are optional; a handler may use either, both, or neither. Each can occur at most once. Parameter order is unrestricted. Other
parameters are resolved using normal handler binding rules. `ref`, `in`, and `out` parameters are
unsupported; handler parameter and output types must be accessible from generated code and closed over
all type parameters. The generator does not validate serializer roots or impose a durable-specific
payload-shape policy.

A handler that explicitly declares `CancellationToken` receives
`ILambdaInvocationContext.CancellationToken`. It represents physical Lambda-invocation cancellation;
own resulting failure/retry consequences and do not treat it as durable-operation cancellation. AWS
operation callbacks already receive SDK-linked cancellation tokens.

Inject `ILambdaInvocationContext` as above, or recover exact physical invocation context carried by
AWS durable context:

```csharp
using MinimalLambda.DurableExecution;

ILambdaInvocationContext invocation = durable.GetInvocationContext();
```

DI scope and invocation context belong to one physical Lambda invocation. Replay creates another
physical invocation and scope; never keep scoped state as logical workflow state.

## Replay, middleware, and ownership

MinimalLambda directly serializes hidden outer envelopes through its configured Lambda serializer;
durable envelopes are not available through `IEventFeature` or `IResponseFeature`. It also owns
raw-stream hosting, middleware, physical invocation context and DI scope. AWS runtime owns workflow
payload handling, checkpoints, replay, suspension, waits, `IDurableContext`, and durable status/result
mapping.

Middleware wraps each physical Lambda invocation, so it runs again during replay. Durable middleware
should call and await `next` once, preserve exceptions, and avoid ordinary-response short circuits.
MinimalLambda does not enforce those rules: skipped or repeated `next` and swallowed failures can
change AWS-visible behavior. Replay-safe observation, logging, metrics, and invocation-scoped behavior
fit; response caches, response fabrication, and exception-to-response translation do not work unchanged.

`MapDurableHandler` requires MinimalLambda compile-time interception. If source generation does not
replace mapping call, runtime fallback throws `InvalidOperationException` instead of running handler.

## Deployment

`MapDurableHandler` supplies host integration only; deployment must configure Lambda Durable Execution,
its IAM policy, and a qualified function target. Follow [sample package recipe](https://github.com/LayeredCraft/minimal-lambda/tree/main/examples/MinimalLambda.Example.DurableExecution#package-for-deployment) to produce a Lambda ZIP and deploy that ZIP with Amazon.Lambda.Tools durable defaults. For a custom role, grant equivalent durable execution permissions plus application-specific permissions. Versions, aliases, and `$LATEST` are supported qualified targets; prefer immutable versions or aliases for stable routing.

No managed-service deployment was run for this package. Consult current [AWS Durable Execution deployment documentation](https://docs.aws.amazon.com/lambda/latest/dg/durable-getting-started.html) before production use.

## Testing

Use MinimalLambda host/integration tests for generated adapter, middleware, DI, serializer identity,
and outer stream roundtrip. Use `Amazon.Lambda.DurableExecution.Testing` for
workflow operations, suspension, waits, and replay. Local tests do not prove IAM, deployment,
managed-runtime behavior, or cloud service integration.

## Raw-envelope and custom-client escape hatch

Use low-level `MapHandler` when raw envelope access, custom `IAmazonLambda`, protocol diagnostics, or
new AWS overloads are required. Register custom client before `builder.Build()`:

```csharp
using Amazon.Lambda;
using Amazon.Lambda.DurableExecution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalLambda;
using MinimalLambda.Builder;

var builder = LambdaApplication.CreateBuilder();
builder.Services.AddLambdaSerializerWithContext<DurableJsonContext>();
builder.Services.AddSingleton<IAmazonLambda, AmazonLambdaClient>();

await using var lambda = builder.Build();
lambda.MapHandler(
    ([FromEvent] DurableExecutionInvocationInput envelope,
        ILambdaInvocationContext invocation,
        [FromServices] IAmazonLambda client) =>
        DurableFunction.WrapAsync<OrderRequest, OrderResult>(
            LowLevelWorkflowAsync,
            envelope,
            invocation,
            client));

await lambda.RunAsync();

static Task<OrderResult> LowLevelWorkflowAsync(
    OrderRequest request,
    IDurableContext durable) =>
    Task.FromResult(
        new OrderResult(
            $"Order {request.OrderId} processed",
            durable.ExecutionContext.DurableExecutionArn,
            durable.LambdaContext.AwsRequestId));
```

Replace high-level mapping; do not register both paths. Low-level workflow has AWS signature
`Func<OrderRequest, IDurableContext, Task<OrderResult>>`. Keep same explicit serializer roots.

## NativeAOT

Durable NativeAOT support remains experimental. Ordinary restore/build/pack does not compile native
code; validate with publish, for example
`dotnet publish -c Release -r linux-x64 -p:PublishAot=true`. Successful local publish still does not
prove cloud deployment or managed durable behavior.
