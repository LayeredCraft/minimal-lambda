using MinimalLambda.EventProcessing;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalLambda;
using MinimalLambda.Builder;
using MinimalLambda.Envelopes.Sqs;
using MinimalLambda.Options;
using System.Text.Json.Serialization;

var builder = LambdaApplication.CreateBuilder();

builder.Services.AddLambdaSerializerWithContext<EventProcessorJsonContext>();
builder.Services.ConfigureEnvelopeOptions(options =>
    options.JsonOptions.TypeInfoResolver = EventProcessorJsonContext.Default);
builder.Services.AddScoped<DocumentImporter>();
builder.Services.Configure<EventHandlerOptions>(options => options.MaxConcurrentExecutions = 5);

await using var lambda = builder.Build();

// The sample registers exactly one processor per run because MapEventHandler consumes the
// single invocation handler slot. Select a family with EVENT_PROCESSOR_SAMPLE.
switch (Environment.GetEnvironmentVariable("EVENT_PROCESSOR_SAMPLE"))
{
    case "n1":
        RegisterNoResult(lambda);
        break;
    case "n2":
        RegisterNoResultWithResponse(lambda);
        break;
    case "n3":
        RegisterNoResultWithFailures(lambda);
        break;
    case "r1":
        RegisterResults(lambda);
        break;
    default:
        RegisterResultsWithRecovery(lambda);
        break;
}

// Registration alone roots the generated processor. This smoke path proves the Native AOT image
// boots, registers the processor, and validates options without a Lambda runtime present.
if (Environment.GetEnvironmentVariable("EVENT_PROCESSOR_SMOKE") == "1")
{
    Console.WriteLine("event processor registered");
    return;
}

await lambda.RunAsync();

// N1: no per-item value and no response. An unhandled item error fails the invocation.
static void RegisterNoResult(LambdaApplication lambda) =>
    lambda.MapEventHandler(
        split: (SqsEnvelope<MyEvent> batch, ILambdaInvocationContext _) => batch.Records,
        handle: (
            SqsEnvelope<MyEvent>.SqsMessageEnvelope item,
            DocumentImporter importer,
            CancellationToken token) =>
        {
            if (item.BodyContent is null)
                return Task.CompletedTask;

            return importer.ImportAsync(item.BodyContent, token);
        });

// N2: every source position has an outcome; count successful handling.
static void RegisterNoResultWithResponse(LambdaApplication lambda) =>
    lambda.MapEventHandler(
        split: (ImportBatch batch, ILambdaInvocationContext _) => batch.Items,
        handle: (ImportDocument item, DocumentImporter importer, CancellationToken token) =>
            importer.ImportAsync(item.Event, token),
        createResponse: (outcomes, _) =>
            new BatchSummary(
                outcomes.Count(outcome => outcome.Status == EventItemStatus.Succeeded)));

// N3: only mapped errors have values; every source position still has an outcome.
static void RegisterNoResultWithFailures(LambdaApplication lambda) =>
    lambda.MapEventHandler(
        split: (SqsEnvelope<MyEvent> batch, ILambdaInvocationContext _) => batch.Records,
        handle: (
                SqsEnvelope<MyEvent>.SqsMessageEnvelope item,
                DocumentImporter importer,
                CancellationToken token) =>
            item.BodyContent is null
                ? Task.CompletedTask
                : importer.ImportAsync(item.BodyContent, token),
        onError: (item, _, _) =>
            new SQSBatchResponse.BatchItemFailure { ItemIdentifier = item.MessageId, },
        createResponse: (outcomes, _) => new SQSBatchResponse(
            outcomes
                .Where(outcome => outcome.Status != EventItemStatus.Succeeded)
                .Select(outcome =>
                    new SQSBatchResponse.BatchItemFailure
                    {
                        ItemIdentifier = outcome.Item.MessageId
                    })
                .ToList()));

// R1: every completed item contributes one TResult; any unhandled error prevents the response.
// A typed delegate local supplies dependency types that a bare method group cannot infer.
static void RegisterResults(LambdaApplication lambda)
{
    Func<ImportDocument, DocumentImporter, CancellationToken, Task<ImportItemResult>> handle =
        EventHandlers.HandleImportAsync;

    lambda.MapEventHandler(
        split: (ImportBatch batch, ILambdaInvocationContext _) => batch.Items,
        handle: handle,
        createResponse: (outcomes, _) => new BatchSummary(
            outcomes
                .Where(outcome => outcome.HasValue)
                .Select(outcome => outcome.Value!)
                .Count(result => result is Success)));
}

// R2: SQS partial batch response. An async lambda block makes the result family unambiguous.
// Include unstarted and cancelled items in the application retry response.
static void RegisterResultsWithRecovery(LambdaApplication lambda) =>
    lambda.MapEventHandler(
        split: (SqsEnvelope<MyEvent> batch, ILambdaInvocationContext _) => batch.Records,
        handle: async Task<ImportItemResult> (
                SqsEnvelope<MyEvent>.SqsMessageEnvelope item,
                DocumentImporter importer,
                IEventProcessingContext processing,
                CancellationToken token) =>
            await EventHandlers.HandleSqsMessageAsync(item, importer, processing, token),
        onError: (item, _, _) => new Retry(item.MessageId),
        createResponse: (outcomes, _) => new SQSBatchResponse(
            outcomes
                .Where(outcome =>
                    outcome.Status != EventItemStatus.Succeeded || outcome.Value is Retry)
                .Select(outcome =>
                    new SQSBatchResponse.BatchItemFailure
                    {
                        ItemIdentifier = outcome.Item.MessageId,
                    })
                .ToList()));

public sealed record MyEvent(string Message, bool StopProcessing = false);

public sealed record ImportBatch(IReadOnlyList<ImportDocument> Items);

public sealed record ImportDocument(MyEvent Event);

public sealed record BatchSummary(int Count);

public abstract record ImportItemResult;

public sealed record Success(string MessageId) : ImportItemResult;

public sealed record Retry(string MessageId) : ImportItemResult;

public sealed class DocumentImporter
{
    public Task ImportAsync(MyEvent message, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    public Task<ImportItemResult> ImportResultAsync(MyEvent message, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        return Task.FromResult<ImportItemResult>(new Success(message.Message));
    }
}

public static class EventHandlers
{
    public static Task<ImportItemResult> HandleImportAsync(
        ImportDocument item,
        DocumentImporter importer,
        CancellationToken token) =>
        importer.ImportResultAsync(item.Event, token);

    public static async Task<ImportItemResult> HandleSqsMessageAsync(
        SqsEnvelope<MyEvent>.SqsMessageEnvelope item,
        DocumentImporter importer,
        IEventProcessingContext processing,
        CancellationToken token)
    {
        if (item.BodyContent is null)
            return new Retry(item.MessageId);

        if (item.BodyContent.StopProcessing)
            processing.StopProcessing();

        return await importer.ImportResultAsync(item.BodyContent, token);
    }
}

[JsonSerializable(typeof(SqsEnvelope<MyEvent>))]
[JsonSerializable(typeof(SQSBatchResponse))]
[JsonSerializable(typeof(MyEvent))]
internal partial class EventProcessorJsonContext : JsonSerializerContext;

// Required so the generated test project can reference this top-level Program.
public partial class Program;
