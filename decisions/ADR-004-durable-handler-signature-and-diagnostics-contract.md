# ADR-004: Durable handler signature and diagnostics contract

## Status

- Accepted
- **Date:** 2026-07-31
- **Deciders:** MinimalLambda maintainers
- **Supersedes:** none

______________________________________________________________________

## Context

`MapDurableHandler` must translate a MinimalLambda handler into one of the public
`Amazon.Lambda.DurableExecution` 1.0.0 workflow delegates:

```csharp
Func<TInput, IDurableContext, Task>
Func<TInput, IDurableContext, Task<TOutput>>
```

The generated outer handler consumes `DurableExecutionInvocationInput` and returns
`DurableExecutionInvocationOutput`. MinimalLambda must add invocation-context and DI bindings without
inventing AWS workflow forms, exposing transport, or emitting unstable diagnostics. Serializer
warnings report missing explicit durable-root declarations only for a statically proven Lambda
application and source-generated serializer context. Runtime metadata behavior is outside this
diagnostic contract.

## Decision Drivers

- Match released AWS `WrapAsync` overloads exactly.
- Preserve MinimalLambda context and DI binding.
- Keep durable transport out of high-level handlers.
- Avoid unsafe automatic cancellation.
- Emit actionable, deterministic diagnostics before generating invalid code.
- Prefer missed serializer warnings over false warnings.
- Remain trimming and NativeAOT friendly.

## Options Considered

### Option A: AWS workflow core with additive MinimalLambda bindings

Require one attributed input, one AWS durable context, and exact `Task` return forms. Capture
additional context and DI bindings in the generated workflow closure.

**Pros:** Matches AWS, preserves MinimalLambda ergonomics, and keeps generated code small.

**Cons:** Durable handlers are stricter than ordinary handlers.

### Option B: Mirror AWS Annotations exactly

Allow exactly two parameters: input followed by `IDurableContext`.

**Pros:** Smallest validation surface and direct AWS precedent.

**Cons:** Prevents context and DI bindings required by MinimalLambda.

### Option C: Reuse ordinary `MapHandler` flexibility

Allow inferred input, synchronous returns, `ValueTask`, custom awaitables, and automatic
`CancellationToken`.

**Pros:** Maximum consistency with ordinary handlers.

**Cons:** Adds conversion and cancellation semantics absent from AWS `WrapAsync`.

### Option D: Expose raw envelope or stream forms

Permit outer durable input/output or `Stream` in high-level signatures.

**Pros:** Maximum protocol control.

**Cons:** Defeats the dedicated API and duplicates the low-level `MapHandler` escape hatch.

### Option E: Emit serializer warnings without application tracing

Associate all durable handlers with any source-generated context found in the compilation.

**Pros:** Simple analysis.

**Cons:** Cross-contaminates builders and produces false warnings when registration order or receiver
identity is unknown.

## Decision

We will use **Option A: AWS workflow core with additive MinimalLambda bindings**.

### Canonical grammar

```text
MapDurableHandler(
  handler: ([FromEvent] TInput,
            IDurableContext,
            zero or more allowed context or DI parameters)
           -> Task | Task<TOutput>)
```

Parameter order is unrestricted. AWS owns the fixed two-argument workflow delegate. MinimalLambda
normalizes the required bindings into that delegate and captures the physical invocation context for
additional context and DI parameters.

`[FromEvent]` is canonical. Legacy obsolete `[Event]` counts identically for input cardinality and
binding. Its existing obsolescence warning remains unchanged.

Unannotated parameters remain DI parameters. Input is never inferred from position or type.

### Reserved-context classification

Exact `IDurableContext`, `ILambdaContext`, and `ILambdaInvocationContext` parameters are reserved
context types.

A reserved context binds as context only when it has none of `[FromEvent]`, `[Event]`,
`[FromServices]`, or `[FromKeyedServices]`. Any one or more of those attributes is one conflicting
binding and produces one `LH0009`. Such a parameter does not count as event input; if no other input
exists, `LH0007` is also emitted.

