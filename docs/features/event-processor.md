# Event Processor

`MapEventHandler` splits one Lambda event into items and processes them concurrently. It replaces
the loop-and-try/catch code most batch handlers write by hand.

```csharp
builder.Services.AddScoped<DocumentImporter>();

lambda.MapEventHandler(
    split: (S3Event batch, ILambdaInvocationContext _) => batch.Records,
    handle: (S3Event.S3EventNotificationRecord item, DocumentImporter importer, CancellationToken token) =>
        importer.IndexAsync(item.S3.Object.Key, token));
```

Registration is source-generated, like `MapHandler`. The generator emits the whole processor, so
there is no runtime reflection and no public runtime bridge.

## What it is not

- It is not Lambda invocation concurrency. Item concurrency happens inside one invocation; the
    runtime still runs one invocation per execution environment by default.
- It is not a retry engine. Unstarted or cancelled items contribute nothing to the response, and the
    processor never invents results for them.
- It is not a task queue. The splitter output is materialized before any handler starts, bounded by
    the event size.

## Callback families

`split` always runs first, once, and serial. It returns `IEnumerable<TItem>`.

| Family | Callbacks after `split`               | Response input                   | Error behavior                                |
| ------ | ------------------------------------- | -------------------------------- | --------------------------------------------- |
| N1     | `handle`                              | none                             | An unhandled item error fails the invocation. |
| N2     | `handle`, `createResponse`            | successful source items          | Any item error prevents the response.         |
| N3     | `handle`, `onError`, `createResponse` | values returned by `onError`     | Successful items contribute nothing.          |
| R1     | `handle`, `createResponse`            | successful `TResult` values      | Any item error prevents the response.         |
| R2     | `handle`, `onError`, `createResponse` | one `TResult` per completed item | Mapped errors contribute a result.            |

`handle` returns `Task` for N1/N2/N3 and `Task<TResult>` for R1/R2. `onError` is a synchronous value
mapper, not an observer. Log and rethrow inside `handle` when you only want observation.

Every family has arities 0 through 10 for parameters after the item, using `TD0` through `TD9`:

```csharp
lambda.MapEventHandler<TEvent, TItem, TD0, TD1>(...);   // two extra handler inputs
```

The count includes framework parameters. The item is always first.

## Handler parameters

`handle` is resolved with a fresh asynchronous DI scope per item:

- the source item, first;
- `ILambdaInvocationContext` or `ILambdaContext`;
- `IEventProcessingContext`;
- `CancellationToken`, which is the *processing* token;
- anything else from DI, including `[FromServices]` and `[FromKeyedServices]`.

Each may appear at most once, and at most ten parameters follow the item. Framework parameters
cannot carry service or event attributes. Scopes are disposed after each item exits, and results
must remain valid after disposal.

```csharp
lambda.MapEventHandler(
    split: (SqsEnvelope<Order> batch, ILambdaInvocationContext _) => batch.Records,
    handle: async Task<OrderResult> (
        SqsEnvelope<Order>.SqsMessageEnvelope item,
        OrderProcessor processor,
        IEventProcessingContext processing,
        CancellationToken token) =>
    {
        if (item.BodyContent is null)
            return new Retry(item.MessageId);

        return await processor.ProcessAsync(item.BodyContent, token);
    },
    onError: (item, _, _) => new Retry(item.MessageId),
    createResponse: (results, _) => new SQSBatchResponse(/* ... */));
```

`split` and `createResponse` keep their explicit `ILambdaInvocationContext` parameter.

## Ordering and contributions

Handlers may finish in any order; contributions stay in source order. `createResponse` receives a
materialized, repeatably enumerable, read-only collection. Duplicate items stay separate entries.
An empty event still calls the response builder once, with an empty collection.

Only completed contributions reach the builder:

- N1 never builds a response;
- N2 supplies items whose handlers succeeded;
- N3 supplies only `onError` values;
- R1 and R2 supply one `TResult` per completed item, mapped errors included.

Nullable values are legitimate contributions. Null items are not filtered.

## Concurrency options

Concurrency is on by default with a cap of 10. It is captured when the handler is mapped.

```csharp
builder.Services.Configure<EventHandlerOptions>(options =>
{
    options.MaxConcurrentExecutions = 5;    // default 10
    options.EnableConcurrency = false;      // force sequential
});
```

`MaxConcurrentExecutions` must be positive in both modes. Invalid values fail before any callback
runs, at application build time. `EnableConcurrency = false` forces sequential execution even when a
larger cap is configured.

Use sequential processing when order matters, such as FIFO queues or stream shards.

## Stopping and cancelling

`IEventProcessingContext` is one object per invocation, shared by item handlers and error mappers.

| Call                                       | New work | Active work                                 | Response           |
| ------------------------------------------ | -------- | ------------------------------------------- | ------------------ |
| `StopProcessing()`                         | blocked  | finishes with the current token             | may still be built |
| `CancelProcessing()`                       | blocked  | processing token is signalled, then drained | may still be built |
| invocation token cancelled during handling | blocked  | processing token is signalled, then drained | suppressed         |

Both calls are thread-safe and idempotent, and stop escalates to cancellation but never reverses.
Calling either after handling completes does nothing.

