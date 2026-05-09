# Client project setup

Read when creating or modifying a consumer project that uses MinimalLambda packages.

## Minimal packages

Plain Lambda handler:

```bash
dotnet add package MinimalLambda
```

Testing:

```bash
dotnet add package MinimalLambda.Testing
```

OpenTelemetry:

```bash
dotnet add package MinimalLambda.OpenTelemetry
dotnet add package OpenTelemetry.Extensions.Hosting
```

Trigger envelopes: add exactly the matching package, e.g.

```bash
dotnet add package MinimalLambda.Envelopes.ApiGateway
dotnet add package MinimalLambda.Envelopes.Sqs
```

Keep `MinimalLambda.Testing` version aligned with `MinimalLambda`.

## Project file basics

Use a current .NET SDK and modern C# language version. Interceptors/source-generation paths require
compiler support; prefer `LangVersion` `latest`/`preview` when package docs or build diagnostics
require it. Repo uses newer versions; client project can use current SDK/LangVersion.

```xml
<PropertyGroup>
  <TargetFramework>net8.0</TargetFramework>
  <Nullable>enable</Nullable>
  <ImplicitUsings>enable</ImplicitUsings>
  <LangVersion>latest</LangVersion>
</PropertyGroup>
```

For Native AOT, client project also needs normal AWS Lambda AOT settings. Validate with publish, not only build.

## `Program.cs` template

```csharp
using MinimalLambda.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = LambdaApplication.CreateBuilder();

builder.Services.AddScoped<IOrderService, OrderService>();

await using var lambda = builder.Build();

lambda.MapHandler(([FromEvent] OrderRequest request, IOrderService orders, CancellationToken cancellationToken) =>
    orders.ProcessAsync(request, cancellationToken));

await lambda.RunAsync();

public sealed record OrderRequest(string OrderId);
public sealed record OrderResponse(string OrderId, bool Accepted);

internal interface IOrderService
{
    Task<OrderResponse> ProcessAsync(OrderRequest request, CancellationToken cancellationToken);
}

internal sealed class OrderService : IOrderService
{
    public Task<OrderResponse> ProcessAsync(OrderRequest request, CancellationToken cancellationToken) =>
        Task.FromResult(new OrderResponse(request.OrderId, Accepted: true));
}
```

## Configuration

`CreateBuilder()` loads defaults in documented order and binds MinimalLambda settings from `LambdaHost`.

`appsettings.json`:

```json
{
  "LambdaHost": {
    "InvocationCancellationBuffer": "00:00:05",
    "ClearLambdaOutputFormatting": true
  }
}
```

Environment variable form:

```bash
LambdaHost__InvocationCancellationBuffer=00:00:05
```

Code override:

```csharp
builder.Services.ConfigureLambdaHostOptions(options =>
{
    options.InvocationCancellationBuffer = TimeSpan.FromSeconds(5);
});
```

## Top-level statements and tests

For integration tests that use `LambdaApplicationFactory<Program>`, make `Program` visible if needed:

```csharp
public partial class Program;
```

Add it at bottom of `Program.cs` in client app if test project cannot access generated top-level `Program` type.

## AOT serializer context

For AOT-friendly JSON serialization:

```csharp
using System.Text.Json.Serialization;

[JsonSerializable(typeof(OrderRequest))]
[JsonSerializable(typeof(OrderResponse))]
internal partial class SerializerContext : JsonSerializerContext;

builder.Services.AddLambdaSerializerWithContext<SerializerContext>();
```

For envelope payloads, also configure envelope options. See `patterns/aot-and-envelopes.md`.

## Agent checklist for client setup

1. Identify trigger and packages.
2. Add `MinimalLambda.Builder` using for builder + `[FromEvent]`.
3. Add service registrations before `Build()`.
4. Add middleware before `MapHandler`.
5. Ensure exactly one `MapHandler` path executes at runtime.
6. Add serializer context for AOT or explicit serialization requirements.
7. Add integration test using `MinimalLambda.Testing` when pipeline behavior matters.
