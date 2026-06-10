using MinimalLambda.Testing;
using Xunit;

[assembly:
    LambdaApplicationFactoryContentRoot(
        "bootstrap",
        "../../../../../src/Company.Project",
        "Company.Project.csproj",
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
