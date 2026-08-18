using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

// ReSharper disable AccessToDisposedClosure

namespace MinimalLambda.Testing.UnitTests;

public class NoResponseLambdaTests
{
    [Fact]
    public async Task NoResponseLambda_ReturnsExpectedValue()
    {
        await using var factory =
            LambdaTestFactory
                .Create<NoResponseLambda>()
                .WithCancellationToken(TestContext.Current.CancellationToken);

        var response = await factory.TestServer.InvokeNoResponseAsync(
            new NoResponseLambdaRequest("World"),
            TestContext.Current.CancellationToken);

        response.WasSuccess.Should().BeTrue();
        response.Should().NotBeNull();
    }

    [Fact]
    public async Task NoResponseLambda_ServicesIsAccessible()
    {
        await using var factory =
            LambdaTestFactory
                .Create<NoResponseLambda>()
                .WithCancellationToken(TestContext.Current.CancellationToken);

        var act = () => factory.TestServer.Services.GetRequiredService<IHostApplicationLifetime>();

        act.Should().NotThrow();
    }

    [Fact]
    public async Task NoResponseLambda_DisposeCanBeCalledMultipleTimes()
    {
        await using var factory =
            LambdaTestFactory
                .Create<NoResponseLambda>()
                .WithCancellationToken(TestContext.Current.CancellationToken);

        var act = async () =>
        {
            await factory.TestServer.DisposeAsync();
            await factory.TestServer.DisposeAsync();
        };

        await act.Should().NotThrowAsync();
    }
}
