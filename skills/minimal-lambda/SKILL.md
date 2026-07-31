---
name: minimal-lambda
description: Work effectively with MinimalLambda, the Lambda-first .NET hosting framework in this repo and in client projects. Use this skill whenever the user asks to build, debug, migrate, test, document, scaffold, template, package, or review code using MinimalLambda APIs, `dotnet new mlambda` templates, envelopes, middleware, lifecycle hooks, source-generated handlers, AOT/trimming, OpenTelemetry, or MinimalLambda.Testing. Trigger even when the user only mentions AWS Lambda with Minimal API-style .NET patterns, MapHandler, FromEvent, LambdaApplication, MinimalLambda package names, MinimalLambda.Templates, Durable Execution, MapDurableHandler, IDurableContext, or adding a Lambda to an existing solution.
---

# MinimalLambda skill

Use this skill to give agents enough MinimalLambda project context without loading entire repo/docs.

## First move

1. Identify task area:
   - client project setup/package/config/template usage or `dotnet new mlambda` → read `references/client-project-setup.md`
   - app setup/handler/DI/lifecycle → read `references/core-hosting.md` and `references/best-practices.md`
   - AWS Lambda Durable Execution, `MapDurableHandler`, `IDurableContext`, replay, or checkpoints → read `references/durable-execution.md` before general handler/cancellation advice
   - handler shape/unit-testable handlers → read `references/patterns/handler-patterns.md`
   - middleware/features/context → read `references/core-hosting.md` and `references/patterns/middleware-patterns.md`
   - lifecycle hooks (`OnInit`/`OnShutdown`) → read `references/core-hosting.md` and
     `references/patterns/lifecycle-hook-patterns.md`
   - SQS/SNS/API Gateway/Kinesis/Firehose/Kafka/CloudWatch/ALB envelopes → read `references/envelopes.md` and `references/patterns/envelope-patterns.md`
   - Native AOT/trimming/serializer context → read `references/patterns/aot-and-envelopes.md`
   - integration tests/client project tests → read `references/testing.md` and `references/patterns/testing-patterns.md`
   - tracing/metrics/shutdown flush → read `references/opentelemetry.md`
   - compile/runtime/test failure → read `references/troubleshooting.md`
   - repo contribution/source generator/AOT/template package work → read `references/repo-workflow.md`
2. Use bundled references as the primary source. They are included so the skill works in client projects and global installs without assuming the MinimalLambda repository is present.
3. Only inspect local MinimalLambda source paths after confirming the current workspace is this repository; for repo contributions, read `references/repo-workflow.md` first.
4. Keep Lambda-first constraints in mind: source generation, AOT friendliness, scoped per-invocation services, one handler per runtime execution.

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
- `MapHandler(...)` registers one ordinary Lambda handler. Source generator intercepts it at compile time.
- `MapDurableHandler(...)` registers one durable workflow with stricter signature, cancellation, replay, middleware, and serializer rules; read `references/durable-execution.md`.
- `[FromEvent]` marks deserialized event payload. Ordinary handlers allow at most one; durable handlers require exactly one.
- Other handler parameters resolve from DI/context/keyed services/cancellation token.
- Middleware wraps invocation pipeline via inline `UseMiddleware(...)` or class `UseMiddleware<T>()`.
- `OnInit(...)` runs once during cold start; `OnShutdown(...)` runs during teardown.
- `MinimalLambda.Testing` runs real pipeline in memory for client project tests.
- Envelope packages provide trigger-specific typed event/body access; use matching package rather than hand-parsing AWS records.

## Portability rule

Assume this skill may run in a client project, not the MinimalLambda repository. Do not try to read MinimalLambda repo-local source, docs, test, or example paths unless the task is explicitly about changing MinimalLambda itself or the workspace clearly contains this repository. For client-project work, answer from bundled references and the user's project files.

## Common advice patterns

Read `references/best-practices.md` before giving architectural advice.

- Prefer inline `MapHandler` arrow functions, inline middleware, and inline lifecycle hooks in
  `Program.cs` when they are Lambda adapter/glue code.
- Keep complex business logic out of handlers, middleware, and hooks; put it in injected services or
  small domain helpers.
- Treat `ILambdaInvocationContext`, raw AWS `ILambdaContext`, features, lifecycle context, and other
  Lambda context objects as edge concerns. Almost never pass them into services; extract needed
  primitive/domain values at the edge. Passing Lambda context into services is usually a
  layer-boundary smell.
- Allow simple inline logic in `Program.cs` when logic is tiny and Lambda remains easy to read.
- Extract middleware classes only when middleware is complex, reusable, stateful, or worth testing
  separately.
- Prefer `CancellationToken` in ordinary async handlers and downstream calls. Durable root handlers must not declare one; use SDK-provided durable-operation callback tokens instead.
- Prefer scoped services for per-invocation state; singleton for reusable clients/caches.
- Avoid storing scoped services in singletons.
- Prefer typed records/responses/envelopes over anonymous response contracts.
- Keep AOT/trimming safe: avoid reflection-heavy dynamic paths unless guarded and tested.
- For direct unit tests, test services/helpers; only extract a named handler when handler adapter
  logic itself needs focused tests.
- For end-to-end behavior, use `LambdaApplicationFactory<TProgram>`.

## Validation checklist

Before final answer or patch:

- Does code compile with source generation? `MapHandler` has 0 or 1 `[FromEvent]`; `MapDurableHandler` has exactly 1 plus exactly 1 `IDurableContext` and returns `Task`/`Task<T>`.
- Does durable code avoid a root `CancellationToken`, register explicit serializer roots, and obey replay-safe middleware rules?
- Does runtime call only one handler mapping path?
- Are packages matched (`MinimalLambda.Testing` same version as `MinimalLambda`)?
- Are envelope package/type and AWS trigger type aligned?
- Are middleware registered before `MapHandler`?
- Are cancellation tokens propagated?
- For repo changes: run format/tests per `AGENTS.md` when practical.
