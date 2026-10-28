using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalLambda;
using MinimalLambda.Builder;

var builder = LambdaApplication.CreateBuilder();

builder.Services.AddLambdaSerializerWithContext<EventProcessorLambdaJsonContext>();
builder.Services.AddSingleton<RecordProbe>();

await using var lambda = builder.Build();

lambda.MapEventHandler(
    split: (SQSEvent batch, ILambdaInvocationContext _) => batch.Records,
    handle: async Task<ImportResult> (
        SQSEvent.SQSMessage item,
        RecordProbe probe,
        IEventProcessingContext processing,
        CancellationToken token) =>
    {
        token.ThrowIfCancellationRequested();
        probe.Record(item.MessageId);

        if (item.Body is "stop")
        {
            processing.StopProcessing();
            return new Success(item.MessageId);
        }

        if (item.Body is "retry" or "fatal")
            throw new InvalidOperationException("retry requested");

        await Task.Yield();
        return new Success(item.MessageId);
    },
    onError: (item, _, _) =>
        item.Body == "fatal"
            ? throw new InvalidOperationException("mapper failed")
            : new Retry(item.MessageId),
    createResponse: (results, _) => new SQSBatchResponse(
        results
            .OfType<Retry>()
            .Select(result =>
                new SQSBatchResponse.BatchItemFailure { ItemIdentifier = result.MessageId, })
            .ToList()));

await lambda.RunAsync();

internal abstract record ImportResult;

internal sealed record Success(string MessageId) : ImportResult;

internal sealed record Retry(string MessageId) : ImportResult;

internal sealed class RecordProbe
{
    private readonly ConcurrentQueue<string> messageIds = new();

    public IReadOnlyCollection<string> MessageIds => messageIds.ToArray();

    public void Record(string messageId) => messageIds.Enqueue(messageId);
}

public class EventProcessorLambda;

[JsonSerializable(typeof(SQSEvent))]
[JsonSerializable(typeof(SQSBatchResponse))]
internal partial class EventProcessorLambdaJsonContext : JsonSerializerContext;
