# Architecture Decision Records

Architecture Decision Records (ADRs) capture consequential, durable decisions affecting MinimalLambda public API, package boundaries, or overall architecture.

Use an ADR when a decision:

- Defines or materially changes public API
- Establishes a package or versioning boundary
- Commits the project to a major architectural integration
- Has multiple credible alternatives with long-term consequences
- Would be expensive or disruptive to reverse after release

Do not use ADRs for routine implementation details, test organization, middleware behavior that follows existing framework semantics, or choices easily changed without public impact. Record those in plans, issues, code, or user documentation instead.

Create ADRs from [`ADR_TEMPLATE.md`](./ADR_TEMPLATE.md). Number them sequentially and use a short noun-phrase filename:

```text
ADR-001-durable-handler-integration-model.md
```

## Records

- [ADR-001: Durable handler integration model](./ADR-001-durable-handler-integration-model.md)
- [ADR-002: Durable package and source-generation ownership](./ADR-002-durable-package-and-source-generation-ownership.md)
- [ADR-003: Durable pipeline and adapter ownership](./ADR-003-durable-pipeline-and-adapter-ownership.md)
