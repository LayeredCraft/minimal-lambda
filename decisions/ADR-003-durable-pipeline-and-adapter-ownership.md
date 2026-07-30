# ADR-003: Durable pipeline and adapter ownership

## Status

- Proposed
- **Date:** 2026-07-29
- **Deciders:** MinimalLambda maintainers
- **Supersedes:** none

______________________________________________________________________

## Context

AWS durable functions have two handler shapes:

```text
Lambda:   DurableExecutionInvocationInput -> DurableExecutionInvocationOutput
Workflow: TInput -> TOutput
```

`DurableFunction.WrapAsync` connects them. MinimalLambda must decide where this wrapper runs and
which adapter work is generated.

## Decision Drivers

- Let AWS own durable execution and inner serialization.
- Preserve the existing middleware and feature pipeline where durable replay semantics permit it.
- Generate only code requiring compile-time handler types.
- Avoid reflection, duplicate serialization, and a second pipeline.
- Prevent invocation timeout cancellation from becoming an accidental terminal workflow failure.
- Ensure the durable terminal runs exactly once per physical invocation.
- Preserve a manual escape hatch.

## Options Considered

### Option A: Generated terminal adapter

```text
middleware -> generated terminal -> WrapAsync -> workflow
```

**Pros:** Reuses the existing pipeline and remains AOT friendly.

**Cons:** Middleware sees the physical invocation, not typed workflow input.

### Option B: Durable middleware adapter

```text
WrapAsync -> middleware -> workflow
```

**Pros:** Middleware can access typed workflow input.

**Cons:** AWS can abandon the workflow task on suspension, so code after `next` may never complete.

### Option C: Separate typed RuntimeSupport pipeline

```text
typed HandlerWrapper -> durable pipeline -> WrapAsync
```

**Pros:** RuntimeSupport owns outer serialization.

**Cons:** Requires a second pipeline and duplicates existing feature and response plumbing.

## Decision

We will use **Option A: a generated terminal adapter inside the existing MinimalLambda pipeline**.

Conceptually, the generator emits:

```csharp
async Task InvokeDurable(ILambdaInvocationContext invocation)
{
    var envelope =
        invocation.GetRequiredEvent<DurableExecutionInvocationInput>();

    var output = await DurableFunction.WrapAsync<OrderRequest, OrderResult>(
        async (input, durable) =>
        {
            var orders = invocation.ServiceProvider
                .GetRequiredService<IOrderService>();

            return await userHandler(input, durable, orders);
        },
        envelope,
        invocation);

    invocation.Features
        .GetRequired<IResponseFeature<DurableExecutionInvocationOutput>>()
        .SetResponse(output);
}
```

### Generated

- Exact user-delegate cast.
- Outer durable input and output feature registration.
- Typed or void `WrapAsync` overload selection.
- Binding of workflow input, `IDurableContext`, MinimalLambda context, and DI services.
- Declaration that the mapped handler requires a durable terminal.
- Handler-shape diagnostics.
- Serializer-metadata diagnostics for types statically inferable from the handler signature.
- A diagnostic rejecting automatic `CancellationToken` binding in durable handler signatures.

### MinimalLambda runtime

- Raw-stream bootstrap and existing middleware pipeline.
- Invocation context and DI scope.
- Outer durable input deserialization and output serialization through existing features.
- Exposure of the configured `ILambdaSerializer` through
  `ILambdaInvocationContext.Serializer`.
- Per-invocation durable-terminal lifecycle tracking and validation.

The terminal lifecycle is atomic:

```text
NotStarted -> Running -> Completed
```

The runtime rejects a second terminal invocation, whether sequential or concurrent. If middleware
returns while the terminal is `NotStarted` or `Running`, the runtime throws an invocation error.
This prevents empty responses, swallowed terminal failures, duplicate workflow execution, and
conflicting checkpoint use.

### AWS runtime

- Inner workflow payload extraction and serialization.
- Checkpoint serialization, replay, and suspension.
- `IDurableContext` construction.
- Durable status and result mapping.

