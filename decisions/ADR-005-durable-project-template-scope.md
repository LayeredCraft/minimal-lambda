# ADR-005: Ship dedicated durable project template

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

Ship `mlambda-durable`, dedicated .NET 10 managed Lambda Durable Execution template.

Template includes direct references to `MinimalLambda`, `MinimalLambda.DurableExecution`, and
`Amazon.Lambda.DurableExecution`; executable `dotnet10` deployment defaults; source-generated JSON
roots; and inline `MapDurableHandler` registration. Template package stamping updates both
MinimalLambda package references when template package is packed.

No durable dependency is added to existing templates. NativeAOT durable template remains out of scope:
local NativeAOT evidence does not prove managed Durable Execution support.

## Consequences

- `dotnet new mlambda-durable` creates focused durable starter without changing ordinary templates.
- Generated projects target only `net10.0` and deploy with `dotnet10`.
- Template docs describe durable callback cancellation and replay boundary.
- Durable template requires smoke coverage: pack/install, generate, restore, and build.

## References

- [ADR-002: Durable package and source-generation ownership](./ADR-002-durable-package-and-source-generation-ownership.md)
- [Durable dependency support matrix](./durable-dependency-support-matrix.md)
- [Canonical durable example](../examples/MinimalLambda.Example.DurableExecution/README.md)
