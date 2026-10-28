using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalLambda;
using MinimalLambda.Builder;
using MinimalLambda.Options;

var builder = LambdaApplication.CreateBuilder();
builder.Services.AddLambdaSerializerWithContext<EventProcessorJsonContext>();
builder.Services.ConfigureEnvelopeOptions(options =>
    options.JsonOptions.TypeInfoResolver = EventProcessorJsonContext.Default);
builder.Services.AddScoped<RecordHandler>();
builder.Services.Configure<EventHandlerOptions>(options => options.MaxConcurrentExecutions = 5);

await using var lambda = builder.Build();
lambda.MapEventHandler(
    split: (SQSEvent batch, ILambdaInvocationContext _) => batch.Records,
    handle: async Task<ImportResult> (
        SQSEvent.SQSMessage item,
        RecordHandler handler,
        IEventProcessingContext processing,
        CancellationToken token) => await handler.HandleAsync(item, processing, token),
    onError: (item, _, _) => new Retry(item.MessageId),
    createResponse: (results, _) => new SQSBatchResponse(
        results
            .OfType<Retry>()
            .Select(result =>
                new SQSBatchResponse.BatchItemFailure { ItemIdentifier = result.MessageId, })
            .ToList()));
await lambda.RunAsync();

internal sealed class RecordHandler
{
    public Task<ImportResult> HandleAsync(
        SQSEvent.SQSMessage item,
        IEventProcessingContext processing,
        CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        _ = processing.Invocation.AwsRequestId;
        return Task.FromResult<ImportResult>(new Success(item.MessageId));
    }
}

internal abstract record ImportResult;

internal sealed record Success(string MessageId) : ImportResult;

internal sealed record Retry(string MessageId) : ImportResult;

[JsonSerializable(typeof(SQSEvent))]
[JsonSerializable(typeof(SQSBatchResponse))]
internal partial class EventProcessorJsonContext : JsonSerializerContext;
