# ADR-005: Defer dedicated durable project templates

## Status

- Accepted
- **Date:** 2026-08-01
- **Deciders:** MinimalLambda maintainers
- **Supersedes:** none

______________________________________________________________________

## Context

MinimalLambda now has buildable Durable Execution example, package-only consumer coverage, and
syntax-validated Amazon.Lambda.Tools and SAM/CloudFormation deployment recipes. AWS resource
deployment was declined for this work. Managed-service create, qualified invocation, suspension,
replay, success, denied-role behavior, and NativeAOT hosting therefore remain unverified.

Existing `mlambda` and `mlambda-aot` templates are ordinary Lambda templates. Adding durable package
dependencies to either would change default dependency and deployment requirements for users who do
not use Durable Execution. Repository release automation also assumes one core cohort version, while
`MinimalLambda.DurableExecution` needs an independent package release lane.

## Decision Drivers

- Keep Durable Execution dependency optional.
- Avoid encoding unverified cloud hosting assumptions in generated projects.
- Keep ordinary templates stable.
- Preserve explicit, independently versioned core and durable package references.
- Avoid implying local NativeAOT publish proves managed Durable Execution behavior.

## Options Considered

### Option A: Add Durable Execution to existing templates

**Pros:**

- No new template identity or discovery entry.

**Cons:**

- Adds optional AWS and MinimalLambda durable dependencies to ordinary applications.
- Changes deployment and IAM defaults for unrelated users.
- Cannot represent durable package version independently with current stamping.

### Option B: Ship dedicated managed and NativeAOT durable templates now

**Pros:**

- Fastest `dotnet new` path for durable applications.

**Cons:**

- Freezes recipes before managed-service evidence exists.
- Risks presenting NativeAOT as cloud-supported based only on local publish.
- Requires independent template version stamping before release lane exists.

### Option C: Defer dedicated templates until evidence and release gates pass

**Pros:**

- Keeps initial release claims aligned with observed evidence.
- Leaves ordinary templates unchanged.
- Allows example and deployment guide to collect feedback before template contract freezes.

**Cons:**

- Initial durable users start from example or add packages manually.
- Template implementation moves to follow-up release.

## Decision

Use Option C. Initial Durable Execution release ships no dedicated template. Existing `mlambda` and
`mlambda-aot` templates remain unchanged and must not reference `MinimalLambda.DurableExecution`.

Future template candidates are:

- `mlambda-durable`: managed `dotnet8` executable ZIP recipe, only after cloud create, qualified
  invoke, suspension/replay, success, denied-role, and rollback paths are observed.
- `mlambda-durable-aot`: separate optional variant, only after Durable Execution with MinimalLambda
  NativeAOT is independently cloud-verified. Approval of `mlambda-durable` does not approve this
  variant.

Any future durable template must stamp `MinimalLambda` and `MinimalLambda.DurableExecution` through
distinct version inputs. Core cohort release must not silently rewrite durable version, and durable
release must not rewrite core version. Generated project must use package-only dependencies and pass
the compatibility matrix against chosen pair.

## Rationale

Template is stronger support signal than example. Static validation proves recipe syntax and package
shape, not Lambda managed-service behavior. Deferral avoids turning missing cloud evidence into public
template promise while preserving usable example and guide. Separate future identities protect
ordinary users and let managed and NativeAOT evidence advance independently.

## Consequences

### Positive

- No durable dependency enters ordinary templates.
- Initial docs state exact evidence boundary.
- Independent version-stamping requirement is fixed before implementation.
- NativeAOT template remains gated on cloud evidence.

### Negative / trade-offs

- No `dotnet new` durable experience in initial release.
- `ml-3c5` closes as deferred-by-decision rather than implementation.
- New decision and implementation task are required after cloud verification.

## Follow-up disposition

Close `ml-3c5` without implementation, referencing this ADR. Open or revive dedicated template work
only after `ml-v3j` cloud acceptance is complete and independent durable release lane is operational.

## References

- [ADR-002: Durable package and source-generation ownership](./ADR-002-durable-package-and-source-generation-ownership.md)
- [Durable dependency support matrix](./durable-dependency-support-matrix.md)
- [Durable Execution Deployment](../docs/guides/durable-execution-deployment.md)
- [Canonical durable example](../examples/MinimalLambda.Example.DurableExecution/README.md)