The same serializer instance handles outer envelopes in MinimalLambda and inner values through AWS.
MinimalLambda does not create a durable envelope abstraction or parse the inner payload.

Serializer diagnostics are necessarily limited. The generator can infer the durable envelope,
workflow input, and workflow output types, but cannot reliably discover serialization types hidden
inside workflow methods or referenced libraries. Users remain responsible for registering metadata
for step results, callback results, invoke and child-workflow payloads, wait-condition state, and
map or parallel results.

### Cancellation

MinimalLambda does not automatically bind its invocation `CancellationToken` into durable handler
signatures. Near-timeout cancellation can fault the root workflow task, which AWS maps to a terminal
`FAILED` durable result instead of allowing the physical invocation to time out and retry.

A durable handler declaring a `CancellationToken` receives a generator diagnostic. Advanced users
can still access `ILambdaInvocationContext.CancellationToken`, but then own the resulting durable
failure and retry semantics.

### Middleware

Middleware wraps one physical Lambda invocation and runs again on replay. It can inspect the outer
input and output through existing features. Typed workflow input remains a handler concern.

Durable-compatible middleware must call `next` exactly once. It must not short-circuit with an
ordinary typed response, fabricate a durable response, or swallow an exception from the terminal.
If the pipeline completes without the generated terminal completing, MinimalLambda throws an
invocation error instead of returning an empty or fabricated durable response. This preserves host
retry behavior for transient checkpoint and state-hydration failures.

Existing middleware that only observes, logs, measures, or adds invocation-scoped behavior remains
reusable. Response caching, ordinary typed-response short-circuiting, and exception-to-response
translation middleware is not reusable unchanged with `MapDurableHandler`.

### Escape hatch

Advanced users can map the outer AWS types directly:

```csharp
lambda.MapHandler((
    [FromEvent] DurableExecutionInvocationInput envelope,
    ILambdaInvocationContext invocation,
    IAmazonLambda client) =>
    DurableFunction.WrapAsync<OrderRequest, OrderResult>(
        Workflow,
        envelope,
        invocation,
        client));
```

This supports custom AWS clients and new AWS overloads without expanding generated policy.

## Rationale

The generated terminal is the narrow point where all required static type information is available.
Type-independent pipeline behavior stays in MinimalLambda runtime; durable behavior stays in AWS.
Atomic lifecycle enforcement belongs to the runtime because it does not depend on handler types.

## Validation requirements

Implementation must cover:

- Missing terminal invocation.
- A swallowed terminal exception.
- Sequential and concurrent double invocation of `next`.
- Successful, failed, and suspended AWS durable outputs.
- Middleware execution before and after a suspended physical invocation.
- Serializer identity across outer MinimalLambda and inner AWS serialization.
- Generator rejection of durable `CancellationToken` parameters.
- Diagnostics for inferable serializer roots without claiming coverage of nested workflow types.

Full host-plus-replay testing may require separate MinimalLambda host tests and AWS durable SDK tests
because the AWS in-memory durable service-client overload is not public.

## Consequences

### Positive

- Existing features, DI, and replay-safe middleware remain reusable.
- Inner serialization remains entirely AWS-owned.
- Generated code stays small and AOT friendly.
- Manual AWS integration remains available.

### Negative / trade-offs

- Middleware cannot inspect typed workflow input before the terminal runs.
- Middleware executes on every physical replay.
- Middleware that short-circuits or translates exceptions into ordinary responses is incompatible.
- Durable handlers cannot receive an automatically bound invocation `CancellationToken`.
- Serializer diagnostics cannot cover types hidden inside workflow implementations or libraries.
- Core runtime needs serializer exposure and atomic terminal-lifecycle validation.

## References

- [`ADR-001: Durable handler integration model`](./ADR-001-durable-handler-integration-model.md)
- [`ADR-002: Durable package and source-generation ownership`](./ADR-002-durable-package-and-source-generation-ownership.md)
- Durable research context: `.agents/docs/durable-execution-context.md`