```csharp
handle: async (Item item, IEventProcessingContext processing, CancellationToken token) =>
{
    if (item.IsPoisonPill)
        processing.StopProcessing();

    await handler.ProcessAsync(item, token);
}
```

Handlers receive the *processing* token. Read
`processingContext.Invocation.CancellationToken` when you need the original invocation token.
Cancellation is cooperative: passing the token downstream is what makes work stop. Successful
handlers that finish despite cancellation keep their results.

Once handling closes, fan-in and response construction add no new framework cancellation checks.
An invocation cancellation observed *during* handling still suppresses the response; one that
arrives later does not invalidate completed work.

## Response input limitations

The processor does not track which items are "missing". A response builder that needs to report
unprocessed identities must do that work in application code, for example by recording identities
inside `onError` or by comparing the response against the original event:

```csharp
createResponse: (results, context) =>
    new SQSBatchResponse(
        context.GetRequiredEvent<SQSEvent>().Records
            .Select(record => record.MessageId)
            .Except(results.OfType<Success>().Select(result => result.MessageId))
            .Select(id => new SQSBatchResponse.BatchItemFailure { ItemIdentifier = id })
            .ToList())
```

Treat omission as "no claim", not as "safe to retry". Applications own acknowledgement, idempotency,
and rollback. Retried messages may already have partial side effects.

## SQS partial batch responses

Return `SQSBatchResponse` with `ReportBatchItemFailures` enabled on the event source mapping.
Families R2 and N3 fit this shape best: successful messages contribute a success value, mapped
failures contribute a retry value.

```csharp
lambda.MapEventHandler(
    split: (SQSEvent batch, ILambdaInvocationContext _) => batch.Records,
    handle: async Task<ImportItemResult> (
        SQSEvent.SQSMessage item, DocumentImporter importer, CancellationToken token) =>
    {
        await importer.ImportAsync(item.Body, token);
        return new Success(item.MessageId);
    },
    onError: (item, _, _) => new Retry(item.MessageId),
    createResponse: (results, _) => new SQSBatchResponse(
        results.OfType<Retry>()
            .Select(result => new SQSBatchResponse.BatchItemFailure
            {
                ItemIdentifier = result.MessageId,
            })
            .ToList()));
```

The handler's declared `Task<ImportItemResult>` return type fixes the shared result type. A
`Task<Success>` handler is not a `Task<ImportItemResult>` handler.

## Choosing the overload

`Task<TResult>` derives from `Task`, so several families can bind the same lambda. Disambiguate
explicitly:

- declare the handler return type, `async Task<ImportItemResult> (...) => ...`;
- pass a typed delegate local when using a method group;
- pass `onError` only when you want recovery;
- specify all generic arguments when nothing else works.

A method group does not supply dependency parameter types for generic inference. This does not
compile:

```csharp
lambda.MapEventHandler(split, EventHandlers.HandleAsync);   // CS0411
```

Use a typed lambda, a typed delegate local, or explicit generic arguments:

```csharp
Func<ImportDocument, DocumentImporter, Task> handle = EventHandlers.HandleAsync;
lambda.MapEventHandler(split, handle);

lambda.MapEventHandler<ImportBatch, ImportDocument, DocumentImporter>(split, EventHandlers.HandleAsync);
```

A result-returning handler bound to a no-result overload is a compile error (`LH0011`), not a silent
discard.

## One handler per run

`MapEventHandler` consumes the single invocation handler slot, like `MapHandler`. Multiple mapping
sites are allowed and all compile; the error only happens when execution reaches a second mapping.
That keeps conditional registration and multi-mode samples working, while a genuinely duplicated
registration still throws the existing `InvalidOperationException`.

## Diagnostics

| ID       | Meaning                                                                                                                                                |
| -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `LH0008` | Unsupported signature: non-`Task` handler, wrong item type, inaccessible type, `ref`/`out`, stream or awaitable event/response, wrong parameter count. |
| `LH0009` | Unsupported binding: event attribute on the item, duplicate context/token parameters, service attribute on a framework parameter.                      |
| `LH0010` | Handler cannot be resolved to one analyzable method, typically a reassigned delegate local.                                                            |
| `LH0011` | A `Task<T>` handler bound to a no-result overload would discard its result.                                                                            |

## Native AOT

The processor is generated into the consumer assembly with concrete closed signatures, so it is
trim- and AOT-safe. Add serializer metadata for the event, response, envelope payloads, and any
result type you serialize yourself:

```csharp
builder.Services.AddLambdaSerializerWithContext<SerializerContext>();
builder.Services.ConfigureEnvelopeOptions(options =>
    options.JsonOptions.TypeInfoResolver = SerializerContext.Default);

[JsonSerializable(typeof(SQSEvent))]
[JsonSerializable(typeof(SQSBatchResponse))]
[JsonSerializable(typeof(Order))]
internal partial class SerializerContext : JsonSerializerContext;
```

Intermediate union results only need metadata when they are serialized.

## Limits

- The event is materialized into memory; item count is bounded by event size, not by an unbounded
    task queue.
- Worker count is bounded by `min(itemCount, effectiveConcurrency)`.
- Concurrency does not preserve order. Use `EnableConcurrency = false` for FIFO and ordered stream
    processing.
- Cancellation is cooperative and never undoes side effects. Poison pills that repeatedly fail will
    repeatedly re-deliver unless the application records progress.
