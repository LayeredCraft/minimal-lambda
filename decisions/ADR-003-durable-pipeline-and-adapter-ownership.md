# ADR-003: Durable pipeline and adapter ownership

## Status

- Accepted
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
- Preserve the existing middleware and feature pipeline.
- Generate only code requiring compile-time handler types.
- Avoid reflection, duplicate serialization, and a second pipeline.
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
- Durable terminal required/completed markers.
- Handler-shape and serializer-metadata diagnostics.

### MinimalLambda runtime

- Raw-stream bootstrap and existing middleware pipeline.
- Invocation context and DI scope.
- Outer durable input deserialization and output serialization through existing features.
- Exposure of the configured `ILambdaSerializer` through
  `ILambdaInvocationContext.Serializer`.
- Validation that the durable terminal completed.

### AWS runtime

- Inner workflow payload extraction and serialization.
- Checkpoint serialization, replay, and suspension.
- `IDurableContext` construction.
- Durable status and result mapping.

The same serializer instance handles outer envelopes in MinimalLambda and inner values through AWS.
MinimalLambda does not create a durable envelope abstraction or parse the inner payload.

### Middleware

Middleware wraps one physical Lambda invocation and runs again on replay. It can inspect the outer
input and output through existing features. Typed workflow input remains a handler concern.

Middleware must call the durable terminal. If the pipeline completes without the generated terminal
completing, MinimalLambda throws an invocation error instead of returning an empty or fabricated
durable response.

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

## Consequences

### Positive

- Existing middleware, features, and DI remain reusable.
- Inner serialization remains entirely AWS-owned.
- Generated code stays small and AOT friendly.
- Manual AWS integration remains available.

### Negative / trade-offs

- Middleware cannot inspect typed workflow input before the terminal runs.
- Middleware executes on every physical replay.
- Core runtime needs serializer exposure and terminal-completion validation.

## References

- [`ADR-001: Durable handler integration model`](./ADR-001-durable-handler-integration-model.md)
- [`ADR-002: Durable package and source-generation ownership`](./ADR-002-durable-package-and-source-generation-ownership.md)
- Durable research context: `.agents/docs/durable-execution-context.md`
