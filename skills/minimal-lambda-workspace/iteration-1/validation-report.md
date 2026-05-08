# MinimalLambda skill validation report — iteration 1

## Scope

Reviewed `skills/minimal-lambda` as dissemination skill for client-project agents, not repo-local `.agents` skill.

## Validation method

- Checked skill structure for progressive disclosure.
- Cross-checked referenced docs/source files in repo.
- Verified key API symbols exist in source:
  - `LambdaApplication`
  - `MapHandler`
  - `FromEventAttribute`
  - `AddLambdaSerializerWithContext`
  - `ConfigureEnvelopeOptions`
  - `LambdaApplicationFactory`
  - common envelope types
- Added prompt eval set in `skills/minimal-lambda/evals/evals.json` for future skill-creator runs.
- Ran deterministic validation script:

```bash
python3 skills/minimal-lambda/scripts/validate_references.py
```

Result:

```text
OK: MinimalLambda skill references validated
```

## Content improvements made

Added best-practice/project-use files:

- `references/best-practices.md` — architecture, DI lifetimes, middleware/lifecycle/testing/AOT checklists.
- `references/client-project-setup.md` — package setup, `Program.cs` template, config, top-level Program testing note, AOT context.
- `references/troubleshooting.md` — generator/runtime/serialization/testing failure playbook.
- `references/patterns/handler-patterns.md` — thin handlers, no-event handlers, keyed services, direct unit tests.
- `references/patterns/middleware-patterns.md` — inline/class middleware, features, short-circuit cache, error boundary.
- `references/patterns/envelope-patterns.md` — exact envelope type table and trigger examples.
- `references/patterns/testing-patterns.md` — `LambdaApplicationFactory`, overrides, invocation APIs, fixture guidance.
- `references/patterns/aot-and-envelopes.md` — serializer context and envelope options patterns.

Updated:

- `SKILL.md` task routing now points to new best-practice/pattern/troubleshooting files.
- `references/envelopes.md` now includes exact common envelope type names and AOT serializer guidance.
- `references/core-hosting.md` now points at exact feature helper source file.

## Gaps / future eval

Full with-skill vs baseline evals were not run because this harness exposes no subagent task tool. Evals are ready in `evals/evals.json` for a future skill-creator runner. Deterministic reference validation passed.
