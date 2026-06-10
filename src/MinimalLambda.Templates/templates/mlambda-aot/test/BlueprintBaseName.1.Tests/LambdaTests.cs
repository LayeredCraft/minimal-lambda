using MinimalLambda.Testing;
using Xunit;

[assembly:
    LambdaApplicationFactoryContentRoot(
        "BlueprintBaseName.1",
        "../../../../../src/BlueprintBaseName.1",
        "BlueprintBaseName.1.csproj",
        "0")]

public class LambdaTests
{
    [Fact]
    public async Task Handler_ReturnsGreeting()
    {
        await using var factory = new LambdaApplicationFactory<Program>();

        var response =
            await factory.TestServer.InvokeAsync<GreetingRequest, GreetingResponse>(
                new GreetingRequest("Lambda"));

        Assert.True(response.WasSuccess);
        Assert.NotNull(response.Response);
        Assert.Equal("Hello Lambda!", response.Response.Message);
    }
}
