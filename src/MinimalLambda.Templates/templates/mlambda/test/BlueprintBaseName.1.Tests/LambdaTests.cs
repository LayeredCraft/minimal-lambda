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
    public async Task Handler_ReturnsUppercaseInput()
    {
        await using var factory = new LambdaApplicationFactory<Program>();

        var response = await factory.TestServer.InvokeAsync<string, string>("hello world");

        Assert.True(response.WasSuccess);
        Assert.Equal("HELLO WORLD", response.Response);
    }
}