Every exact `IDurableContext` counts toward durable-context cardinality even when its binding
attribute conflicts. A conflicting exact `IDurableContext` can therefore receive `LH0009` while the
handler also receives `LH0008` for a duplicate count. Exact Lambda context parameters do not count
toward durable-context cardinality.

Classification order is:

1. identify top-level exact reserved-context types;
2. count every exact `IDurableContext` for `LH0008`;
3. diagnose conflicting binding on each reserved context with `LH0009`;
4. exclude conflicting reserved contexts from event-input candidates;
5. count remaining `[FromEvent]` and `[Event]` parameters for `LH0007`;
6. validate retained input and all other parameters.

### Supported signatures

| Shape                                                                                       | Rule                                         |
| ------------------------------------------------------------------------------------------- | -------------------------------------------- |
| One `[FromEvent] TInput` or `[Event] TInput`, one exact `IDurableContext`, returning `Task` | Uses `WrapAsync<TInput>`                     |
| Same required parameters returning `Task<TOutput>`                                          | Uses `WrapAsync<TInput, TOutput>`            |
| Required parameters in any order                                                            | Binding is source-based, not positional      |
| Unattributed exact `ILambdaContext` or `ILambdaInvocationContext`                           | Receives the current invocation object       |
| Both or repeated unattributed Lambda context bindings                                       | Every binding receives the same object       |
| Ordinary DI, `[FromServices]`, and `[FromKeyedServices]`                                    | Uses existing scoped/keyed resolution        |
| Optional DI parameters                                                                      | Preserves existing optional/default behavior |
| Nullable closed types and closed constructed generic input/output                           | Supported                                    |
| Lambdas, local functions, method groups, and supported delegate references                  | Uses existing discovery forms                |

Only closed effective signature types are supported. A constructed generic method is supported only
when substitution leaves no `ITypeParameterSymbol` anywhere in any parameter or output signature
type.

### Recursive signature-type validation

Every parameter type and `Task<TOutput>` output is walked recursively through:

- array element types, preserving rank;
- pointer pointed-at types;
- tuple element types;
- every type argument of a closed constructed generic;
- each containing named type and its type arguments;
- function-pointer parameter and return types.

Every recursively emitted type must be closed, spellable in C#, and effectively accessible from an
unrelated generated interceptor file in the same consumer assembly. Reject a signature when any
visited node:

- is an `ITypeParameterSymbol`;
- is anonymous or file-local;
- is inaccessible after considering its own declared accessibility and every containing type;
- is ref-like, a pointer, or a function pointer.

Evaluate accessibility from an unrelated top-level generated type in the consumer compilation
assembly. Public types are accessible; internal access requires the declaring assembly to grant that
consumer assembly access; protected access cannot rely on derivation. Every containing type must
also pass the same check. Private, protected-only, and private-protected types fail. C# aliases are
not required because generated code uses fully qualified names.

Transport containment differs by binding source:

- for the workflow event input and `Task<TOutput>`, recursively reject any visited node that is,
  derives from, or implements `System.IO.Stream` where assignability applies, or is exactly
  `DurableExecutionInvocationInput` or `DurableExecutionInvocationOutput`;
- for every other DI parameter, reject those transport types only when the top-level parameter type
  itself is Stream-derived or an exact durable envelope. Allow transport nested in service arrays,
  tuples, or generic type arguments because it does not expose invocation transport.

Neither rule traverses object fields, properties, runtime collection contents, or other member graphs.

### Rejected signatures

