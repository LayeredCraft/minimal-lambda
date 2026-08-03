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

The generated terminal handler owns the outer `DurableExecutionInvocationInput` and
`DurableExecutionInvocationOutput` transport. It resolves `ILambdaSerializer` from the invocation
service provider rather than adding serializer state to `ILambdaInvocationContext`.

## Decision

- `[FromEvent]` input and `IDurableContext` parameters are optional. If no event parameter is
  present, the generated adapter uses an ignored `object` payload; if no durable context is present,
  it is unused.
- Other parameters follow normal MinimalLambda binding rules. The adapter does not impose a
  durable-specific payload-shape or serializer-root policy.
- The generator does not inspect source-generated serializer contexts. Applications remain
  responsible for registering the metadata needed by their configured serializer.
- The only durable-specific signature diagnostic is `LH0007`, emitted when a handler does not return
  `Task` or `Task<TOutput>`. That is required by the AWS `DurableFunction.WrapAsync` overloads.

## Consequences

Handlers can be minimal (`Task Handle()`) or opt into input, durable context, invocation context,
and DI as needed. More shapes are left to the compiler, runtime, and application serializer rather
than rejected by generator-specific policy.
