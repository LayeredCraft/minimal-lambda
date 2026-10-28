# Event Processor

Read when the task processes one Lambda event as a batch: SQS/SNS records, S3 records, stream
shards, fan-out per item, per-item DI, partial batch responses (`ReportBatchItemFailures`),
`MapEventHandler`, `IEventProcessingContext`, `EventHandlerOptions`, or `StopProcessing` /
`CancelProcessing`.

## Portability note

This reference is self-contained for client-project use. Do not assume MinimalLambda source or tests
exist in the workspace. For repo contributions, read `repo-workflow.md` first.

## Mental model

`MapEventHandler` replaces the hand-written loop + `try/catch` in a batch handler. One call:

1. runs `split` once to materialize items;
2. runs one asynchronous DI scope per item, concurrently;
3. collects contributions in source order;
4. builds one response through `createResponse`.

It is source-generated. Never add a runtime helper or reflection fallback to replace it.

`MapEventHandler` consumes the same single invocation handler slot as `MapHandler`. Multiple mapping
sites are fine and all compile; reaching a second mapping for one handler throws at runtime.

## Signature shape

```csharp
lambda.MapEventHandler(
    split: (SQSEvent batch, ILambdaInvocationContext _) => batch.Records,
    handle: (SQSEvent.SQSMessage item, IOrderService service, CancellationToken token) =>
        service.HandleAsync(item, token));
```

- `split`: `Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>>`, called once, serial.
- `handle`: item first. Returns `Task` (N1/N2/N3) or `Task<TResult>` (R1/R2).
- `onError`: synchronous value mapper, `(item, exception, IEventProcessingContext) => TValue`.
  Present in N3/R2 only.
- `createResponse`: `(IEnumerable<TValue>, ILambdaInvocationContext) => TResponse`.
- Arities 0-10 after the item, via `TD0`-`TD9`. Framework parameters count toward ten.

Handler parameters after the item: `ILambdaInvocationContext`/`ILambdaContext`,
`IEventProcessingContext`, `CancellationToken` (the processing token), then anything from DI
(`[FromServices]`, `[FromKeyedServices]`). Each framework parameter may appear once.

## Families

| Family | Callbacks                             | Response input                   | Unhandled item error |
| ------ | ------------------------------------- | -------------------------------- | -------------------- |
| N1     | `handle`                              | none                             | fails invocation     |
| N2     | `handle`, `createResponse`            | successful source items          | fails, no response   |
| N3     | `handle`, `onError`, `createResponse` | `onError` values                 | mapped values        |
| R1     | `handle`, `createResponse`            | successful `TResult`             | fails, no response   |
| R2     | `handle`, `onError`, `createResponse` | one `TResult` per completed item | mapped               |

Pick N1 when nothing is returned. Pick N3/R2 when you need a partial response. Never bind a
`Task<T>` handler to N1/N2/N3 - the generator reports `LH0011`.

## Concurrency and ordering

Concurrency is on by default, cap 10, captured at registration:

```csharp
builder.Services.Configure<EventHandlerOptions>(options =>
{
    options.MaxConcurrentExecutions = 5;
    options.EnableConcurrency = false;   // sequential
});
```

`MaxConcurrentExecutions` must be positive in both modes or the app fails at build time.
Contributions stay in **source order** regardless of completion order. Use
`EnableConcurrency = false` for FIFO queues and ordered stream shards.

Item concurrency is not Lambda invocation concurrency. The runtime still handles one invocation per
environment by default.

## Stopping and cancelling

`IEventProcessingContext` (namespace `MinimalLambda`) is per invocation, shared by handlers and
error mappers.

- `StopProcessing()` - block new items, let active ones finish with the current token. A response may
  still be built.
- `CancelProcessing()` - same, plus signal the processing token, then drain.
- Invocation cancellation observed *during* handling - blocks new items, signals the processing
  token, suppresses the response.

Both are thread-safe and idempotent; stop escalates to cancel but never reverses. `handle` receives
the processing token; read `processingContext.Invocation.CancellationToken` for the original. After
handling closes, fan-in and response construction add no new cancellation checks.

## Response contract

`createResponse` receives only completed contributions, source order, materialized and repeatably
enumerable. Unstarted or cancelled items contribute nothing - not a null, not a placeholder. The
processor does not validate completeness or retry items.

An empty event still calls the builder once with an empty collection. Nullable values are valid
contributions; null items are not filtered.

## Partial batch responses (SQS)

```csharp
lambda.MapEventHandler(
    split: (SQSEvent batch, ILambdaInvocationContext _) => batch.Records,
    handle: async Task<ImportResult> (
        SQSEvent.SQSMessage item, Importer importer, CancellationToken token) =>
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

Requirements and limits:

- Enable `ReportBatchItemFailures` on the event source mapping.
- The declared handler return type fixes the shared union type. `Task<Success>` is not
  `Task<ImportResult>`.
- Success values carry identity so the response can be computed; use `Success`/`Retry` records.
- Omitted items mean "no claim", never "safe to retry". Partial imports may already have side
  effects. Applications own idempotency and rollback.
- When a response must report *unprocessed* identities, compute them in application code from the
  original event, for example
  `except`-ing completed identities against `context.GetRequiredEvent<SQSEvent>().Records`.
- FIFO queues need `EnableConcurrency = false` plus `StopProcessing()` on a poison pill. Otherwise
  later messages may be committed before an earlier failure is retried.

## Overload selection

`Task<T>` derives from `Task`, so bindings can be ambiguous. In order:

1. declare the handler return type: `async Task<ImportResult> (item, dep, token) => ...`;
2. pass a typed delegate local when using a method group
   (`Func<TItem, Dep, CancellationToken, Task<ImportResult>> handle = Handler;`);
3. pass all generic arguments:
   `lambda.MapEventHandler<Batch, Item, Dep>(split, Handler);`.

A method group alone cannot supply DI parameter types - it produces `CS0411`. Do not add a `Delegate`
catch-all.

## Diagnostics

| ID       | Meaning                                                                                                                                                |
| -------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `LH0008` | Unsupported signature: not `Task`-returning, wrong item type, inaccessible type, `ref`/`out`, stream/awaitable event or response, bad parameter count. |
| `LH0009` | Unsupported binding: event attribute on the item, duplicate context/token parameters, service attribute on a framework parameter.                      |
| `LH0010` | Handler not resolvable to one analyzable method, usually a reassigned delegate local.                                                                  |
| `LH0011` | `Task<T>` handler bound to a no-result overload.                                                                                                       |

## AOT

Generated code uses concrete closed signatures, so the processor itself is trim-safe. Add serializer
metadata for the event, response, envelope payloads, and any result type you serialize:

```csharp
builder.Services.AddLambdaSerializerWithContext<SerializerContext>();
builder.Services.ConfigureEnvelopeOptions(options =>
    options.JsonOptions.TypeInfoResolver = SerializerContext.Default);
```

## Checklist

- Family matches the return type; no `Task<T>` handler on a no-result overload.
- Dependencies resolve from DI; the item is first; at most ten parameters after the item.
- Concurrency setting works for the event source (sequential for FIFO/ordered streams).
- `createResponse` handles an empty collection and only relies on completed contributions.
- SQS: `ReportBatchItemFailures` configured, identity carried in results, unprocessed identities
  computed by application code.
- Envelope payload types are in the serializer context.
