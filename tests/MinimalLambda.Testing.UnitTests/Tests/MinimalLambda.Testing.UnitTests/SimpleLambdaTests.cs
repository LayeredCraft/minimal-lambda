using Microsoft.Extensions.DependencyInjection;
using MinimalLambda.Options;

namespace MinimalLambda.Testing.UnitTests;

public class SimpleLambdaTests
{
    [Fact]
    public async Task SimpleLambda_ReturnsExpectedValue()
    {
        await using var factory =
            new LambdaApplicationFactory<SimpleLambda>().WithCancellationToken(
                TestContext.Current.CancellationToken);
        var setup = await factory.TestServer.StartAsync(TestContext.Current.CancellationToken);
        setup.InitStatus.Should().Be(InitStatus.InitCompleted);

        var response = await factory.TestServer.InvokeAsync<string, string>(
            "World",
            TestContext.Current.CancellationToken);

        response.WasSuccess.Should().BeTrue();
        response.Should().NotBeNull();
        response.Response.Should().Be("Hello World!");
    }

    [Fact]
    public async Task SimpleLambda_WorksWhenStartIsNotCalled()
    {
        await using var factory =
            new LambdaApplicationFactory<SimpleLambda>().WithCancellationToken(
                TestContext.Current.CancellationToken);

        var response = await factory.TestServer.InvokeAsync<string, string>(
            "World",
            TestContext.Current.CancellationToken);

        response.WasSuccess.Should().BeTrue();
        response.Should().NotBeNull();
        response.Response.Should().Be("Hello World!");
    }

    [Fact]
    public async Task SimpleLambda_WorksWhenInvokeCalledMultipleTimes()
    {
        await using var factory =
            new LambdaApplicationFactory<SimpleLambda>().WithCancellationToken(
                TestContext.Current.CancellationToken);

        // Launch 5 concurrent invocations
        var tasks = Enumerable
            .Range(1, 5)
            .Select(i => factory.TestServer.InvokeAsync<string, string>(
                $"User{i}",
                TestContext.Current.CancellationToken))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        responses.Should().AllSatisfy(r => r.WasSuccess.Should().BeTrue());

        responses
            .Select(r => r.Response)
            .Should()
            .ContainInOrder(
                "Hello User1!",
                "Hello User2!",
                "Hello User3!",
                "Hello User4!",
                "Hello User5!");
    }

    [Fact]
    public async Task SimpleLambda_WorksWhenInvokeCalledMultipleTimes_WithoutStart()
    {
        await using var factory =
            new LambdaApplicationFactory<SimpleLambda>().WithCancellationToken(
                TestContext.Current.CancellationToken);
        await factory.TestServer.StartAsync(TestContext.Current.CancellationToken);

        // Launch 5 concurrent invocations
        var tasks = Enumerable
            .Range(1, 5)
            .Select(i => factory.TestServer.InvokeAsync<string, string>(
                $"User{i}",
                TestContext.Current.CancellationToken))
            .ToArray();

        var responses = await Task.WhenAll(tasks);

        responses.Should().AllSatisfy(r => r.WasSuccess.Should().BeTrue());

        responses
            .Select(r => r.Response)
            .Should()
            .ContainInOrder(
                "Hello User1!",
                "Hello User2!",
                "Hello User3!",
                "Hello User4!",
                "Hello User5!");
    }

    [Fact]
    public async Task SimpleLambda_ReturnsError()
    {
        await using var factory =
            new LambdaApplicationFactory<SimpleLambda>().WithCancellationToken(
                TestContext.Current.CancellationToken);

        var response = await factory.TestServer.InvokeAsync<string, string>(
            "",
            TestContext.Current.CancellationToken);

        response.Should().NotBeNull();
        response.WasSuccess.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Error.ErrorMessage.Should().Be("Name is required");

        await factory.TestServer.DisposeAsync();
    }

    [Fact]
    public async Task SimpleLambda_ErrorsArePropagated()
    {
        await using var factory = new LambdaApplicationFactory<SimpleLambda>()
            .WithCancellationToken(TestContext.Current.CancellationToken)
            .WithHostBuilder(builder =>
            {
                builder.ConfigureServices((_, services) =>
                {
                    services.Configure<LambdaHostOptions>(options =>
                    {
                        options.BootstrapOptions.RuntimeApiEndpoint = "http://localhost:3002";
                    });
                });
            });

        var act = async () =>
            // ReSharper disable once AccessToDisposedClosure
            await factory.TestServer.StartAsync(TestContext.Current.CancellationToken);

        await act
            .Should()
            .ThrowAsync<InvalidOperationException>()
            .WithMessage(
                "Unexpected request received from the Lambda HTTP handler: GET http://http//localhost:3002/2018-06-01/runtime/invocation/next");
    }

    [Fact]
    public async Task SimpleLambda_WithPreCanceledToken_CancelsInvocation()
    {
        await using var factory =
            new LambdaApplicationFactory<SimpleLambda>().WithCancellationToken(
                TestContext.Current.CancellationToken);
        await factory.TestServer.StartAsync(TestContext.Current.CancellationToken);

        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // ReSharper disable AccessToDisposedClosure
        var act = () => factory.TestServer.InvokeAsync<string, string>("Jonas", cts.Token);
        // ReSharper restore AccessToDisposedClosure

        await act.Should().ThrowAsync<TaskCanceledException>();
    }
}
