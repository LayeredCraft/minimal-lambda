# Testing

MinimalLambda.Testing works like ASP.NET Core’s `WebApplicationFactory`: it boots your real Lambda
entry point in memory, speaking the same Runtime API contract that AWS uses. It is the
end-to-end/integration layer above pure unit tests: you run the real pipeline (handlers, middleware,
lifecycle hooks, DI) without deploying or opening ports.

## When to Use

- **End-to-end pipeline coverage** – Exercise source-generated handlers, middleware, envelopes, and
    lifecycle hooks with real DI and serialization.
- **Regression nets** – Verify bootstrapping, cold-start logic, and error payloads stay stable.
- **Host customization** – Override configuration/services per test via `WithHostBuilder`.
- Prefer plain unit tests for isolated logic; reach for MinimalLambda.Testing when you need
    confidence in the Lambda runtime behavior.

## Quick Start

Install both packages:

```bash
dotnet add package MinimalLambda
dotnet add package MinimalLambda.Testing
```

!!! warning "Package Versions"

    Ensure `MinimalLambda.Testing` version matches your `MinimalLambda` version.
    Mismatched versions may cause runtime errors or unexpected behavior.

Write an end-to-end test with xUnit v3:

```csharp title="HelloWorldTests.cs" linenums="1"
using MinimalLambda.Testing;
using Xunit;

public record MyEvent(string Name);
public record MyResponse(string Message);

public class HelloWorldTests
{
    [Fact]
    public async Task HelloWorld_ReturnsGreeting()
    {
        await using var factory = new LambdaApplicationFactory<Program>()
            .WithCancellationToken(TestContext.Current.CancellationToken);

        // Optional: StartAsync mirrors Lambda init; InvokeAsync will start on demand if you skip this.
        var initResult = await factory.TestServer.StartAsync(TestContext.Current.CancellationToken);
        Assert.Equal(InitStatus.InitCompleted, initResult.InitStatus);

        var response = await factory.TestServer.InvokeAsync<MyEvent, MyResponse>(
            new MyEvent("World"),
            TestContext.Current.CancellationToken
        );

        Assert.True(response.WasSuccess);
        Assert.Equal("Hello World!", response.Response.Message);
    }
}
```

## Invocation APIs

- `InvokeAsync<TEvent, TResponse>(event, token)` – Send a strongly typed event, expect a typed
    response; fails with an `InvocationResponse<TResponse>` containing error details on handler
    exceptions.
- `InvokeNoEventAsync<TResponse>(token)` – Invoke a handler that does not take an event payload.
- `InvokeNoResponseAsync<TEvent>(event, token)` – Fire-and-forget style; skips response
    deserialization for handlers that return `void`/`Task` or write directly to streams.

`InvocationResponse`/`InvocationResponse<T>` include `WasSuccess`, `Response`, and structured
`Error` information that mirrors Lambda runtime error payloads—assert on these to verify failures.

### Trace IDs

Pass a custom `traceId` to control the `Lambda-Runtime-Trace-Id` header for correlation in logs and telemetry:

```csharp linenums="1"
var response = await factory.TestServer.InvokeAsync<MyEvent, MyResponse>(
    new MyEvent("test"),
    TestContext.Current.CancellationToken,
    traceId: "custom-trace-id-12345"
);

// Verify trace ID was used in logging/telemetry
```

If omitted, a new GUID is generated for each invocation.

## Working with Cancellation

- **Propagate test cancellation** – Call `WithCancellationToken(...)` on the factory to flow your
    test framework's token into the in-memory runtime. All server operations observe it.
- **Per-call tokens** – Pass tokens to `StartAsync` and `Invoke*` to bound individual operations.
- **Pre-canceled tokens** – A pre-canceled token will fail the invocation immediately (see
    `SimpleLambda_WithPreCanceledToken_CancelsInvocation` in the test suite).
