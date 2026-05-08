---
name: minimal-lambda
description: Work effectively with MinimalLambda, the Lambda-first .NET hosting framework in this repo and in client projects. Use this skill whenever the user asks to build, debug, migrate, test, document, or review code using MinimalLambda APIs, envelopes, middleware, lifecycle hooks, source-generated handlers, AOT/trimming, OpenTelemetry, or MinimalLambda.Testing. Trigger even when the user only mentions AWS Lambda with Minimal API-style .NET patterns, MapHandler, FromEvent, LambdaApplication, or MinimalLambda package names.
---

# MinimalLambda skill

Use this skill to give agents enough MinimalLambda project context without loading entire repo/docs.

## First move

1. Identify task area:
   - client project setup/package/config template → read `references/client-project-setup.md`
   - app setup/handler/DI/lifecycle → read `references/core-hosting.md` and `references/best-practices.md`
   - handler shape/unit-testable handlers → read `references/patterns/handler-patterns.md`
   - middleware/features/context → read `references/core-hosting.md` and `references/patterns/middleware-patterns.md`
   - SQS/SNS/API Gateway/Kinesis/Firehose/Kafka/CloudWatch/ALB envelopes → read `references/envelopes.md` and `references/patterns/envelope-patterns.md`
   - Native AOT/trimming/serializer context → read `references/patterns/aot-and-envelopes.md`
   - integration tests/client project tests → read `references/testing.md` and `references/patterns/testing-patterns.md`
   - tracing/metrics/shutdown flush → read `references/opentelemetry.md`
   - compile/runtime/test failure → read `references/troubleshooting.md`
   - repo contribution/source generator/AOT work → read `references/repo-workflow.md`
2. Validate against local docs/code when in MinimalLambda repo. Prefer docs first, then source/tests for exact API.
3. Keep Lambda-first constraints in mind: source generation, AOT friendliness, scoped per-invocation services, one handler per runtime execution.

## Fast mental model

MinimalLambda = ASP.NET Core Minimal API ergonomics adapted to AWS Lambda:

```csharp
var builder = LambdaApplication.CreateBuilder();
builder.Services.AddScoped<IMyService, MyService>();

await using var lambda = builder.Build();
lambda.MapHandler(([FromEvent] MyEvent evt, IMyService service, CancellationToken ct) =>
    service.HandleAsync(evt, ct));

await lambda.RunAsync();
```

Core pieces:

- `LambdaApplication.CreateBuilder()` creates standard .NET host/config/DI defaults.
- `MapHandler(...)` registers one Lambda handler. Source generator intercepts it at compile time.
- `[FromEvent]` marks deserialized event payload. At most one payload parameter.
- Other handler parameters resolve from DI/context/keyed services/cancellation token.
- Middleware wraps invocation pipeline via inline `UseMiddleware(...)` or class `UseMiddleware<T>()`.
- `OnInit(...)` runs once during cold start; `OnShutdown(...)` runs during teardown.
- `MinimalLambda.Testing` runs real pipeline in memory for client project tests.
- Envelope packages provide trigger-specific typed event/body access; use matching package rather than hand-parsing AWS records.

## Source-of-truth files

When details matter, inspect these:

- docs index: `docs/`
- package READMEs: `src/*/README.md`, `src/Envelopes/*/README.md`
- core runtime: `src/MinimalLambda/`
- abstractions: `src/MinimalLambda.Abstractions/`
- source generator: `src/MinimalLambda.SourceGenerators/`
- tests: `tests/`
- examples: `examples/`

Use `rg` for exact APIs before changing code. Existing docs can lag implementation; code/tests win.

## Common advice patterns

Read `references/best-practices.md` before giving architectural advice.

- Prefer thin handlers delegating to injected services.
- Prefer `CancellationToken` in async handlers and downstream calls.
- Prefer scoped services for per-invocation state; singleton for reusable clients/caches.
- Avoid storing scoped services in singletons.
- Prefer typed records/responses/envelopes over anonymous response contracts.
- Keep AOT/trimming safe: avoid reflection-heavy dynamic paths unless guarded and tested.
- Use method-group handlers or static handler methods when unit-testing handler logic directly.
- For end-to-end behavior, use `LambdaApplicationFactory<TProgram>`.

## Validation checklist

Before final answer or patch:

- Does code compile with source generation? `MapHandler` signature has 0 or 1 `[FromEvent]`.
- Does runtime call only one handler mapping path?
- Are packages matched (`MinimalLambda.Testing` same version as `MinimalLambda`)?
- Are envelope package/type and AWS trigger type aligned?
- Are middleware registered before `MapHandler`?
- Are cancellation tokens propagated?
- For repo changes: run format/tests per `AGENTS.md` when practical.
