using System.Text.Json;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using MinimalLambda.EventProcessing;

namespace MinimalLambda.Testing.UnitTests;

public class EventProcessorLambdaTests
{
    // The default server function timeout is AWS Lambda's 3 seconds, which is too tight when the
    // whole solution runs all test assemblies in parallel. The processor behaviour under test is
    // not the deadline, so give every invocation headroom inside the shared test server.
    private static readonly TimeSpan TestFunctionTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task EventProcessorLambda_BuildsPartialBatchResponseInSourceOrder()
    {
        // Arrange
        await using var factory =
            LambdaTestFactory
                .Create<EventProcessorLambda>()
                .WithCancellationToken(TestContext.Current.CancellationToken);
        factory.ServerOptions.FunctionTimeout = TestFunctionTimeout;
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
        factory.ServerOptions.FunctionTimeout = TestFunctionTimeout;
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
        factory.ServerOptions.FunctionTimeout = TestFunctionTimeout;

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
        factory.ServerOptions.FunctionTimeout = TestFunctionTimeout;
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

    [Fact]
    public async Task EventProcessorLambda_SerializesFailureResponseWithMessageIdsOnly()
    {
        // Arrange
        await using var factory =
            LambdaTestFactory
                .Create<EventProcessorLambda>()
                .WithCancellationToken(TestContext.Current.CancellationToken);
        factory.ServerOptions.FunctionTimeout = TestFunctionTimeout;
        var request = CreateEvent(("id-1", "retry"), ("id-2", "ok"), ("id-3", "retry"));

        // Act
        var response = await factory.TestServer.InvokeAsync<SQSEvent, SQSBatchResponse>(
            request,
            TestContext.Current.CancellationToken);

        // Assert
        response.WasSuccess.Should().BeTrue();
        response.Response.Should().NotBeNull();

        var json = JsonSerializer.Serialize(
            response.Response!,
            EventProcessorLambdaJsonContext.Default.SQSBatchResponse);
        json
            .Should()
            .Be("""{"batchItemFailures":[{"itemIdentifier":"id-1"},{"itemIdentifier":"id-3"}]}""");
        json.Should().NotContain("receipt-");
        json.Should().NotContain("retry");
    }

    [Fact]
    public async Task EventProcessorLambda_SequentialStopReportsUnprocessedRecordsAsFailures()
    {
        // Arrange
        await using var factory = LambdaTestFactory
            .Create<EventProcessorLambda>()
            .WithCancellationToken(TestContext.Current.CancellationToken)
            .WithHostBuilder(builder => builder.ConfigureServices((_, services) =>
                services.Configure<EventHandlerOptions>(options =>
                    options.EnableConcurrency = false)));
        factory.ServerOptions.FunctionTimeout = TestFunctionTimeout;
        var request = CreateEvent(
            ("id-1", "retry"),
            ("id-2", "stop"),
            ("id-3", "retry"),
            ("id-4", "retry"));

        // Act
        var response = await factory.TestServer.InvokeAsync<SQSEvent, SQSBatchResponse>(
            request,
            TestContext.Current.CancellationToken);

        // Assert
        response.WasSuccess.Should().BeTrue();
        response.Response.Should().NotBeNull();

        // The application retries every failed or unstarted source position.
        response
            .Response
            .BatchItemFailures
            .Select(failure => failure.ItemIdentifier)
            .Should()
            .Equal("id-1", "id-3", "id-4");
        factory
            .Services
            .GetRequiredService<RecordProbe>()
            .MessageIds
            .Should()
            .Equal("id-1", "id-2");
    }

    [Fact]
    public async Task EventProcessorLambda_SharesFullEventAndPacksResponseThroughMiddleware()
    {
        await using var factory =
            LambdaTestFactory
                .Create<EventProcessorLambda>()
                .WithCancellationToken(TestContext.Current.CancellationToken);
        factory.ServerOptions.FunctionTimeout = TestFunctionTimeout;
        var response = await factory.TestServer.InvokeAsync<SQSEvent, SQSBatchResponse>(
            CreateEvent(("id-1", "ok"), ("id-2", "ok")),
            TestContext.Current.CancellationToken);
        response.WasSuccess.Should().BeTrue();
        var probe = factory.Services.GetRequiredService<RecordProbe>();
        probe.MiddlewareEvent.Should().BeSameAs(probe.SplitEvent);
        probe.SplitEvent!.Records.Select(item => item.BodyContent).Should().Equal("ok", "ok");
        probe.ScopeIds.Should().OnlyHaveUniqueItems().And.HaveCount(2);
        probe.Disposed.Should().Be(2);
        probe.Packed.Should().BeTrue();
        probe.Stages.Should().Equal("before", "split", "handle", "handle", "response", "after");
    }

    [Fact]
    public async Task EventProcessorLambda_DisposalFailureSuppressesSerializedResponse()
    {
        await using var factory =
            LambdaTestFactory
                .Create<EventProcessorLambda>()
                .WithCancellationToken(TestContext.Current.CancellationToken);
        factory.ServerOptions.FunctionTimeout = TestFunctionTimeout;
        factory.Services.GetRequiredService<RecordProbe>().ThrowOnDispose = true;
        var response = await factory.TestServer.InvokeAsync<SQSEvent, SQSBatchResponse>(
            CreateEvent(("id-1", "ok")),
            TestContext.Current.CancellationToken);
        response.WasSuccess.Should().BeFalse();
        response.Error.Should().NotBeNull();
        factory.Services.GetRequiredService<RecordProbe>().Stages.Should().NotContain("response");
    }

    [Fact]
    public async Task EventProcessorLambda_MapsItemResolutionFailuresIntoSerializedResponse()
    {
        await using var factory =
            LambdaTestFactory
                .Create<EventProcessorLambda>()
                .WithCancellationToken(TestContext.Current.CancellationToken)
                .WithHostBuilder(builder => builder.ConfigureServices((_, services) =>
                    services.AddScoped<ItemScopeProbe>(_ =>
                        throw new InvalidOperationException("item resolution"))));
        factory.ServerOptions.FunctionTimeout = TestFunctionTimeout;
        var response = await factory.TestServer.InvokeAsync<SQSEvent, SQSBatchResponse>(
            CreateEvent(("id-1", "ok"), ("id-2", "ok")),
            TestContext.Current.CancellationToken);
        response.WasSuccess.Should().BeTrue();
        response.Response!
            .BatchItemFailures
            .Select(item => item.ItemIdentifier)
            .Should()
            .Equal("id-1", "id-2");
    }

    private static SQSEvent CreateEvent(params (string MessageId, string Body)[] records) =>
        new()
        {
            Records =
            [
                .. records.Select(record => new SQSEvent.SQSMessage
                {
                    MessageId = record.MessageId,
                    ReceiptHandle = "receipt-" + record.MessageId,
                    Body = JsonSerializer.Serialize(record.Body),
                }),
            ],
        };
}
