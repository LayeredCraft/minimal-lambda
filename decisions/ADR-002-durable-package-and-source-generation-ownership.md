# ADR-002: Durable package and source-generation ownership

## Status

- Accepted
- **Date:** 2026-07-29
- **Deciders:** MinimalLambda maintainers
- **Supersedes:** none

______________________________________________________________________

## Context

Durable execution support introduces optional dependencies and release concerns that do not apply to
every MinimalLambda application:

- `Amazon.Lambda.DurableExecution`
- AWS Lambda service API models/client dependencies
- Durable-specific public APIs and documentation
- Durable handler source generation
- Durable deployment templates and examples
- Compatibility with AWS durable package releases

MinimalLambda core currently embeds `MinimalLambda.SourceGenerators.dll` in the `MinimalLambda`
NuGet package. The generator recognizes exact interception targets by method, namespace, and
containing assembly. A `MapDurableHandler` method declared in a separate integration assembly will
therefore require explicit generator support.

The desired product boundary is a separately installable and separately versioned durable package.
At the same time, adding a second generator creates risks:

- duplicate generator infrastructure;
- duplicate or conflicting generated hint names;
- separate diagnostic and compatibility models;
- analyzer dependency loading complexity;
- two generators independently modeling the same MinimalLambda invocation pipeline.

Keeping durable support inside the core package avoids those risks but forces every MinimalLambda
consumer to receive AWS durable runtime dependencies and ties durable fixes to core releases.

The repository currently versions and publishes its packages synchronously. Independent durable
versioning therefore also requires an explicit release-lane and compatibility policy.

## Decision Drivers

- Keep durable dependencies optional for ordinary MinimalLambda users.
- Allow durable runtime/integration fixes to release independently.
- Keep all projects in the same repository unless stronger isolation becomes necessary.
- Avoid duplicate source-generator assemblies and generated outputs.
- Reuse existing MinimalLambda binding and emitter infrastructure.
- Keep consumer dependency and upgrade behavior understandable.
- Preserve symbol-based interception rather than method-name-only matching.
- Avoid direct runtime references from the generator to optional AWS assemblies.
- Maintain NativeAOT/trimming and compile-time generation guarantees.

## Options Considered

### Option A: Separate durable runtime package with core generator support

Ship `MinimalLambda.DurableExecution` as a separately versioned package. Extend the existing core
`MinimalLambda.SourceGenerators` assembly to recognize and generate the durable target declared by
the integration package.

**Pros:**

- Keeps AWS durable dependencies optional.
- Uses one MinimalLambda generator assembly.
- Reuses existing binding/emission infrastructure.
- Avoids duplicate source hints and generator loading.
- Allows durable runtime-only fixes to release independently.
- Keeps implementation in one repository.

**Cons:**

- New generated durable API behavior can require a core MinimalLambda release.
- Durable package needs a minimum compatible core version.
- Generator/runtime compatibility must be managed explicitly.
- Existing release workflow needs an independent durable lane.

### Option B: Put durable support in core package and core generator

Add runtime API, AWS dependencies, and generator behavior directly to `MinimalLambda`.

**Pros:**

- Simplest package compatibility model.
- One package and one version.
- One generator.

**Cons:**

- Every consumer receives durable dependencies.
- Durable release cadence becomes core release cadence.
- Expands core API and dependency surface for an optional feature.
- Makes future AWS package compatibility changes core-package concerns.

### Option C: Separate durable runtime package and separate durable generator

Embed a durable-only generator in `MinimalLambda.DurableExecution` while core keeps its existing
generator.

**Pros:**

- Strongest independent versioning.
- Durable runtime and generator ship together.
- Core generator need not know durable target.

**Cons:**

- Two MinimalLambda generators run in the same compilation.
- Duplicates model, binding, and template infrastructure.
- Requires strict hint/diagnostic isolation.
- Creates analyzer dependency and loading complexity.
- Increases long-term risk of different handler semantics between generators.

### Option D: Separate durable repository

Move durable runtime, generator, tests, release, and templates to another repository.

**Pros:**

- Full release and dependency isolation.
- Independent ownership and CI.

**Cons:**

- Harder coordinated changes across runtime and generator.
- Duplicates repository infrastructure.
- Makes compatibility testing and atomic updates more difficult.
- Adds overhead without current organizational need.

