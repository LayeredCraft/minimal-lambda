# ADR-004: Durable handler adapter contract

## Status

- Accepted
- **Date:** 2026-08-03
- **Deciders:** MinimalLambda maintainers

## Context

`MapDurableHandler` adapts a MinimalLambda handler to the public AWS Durable Execution wrappers:

```csharp
Func<TInput, IDurableContext, Task>
Func<TInput, IDurableContext, Task<TOutput>>
```

The generated terminal handler owns outer `DurableExecutionInvocationInput` and
`DurableExecutionInvocationOutput` transport. It resolves `ILambdaSerializer` from invocation
services for outer transport. `ILambdaInvocationContext` forwards runtime `ILambdaContext.Serializer` or falls back to invocation
services, which AWS `DurableFunction.WrapAsync` requires for inner durable payload transport.

## Decision

- `[FromEvent]` input and `IDurableContext` parameters are optional. If no event parameter is
  present, the generated adapter uses an ignored `object` payload; if no durable context is present,
  it is unused.
- Other value parameters follow normal MinimalLambda binding rules. `ref`, `in`, and `out` parameters
  are rejected because generated durable adapters cannot safely preserve their calling semantics.
- Handler parameter, input, output, and service types must be accessible from namespace-level generated
  code, cannot contain unbound type parameters, and cannot be pointer or ref-like types. Invalid signature
  components suppress adapter emission.
- A requested root `CancellationToken` binds to `ILambdaInvocationContext.CancellationToken`: it is a
  physical Lambda-invocation token, not an AWS durable-operation token. SDK operation callbacks retain
  their own cancellation tokens; their behavior is out of scope here.
- The generator does not inspect source-generated serializer contexts. Applications remain responsible
  for registering metadata needed by configured serializer.
- `LH0007` is emitted for unsupported durable signature components: return type must be `Task` or
  `Task<TOutput>`; parameters must be values; emitted types must be accessible, closed, and valid generic
  type arguments.

## Consequences

Handlers can be minimal (`Task Handle()`) or opt into input, durable context, invocation context,
and DI as needed. More shapes are left to the compiler, runtime, and application serializer rather
than rejected by generator-specific policy.
