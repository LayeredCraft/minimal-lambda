# AGENTS.md (MinimalLambda)

MinimalLambda is a Lambda-first hosting framework for .NET.
Code under `src/` runs on AWS Lambda or generates code that will.

## Core guidance

- Prefer small, focused diffs.
- Match existing patterns; avoid unnecessary refactors.
- Nullable warnings are bugs.
- Prefer AOT/trimming-friendly code.
- Avoid reflection-heavy/dynamic approaches unless required and guarded.
- Run formatting and relevant tests before handoff.

## Common commands

```bash
DOTNET_NOLOGO=1 dotnet restore
DOTNET_NOLOGO=1 dotnet tool restore
task format
task test:all
task build:aot-check
```

## Repo conventions

- C# preview is used in many projects.
- C# 14 `extension(...)` blocks are valid. Do not rewrite them to old `this` extension methods.
- XML docs: use only C# XML-supported tags. Do not use unsupported HTML tags like `<strong>`.

## More specific guidance

- Source/runtime code: `src/AGENTS.md`
- Tests: `tests/AGENTS.md`
- GitHub, PR, release, CI: `.github/AGENTS.md`
- Documentation: `docs/AGENTS.md`

## Git / PR

Use `git-workflow` skill for commits, branches, and PRs.
When writing PRs, use `./.github/pull_request_template.md`.
