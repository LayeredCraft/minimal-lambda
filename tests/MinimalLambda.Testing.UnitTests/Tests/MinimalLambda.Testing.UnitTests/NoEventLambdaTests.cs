using Microsoft.Extensions.Configuration;

namespace MinimalLambda.Testing.UnitTests;

public class NoEventLambdaTests
{
    [Fact]
    public async Task NoEvent_ReturnsExpectedValue()
    {
        await using var factory =
            LambdaTestFactory
                .Create<NoEventLambda>()
                .WithCancellationToken(TestContext.Current.CancellationToken);

        var response =
            await factory.TestServer.InvokeNoEventAsync<NoEventLambdaResponse>(
                TestContext.Current.CancellationToken);

        response.Should().NotBeNull();
        response.WasSuccess.Should().BeTrue();
        response.Response.Should().NotBeNull();
        response.Response.Message.Should().Be("Hello World!");
    }

    [Fact]
    public async Task NoEvent_ConfigurationCanBeOverwritten()
    {
        await using var factory =
            LambdaTestFactory
                .Create<NoEventLambda>()
                .WithCancellationToken(TestContext.Current.CancellationToken)
                .WithHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
                    config.AddInMemoryCollection(
                        new Dictionary<string, string> { ["MESSAGE"] = "Hello Mars!" }!)));

        var response =
            await factory.TestServer.InvokeNoEventAsync<NoEventLambdaResponse>(
                TestContext.Current.CancellationToken);

        response.Should().NotBeNull();
        response.WasSuccess.Should().BeTrue();
        response.Response.Should().NotBeNull();
        response.Response.Message.Should().Be("Hello Mars!");
    }
}
