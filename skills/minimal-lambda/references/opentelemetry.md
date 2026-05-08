# OpenTelemetry

Read when task asks for tracing, metrics, X-Ray/OTLP, `UseOpenTelemetryTracing`, AWS Lambda instrumentation, or telemetry flush on shutdown.

## Portability note

This reference is self-contained for client-project use. Do not assume MinimalLambda OpenTelemetry source, examples, or tests exist in the current workspace. If the task is a repo contribution, switch to `repo-workflow.md` before inspecting local source.

## Packages

Typical packages:

```bash
dotnet add package MinimalLambda.OpenTelemetry
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol
dotnet add package OpenTelemetry.Extensions.Hosting
```

X-Ray often also needs:

```bash
dotnet add package OpenTelemetry.Contrib.Extensions.AWSXRay
```

## Basic setup

```csharp
var builder = LambdaApplication.CreateBuilder();

builder.Services
    .AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddAWSLambdaConfigurations();
        tracing.AddSource("MyService");
        tracing.SetResourceBuilder(ResourceBuilder.CreateDefault().AddService("MyService"));
        tracing.AddOtlpExporter();
    })
    .WithMetrics(metrics =>
    {
        metrics.AddMeter("MyService");
        metrics.AddOtlpExporter();
    });

await using var lambda = builder.Build();

lambda.UseOpenTelemetryTracing();
lambda.OnShutdownFlushOpenTelemetry();

lambda.MapHandler(async ([FromEvent] Request request, ILogger<Program> logger, CancellationToken ct) =>
{
    logger.LogInformation("Handling {Name}", request.Name);
    return new Response($"Hello {request.Name}");
});

await lambda.RunAsync();
```

## How it works

`UseOpenTelemetryTracing()` adds invocation middleware. It reads event and response through feature collection and delegates root-span creation/context propagation to official `OpenTelemetry.Instrumentation.AWSLambda`.

`OnShutdownFlushOpenTelemetry()` registers shutdown hook to force-flush tracer and meter providers before Lambda freezes/terminates environment.

## Rules and pitfalls

- Configure OpenTelemetry services before `builder.Build()`.
- Call `lambda.UseOpenTelemetryTracing()` before `MapHandler` so tracing wraps handler.
- Register `TracerProvider`; startup fails if required provider missing.
- Add custom `ActivitySource` names via `AddSource` and custom `Meter` names via `AddMeter`.
- Use shutdown flush for buffered exporters.
- Keep exporter config Lambda-safe; avoid long flush windows beyond shutdown budget.

## Agent workflow

1. Identify backend: OTLP, X-Ray, console, vendor exporter.
2. Add required NuGet packages.
3. Configure `AddOpenTelemetry()` with tracing/metrics.
4. Add `UseOpenTelemetryTracing()` and shutdown flush.
5. Validate source names/meters match app instrumentation.
6. For tests, inspect OpenTelemetry unit tests for exact assertions/patterns.
