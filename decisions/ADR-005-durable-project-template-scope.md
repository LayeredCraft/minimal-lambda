# ADR-005: Defer dedicated durable project template

## Status

- Accepted
- **Date:** 2026-08-01
- **Deciders:** MinimalLambda maintainers
- **Supersedes:** none

______________________________________________________________________

## Context

Durable Execution requires package references, serializer roots, deployment defaults, and replay-aware
handler guidance that differ from ordinary Lambda applications. Existing `mlambda` and `mlambda-aot`
templates remain ordinary Lambda templates and must not gain Durable Execution dependencies.

## Decision

Defer `mlambda-durable` from `MinimalLambda.Templates` until durable package release coupling and managed-service deployment evidence are available. Candidate template sources remain unshipped and are excluded from the template package and package-reference stamping.

No durable dependency is added to existing templates. NativeAOT durable template remains out of scope:
local NativeAOT evidence does not prove managed Durable Execution support.

## Consequences

- Published template package contains only standard and NativeAOT templates.
- Candidate durable template sources target only `net10.0` and deploy with `dotnet10` when later shipped.
- Durable template release requires managed-service deployment evidence and a versioning contract with `MinimalLambda.DurableExecution`.

## References

- [ADR-002: Durable package and source-generation ownership](./ADR-002-durable-package-and-source-generation-ownership.md)
- [Durable dependency support matrix](./durable-dependency-support-matrix.md)
- [Canonical durable example](../examples/MinimalLambda.Example.DurableExecution/README.md)
