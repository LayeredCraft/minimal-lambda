# Testing client projects with MinimalLambda.Testing

Read when task asks for integration tests, in-memory Lambda execution, test fixtures, host overrides, lifecycle tests, or client project test setup.

## Docs to consult

- `docs/guides/testing.md`
- `src/MinimalLambda.Testing/README.md`
- `tests/MinimalLambda.Testing.UnitTests/`

## Core idea

`MinimalLambda.Testing` behaves like ASP.NET Core `WebApplicationFactory`: boot real Lambda entry point in memory and speak same Runtime API contract as AWS.

Use for:

- source-generated handler coverage
- middleware/envelope/DI/lifecycle integration
- host customization in tests
- regression tests for error payloads and cold-start behavior

Prefer plain unit tests for isolated business logic.

## Package rule

`MinimalLambda.Testing` version should match `MinimalLambda` version.

## Basic xUnit shape

```csharp
await using var factory = new LambdaApplicationFactory<Program>()
    .WithCancellationToken(TestContext.Current.CancellationToken);

var initResult = await factory.TestServer.StartAsync(TestContext.Current.CancellationToken);
initResult.InitStatus.Should().Be(InitStatus.InitCompleted);

var response = await factory.TestServer.InvokeAsync<MyEvent, MyResponse>(
    new MyEvent("World"),
    TestContext.Current.CancellationToken);

response.WasSuccess.Should().BeTrue();
response.Response.Message.Should().Be("Hello World!");
```

## Invocation APIs

- `InvokeAsync<TEvent, TResponse>(event, token)` for typed input + typed output.
- `InvokeNoEventAsync<TResponse>(token)` for no event payload.
- `InvokeNoResponseAsync<TEvent>(event, token)` for no response body.

Responses expose:

- `WasSuccess`
- `Response`
- `Error` for structured Lambda-style failure payload

## Host customization

Use `WithHostBuilder` for test-only config/services.

```csharp
await using var factory = new LambdaApplicationFactory<Program>()
    .WithHostBuilder(builder =>
    {
        builder.ConfigureServices((_, services) =>
        {
            services.RemoveAll<IOrderService>();
            services.AddScoped<IOrderService, FakeOrderService>();
        });
    });
```

Also supports app configuration overrides and custom service provider factories.

## Shared fixtures caution

Using one factory across many tests improves speed but shares:

- `OnInit` once
- `OnShutdown` once at fixture disposal
- singleton services across tests

Do not share factory when testing init/shutdown behavior or singleton isolation.

## Cancellation/timeouts

- Use `WithCancellationToken` to flow test cancellation.
- Per-call tokens bound individual invokes.
- `ServerOptions.FunctionTimeout` defaults to 3 seconds; adjust to test timeout behavior.

## Agent workflow

1. Check client target framework/test framework.
2. Add matching `MinimalLambda.Testing` package.
3. Ensure `Program` accessible to test project if needed (`public partial class Program` pattern if client uses top-level statements).
4. Pick right invoke API.
5. Assert `WasSuccess` before accessing response.
6. Test error cases through `Error`, not raw exceptions unless host startup fails.
