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
DOTNET_NOLOGO=1 dotnet test --configuration Release -f net10.0
```

Use `task test:all` when practical; focused `dotnet test -f ...` commands are shortcuts, not the
canonical full suite.

AOT check:

```bash
DOTNET_NOLOGO=1 dotnet publish src/AotCompatibility.TestApp/AotCompatibility.TestApp.csproj /p:TreatWarningsAsErrors=true
```

Format:

```bash
task format
# or task format:csharpier for C# formatting only
```

## Code style

- nullable enabled; treat nullability warnings as bugs
- C# preview/C# 14 features present
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

## Before final handoff

Report commands run and results. If not run, say why.