- **Automatic timeouts** – Every invocation automatically times out after
    `LambdaServerOptions.FunctionTimeout` (defaults to 3 seconds, matching AWS Lambda's default).
    The test server creates a linked cancellation token for each invocation that enforces this deadline,
    mirroring Lambda's actual timeout behavior. Adjust `factory.ServerOptions.FunctionTimeout` before
    invoking to test different timeout scenarios or catch slow handlers.

## Host Customization and Fixtures

### Override Host Configuration

Use `WithHostBuilder` to tweak services and configuration for a specific test run:

```csharp title="DI override" linenums="1"
await using var factory = new LambdaApplicationFactory<Program>()
    .WithHostBuilder(builder =>
    {
        builder.ConfigureServices((_, services) =>
        {
            // Swap implementations or inject test doubles
            services.Configure<LambdaHostOptions>(options =>
            {
                options.BootstrapOptions.RuntimeApiEndpoint = "http://localhost:9001";
            });
        });
    });
```

You can also override app configuration (`ConfigureAppConfiguration`) or swap the DI container using
`UseServiceProviderFactory` / `ConfigureContainer` (Autofac, etc.)—the factory will replay those
changes before the Lambda host boots.

### Content Roots for File Fixtures

Add `LambdaApplicationFactoryContentRootAttribute` to your test assembly when you need a predictable
content root (e.g., static files, JSON fixtures). The factory will pick it up and set the content
root before booting the host.

### Shared Fixtures with IClassFixture

Use xUnit's `IClassFixture` to share a single `LambdaApplicationFactory` across all tests in a class.
This improves test performance by reusing the same Lambda host instance:

```csharp linenums="1"
public class SimpleLambdaTests(LambdaApplicationFactory<Program> factory)
    : IClassFixture<LambdaApplicationFactory<Program>>
{
    private readonly LambdaTestServer _server = factory.TestServer;

    [Fact]
    public async Task SimpleLambda_ReturnsExpectedValue()
    {
        var response = await _server.InvokeAsync<MyEvent, MyResponse>(
            new MyEvent("World"),
            TestContext.Current.CancellationToken
        );

        response.WasSuccess.Should().BeTrue();
        response.Response.Message.Should().Be("Hello World!");
    }

    [Fact]
    public async Task SimpleLambda_WithDifferentInput_ReturnsExpectedValue()
    {
        var response = await _server.InvokeAsync<MyEvent, MyResponse>(
            new MyEvent("Lambda"),
            TestContext.Current.CancellationToken
        );

        response.WasSuccess.Should().BeTrue();
        response.Response.Message.Should().Be("Hello Lambda!");
    }
}
```

**Important:** The same factory instance is used for all tests in the class. This means:

- OnInit runs once when the first test executes
- OnShutdown runs once when all tests complete
- Singleton services are shared across all tests in the class
- Do not use this pattern if you need to test initialization/shutdown behavior (use a fresh factory
    per test instead)

#### Custom Factory for Reusable Configuration

For more complex scenarios, extend `LambdaApplicationFactory<TProgram>` to create a reusable fixture
with pre-configured test doubles and settings:

=== "Custom Factory"

    ```csharp linenums="1"
    public class CustomLambdaApplicationFactory<TProgram> : LambdaApplicationFactory<TProgram>
        where TProgram : class
    {
        // Expose test doubles as properties for easy access in tests
        public ILifecycleService LifecycleService { get; } = Substitute.For<ILifecycleService>();

        protected override void ConfigureWebHost(IHostBuilder builder)
        {
            builder.ConfigureServices((_, services) =>
            {
                // Replace real implementations with test doubles
                services.RemoveAll<ILifecycleService>();
                services.AddSingleton<ILifecycleService>(_ => LifecycleService);
            });

            builder.UseEnvironment("Development");
        }
    }
    ```

=== "Test Class"

    ```csharp linenums="1"
    public class MyLambdaTests(CustomLambdaApplicationFactory<Program> factory)
        : IClassFixture<CustomLambdaApplicationFactory<Program>>
    {
        private readonly LambdaTestServer _server = factory.TestServer;

        [Fact]
        public async Task Lambda_WithMockedDependency_ReturnsExpectedValue()
        {
            // Configure the test double for this specific test
            factory.LifecycleService.OnStart().Returns(true);

            var response = await _server.InvokeAsync<MyEvent, MyResponse>(
                new MyEvent("World"),
                TestContext.Current.CancellationToken
            );

            response.WasSuccess.Should().BeTrue();
            response.Response.Message.Should().Be("Hello World!");
        }
    }
    ```

This pattern is useful when:

- Multiple test classes need the same test setup
- You want to expose test doubles as properties for easy configuration
- You need consistent environment settings across many tests

### Tuning the Runtime Shim

`LambdaServerOptions` controls the simulated runtime headers, timing, and serialization behavior.
Access it via `factory.ServerOptions` before starting the server if you need test-specific values:

- **Runtime headers** – `FunctionArn`, `AdditionalHeaders` for custom Lambda runtime headers
- **Timeout behavior** – `FunctionTimeout` controls invocation deadline (defaults to 3 seconds)
- **JSON serialization** – `SerializerOptions` controls how the test server serializes events and
    responses sent to your handler

```csharp linenums="1"
await using var factory = new LambdaApplicationFactory<Program>();

// Configure test server options before starting
factory.ServerOptions.FunctionTimeout = TimeSpan.FromSeconds(10);
factory.ServerOptions.FunctionArn = "arn:aws:lambda:us-east-1:123456789012:function:MyFunc";
factory.ServerOptions.SerializerOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
    WriteIndented = true
};

var response = await factory.TestServer.InvokeAsync<MyEvent, MyResponse>(
    new MyEvent("test"),
    TestContext.Current.CancellationToken
);
```

## Initialization and Shutdown Behavior

- `StartAsync` returns `InitResponse` with `InitStatus` values:
    - `InitCompleted` / `InitAlreadyCompleted` – Ready to invoke.
    - `InitError` – An `ErrorResponse` from OnInit failures; server stops itself.
    - `HostExited` – Entry point exited early (e.g., OnInit signaled stop).
- `Invoke*` will start the server on-demand; if init fails it throws with the reported status.
- `StopAsync` triggers OnShutdown and aggregates any exceptions (surfaced as `AggregateException`).
- `DisposeAsync` is idempotent; safe to call multiple times.

!!! tip "StartAsync is Optional"

    `InvokeAsync` will automatically call `StartAsync` if you haven't called it explicitly.

    **When to call StartAsync explicitly:**

    - To inspect `InitStatus` before invoking
    - To measure cold start time separately
    - To ensure OnInit completes before tests run

    **When to skip StartAsync:**

    - Simple handler tests where init success is assumed
    - Tests focused on invocation behavior, not initialization

## Testing Patterns

### Testing Error Responses

Validate error handling by asserting on `InvocationResponse.Error`:

```csharp linenums="1"
[Fact]
public async Task Handler_WithInvalidInput_ReturnsStructuredError()
{
    await using var factory = new LambdaApplicationFactory<Program>()
        .WithCancellationToken(TestContext.Current.CancellationToken);

    var response = await factory.TestServer.InvokeAsync<MyEvent, MyResponse>(
        new MyEvent(""), // Invalid: empty name
        TestContext.Current.CancellationToken
    );

    response.WasSuccess.Should().BeFalse();
    response.Error.Should().NotBeNull();
    response.Error.ErrorMessage.Should().Contain("Name is required");
    // Error.ErrorType and Error.StackTrace also available
}
```

### Testing Concurrent Invocations

The test server handles concurrent invocations safely with FIFO ordering:

```csharp linenums="1"
[Fact]
public async Task ConcurrentInvocations_AreHandledInOrder()
{
    await using var factory = new LambdaApplicationFactory<Program>()
        .WithCancellationToken(TestContext.Current.CancellationToken);

    // Launch multiple concurrent invocations
    var tasks = Enumerable.Range(1, 10)
        .Select(i => factory.TestServer.InvokeAsync<MyEvent, MyResponse>(
            new MyEvent($"User{i}"),
            TestContext.Current.CancellationToken))
        .ToArray();

    var responses = await Task.WhenAll(tasks);

    // All invocations succeed
    responses.Should().AllSatisfy(r => r.WasSuccess.Should().BeTrue());

    // Responses maintain FIFO order
    responses.Select(r => r.Response.Message)
        .Should().ContainInOrder("Hello User1!", "Hello User2!", "Hello User3!", "Hello User4!", "Hello User5!",
                                  "Hello User6!", "Hello User7!", "Hello User8!", "Hello User9!", "Hello User10!");
}
```

### Testing Middleware

Verify middleware behavior by inspecting response metadata or side effects:

```csharp linenums="1"
[Fact]
public async Task CustomMiddleware_AddsExpectedHeaders()
{
    await using var factory = new LambdaApplicationFactory<Program>()
        .WithCancellationToken(TestContext.Current.CancellationToken);

    var response = await factory.TestServer.InvokeAsync<MyEvent, MyResponse>(
        new MyEvent("test"),
        TestContext.Current.CancellationToken
    );

    response.WasSuccess.Should().BeTrue();

    // Verify middleware added metadata to response
    response.Response.Headers.Should().ContainKey("X-Request-Id");
}
```

### Testing Lifecycle Hooks

#### OnInit That Signals Shutdown

```csharp linenums="1"
[Fact]
public async Task OnInit_WhenReturningFalse_ShutsDownGracefully()
{
    var mockService = Substitute.For<IMyService>();
    mockService.Initialize().Returns(false); // Signal shutdown

    await using var factory = new LambdaApplicationFactory<Program>()
        .WithHostBuilder(builder =>
            builder.ConfigureServices((_, services) =>
            {
                services.RemoveAll<IMyService>();
                services.AddSingleton<IMyService>(mockService);
            }));

    var initResult = await factory.TestServer.StartAsync(
        TestContext.Current.CancellationToken
    );

    initResult.InitStatus.Should().Be(InitStatus.HostExited);
}
```

#### OnInit That Throws Exceptions

```csharp linenums="1"
[Fact]
public async Task OnInit_WhenThrowingException_ReturnsInitError()
{
    var mockService = Substitute.For<IMyService>();
    mockService.Initialize().Throws(new Exception("Database unavailable"));

    await using var factory = new LambdaApplicationFactory<Program>()
        .WithHostBuilder(builder =>
            builder.ConfigureServices((_, services) =>
            {
                services.RemoveAll<IMyService>();
                services.AddSingleton<IMyService>(mockService);
            }));

    var initResult = await factory.TestServer.StartAsync(
        TestContext.Current.CancellationToken
    );

    initResult.InitStatus.Should().Be(InitStatus.InitError);
    initResult.Error.ErrorMessage.Should().Contain("Database unavailable");
}
```

#### OnShutdown Exception Handling

```csharp linenums="1"
[Fact]
public async Task OnShutdown_WhenThrowingException_AggregatesExceptions()
{
    var mockService = Substitute.For<IMyService>();
    mockService.When(x => x.Cleanup()).Do(_ => throw new Exception("Cleanup failed"));

    await using var factory = new LambdaApplicationFactory<Program>()
        .WithHostBuilder(builder =>
            builder.ConfigureServices((_, services) =>
            {
                services.RemoveAll<IMyService>();
                services.AddSingleton<IMyService>(mockService);
            }));

    await factory.TestServer.StartAsync(TestContext.Current.CancellationToken);

    var act = async () => await factory.TestServer.StopAsync(
        TestContext.Current.CancellationToken
    );

    (await act.Should().ThrowAsync<AggregateException>())
        .WithInnerException<Exception>()
        .WithMessage("*Cleanup failed*");
}
```

### Alternative DI Containers

Replace the default DI container with Autofac, DryIoc, or other containers:

```csharp linenums="1"
[Fact]
public async Task WithAutofac_CustomContainerWorks()
{
    await using var factory = new LambdaApplicationFactory<Program>()
        .WithHostBuilder(builder =>
            builder
                .UseServiceProviderFactory(new AutofacServiceProviderFactory())
                .ConfigureContainer<ContainerBuilder>((_, containerBuilder) =>
                {
                    containerBuilder.RegisterType<MyService>().As<IMyService>();
                }));

    var response = await factory.TestServer.InvokeAsync<MyEvent, MyResponse>(
        new MyEvent("test"),
        TestContext.Current.CancellationToken
    );

    response.WasSuccess.Should().BeTrue();
}
```

### Performance and Cold Start Testing

Measure initialization and invocation performance:

```csharp linenums="1"
[Fact]
public async Task ColdStart_InitCompletesWithinTimeout()
{
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));

    await using var factory = new LambdaApplicationFactory<Program>()
        .WithCancellationToken(cts.Token);

    var stopwatch = Stopwatch.StartNew();
    var initResult = await factory.TestServer.StartAsync(cts.Token);
    stopwatch.Stop();

    initResult.InitStatus.Should().Be(InitStatus.InitCompleted);
    stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(3));
}
```

## Best Practices

- **Reuse factories per class** – Creating a new factory per test is fine; reuse within a class to
    speed up suites that share the same host configuration.
- **Runtime headers** – Responses include the same headers Lambda sends (`Lambda-Runtime-*` plus any
    `AdditionalHeaders` you set); assert on them if you need to prove deadline/ARN behavior.
- **Fresh factory per test for lifecycle testing** – When testing OnInit/OnShutdown, create a new
    factory per test so lifecycle hooks run predictably.

!!! warning "Fixture Reuse Pitfalls"

    - Using `IClassFixture`/`ICollectionFixture` with a single `LambdaApplicationFactory` means one
        host instance is shared across all tests in that scope. Avoid this pattern if you need to test
        startup/shutdown logic—use a fresh factory per test so OnInit/OnShutdown run predictably.
    - Do not mix a fixture-based factory with new factories created inside individual tests; they can
        overlap and run simultaneously, leading to multiple hosts executing in parallel and surprising
        side effects. Choose one approach (per-test or shared fixture) for a given test class/collection
        and clean up via `DisposeAsync`/`StopAsync` when done.

## Complete Examples

For comprehensive examples covering all scenarios, see the **[MinimalLambda.Testing test suite](https://github.com/j-d-ha/minimal-lambda/tree/main/tests/MinimalLambda.Testing.UnitTests)**:

- `SimpleLambdaTests.cs` – Basic invocation patterns and concurrent invocations
- `DiLambdaTests.cs` – DI container replacement and lifecycle testing
- `NoEventLambdaTests.cs` – Configuration overrides and handlers without events
- `NoResponseLambdaTests.cs` – Fire-and-forget handlers

The MinimalLambda.Testing source (`src/MinimalLambda.Testing/`) also contains additional examples of
host overrides, cancellation, and error handling patterns.
