# ADR-002: Durable package and source-generation ownership

## Status

- Accepted
- **Date:** 2026-07-29
- **Deciders:** MinimalLambda maintainers
- **Supersedes:** none

______________________________________________________________________

## Context

Durable support adds optional AWS dependencies and needs source generation for
`MapDurableHandler`. Repository package releases use one synchronous cadence.

MinimalLambda currently ships one source generator inside the core `MinimalLambda` package. We need
to decide where durable runtime APIs and generation should live.

## Decision Drivers

- Keep durable dependencies optional.
- Keep durable dependency optional without introducing a separate release lane.
- Avoid duplicate generators and generated output.
- Reuse existing MinimalLambda handler-generation behavior.
- Keep package compatibility understandable.

## Options Considered

### Option A: Separate package, core generator

```text
Application
├── MinimalLambda
│   └── MinimalLambda.SourceGenerators
└── MinimalLambda.DurableExecution
    └── Amazon.Lambda.DurableExecution
```

**Pros:** Optional runtime package, one generator, shared handler-generation behavior.

**Cons:** Generator changes may require coordinated core and durable releases.

### Option B: Put everything in `MinimalLambda`

```text
Application
└── MinimalLambda
    ├── durable runtime APIs
    ├── AWS durable dependencies
    └── source generator
```

**Pros:** Simplest version and compatibility model.

**Cons:** Every MinimalLambda user receives durable dependencies.

### Option C: Separate package and separate generator

```text
Application
├── MinimalLambda
│   └── MinimalLambda.SourceGenerators
└── MinimalLambda.DurableExecution
    └── MinimalLambda.DurableExecution.SourceGenerators
```

**Pros:** Durable runtime and generator can version together.

**Cons:** Two generators duplicate handler-binding infrastructure and increase collision risk.

### Option D: Separate repository

```text
minimal-lambda
minimal-lambda-durable-execution
```

**Pros:** Full release isolation.

**Cons:** More coordination and duplicated repository infrastructure.

## Decision

We will use **Option A: a separate `MinimalLambda.DurableExecution` package with durable generation
implemented by the existing core `MinimalLambda.SourceGenerators` assembly**. It shares repository
synchronous package versioning and release cadence with core.

A durable application references both packages:

```xml
<PackageReference Include="MinimalLambda" Version="X.Y.Z" />
<PackageReference Include="MinimalLambda.DurableExecution" Version="A.B.C" />
```

- `MinimalLambda.DurableExecution` owns `MapDurableHandler`, context extensions, and the AWS durable
  dependency.
- Core source generator recognizes `MapDurableHandler` from the durable package.
- Durable package declares the minimum compatible `MinimalLambda` version.
- Durable runtime and generator changes release with the synchronous package set.

## Rationale

This keeps durable dependencies outside core while retaining one generator and one handler-binding
model. Version coupling is explicit through the durable package's minimum core dependency.

## Consequences

### Positive

- Durable dependencies remain optional.
- Only one MinimalLambda generator runs.
- Durable and ordinary handlers share generation behavior.
- One trusted-publishing release workflow publishes all packages together.

### Negative / trade-offs

- Durable generator changes require a core release.
- Package compatibility must be tested and documented.
- Durable runtime fixes wait for the repository's synchronous release cadence.

## References

- [`ADR-001: Durable handler integration model`](./ADR-001-durable-handler-integration-model.md)
- [Durable dependency and support matrix](./durable-dependency-support-matrix.md)