| Shape                                                                                     | Result                                                        |
| ----------------------------------------------------------------------------------------- | ------------------------------------------------------------- |
| Zero event-input candidates                                                               | `LH0007` with found count `0`                                 |
| More than one event-input candidate across `[FromEvent]` and `[Event]`                    | One `LH0007` per extra candidate, each with total found count |
| Zero exact `IDurableContext` parameters                                                   | `LH0008` with found count `0`                                 |
| More than one exact `IDurableContext`                                                     | One `LH0008` per extra parameter, each with total found count |
| Reserved context with event/service/keyed-service binding attribute                       | `LH0009`; it does not bind from that attribute                |
| Any `CancellationToken`, regardless of attributes                                         | `LH0009`                                                      |
| Recursive Stream or envelope containment in event input or `TOutput`                      | `LH0009` for input; `LH0010` for output                       |
| Direct Stream-derived or direct envelope type in another DI parameter                     | `LH0009`; nested service containment is allowed               |
| `ref`, `in`, or `out` parameter                                                           | `LH0009`                                                      |
| Ref-like, pointer, or function-pointer signature type                                     | `LH0009` for parameters; `LH0010` for output                  |
| Any recursively contained `ITypeParameterSymbol`                                          | `LH0009` for parameters; `LH0010` for output                  |
| Anonymous, file-local, or effectively inaccessible recursively emitted type               | `LH0009` for parameters; `LH0010` for output                  |
| `void`, `async void`, synchronous value, `ValueTask`, `ValueTask<T>`, or custom awaitable | `LH0010`                                                      |
| Any return other than exact `Task` or `Task<TOutput>`                                     | `LH0010`                                                      |

No synchronous, `ValueTask`, or custom-awaitable conversion is generated. Raw envelope, stream,
explicit AWS client, and protocol-control scenarios use low-level `MapHandler` with AWS
`DurableFunction.WrapAsync` directly.

`CancellationToken` is forbidden even as an explicitly attributed service. Advanced code can read
`ILambdaInvocationContext.CancellationToken` and then owns durable failure/retry consequences.

## Diagnostic Contract

New diagnostics use the existing `LH` namespace.

| ID       | Title                                              | Category                      | Severity | Stable message and arguments                                                                                                                                                                                                 |
| -------- | -------------------------------------------------- | ----------------------------- | -------- | ---------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `LH0007` | Invalid durable workflow input cardinality         | `MinimalLambda.Usage`         | Error    | `Durable handler must declare exactly one event input using '[FromEvent]'; found {0}.` Args: `foundCount`                                                                                                                    |
| `LH0008` | Invalid durable context cardinality                | `MinimalLambda.Usage`         | Error    | `Durable handler must declare exactly one 'Amazon.Lambda.DurableExecution.IDurableContext' parameter; found {0}.` Args: `foundCount`                                                                                         |
| `LH0009` | Unsupported durable handler parameter              | `MinimalLambda.Usage`         | Error    | `Durable handler parameter '{0}' of type '{1}' is not supported: {2}.` Args: `parameterName`, `effectiveType`, `reason`                                                                                                      |
| `LH0010` | Unsupported durable handler return type            | `MinimalLambda.Usage`         | Error    | `Durable handler return type '{0}' is not supported; use 'Task' or 'Task<TOutput>' with a closed, nameable, accessible, non-transport output type.` Args: `effectiveType`                                                    |
| `LH0011` | Durable serializer root is not explicitly declared | `MinimalLambda.Configuration` | Warning  | `Source-generated serializer context '{0}' does not explicitly declare durable serialization root '{1}'. Add [JsonSerializable(typeof({1}))] to that context or a base context declaration.` Args: `contextType`, `rootType` |

`foundCount` is the total classified count, not the ordinal of an extra parameter. Type arguments use
the canonical type display defined below. `parameterName` is the declared source/metadata name.

`LH0009` reason is exactly one of these stable strings, selected in this precedence order:

1. `outer durable envelope types are reserved for low-level MapHandler`;
2. `CancellationToken is not bound automatically; use ILambdaInvocationContext for explicit access`;
3. `Stream transport types are reserved for low-level MapHandler`;
4. `reserved context parameters cannot use event or service binding attributes`;
5. `ref, in, and out parameters are not supported`;
6. `ref-like, pointer, and function-pointer types are not supported`;
7. `signature types must be closed and cannot contain type parameters`;
8. `signature types must be nameable and accessible from generated code`.

