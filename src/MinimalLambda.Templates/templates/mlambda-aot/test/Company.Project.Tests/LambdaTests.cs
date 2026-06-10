using MinimalLambda.Testing;
using Xunit;

public class LambdaTests
{
    [Fact]
    public async Task Handler_ReturnsGreeting()
    {
        await using var factory = new LambdaApplicationFactory<Program>();

        var response = await factory.TestServer.InvokeAsync<GreetingRequest, GreetingResponse>(
            new GreetingRequest("Lambda"));

        Assert.True(response.WasSuccess);
        Assert.Equal("Hello Lambda!", response.Response.Message);
    }
}
