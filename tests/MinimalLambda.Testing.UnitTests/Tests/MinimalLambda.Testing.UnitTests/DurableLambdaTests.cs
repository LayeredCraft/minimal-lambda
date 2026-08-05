#if NET10_0_OR_GREATER
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace MinimalLambda.Testing.UnitTests;

public class DurableLambdaTests
{
    private const string ExecutionArn =
        "arn:aws:lambda:us-east-1:123456789012:durable-execution:ml-ses";

    [Fact]
    public async Task DurableLambda_Success_RoundTripsGeneratedAdapterPipeline()
    {
        // Arrange
        await using var factory =
            new LambdaApplicationFactory<DurableLambda>().WithCancellationToken(
                TestContext.Current.CancellationToken);
        var input = CreateInvocationInput(shouldFail: false);

        // Act
        var response = await factory.TestServer.InvokeAsync<JsonElement, JsonElement>(
            input,
            TestContext.Current.CancellationToken);

        // Assert
        response.WasSuccess.Should().BeTrue();
        response.Error.Should().BeNull();
        var output = response.Response;
        output.GetProperty("Status").GetString().Should().Be("SUCCEEDED");
        output.GetProperty("Result").ValueKind.Should().Be(JsonValueKind.String);

        using var resultDocument = JsonDocument.Parse(output.GetProperty("Result").GetString()!);
        var result = resultDocument.RootElement;
        result.GetProperty("Message").GetString().Should().Be("Hello World!");
        result.GetProperty("ExecutionArn").GetString().Should().Be(ExecutionArn);
        result.GetProperty("MiddlewareEntered").GetBoolean().Should().BeTrue();
        result.GetProperty("SerializerIdentityPreserved").GetBoolean().Should().BeTrue();

        AssertMiddlewareProbe(factory);
    }

    [Fact]
    public async Task DurableLambda_Failure_ReturnsFailedOuterEnvelope()
    {
        // Arrange
        await using var factory =
            new LambdaApplicationFactory<DurableLambda>().WithCancellationToken(
                TestContext.Current.CancellationToken);
        var input = CreateInvocationInput(shouldFail: true);

        // Act
        var response = await factory.TestServer.InvokeAsync<JsonElement, JsonElement>(
            input,
            TestContext.Current.CancellationToken);

        // Assert
        response.WasSuccess.Should().BeTrue();
        response.Error.Should().BeNull();
        var output = response.Response;
        output.GetProperty("Status").GetString().Should().Be("FAILED");
        var error = output.GetProperty("Error");
        error.GetProperty("ErrorType").GetString().Should().Be("System.InvalidOperationException");
        error.GetProperty("ErrorMessage").GetString().Should().Be("durable fixture failure");

        AssertMiddlewareProbe(factory);
    }

    private static JsonElement CreateInvocationInput(bool shouldFail)
    {
        var inputPayload = JsonSerializer.Serialize(new { name = "World", shouldFail });
        var envelope = JsonSerializer.Serialize(
            new
            {
                DurableExecutionArn = ExecutionArn,
                CheckpointToken = "checkpoint-token",
                InitialExecutionState = new
                {
                    Operations = new[]
                    {
                        new
                        {
                            Id = "execution-0",
                            Type = "EXECUTION",
                            Status = "STARTED",
                            ExecutionDetails = new { InputPayload = inputPayload }
                        }
                    }
                }
            });

        using var document = JsonDocument.Parse(envelope);
        return document.RootElement.Clone();
    }

    private static void AssertMiddlewareProbe(LambdaApplicationFactory<DurableLambda> factory)
    {
        var probe = factory.TestServer.Services.GetRequiredService<DurableMiddlewareProbe>();
        probe.BeforeCount.Should().Be(1);
        probe.AfterCount.Should().Be(1);
        probe.Events.Should().Equal("before", "after");
    }
}
#endif