Errors suppress emission for the affected durable mapping. `LH0011` does not. Invalid or
error-suppressed durable mappings contribute no serializer roots and produce no `LH0011`.

### Effective type display and identity

Diagnostic type arguments use a canonical fully qualified display after recursive nullability
erasure: `dynamic` normalizes to `global::System.Object`, namespace-qualified types start with
`global::`, framework types use metadata names rather than C# aliases, constructed generics and
containing named types display every normalized type argument, arrays preserve rank, and tuples
display normalized `global::System.ValueTuple<...>` construction without element names. Type
parameters use their declared name when explaining an invalid open signature. Erasure removes
nullable annotations from the top-level symbol, array elements, tuple elements, every closed generic
type argument, and every containing-type argument. The same normalization is used for serializer-root equality and display.

`LH0011` identity always includes both normalized `contextType` and `rootType`, even when several
warnings share one source location.

### Diagnostic locations and deduplication

For `LH0007` through `LH0010`, use source declaration locations when available:

- missing-cardinality diagnostics use the handler declaration identifier or lambda parameter-list
  start;
- extra-cardinality and parameter diagnostics use the offending parameter declaration;
- return diagnostics use the return-type declaration, or lambda body/arrow token when no return type
  exists.

When the handler has no source declaration, every diagnostic uses the handler argument syntax at its
`MapDurableHandler` call. Metadata method groups mapped at two calls therefore retain one diagnostic
unit per call.

`LH0011` uses the `TContext` type-argument location of the proven serializer registration. All roots
lacking an explicit declaration for that application can share this location. If that registration lacks source syntax, use
the first contributing `MapDurableHandler` argument location in deterministic order.

Globally deduplicate by exact `(diagnostic ID, effective location, stable message arguments)`. Mapping
the same source method twice yields one declaration diagnostic. Metadata fallback locations differ,
so diagnostics remain per call. Diagnostic model equality and hashing must include every stable
message argument; for `LH0011`, this necessarily includes context and root.

### Total deterministic order

Every diagnostic and handler model uses this total ordering key:

1. compilation syntax-tree ordinal;
2. span start;
3. diagnostic precedence `LH0007`, `LH0008`, `LH0009`, `LH0010`, `LH0011`;
4. parameter/root subkey: `0` for handler-level or return diagnostics, parameter ordinal plus one for
   parameter diagnostics, or serializer root rank `0` through `3` for `LH0011`;
5. stable message arguments, comparing integers numerically and strings with ordinal comparison.

Locations without source syntax use their mapping-call syntax before ordering. Parameter candidate
selection uses parameter ordinal. The first fallback handler, first serializer warning location,
diagnostic output, and generated durable-handler emission all use this ordering; generated handlers
use their `MapDurableHandler` call location as the first two key parts and zero for non-applicable key
parts.

## Serializer Warning Scope

Serializer analysis is isolated per **traceable Lambda application**. A trace exists only when all of
the following are in one method and one straight-line block:

1. a builder local is initialized once;
2. exactly one direct, unconditional
   `builder.Services.AddLambdaSerializerWithContext<TContext>()` occurs after initialization and
   before build;
3. an application local is initialized once by direct `builder.Build()`;
4. each contributing `MapDurableHandler` receiver is that application local;
5. neither local is reassigned, aliased, captured, returned, stored, passed as an argument, or used by
   another operation that can escape it;
6. `builder.Services` is never assigned to a local, returned, stored, captured, or passed as an
   argument.

The registration, build, and mappings must not be inside a conditional expression/statement, loop,
local function, lambda, exception filter, or separate block. Multiple builder/application pairs are
analyzed independently.

Between builder initialization and `Build()`, only these service operations preserve traceability:

