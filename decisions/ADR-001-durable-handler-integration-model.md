# ADR-001: Durable handler integration model

## Status

- Accepted
- **Date:** 2026-07-29
- **Deciders:** MinimalLambda maintainers
- **Supersedes:** none

______________________________________________________________________

## Context

AWS durable functions receive a service envelope, then extract the user's workflow input from that
envelope. MinimalLambda needs a durable API that keeps this plumbing hidden while preserving its
minimal handler and dependency-injection model.

AWS already owns replay, checkpoints, and durable operations through
`Amazon.Lambda.DurableExecution`. MinimalLambda should integrate with that SDK rather than create a
second durable runtime.

## Decision Drivers

- Keep durable registration explicit and minimal.
- Reuse AWS `IDurableContext` and execution semantics.
- Preserve MinimalLambda dependency injection and invocation context access.
- Keep the outer AWS service envelope out of normal workflow code.
- Remain source-generated and NativeAOT friendly.

## Options Considered

### Option A: Dedicated `MapDurableHandler`

```csharp
lambda.MapDurableHandler(async (
    [FromEvent] OrderRequest request,
    IDurableContext durable,
    IOrderService orders) =>
{
    return await durable.StepAsync(
        (_, ct) => orders.ProcessAsync(request, ct));
});
```

**Pros:** Explicit, minimal, and consistent with `MapHandler`.

**Cons:** Requires durable-specific source generation.

### Option B: Attribute-sensitive `MapHandler`

```csharp
[DurableHandler]
static Task<OrderResult> ProcessAsync(
    [FromEvent] OrderRequest request,
    IDurableContext durable) => ...;

lambda.MapHandler(ProcessAsync);
```

**Pros:** Keeps one mapping method.

**Cons:** Hides the different durable wire protocol behind an attribute.

### Option C: Explicit AWS wrapper

```csharp
lambda.MapHandler((
    DurableExecutionInvocationInput envelope,
    ILambdaInvocationContext context) =>
    DurableFunction.WrapAsync<OrderRequest, OrderResult>(
        Workflow,
        envelope,
        context));
```

**Pros:** Full AWS control with minimal framework behavior.

**Cons:** Exposes envelope plumbing to every user.

### Option D: MinimalLambda durable context wrapper

```csharp
static Task<OrderResult> Workflow(
    OrderRequest request,
    IMinimalDurableContext context) => ...;
```

**Pros:** One MinimalLambda-owned context.

**Cons:** Duplicates and must track the evolving AWS `IDurableContext` API.

## Decision

We will use **Option A: dedicated `MapDurableHandler` backed by
`Amazon.Lambda.DurableExecution`**.

- AWS `IDurableContext` is injected unchanged.
- `ILambdaInvocationContext` can also be injected when needed.
- MinimalLambda passes its invocation context to `DurableFunction.WrapAsync`, making it available
  through `IDurableContext.LambdaContext`.
- A typed extension provides convenient access when direct injection is not practical:

```csharp
var invocation = durable.GetInvocationContext();
```

- Option C remains available as the advanced escape hatch.

## Rationale

`MapDurableHandler` makes durable behavior visible while keeping the normal MinimalLambda authoring
model. Reusing AWS `IDurableContext` avoids API duplication and keeps AWS responsible for replay and
checkpoint semantics.

## Consequences

### Positive

- Familiar MinimalLambda API and DI.
- AWS durable behavior remains authoritative.
- Users can access both durable and MinimalLambda contexts.
- Outer service-envelope plumbing stays generated.

### Negative / trade-offs

- Requires durable-specific generator support.
- Scoped services are recreated for every replay invocation.
- MinimalLambda must keep serializer/context integration compatible with the AWS SDK.

## References

- [`ADR-002: Durable package and source-generation ownership`](./ADR-002-durable-package-and-source-generation-ownership.md)
- Durable research context: `.agents/docs/durable-execution-context.md`
