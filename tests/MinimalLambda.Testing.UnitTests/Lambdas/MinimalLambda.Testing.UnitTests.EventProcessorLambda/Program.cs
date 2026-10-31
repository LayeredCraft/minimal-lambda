using MinimalLambda.EventProcessing;
using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalLambda;
using MinimalLambda.Builder;
using MinimalLambda.Envelopes;
using MinimalLambda.Envelopes.Sqs;
using MinimalLambda.Options;

var builder = LambdaApplication.CreateBuilder();

builder.Services.AddLambdaSerializerWithContext<EventProcessorLambdaJsonContext>();
builder.Services.AddSingleton<RecordProbe>();
builder.Services.AddScoped<ItemScopeProbe>();
builder.Services.AddKeyedSingleton<string>("primary", "keyed");
builder.Services.ConfigureEnvelopeOptions(options =>
    options.JsonOptions.TypeInfoResolver = EventProcessorLambdaJsonContext.Default);

await using var lambda = builder.Build();

lambda.UseMiddleware(async (context, next) =>
{
    var probe = context.ServiceProvider.GetRequiredService<RecordProbe>();
    probe.MiddlewareEvent = context.GetRequiredEvent<SqsEnvelope<string>>();
    probe.Stages.Enqueue("before");
    await next(context);
    probe.Packed =
        ((IResponseFeature<BatchResponse>)context.Features.Get<IResponseFeature>()!).GetResponse()!
        .Packed;
    probe.Stages.Enqueue("after");
});

lambda.MapEventHandler(
    split: (SqsEnvelope<string> batch, ILambdaInvocationContext context) =>
    {
        var probe = context.ServiceProvider.GetRequiredService<RecordProbe>();
        probe.SplitEvent = batch;
        probe.Stages.Enqueue("split");
        return batch.Records;
    },
    handle: async Task<ImportResult> (
        SqsEnvelope<string>.SqsMessageEnvelope item,
        RecordProbe probe,
        ItemScopeProbe scope,
        [FromKeyedServices("primary")] string keyed,
        OptionalMarker? optional,
        IEventProcessingContext processing,
        CancellationToken token) =>
    {
        token.ThrowIfCancellationRequested();
        if (keyed != "keyed" || optional is not null || processing.Invocation is null)
            throw new InvalidOperationException("incorrect argument binding");
        probe.ScopeIds.Enqueue(scope.Id);
        probe.Stages.Enqueue("handle");
        probe.Record(item.MessageId);

        if (item.BodyContent is "stop")
        {
            processing.StopProcessing();
            return new Success(item.MessageId);
        }

        if (item.BodyContent is "retry" or "fatal")
            throw new InvalidOperationException("retry requested");

        await Task.Yield();
        return new Success(item.MessageId);
    },
    onError: (item, _, _) =>
        item.BodyContent == "fatal"
            ? throw new InvalidOperationException("mapper failed")
            : new Retry(item.MessageId),
    createResponse: (outcomes, context) =>
    {
        context.ServiceProvider.GetRequiredService<RecordProbe>().Stages.Enqueue("response");
        return new BatchResponse
        {
            BatchItemFailures = outcomes
                .Where(outcome =>
                    outcome.Status is EventItemStatus.MappedError
                        or EventItemStatus.Skipped
                        or EventItemStatus.Cancelled)
                .Select(outcome =>
                    new SQSBatchResponse.BatchItemFailure
                    {
                        ItemIdentifier = outcome.Item.MessageId
                    })
                .ToList(),
        };
    });

await lambda.RunAsync();

internal abstract record ImportResult;

internal sealed record Success(string MessageId) : ImportResult;

internal sealed record Retry(string MessageId) : ImportResult;

internal sealed class RecordProbe
{
    private readonly ConcurrentQueue<string> messageIds = new();
    public SqsEnvelope<string>? MiddlewareEvent { get; set; }
    public SqsEnvelope<string>? SplitEvent { get; set; }
    public ConcurrentQueue<string> Stages { get; } = new();
    public ConcurrentQueue<int> ScopeIds { get; } = new();
    public bool Packed { get; set; }
    public bool ThrowOnDispose { get; set; }
    public int Disposed;

    public IReadOnlyCollection<string> MessageIds => messageIds.ToArray();

    public void Record(string messageId) => messageIds.Enqueue(messageId);
}

public class EventProcessorLambda;

internal sealed class OptionalMarker;

internal sealed class ItemScopeProbe(RecordProbe probe) : IAsyncDisposable
{
    private static int nextId;
    public int Id { get; } = Interlocked.Increment(ref nextId);

    public async ValueTask DisposeAsync()
    {
        await Task.Yield();
        Interlocked.Increment(ref probe.Disposed);
        if (probe.ThrowOnDispose)
            throw new InvalidOperationException("scope cleanup failed");
    }
}

internal sealed class BatchResponse : SQSBatchResponse, IResponseEnvelope
{
    [JsonIgnore]
    public bool Packed { get; private set; }

    public void PackPayload(EnvelopeOptions options) => Packed = true;
}

[JsonSerializable(typeof(SqsEnvelope<string>))]
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(BatchResponse))]
[JsonSerializable(typeof(SQSBatchResponse))]
internal partial class EventProcessorLambdaJsonContext : JsonSerializerContext;