- the single `AddLambdaSerializerWithContext<TContext>()` call;
- direct Microsoft DI registration overloads for which Roslyn resolves the registered service type
  and proves it is not assignable to `ILambdaSerializer`;
- operations that do not reference the builder or `builder.Services`.

Any other invocation/member access using `builder.Services` is an opaque mutation and suppresses
serializer warnings for that application.

Warnings are suppressed for the application when any of these occurs before or after the source
context registration in the traceable block:

- another `AddLambdaSerializerWithContext`, including the same `TContext`;
- direct custom, default, reflection-based, keyed, factory, instance, `TryAdd`, replace, remove, or
  decorate registration whose service type is or can be `ILambdaSerializer`;
- conditional or loop-contained serializer registration;
- opaque service mutation or escape;
- builder/application alias, capture, reassignment, or escape;
- untraceable `MapDurableHandler` receiver.

Thus sequential source-generated contexts, whether equal or different, suppress warnings. Overrides
before or after the source-generated registration suppress warnings. Statically proven non-serializer
registrations do not. False negatives are preferred over cross-builder or registration-order guesses.

Only valid emitted durable handlers associated with the traced application contribute roots. Roots
are unique under recursive nullability erasure and checked in this stable rank:

1. `DurableExecutionInvocationInput`;
2. `DurableExecutionInvocationOutput`;
3. `TInput`;
4. `TOutput`, only for `Task<TOutput>`.

Emit one `LH0011` per normalized `(TContext, root)` pair lacking an explicit declaration. Deduplicate
the pair across handlers in the same application and through the global diagnostic key. Read only
`[JsonSerializable(typeof(...))]` attributes declared directly on `TContext` or its base context
declarations. If those declaration symbols or attribute arguments cannot be resolved completely,
suppress all `LH0011` for that application.

This warning enforces explicit durable roots as a deterministic AOT contract. System.Text.Json may
still generate or resolve metadata transitively through object member graphs, resolver composition,
or other declarations. `LH0011` reports only absence of the explicit attribute; it makes no assertion
about `JsonSerializerContext.GetTypeInfo` behavior or runtime serialization success.

The analysis checks explicit signature-root declarations only. It does not traverse object member
graphs or inspect types hidden in steps, callbacks, invokes, child workflows, waits, maps, parallel
branches, or referenced libraries. Silence never claims serializer completeness.

## Rationale

AWS 1.0.0 accepts only typed-input `Task` and `Task<T>` workflow delegates. Matching those overloads
avoids novel completion and cancellation behavior. Generated closure capture preserves MinimalLambda
context and DI without changing AWS's workflow contract.

Explicit event attributes preserve the existing unannotated-equals-DI rule. Reserved-context
classification prevents attributes from changing framework-owned context semantics. Recursive
closed/nameable/accessibility checks keep emitted type syntax valid. Recursive transport checks apply
only to serialized workflow roots; direct checks keep invocation transport out of DI parameters
without rejecting nested service implementation types.

Stable messages, locations, identities, and total ordering make incremental generation and snapshots
repeatable. Strict application tracing prevents serializer warnings from leaking across builders or
ambiguous service-registration flows.

## Implementation Validation Contract

Implementation must cover:

### Signature and binding

- `[FromEvent]`, legacy `[Event]`, and mixed duplicate cardinality;
- each reserved context without attributes and with event, service, and keyed-service conflicts;
- conflicting `IDurableContext` interactions with missing input and duplicate durable contexts;
- context order, both/repeated Lambda contexts, ordinary/keyed/optional DI;
- nullable types, `dynamic` normalization, closed generics, constructed generic methods, and open or
  nested type parameters;
- anonymous, file-local, and inaccessible nested types, including containing-type chains and their
  open, dynamic, or inaccessible type arguments;
- recursive Stream/envelope containment in event inputs and outputs;
- direct Stream/envelope DI parameters rejected while nested service arrays, tuples, and generic
  arguments are allowed;
- proof that member-only transport containment is not generator-rejected;
- every return family and recursive output rule.

