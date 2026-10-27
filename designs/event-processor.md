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
| `onError` | Optionally converts an item exception into a value. |
| `createResponse` | Converts collected values into the Lambda response. |

`onError` receives the source item. It can use source-specific metadata, such as an SQS `MessageId`, before returning a value for `createResponse`.

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
    },

    onError: (item, exception, ILambdaInvocationContext _) =>
        new SQSBatchResponse.BatchItemFailure
        {
            ItemIdentifier = item.MessageId,
        },

    createResponse: (failures, ILambdaInvocationContext _) =>
        new SQSBatchResponse(failures.ToList()));
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

For a response after every item succeeds:

```csharp
lambda.MapEventHandler(
    split: (S3Event batch, ILambdaInvocationContext _) => batch.Records,
    handle: async (S3Event.S3EventNotificationRecord item, DocumentIndexer indexer) =>
    {
        await indexer.IndexAsync(item.S3.Object.Key);
    },
    createResponse: (items, ILambdaInvocationContext _) =>
        new BatchSummary(items.Count()));
```

## Type flow

Without error mapping:

```text
TEvent -> IEnumerable<TItem> -> handle every item -> IEnumerable<TItem> -> TResponse
```

```csharp
Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split

Func<TItem, TDependency1, ..., Task> handle

Func<IEnumerable<TItem>, ILambdaInvocationContext, TResponse> createResponse
```

If a handler throws, `createResponse` is not called.

With error mapping:

```text
TEvent -> IEnumerable<TItem> -> TFailure -> TResponse
```

```csharp
Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split

Func<TItem, TDependency1, ..., Task> handle

Func<TItem, Exception, ILambdaInvocationContext, TFailure> onError

Func<IEnumerable<TFailure>,
     ILambdaInvocationContext,
     TResponse> createResponse
```

`TItem` is always the first handler parameter. This keeps the processed source record available to `onError`.

`TFailure` can be any type. SQS may return `SQSBatchResponse.BatchItemFailure`; another source may return an identifier, an error record, or a custom type. `createResponse` receives the collected failures only.

## Unified item results

When both `handle` and `onError` produce values for one response builder, they must return the same type:

```csharp
Func<TItem, TDependency1, ..., Task<TResult>> handle

Func<TItem, Exception, ILambdaInvocationContext, TResult> onError

Func<IEnumerable<TResult>, ILambdaInvocationContext, TResponse> createResponse
```

```text
TEvent -> IEnumerable<TItem> -> TResult -> IEnumerable<TResult> -> TResponse
```

If `handle` returns `Task` or no value, it does not contribute a result to `createResponse`. The SQS failure example above uses this shape: only `onError` returns `TFailure`.

If success and failure need different data, use one shared union type for `TResult`:

```csharp
public abstract record ImportItemResult;

public sealed record Imported(ImportResult Value) : ImportItemResult;

public sealed record Retry(SQSBatchResponse.BatchItemFailure Value) : ImportItemResult;
```

`handle` returns `Imported`, `onError` returns `Retry`, and `createResponse` accepts `IEnumerable<ImportItemResult>`.

## Overloads

Provide handler overloads for zero through ten injected dependencies. The only changing part is the handler delegate:

```csharp
Func<TItem, Task>
Func<TItem, TDependency1, Task>
Func<TItem, TDependency1, TDependency2, Task>
```

Use separate overloads for each meaningful callback combination. Do not require callers to pass `null`.

```csharp
MapEventHandler(split, handle)
MapEventHandler(split, handle, onError)
MapEventHandler(split, handle, createResponse)
MapEventHandler(split, handle, onError, createResponse)
```

`onError` without `createResponse` is an error observer and returns `Task`. The processor invokes it before following the default error policy.

The response-only overload receives successful source items. The error-and-response overload receives failure values when `handle` has no result. When both callbacks return values, the builder receives their shared result type.

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
