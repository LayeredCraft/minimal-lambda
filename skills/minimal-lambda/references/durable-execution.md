# Durable Execution

Read this before applying ordinary handler, cancellation, middleware, serializer, or testing advice to
`MapDurableHandler`.

## Packages and targets

Reference all three packages directly:

```bash
dotnet add package MinimalLambda
dotnet add package MinimalLambda.DurableExecution
dotnet add package Amazon.Lambda.DurableExecution --version 1.0.0
```

Direct `MinimalLambda` reference activates MinimalLambda source generator. Direct AWS package
reference activates DE001-DE004 analyzers; runtime dependency otherwise arrives transitively.
Initial supported target is .NET 10. Treat NativeAOT durable deployment as experimental
until project documentation records cloud evidence.

## Handler contract

Durable handler must have:

- exactly one `[FromEvent] TInput` workflow input;
- exactly one exact AWS `IDurableContext`;
- exact `Task` or `Task<TOutput>` return;
- optional `ILambdaContext`, `ILambdaInvocationContext`, ordinary DI, keyed DI, or optional DI
  parameters.

Parameter order does not matter. Unannotated parameters are DI; input is never inferred.

```csharp
lambda.MapDurableHandler(async (
    [FromEvent] OrderRequest request,
    IDurableContext durable,
    IOrderService orders) =>
{
    return await durable.StepAsync(
        (_, cancellationToken) => orders.ProcessAsync(request, cancellationToken));
});
```

Do not use synchronous, `ValueTask`, custom-awaitable, raw `Stream`, or outer durable envelope forms
in high-level durable handler. Use ordinary `MapHandler` plus `DurableFunction.WrapAsync` when raw
envelope or explicit-client control is required.

## Cancellation

Do not declare `CancellationToken` on durable root handler. Near-timeout cancellation can fault root
workflow and become terminal `FAILED` result instead of allowing physical invocation retry. AWS operation
callbacks already receive SDK-linked cancellation tokens, and `WrapAsync` exposes no lifecycle-token hook.

Use cancellation token supplied by durable operation callback (`StepAsync`, callback, map/parallel,
child workflow, and related APIs). Advanced code can read
`ILambdaInvocationContext.CancellationToken`, but then owns durable failure/retry consequences.

## Context and DI

AWS durable context carries MinimalLambda invocation context as `LambdaContext`.

```csharp
ILambdaInvocationContext invocation = durable.GetInvocationContext();
```

Returned object is exact MinimalLambda context. Prefer injecting `ILambdaInvocationContext` directly
when handler needs it. Keep context objects at Lambda edge; pass domain values into services.

DI scope is per physical invocation and recreated on replay. Do not assume scoped service state
survives logical execution.

## Replay and middleware

Middleware wraps each physical Lambda invocation and runs again on replay. It cannot depend on raw
checkpoint transport or typed workflow input before terminal runs.

Durable-compatible middleware:

- calls `next` exactly once and awaits it;
- does not short-circuit with ordinary response;
- does not swallow terminal exceptions;
- limits work to replay-safe observation, logging, metrics, or invocation-scoped behavior.

Do not reuse response caching, ordinary response fabrication, or exception-to-response translation
middleware unchanged. AWS owns replay, checkpoints, suspension, waits, and durable status mapping.

## Serialization and AOT

Same registered `ILambdaSerializer` handles MinimalLambda outer envelopes and AWS inner durable
values. For source-generated JSON, explicitly declare at least:

- `DurableExecutionInvocationInput`;
- `DurableExecutionInvocationOutput`;
- workflow `TInput`;
- workflow `TOutput` for `Task<TOutput>`.

Also explicitly register payload/result/state types used inside steps, callbacks, invokes, child
workflows, waits, maps, and parallel branches. Generator cannot discover types hidden in operation
bodies or referenced libraries.

## Testing split

Use MinimalLambda host/integration tests to prove generated adapter, middleware, DI, exact serializer
identity, terminal lifecycle, and outer stream roundtrip. Use
`Amazon.Lambda.DurableExecution.Testing` to prove workflow operations, suspension, and replay. Local
runner does not prove IAM, deployment, managed runtime, or cloud service behavior.

## Low-level escape hatch

```csharp
lambda.MapHandler(
    ([FromEvent] DurableExecutionInvocationInput envelope, ILambdaInvocationContext invocation) =>
        DurableFunction.WrapAsync<OrderRequest, OrderResult>(Workflow, envelope, invocation));
```

Use this only for raw envelope, explicit AWS client, protocol diagnostics, or SDK capabilities not yet
represented by high-level adapter.