### Diagnostics and incremental identity

- exact ID, severity, message, stable arguments, source/metadata fallback location, and multiplicity;
- reused source method groups mapped twice versus reused metadata method groups;
- global deduplication with distinct message arguments at one location;
- multi-file syntax-tree ordering and parameter/root tie-breakers;
- invalid and valid handlers in one compilation, proving only the invalid adapter is suppressed;
- root swaps and incremental updates proving equality/hash includes all arguments;
- two roots lacking explicit declarations and sharing the serializer registration location.

### Serializer tracing

- one valid builder/context with all roots explicitly declared versus each independently omitted root;
- multiple builders with isolated contexts and roots;
- sequential equal and different contexts suppressing warnings;
- custom/default/reflection serializer overrides before and after context registration;
- conditional/loop registrations, aliasing, capture, receiver escape, Services escape, and opaque
  service mutation;
- allowed proven non-serializer registrations between initialization and build;
- untraceable mapping receivers;
- invalid/suppressed handlers contributing no roots;
- root deduplication, recursive nullability erasure, `dynamic`/`object` identity, same-location
  context/root identity, and stable root rank;
- explicit declarations on `TContext` and base contexts;
- a root reachable through transitive member-graph metadata still producing `LH0011`, with message
  text referring only to the missing explicit declaration.

### Emission and compatibility

- typed and void handlers compile against released AWS 1.0.0 on supported TFMs;
- generated calls use explicit `WrapAsync` generic arguments;
- warning-only handlers still emit;
- ordinary `MapHandler` snapshots remain byte-for-byte unchanged;
- analyzer release metadata records `LH0007` through `LH0011`.

Compiler-rejected forms require generator assertions only when Roslyn still supplies a resolvable
handler model.

## Consequences

### Positive

- Public behavior maps directly to released AWS APIs.
- MinimalLambda context and DI remain available.
- Diagnostics are actionable, deduplicated, and deterministic.
- Raw transport and unsafe automatic cancellation stay outside the high-level API.
- Serializer warnings are isolated to statically proven applications.

### Negative / trade-offs

- Durable handlers support fewer return forms than ordinary handlers.
- `[FromEvent]` or obsolete `[Event]` remains mandatory.
- Repeated Lambda invocation-context bindings are allowed despite redundancy.
- Strict tracing intentionally misses serializer warnings in abstractions and complex setup flows.
- Hidden operation and object-member types remain user responsibility.
- Diagnostic messages and arguments become compatibility surface.

## References

- [`ADR-001: Durable handler integration model`](./ADR-001-durable-handler-integration-model.md)
- [`ADR-002: Durable package and source-generation ownership`](./ADR-002-durable-package-and-source-generation-ownership.md)
- [`ADR-003: Durable pipeline and adapter ownership`](./ADR-003-durable-pipeline-and-adapter-ownership.md)
- [Durable Execution dependency and support matrix](./durable-dependency-support-matrix.md)
- [AWS DurableFunction 1.0.0 source](https://github.com/aws/aws-lambda-dotnet/blob/f5249ff589ba726b3e8283e01a111cf7fcb32b21/Libraries/src/Amazon.Lambda.DurableExecution/DurableFunction.cs)
- [AWS durable diagnostics at the 1.0.0 release commit](https://github.com/aws/aws-lambda-dotnet/blob/f5249ff589ba726b3e8283e01a111cf7fcb32b21/Libraries/src/Amazon.Lambda.Annotations.SourceGenerator/Diagnostics/DiagnosticDescriptors.cs)
- [AWS durable cancellation contract at the 1.0.0 release commit](https://github.com/aws/aws-lambda-dotnet/blob/f5249ff589ba726b3e8283e01a111cf7fcb32b21/Libraries/src/Amazon.Lambda.DurableExecution/docs/core/cancellation.md)
- [AWS .NET Durable Execution SDK guide](https://docs.aws.amazon.com/durable-execution/sdk-reference/languages/csharp/)
