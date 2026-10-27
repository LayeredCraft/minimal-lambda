# Event processor API proposal

**Status:** Draft  
**Scope:** Public API only

## Context

Lambda receives one event that may contain many independently processable records. SQS is the motivating case: each message may succeed or need retry, while Lambda expects one batch response containing the failed message IDs.

`MapHandler` handles one Lambda invocation. This API adds a batch layer that splits the event, invokes one handler per item, collects results, and optionally creates the invocation response.

## Goal

Process each item in a Lambda event batch, optionally in parallel, and let an event-source-specific callback build the final Lambda response.

The processor must not know SQS, Kinesis, DynamoDB Streams, or a response format.

## API roles

| Callback | Job |
| --- | --- |
| `split` | Turns the incoming event into source items. |
| `handle` | Processes one source item with injected services. |
| `onError` | Converts an item exception into the same result type as `handle`. |
| `createResponse` | Converts all source items and results into the Lambda response. |

The response builder receives the original source item. It can use source-specific metadata, such as an SQS `MessageId`, without making the processor source-specific.

## Proposed API

```csharp
lambda.MapEventHandler(
    split: (SqsEnvelope<ImportDocument> batch, ILambdaInvocationContext _) =>
        batch.Records,

    handle: async (
        SqsEnvelope<ImportDocument>.SqsMessageEnvelope item,
        DocumentImporter importer) =>
    {
        await importer.ImportAsync(item.BodyContent!);
        return EventOutcome.Success;
    },

    onError: (item, exception, ILambdaInvocationContext _) =>
        EventOutcome.Retry,

    createResponse: (result, ILambdaInvocationContext _) =>
        new SQSBatchResponse(
            result.Items
                .Where(x => x.Result == EventOutcome.Retry)
                .Select(x => new SQSBatchResponse.BatchItemFailure
                {
                    ItemIdentifier = x.Item.MessageId,
                })
                .ToList()));
```

For event sources that do not return a batch response:

```csharp
lambda.MapEventHandler(
    split: (S3Event batch, ILambdaInvocationContext _) => batch.Records,
    handle: async (S3Event.S3EventNotificationRecord item, DocumentIndexer indexer) =>
    {
        await indexer.IndexAsync(item.S3.Object.Key);
    });
```

## Type flow

```text
TEvent -> IEnumerable<TItem> -> TItemResult -> TResponse
```

```csharp
Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split

Func<TItem, TDependency1, ..., Task<TItemResult>> handle

Func<TItem, Exception, ILambdaInvocationContext, TItemResult> onError

Func<EventBatchResult<TItem, TItemResult>,
     ILambdaInvocationContext,
     TResponse> createResponse
```

`TItem` is always the first handler parameter. This keeps the processed source record available to `createResponse`, including SQS `MessageId` and stream sequence numbers.

`onError` returns the same `TItemResult` as `handle`. The response builder therefore consumes one result type.

```csharp
public sealed record EventItemResult<TItem, TItemResult>(
    TItem Item,
    TItemResult Result);

public sealed class EventBatchResult<TItem, TItemResult>
{
    public required IReadOnlyList<EventItemResult<TItem, TItemResult>> Items { get; init; }
}
```

## Overloads

Provide handler overloads for zero through ten injected dependencies. The only changing part is the handler delegate:

```csharp
Func<TItem, Task>
Func<TItem, TDependency1, Task>
Func<TItem, TDependency1, TDependency2, Task>
```

Use separate overloads when `onError` is present. Do not require callers to pass `null`.

```csharp
MapEventHandler(split, handle, createResponse)
MapEventHandler(split, handle, onError, createResponse)
```

## Boundaries

This proposal defines callback signatures and type relationships. It does not yet define:

- how generated code resolves handler dependencies;
- concurrency, cancellation, or item-service scopes;
- logging, metrics, or exception policy;
- event-source configuration such as SQS `ReportBatchItemFailures`.

## Static methods

An inline lambda can infer dependency types. A static method group cannot infer generic dependency parameters from this API:

```csharp
lambda.MapEventHandler(split, EventHandlers.HandleAsync);
```

Static handlers need explicit generic arguments or a typed local delegate. Decide whether that trade-off is acceptable before committing to generic dependency overloads.

## Not decided

- `Task` only, or `ValueTask` and synchronous handler overloads.
- Whether `onError` can be asynchronous or receive injected dependencies.
- Default behavior when `onError` is omitted.
- FIFO ordering and stop-on-failure behavior.
- Global and named `EventHandlerOptions` registration through DI.
