// This executable roots MinimalLambda assemblies for Native AOT publish analysis.
// It also registers a real event-processor mapping so the generated processor is compiled,
// trimmed, and analyzed rather than discarded as unreachable code.

using System.Text.Json.Serialization;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalLambda;
using MinimalLambda.Builder;
using MinimalLambda.Options;
using MinimalLambda.EventProcessing;

var builder = LambdaApplication.CreateBuilder();

builder.Services.AddLambdaSerializerWithContext<EventProcessorAotJsonContext>();
builder.Services.ConfigureEnvelopeOptions(options =>
    options.JsonOptions.TypeInfoResolver = EventProcessorAotJsonContext.Default);
builder.Services.AddScoped<RecordHandler>();

await using var lambda = builder.Build();

// One mapping site only: reaching a second mapping for the same invocation handler throws.
lambda.MapEventHandler(
    split: (SQSEvent batch, ILambdaInvocationContext _) => batch.Records,
    handle: async
        Task<ImportResult> (
            SQSEvent.SQSMessage item,
            RecordHandler handler,
            CancellationToken token) => await handler.HandleAsync(item, token),
    onError: (item, _, _) => new Retry(item.MessageId),
    createResponse: (outcomes, _) => new SQSBatchResponse(
        outcomes
            .Where(outcome => outcome.Status != EventItemStatus.Succeeded || outcome.Value is Retry)
            .Select(outcome =>
                new SQSBatchResponse.BatchItemFailure { ItemIdentifier = outcome.Item.MessageId, })
            .ToList()));

// Registration alone roots the generated processor. The runtime loop is intentionally not started
// because this project exists for publish analysis, not execution.
return;

internal sealed class RecordHandler
{
    public Task<ImportResult> HandleAsync(SQSEvent.SQSMessage item, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult<ImportResult>(new Success(item.MessageId));
    }
}

internal abstract record ImportResult;

internal sealed record Success(string MessageId) : ImportResult;

internal sealed record Retry(string MessageId) : ImportResult;

[JsonSerializable(typeof(SQSEvent))]
[JsonSerializable(typeof(SQSBatchResponse))]
internal partial class EventProcessorAotJsonContext : JsonSerializerContext;
