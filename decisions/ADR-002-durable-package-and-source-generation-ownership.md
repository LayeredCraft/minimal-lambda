# ADR-002: Durable package and source-generation ownership

## Status

- Accepted
- **Date:** 2026-07-29
- **Deciders:** MinimalLambda maintainers
- **Supersedes:** none

______________________________________________________________________

## Context

Durable support adds optional AWS dependencies and needs source generation for
`MapDurableHandler`. Durable package releases use the same workflow and version as core packages.

MinimalLambda currently ships one source generator inside the core `MinimalLambda` package. We need
to decide where durable runtime APIs and generation should live.

## Decision Drivers

- Keep durable dependencies optional.
- Release all MinimalLambda packages together with one version.
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
implemented by the existing core `MinimalLambda.SourceGenerators` assembly**. It publishes with all
other MinimalLambda packages from the shared `v<semver>` release lane.

A durable application references both packages:

```xml
<PackageReference Include="MinimalLambda" Version="X.Y.Z" />
<PackageReference Include="MinimalLambda.DurableExecution" Version="X.Y.Z" />
```

- `MinimalLambda.DurableExecution` owns `MapDurableHandler`, context extensions, and the AWS durable
  dependency.
- Core source generator recognizes `MapDurableHandler` from the durable package.
- Durable package declares the matching `MinimalLambda` version.
- Durable runtime and generator changes release together with core.

## Rationale

This keeps durable dependencies outside core while retaining one generator and one handler-binding
model. Compatibility is explicit through matching package versions in each shared release.

## Consequences

### Positive

- Durable dependencies remain optional.
- Only one MinimalLambda generator runs.
- Durable and ordinary handlers share generation behavior.
- One trusted-publishing lane publishes all packages with one version.

### Negative / trade-offs

- Durable runtime fixes require a core package release.

## References

- [`ADR-001: Durable handler integration model`](./ADR-001-durable-handler-integration-model.md)
- [Durable dependency and support matrix](./durable-dependency-support-matrix.md)
