# Hosting & Application Builder

`LambdaApplication.CreateBuilder()` mirrors ASP.NET Core’s `WebApplication.CreateBuilder()` but is tuned for Lambda’s execution model. This guide explains what the builder does for you, how to customize it, and how the hosting infrastructure composes lifecycle hooks, middleware, and handlers.

## Builder Defaults

Calling `LambdaApplication.CreateBuilder()` assembles a standard .NET host with Lambda-friendly defaults:

- **Configuration sources** – Adds `appsettings.json`, `appsettings.{Environment}.json`, user secrets (development), `AWS_` and `DOTNET_` prefixed environment variables, and ambient environment variables. The resulting `ConfigurationManager` is exposed via `builder.Configuration`.
- **Environment & content root** – Sets `IHostEnvironment.ApplicationName` from `AWS_LAMBDA_FUNCTION_NAME` (when available) and resolves the content root by honoring `DOTNET_CONTENTROOT`, `AWS_LAMBDA_TASK_ROOT`, or falling back to `Directory.GetCurrentDirectory()`.
- **Logging** – Registers console logging with activity tracking enabled. In Development, scope validation is turned on so singleton/scoped misuse throws during build.
- **Dependency injection** – Every call to `builder.Services` hits the standard `IServiceCollection`. On `builder.Build()`, minimal-lambda registers:
    - `ILambdaInvocationBuilderFactory`, `ILambdaOnInitBuilderFactory`, and `ILambdaOnShutdownBuilderFactory` so lambda-specific pipelines can be composed later.
    - `LambdaHostedService`, `ILambdaHandlerFactory`, feature collections, and `ILambdaBootstrapOrchestrator`.
    - Default implementations of `ILambdaSerializer` (System.Text.Json) and `ILambdaCancellationFactory` unless you already registered your own via `TryAddLambdaHostDefaultServices()`.

Most applications can rely entirely on `CreateBuilder()` + `builder.Build()`—just add services, middleware, handlers, and call `await lambda.RunAsync();`.

```csharp title="Program.cs" linenums="1"
using MinimalLambda.Builder;
using Microsoft.Extensions.DependencyInjection;

var builder = LambdaApplication.CreateBuilder();

builder.Services.AddSingleton<IGreetingService, GreetingService>();

var lambda = builder.Build();

lambda.MapHandler(([FromEvent] string name, IGreetingService service) =>
    service.Greet(name)
);

await lambda.RunAsync();
```

## Customizing the Builder

Use `LambdaApplicationOptions` when you need to tweak how the builder initializes:

```csharp title="Program.cs" linenums="1"
var builder = LambdaApplication.CreateBuilder(new LambdaApplicationOptions
{
    DisableDefaults = true,                          // start from an empty HostApplicationBuilder
    Configuration = new ConfigurationManager(),      // seed your own configuration sources
    ContentRootPath = "/var/task",                  // explicitly set the content root
    EnvironmentName = Environments.Production,
});
```

When `DisableDefaults = true`, you’re responsible for adding configuration providers, logging, and environment metadata before calling `builder.Build()`. This mode is useful for specialized hosting scenarios (custom service provider factory, integration tests, CLI applications) but most Lambda projects should keep the defaults.

Other customization hooks:

- `LambdaApplicationOptions.Args` – Flow command-line arguments into configuration.
- `builder.Services.ConfigureLambdaHostOptions(...)` – Override runtime behavior (Init/Shutdown timeouts, invocation cancellation buffer, output formatting).
- `builder.Services.AddLambdaSerializerWithContext<TContext>()` – Swap the default serializer with a source-generated one (or register any `ILambdaSerializer` manually).
- Register an `ILambdaInvocationContextAccessor` if you need to resolve `ILambdaInvocationContext` outside handlers/middleware.

## Build Phase

`builder.Build()` finalizes configuration and returns a `LambdaApplication` that implements `IHost`, `ILambdaInvocationBuilder`, `ILambdaOnInitBuilder`, and `ILambdaOnShutdownBuilder`. During build:

1. `TryAddLambdaHostDefaultServices()` ensures a serializer and cancellation factory exist.
2. A standard `IHost` is constructed via `Host.CreateEmptyApplicationBuilder` + your service registrations.
3. The `LambdaApplication` wrapper caches factories for the invocation, init, and shutdown builders.

After build you can still call:

- `lambda.UseMiddleware(...)`, `lambda.MapHandler(...)` – Compose the invocation pipeline.
- `lambda.OnInit(...)`, `lambda.OnShutdown(...)` – Register lifecycle hooks.
- `lambda.Properties[...]` – Store metadata consumed by middleware or handlers.

## Runtime Execution

The `LambdaHostedService` orchestrates execution when you call `await lambda.RunAsync();`:

1. The hosted service creates a linked `CancellationTokenSource` so it can cancel the loop during shutdown.
2. Middleware and the handler delegate are composed via `ILambdaHandlerFactory`, which calls `ConfigureHandlerBuilder` (injecting your registered middleware and the default envelope middleware).
3. OnInit and OnShutdown builders are configured, including the optional `ClearLambdaOutputFormatting` handler.
4. The bootstrap (`ILambdaBootstrapOrchestrator`) starts polling the Lambda Runtime API, invoking the composed handler for each event, and honoring `LambdaHostOptions` (cancellation buffers, shutdown durations).
5. When the runtime stops, the hosted service calls the aggregated shutdown handler and bubbles exceptions as `AggregateException` so you see every failure in logs.

## Testing & Alternate Hosts

Because `LambdaApplication` implements `IHost`, you can start it outside Lambda, resolve services, and
stop it like any other generic host:

```csharp title="Program.cs" linenums="1"
await lambda.StartAsync();

// Resolve services or invoke handlers manually
var client = lambda.Services.GetRequiredService<TestClient>();
await client.CallAsync();

await lambda.StopAsync();
```

For non-Lambda entry points (console apps, acceptance tests), combine `StartAsync`/`StopAsync` with
`DisableDefaults = true` if you want a trimmed-down host that reuses Lambda-ready middleware and
handlers.

## Troubleshooting

| Issue                                                                  | Cause                                                                                       | Fix                                                                                                        |
| ---------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- | ---------------------------------------------------------------------------------------------------------- |
| `InvalidOperationException: Lambda Handler is not set.`                | `builder.Build()` succeeded but `lambda.MapHandler(...)` was never called.                  | Register a handler before calling `lambda.RunAsync()`.                                                     |
| `AggregateException: Encountered errors while running OnInit handlers` | An OnInit delegate threw or returned `false`.                                               | Inspect inner exceptions; ensure handlers honor cancellation and only return `false` for fatal conditions. |
| `Graceful shutdown ... did not complete within the allocated timeout`  | OnShutdown handlers exceeded `LambdaHostOptions.ShutdownDuration - ShutdownDurationBuffer`. | Reduce work, increase shutdown duration, or skip optional cleanup.                                         |
| Environment variables not loaded                                       | You disabled defaults without re-adding `builder.Configuration.AddEnvironmentVariables()`.  | Re-add configuration sources or keep defaults.                                                             |

## Related Guides

- **[Dependency Injection](dependency-injection.md)** – Register services and understand lifetimes/scopes.
- **[Lifecycle Management](lifecycle-management.md)** – Deep dive into `OnInit`/`OnShutdown` behavior.
- **[Configuration](configuration.md)** – Bind `LambdaHostOptions` and application settings.
- **[Middleware](middleware.md)** – Build cross-cutting concerns with the invocation pipeline.
