using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;

namespace MinimalLambda.Testing.UnitTests;

public class EventProcessorLambdaTests
{
    [Fact]
    public async Task EventProcessorLambda_BuildsPartialBatchResponseInSourceOrder()
    {
        // Arrange
        await using var factory =
            LambdaTestFactory
                .Create<EventProcessorLambda>()
                .WithCancellationToken(TestContext.Current.CancellationToken);
        var request = CreateEvent(("id-1", "retry"), ("id-2", "ok"), ("id-3", "retry"));

        // Act
        var response = await factory.TestServer.InvokeAsync<SQSEvent, SQSBatchResponse>(
            request,
            TestContext.Current.CancellationToken);

        // Assert
        response.Error.Should().BeNull();
        response.WasSuccess.Should().BeTrue();
        response.Response.Should().NotBeNull();
        response
            .Response
            .BatchItemFailures
            .Select(failure => failure.ItemIdentifier)
            .Should()
            .Equal("id-1", "id-3");
    }

    [Fact]
    public async Task EventProcessorLambda_ProcessesEveryRecordThroughItsOwnScope()
    {
        // Arrange
        await using var factory =
            LambdaTestFactory
                .Create<EventProcessorLambda>()
                .WithCancellationToken(TestContext.Current.CancellationToken);
        var request = CreateEvent(("id-1", "ok"), ("id-2", "ok"), ("id-3", "ok"));

        // Act
        await factory.TestServer.InvokeAsync<SQSEvent, SQSBatchResponse>(
            request,
            TestContext.Current.CancellationToken);

        // Assert
        factory
            .Services
            .GetRequiredService<RecordProbe>()
            .MessageIds
            .Should()
            .BeEquivalentTo("id-1", "id-2", "id-3");
    }

    [Fact]
    public async Task EventProcessorLambda_EmptyEventStillBuildsEmptyResponse()
    {
        // Arrange
        await using var factory =
            LambdaTestFactory
                .Create<EventProcessorLambda>()
                .WithCancellationToken(TestContext.Current.CancellationToken);

        // Act
        var response = await factory.TestServer.InvokeAsync<SQSEvent, SQSBatchResponse>(
            CreateEvent(),
            TestContext.Current.CancellationToken);

        // Assert
        response.WasSuccess.Should().BeTrue();
        response.Response.Should().NotBeNull();
        response.Response.BatchItemFailures.Should().BeEmpty();
    }

    [Fact]
    public async Task EventProcessorLambda_ErrorMapperFailureFailsInvocationWithoutResponse()
    {
        // Arrange
        await using var factory =
            LambdaTestFactory
                .Create<EventProcessorLambda>()
                .WithCancellationToken(TestContext.Current.CancellationToken);
        var request = CreateEvent(("id-1", "fatal"));

        // Act
        var response = await factory.TestServer.InvokeAsync<SQSEvent, SQSBatchResponse>(
            request,
            TestContext.Current.CancellationToken);

        // Assert
        response.WasSuccess.Should().BeFalse();
        response.Error.Should().NotBeNull();
        response.Response.Should().BeNull();
    }

    private static SQSEvent CreateEvent(params (string MessageId, string Body)[] records) =>
        new()
        {
            Records =
            [
                .. records.Select(record => new SQSEvent.SQSMessage
                {
                    MessageId = record.MessageId, Body = record.Body,
                }),
            ],
        };
}
