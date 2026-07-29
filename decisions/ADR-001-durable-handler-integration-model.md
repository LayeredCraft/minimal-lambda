---
adr: 1
status: accepted
date: '2026-07-29'
deciders:
  - MinimalLambda maintainers
supersedes: []
superseded_by:
---

# ADR-001: Durable handler integration model

## Context

AWS Lambda Durable Functions use a service-backed replay model. The Lambda service invokes a
function with `DurableExecutionInvocationInput`; the .NET durable SDK extracts the user's workflow
input from the envelope, replays prior operations, checkpoints new operations, and returns
`DurableExecutionInvocationOutput` with `SUCCEEDED`, `FAILED`, or `PENDING` status.

MinimalLambda's normal handler API exposes user event types directly:

```csharp
lambda.MapHandler(([FromEvent] OrderRequest request, IOrders orders) => ...);
```

That model cannot be reused unchanged for durable functions. The raw Lambda event is the durable
service envelope, while the user's `OrderRequest` exists as serialized JSON inside the envelope's
`EXECUTION` operation. Durable workflows also require AWS `IDurableContext` for checkpointed steps,
waits, callbacks, invokes, parallel work, and replay-aware logging.

MinimalLambda needs a public API that:

- preserves its explicit minimal-API registration style;
- hides the durable service envelope for normal workflows;
- supports normal invocation-scoped dependency injection;
- exposes AWS durable behavior without reimplementing it;
- allows access to MinimalLambda invocation features when needed;
- remains source-generated and NativeAOT/trimming friendly;
- keeps an explicit lower-level escape hatch for advanced scenarios.

The AWS durable SDK creates its own internal `IDurableContext` implementation. It accepts an
`ILambdaContext` when `DurableFunction.WrapAsync` starts and exposes that same object through
`IDurableContext.LambdaContext`. MinimalLambda's `ILambdaInvocationContext` already extends the AWS
Lambda context contract and can therefore serve as the underlying context when it also exposes the
registered serializer.

## Decision drivers

- Follow existing `MapHandler`-style MinimalLambda ergonomics.
- Make durable intent visible at registration.
- Keep AWS responsible for replay, checkpoints, operation semantics, and service compatibility.
- Avoid duplicating or wrapping the growing `IDurableContext` API without a concrete need.
- Preserve invocation-scoped DI and keyed DI.
- Give workflows typed access to MinimalLambda invocation facilities.
- Keep outer durable wire types out of the normal user workflow signature.
- Remain reflection-free and compatible with source-generated JSON serialization.
- Preserve a lower-level path for custom AWS client or unsupported integration scenarios.

## Options considered

### Option A: Dedicated `MapDurableHandler` backed by AWS durable execution

Add a distinct source-generated registration API. Inject the nested workflow input,
`IDurableContext`, optional `ILambdaInvocationContext`, and normal DI services into the user's
workflow delegate. Use `DurableFunction.WrapAsync` as the engine.

**Pros:**

- Closely matches existing MinimalLambda public API.
- Makes durable behavior explicit.
- Hides outer service-envelope plumbing.
- Reuses AWS behavior and analyzers.
- Supports compile-time binding and diagnostics.
- Keeps normal DI ergonomics.
- Supports typed and void workflows.

**Cons:**

- Requires durable-specific source-generation support.
- Introduces a second handler registration method.
- Requires compatible MinimalLambda and durable integration package versions.

### Option B: Attribute-sensitive `MapHandler`

Continue using `MapHandler`, but infer durable behavior from an attribute on the mapped method.

**Pros:**

- Keeps one registration method.
- Can colocate durable metadata with the workflow method.
- Supports compile-time generation.

**Cons:**

- Makes `MapHandler` represent two different wire protocols.
- Durable intent is less visible at the composition root.
- Risks conflating workflow configuration with infrastructure/deployment configuration.
- Adds attribute semantics to an API otherwise based on explicit builder composition.

### Option C: Explicit outer-envelope wrapper only

Require users to map `DurableExecutionInvocationInput` and call `DurableFunction.WrapAsync`
manually.

**Pros:**

- Minimal framework implementation.
- Full AWS API visibility and control.
- Easy to use a custom `IAmazonLambda` client.

**Cons:**

- Exposes service-envelope plumbing in every function.
- Loses MinimalLambda's primary ergonomic value.
- Makes DI closure construction and serializer correctness user responsibilities.
- Provides weaker compile-time diagnostics.

### Option D: MinimalLambda-owned durable context abstraction