## Decision

We will use **Option A: a separately versioned `MinimalLambda.DurableExecution` package with durable
source generation implemented by the existing core `MinimalLambda.SourceGenerators` assembly**.

The ownership model is:

- `MinimalLambda.DurableExecution` owns the public `MapDurableHandler` interception target, durable
  context extensions, AWS durable runtime dependency, package README, and integration-specific
  runtime surface.
- `MinimalLambda.SourceGenerators` recognizes the target from the
  `MinimalLambda.DurableExecution` assembly and emits the outer durable envelope adapter.
- The core generator resolves AWS durable symbols from the consumer compilation by metadata name;
  it does not take a runtime reference on AWS durable assemblies.
- `MinimalLambda.DurableExecution` declares a minimum compatible `MinimalLambda` version containing
  required generator and context/serializer support.
- Durable package versions can advance independently for runtime-only changes.
- Generator-affecting durable changes require a compatible core release and corresponding minimum
  version update.
- Durable support remains in this repository but receives a release path that can publish it
  independently from synchronously versioned core packages.
- AWS determinism analyzer delivery is treated as part of package-consumer compatibility testing;
  MinimalLambda will not copy AWS analyzer binaries without an explicit deduplication design.

Applications continue to reference `MinimalLambda` directly, as normal MinimalLambda applications
do, and add `MinimalLambda.DurableExecution` for durable support. The direct core reference ensures
its embedded source generator participates in compilation.

## Rationale

Option A keeps durable dependencies and versioning outside core while preserving one source of truth
for MinimalLambda handler generation. Existing generator code already understands event binding,
contexts, services, keyed services, response handling, interceptors, diagnostics, and snapshot
validation. Extending that implementation is less risky than maintaining a parallel generator.

This choice knowingly accepts version coupling between a separately versioned durable runtime
package and the core package that ships its generator. The coupling is manageable when expressed as
a minimum core dependency and compatibility-tested package matrix. Runtime-only durable fixes remain
independently releasable, while generator contract changes trigger coordinated releases.

A separate repository or generator would maximize isolation but create more duplicated machinery and
more opportunities for ordinary and durable handlers to diverge.

## Consequences

**Positive:**

- Ordinary MinimalLambda users do not receive AWS durable dependencies.
- Durable runtime package can release independently for runtime-only changes.
- Consumer compilation loads one MinimalLambda generator.
- Durable generation reuses existing handler-binding behavior.
- Package and generator remain developed and tested in one repository.
- Public durable package boundary remains clear.

**Negative / Trade-offs:**

- Durable package cannot evolve generated API independently of core generator releases.
- Minimum core-version compatibility must be maintained and documented.
- Release automation must support an independent durable package lane.
- Package-consumer tests must cover mixed core/durable versions.
- Analyzer transitivity may require direct consumer references or build-target work depending on
  actual AWS package behavior.

**Neutral / Follow-on work:**

- Add `src/MinimalLambda.DurableExecution` project and package metadata.
- Extend core generator's symbol matching for external durable target.
- Add durable method models, diagnostics, emitter/template, and snapshots.
- Define version compatibility tests using packed NuGet packages rather than only project references.
- Update release workflows and release documentation for independent durable versioning.
- Inspect actual released AWS durable nupkg analyzer behavior.
- Decide durable template packaging after deployment support scope is established.
- Validate target frameworks against AWS package and live service support matrix.

## References

- [`ADR-001: Durable handler integration model`](./ADR-001-durable-handler-integration-model.md)
- Durable research context: `.agents/docs/durable-execution-context.md`
- Core package generator packing: `src/MinimalLambda/MinimalLambda.csproj`
- Core generator project: `src/MinimalLambda.SourceGenerators/MinimalLambda.SourceGenerators.csproj`
- Current target matching:
  `src/MinimalLambda.SourceGenerators/SyntaxProviders/HandlerSyntaxProvider.cs`
- Current generated handler template:
  `src/MinimalLambda.SourceGenerators/Templates/MapHandler.scriban`
- AWS durable package project:
  `Libraries/src/Amazon.Lambda.DurableExecution/Amazon.Lambda.DurableExecution.csproj` in
  `aws-lambda-dotnet`
