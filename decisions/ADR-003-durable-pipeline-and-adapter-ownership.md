# ADR-003: Durable pipeline and adapter ownership

## Status

- Accepted
- **Date:** 2026-07-29
- **Deciders:** MinimalLambda maintainers
- **Supersedes:** none
- **Amended:** 2026-08-03 — terminal lifecycle tracking removed; see middleware contract below.

______________________________________________________________________

## Context

AWS durable functions have two handler shapes:

```text
Lambda:   DurableExecutionInvocationInput -> DurableExecutionInvocationOutput
Workflow: TInput -> TOutput
```

`DurableFunction.WrapAsync` connects them. MinimalLambda must decide where this wrapper runs and
which adapter work is generated.

AWS TypeScript, Python, and Java durable SDKs hide the service envelope: workflows receive typed
input and durable context while the SDK owns checkpoint transport. AWS .NET exposes the outer types
only at its required Lambda adapter boundary.

## Decision Drivers

- Let AWS own durable execution and inner serialization.
- Preserve the existing middleware and feature pipeline where durable replay semantics permit it.
- Generate only code requiring compile-time handler types.
- Avoid reflection, duplicate serialization, and a second pipeline.
- Prevent invocation timeout cancellation from becoming an accidental terminal workflow failure.
- Keep checkpoint tokens and replay history out of the normal workflow API.
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
The outer AWS envelope exists only inside generated transport plumbing; the mapped workflow receives
typed input and `IDurableContext`.

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
- Direct outer durable envelope stream serialization.
- Typed or void `WrapAsync` overload selection.
- Binding of workflow input, `IDurableContext`, MinimalLambda context, and DI services.
- Handler-shape diagnostics.
- Serializer-metadata diagnostics for types statically inferable from the handler signature.
- Binding of an optional root `CancellationToken` to physical invocation cancellation.

### MinimalLambda runtime

- Raw-stream bootstrap and existing middleware pipeline.
- Invocation context and DI scope.
- Outer durable input deserialization and output serialization through invocation streams.
- Exposure of the configured `ILambdaSerializer` through
  `ILambdaInvocationContext.Serializer`.

### AWS runtime

- Inner workflow payload extraction and serialization.
- Checkpoint serialization, replay, and suspension.
- `IDurableContext` construction.
- Durable status and result mapping.

The same serializer instance handles outer envelopes in MinimalLambda and inner values through AWS.
Durable envelopes are not exposed through `IEventFeature` or `IResponseFeature`; MinimalLambda does
not create a public durable envelope abstraction or parse the inner payload.

For `MapDurableHandler`, `[FromEvent] TInput` always means workflow input. The outer
`DurableExecutionInvocationInput`, checkpoint token, and replay history are not bindable handler
parameters. Execution identity and Lambda metadata remain available through `IDurableContext`:

```csharp
var executionArn = durable.ExecutionContext.DurableExecutionArn;
var requestId = durable.LambdaContext.AwsRequestId;
```

Serializer diagnostics are necessarily limited. The generator can infer the durable envelope,
workflow input, and workflow output types, but cannot reliably discover serialization types hidden
inside workflow methods or referenced libraries. Users remain responsible for registering metadata
for step results, callback results, invoke and child-workflow payloads, wait-condition state, and
map or parallel results.

### Cancellation

A durable handler may declare one root `CancellationToken`. MinimalLambda binds it to physical
invocation cancellation through `ILambdaInvocationContext.CancellationToken`; it is not a durable
workflow-operation token. Near-timeout cancellation can fault the root workflow task, which AWS maps
to a terminal `FAILED` durable result instead of allowing the physical invocation to time out and retry.

Use SDK-provided callback tokens for durable steps. A handler that uses the root token owns the
resulting durable failure and retry semantics.

### Middleware

Middleware wraps one physical Lambda invocation and runs again on replay. Neither raw checkpoint
transport nor typed workflow input is part of the durable middleware contract. Existing outer
features are framework transport plumbing, not a supported application abstraction.

If a concrete middleware use case emerges, MinimalLambda may expose read-only semantic metadata,
such as execution ARN. It will not expose checkpoint token or replay history through that API.

Durable middleware should call and await `next` once, preserve exceptions, and avoid response
short-circuits or fabrication. This is guidance, not framework enforcement: skipped `next` can return
an empty response, repeated `next` reruns the adapter, and swallowed failures can change AWS-visible
behavior. This tradeoff removes durable-specific state from the shared host pipeline.

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

This supports custom AWS clients, protocol diagnostics, and new AWS overloads without expanding the
normal workflow API. It is the only supported path for raw envelope access.

## Rationale

The generated adapter is the narrow point where all required static type information is available.
Type-independent pipeline behavior stays in MinimalLambda runtime; durable protocol behavior stays in AWS.

## Validation requirements

Implementation must cover:

- Successful, failed, and suspended AWS durable outputs.
- Middleware execution before and after a suspended physical invocation.
- Serializer identity across outer MinimalLambda and inner AWS serialization.
- Generator binding of an optional durable root `CancellationToken` to physical invocation cancellation.
- Diagnostics for inferable serializer roots without claiming coverage of nested workflow types.

Full host-plus-replay testing may require separate MinimalLambda host tests and AWS durable SDK tests
because the AWS in-memory durable service-client overload is not public.

## Consequences

### Positive

- Existing features, DI, and replay-safe middleware remain reusable.
- Inner serialization remains entirely AWS-owned.
- Normal workflows match the typed-input-and-context model used by other AWS language SDKs.
- Checkpoint tokens and replay history remain transport details.
- Generated code stays small and AOT friendly.
- Manual AWS integration remains available.

### Negative / trade-offs

- Middleware cannot inspect typed workflow input before the terminal runs.
- Raw envelope access requires the explicit low-level mapping path.
- Middleware executes on every physical replay.
- Middleware that short-circuits, repeats `next`, or translates exceptions into ordinary responses can change durable behavior and is unsupported guidance rather than a host-enforced error.
- Root `CancellationToken` represents physical invocation cancellation, not durable operation cancellation.
- Serializer diagnostics cannot cover types hidden inside workflow implementations or libraries.
- Core runtime needs serializer exposure, but no durable-specific lifecycle state.

## References

- [`ADR-001: Durable handler integration model`](./ADR-001-durable-handler-integration-model.md)
- [`ADR-002: Durable package and source-generation ownership`](./ADR-002-durable-package-and-source-generation-ownership.md)
- [Durable dependency and support matrix](./durable-dependency-support-matrix.md)
- [AWS durable execution key concepts](https://docs.aws.amazon.com/durable-execution/getting-started/key-concepts/)