Create a new MinimalLambda durable interface or wrapper that forwards to AWS `IDurableContext` and
adds MinimalLambda invocation facilities.

**Pros:**

- Gives MinimalLambda complete ownership of user-facing durable API.
- Could combine durable and invocation facilities in one parameter.

**Cons:**

- Duplicates a large and evolving AWS API.
- Creates ongoing compatibility and forwarding work.
- Risks incompatibility with AWS analyzers and documentation.
- Obscures which behavior belongs to AWS versus MinimalLambda.
- Adds no required capability because AWS context can retain the MinimalLambda invocation context.

## Decision

We will use **Option A: a dedicated `MapDurableHandler` registration backed by
`Amazon.Lambda.DurableExecution`**.

The public workflow model will follow these rules:

- One workflow input is bound from `[FromEvent] TInput`, but sourced from the nested durable
  `EXECUTION.InputPayload`, not directly from the raw Lambda event stream.
- AWS `IDurableContext` is injected unchanged.
- `ILambdaInvocationContext` may be injected separately when MinimalLambda invocation facilities
  are needed.
- Normal and keyed services are resolved from the current Lambda invocation scope.
- Typed and void asynchronous workflows are supported.
- MinimalLambda passes its current `ILambdaInvocationContext` to `DurableFunction.WrapAsync`, making
  it the object exposed by `IDurableContext.LambdaContext`.
- `MinimalLambda.DurableExecution` provides a typed extension method for retrieving
  `ILambdaInvocationContext` from `IDurableContext` when direct parameter injection is inconvenient.
- The explicit outer-envelope `MapHandler` plus `DurableFunction.WrapAsync` path remains available
  as an advanced escape hatch.

MinimalLambda will not reimplement AWS replay, checkpoint, operation, or durable service behavior.
It will own handler registration, generated binding, serializer/context integration, DI,
diagnostics, templates, and documentation around that engine.

## Rationale

Option A best preserves MinimalLambda's defining API style while making the durable protocol boundary
clear. `MapDurableHandler` tells users and tooling that the raw invocation is not an ordinary user
event and that AWS durable replay semantics apply.

Keeping AWS `IDurableContext` unchanged avoids a second durable API that would drift from AWS. The
MinimalLambda invocation context can still be injected directly, and passing it into
`DurableFunction.WrapAsync` makes it naturally available through `IDurableContext.LambdaContext`.
This gives users both context models without wrapper maintenance.

The explicit wrapper path remains important. It gives advanced users access to custom AWS clients
and new AWS capabilities before first-class MinimalLambda generation catches up, without making
that plumbing the default experience.

## Consequences

### Positive

- Durable workflows use familiar MinimalLambda registration and DI.
- Public durable intent is explicit.
- AWS remains authoritative for durable execution semantics.
- MinimalLambda avoids duplicating `IDurableContext`.
- Workflow code can access both durable and invocation contexts.
- Source generation can produce AOT-safe outer-envelope adapters.
- Advanced users retain direct access to AWS wrapper APIs.

### Negative / trade-offs

- `MapDurableHandler` requires dedicated source-generator modeling and diagnostics.
- Users must understand that scoped services are recreated on each replay invocation.
- Normal workflow input is not the raw Lambda event, requiring durable-specific generated binding.
- MinimalLambda must keep its context and serializer integration compatible with AWS durable SDK.

## Follow-up actions

- Define exact supported delegate return shapes.
- Choose final typed context extension method name.
- Add generated parameter diagnostics and snapshots.
- Expose the exact DI serializer through `ILambdaInvocationContext.Serializer`.
- Document replay-safe use of DI, logging, middleware, and cancellation.
- Add host adapter tests and AWS durable workflow tests.
- Validate NativeAOT and real AWS deployment paths before documenting support matrix.

## References

- [`ADR-002: Durable package and source-generation ownership`](./ADR-002-durable-package-and-source-generation-ownership.md)
- Durable research context: `.agents/docs/durable-execution-context.md`
- MinimalLambda handler target:
  `src/MinimalLambda/Builder/InterceptionTargets/MapHandlerLambdaApplicationExtensions.cs`
- MinimalLambda generated handler template:
  `src/MinimalLambda.SourceGenerators/Templates/MapHandler.scriban`
- AWS durable wrapper:
  `Libraries/src/Amazon.Lambda.DurableExecution/DurableFunction.cs` in `aws-lambda-dotnet`
- AWS durable context:
  `Libraries/src/Amazon.Lambda.DurableExecution/IDurableContext.cs` in `aws-lambda-dotnet`
