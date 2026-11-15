# MinimalLambda repo workflow and implementation notes

Read when changing MinimalLambda itself, source generators, packages, docs, examples, AOT compatibility, or tests.

## Repo guardrails

Follow root `AGENTS.md`:

- small focused diffs
- match existing patterns
- run formatting + tests before handoff when practical
- avoid reflection-heavy/dynamic code unless required and guarded
- Lambda-first and AOT-friendly

## Commands

The repository requires the stable .NET 11 SDK, version 11.0.100 or later.
Libraries target .NET 8, 9, 10, and 11; Durable Execution targets .NET 10.

For a new checkout, use mise 2026.11.8 or later:

```bash
mise trust
mise install
mise run setup
mise exec -- dotnet build --configuration Release
mise exec -- task test:all
```

`mise.toml` reads the SDK version from `global.json` and installs older SDKs in the same .NET root
for the test matrix. Install sequentially with the configured `jobs = 1` to avoid SDK installer races.
`mise.lock` pins resolved tool versions. The setup task restores npm, NuGet,
local .NET tools, and the uv documentation environment. Keep existing Taskfiles as the command
source; use `mise exec --` when mise is not activated in the shell. Mise is for local development
setup; CI uses the existing GitHub Actions and LayeredCraft DevOps workflows.

Restore:

```bash
DOTNET_NOLOGO=1 dotnet restore
DOTNET_NOLOGO=1 dotnet tool restore
```

Build:

```bash
DOTNET_NOLOGO=1 dotnet build --configuration Release --no-restore /p:TreatWarningsAsErrors=true
```

Tests:

```bash
task test:all
# or pick the target framework relevant to the change
DOTNET_NOLOGO=1 dotnet test --project tests/MinimalLambda.UnitTests/MinimalLambda.UnitTests.csproj --configuration Release -f net11.0
```

Use `task test:all` when practical; focused `dotnet test -f ...` commands are shortcuts, not the
canonical full suite.

AOT check:

```bash
task build:aot-check
```

Format:

```bash
task format
# or task format:csharpier for C# formatting only
```

## Performance checks

The benchmark project compares .NET 10, .NET 11, and an opt-in .NET 11 Runtime Async build.
It covers full invocations, middleware, serialization, batch processing, and feature allocations.
Run from the repository root:

```bash
dotnet run --project benchmarks/MinimalLambda.Benchmarks -c Release -f net11.0 -- --filter '*InvocationBenchmarks*' '*BatchBenchmarks*' '*FeatureBenchmarks*'
```

Runtime Async is disabled by default. Evaluate it with `-p:MinimalLambdaRuntimeAsync=true`;
the repository applies the compiler feature only to `net11.0`. This property builds repository
projects; it does not change precompiled NuGet assemblies or configure client projects.
Validate tests and AOT publishing in both modes before changing the default.

## Code style

- nullable enabled; treat nullability warnings as bugs
- stable C# 15; C# 14 extension blocks remain valid
- extension blocks `extension(...) { ... }` are intentional; do not rewrite to old extension syntax
- file-scoped namespaces
- `sealed` for public classes unless inheritance intended
- `internal` for implementation details
- prefer `ArgumentNullException.ThrowIfNull(arg)`
- avoid dynamic/reflection on hot paths and source-generated/AOT paths

## Source generator landmarks

- entry: `src/MinimalLambda.SourceGenerators/MinimalLambdaGenerator.cs`
- syntax providers: `SyntaxProviders/`
- models: `Models/Handlers/`, `Models/Middleware/`
- diagnostics: `Diagnostics/`
- emitters: `Emitters/`
- templates: search for `.scriban`
- tests/snapshots: `tests/MinimalLambda.SourceGenerators.UnitTests/`

Generator responsibilities include:

- intercepting `MapHandler`, lifecycle hooks, class middleware registration
- validating `[FromEvent]` count and keyed service metadata
- generating reflection-free invocation glue

## Runtime landmarks

- `src/MinimalLambda/Builder/` app builder, invocation/lifecycle builders, extension targets
- `src/MinimalLambda/Core/Context/` invocation/lifecycle contexts
- `src/MinimalLambda/Core/Features/` event/response/features
- `src/MinimalLambda/Runtime/` hosted service/bootstrap integration
- `src/MinimalLambda.Abstractions/` public contracts

## Template package landmarks

- package project: `src/MinimalLambda.Templates/MinimalLambda.Templates.csproj`
- standard template: `src/MinimalLambda.Templates/templates/mlambda/`
- Native AOT template: `src/MinimalLambda.Templates/templates/mlambda-aot/`
- template metadata: `.template.config/template.json`
- generated app project: `src/BlueprintBaseName.1/BlueprintBaseName.1.csproj`
- generated tests: `test/BlueprintBaseName.1.Tests/`

Template behavior should stay aligned with AWS Lambda .NET templates:

- keep `preferNameDirectory=true`
- use `-o .` in docs/tests for adding to an existing solution folder
- do not add separate `*-add` templates
- keep versioned `PackageReference` items; document manual Central Package Management cleanup instead of adding a `--use-cpm` option
- keep Lambda project settings such as `AWSProjectType`, `CopyLocalLockFileAssemblies`, `PublishReadyToRun` for standard templates, and `PublishAot`, `StripSymbols`, `TrimMode=partial` for AOT templates

Template smoke test from repo root:

```bash
DOTNET_NOLOGO=1 dotnet pack src/MinimalLambda.Templates/MinimalLambda.Templates.csproj -c Release
DOTNET_NOLOGO=1 dotnet new install ./src/MinimalLambda.Templates --force
```

Then in a clean folder:

```bash
dotnet new sln -n TemplateSmoke
dotnet new mlambda -n SmokeFunction -o . --profile default --region us-east-1
dotnet sln add src/SmokeFunction/SmokeFunction.csproj
dotnet sln add test/SmokeFunction.Tests/SmokeFunction.Tests.csproj --include-references false
dotnet test

dotnet new mlambda-aot -n SmokeAotFunction -o . --profile default --region us-east-1
dotnet sln add src/SmokeAotFunction/SmokeAotFunction.csproj
dotnet sln add test/SmokeAotFunction.Tests/SmokeAotFunction.Tests.csproj --include-references false
dotnet test
```

## Test strategy

- Unit tests for isolated runtime/generator behavior.
- Snapshot tests for generated code changes; update snapshots only when intended.
- Integration tests via `MinimalLambda.Testing` for pipeline behavior.
- AOT test app for trimming/native publish compatibility.

## Release artifact validation

Release and preview publishing download and validate the complete package manifest before trusted
publishing. Keep all 16 `.nupkg` and 15 `.snupkg` files in the uploaded artifact. The shared workflows
are pinned to the symbol-upload fix from `LayeredCraft/devops-templates#53`.

## Before final handoff

Report commands run and results. If not run, say why.
