# Event processing

Install `MinimalLambda.EventProcessing` to process items from one Lambda event with separate DI
scopes. Core-only applications do not expose these APIs or register processing services.

```bash
dotnet add package MinimalLambda.EventProcessing
```

```csharp
using MinimalLambda;
using MinimalLambda.Builder;
using MinimalLambda.EventProcessing;

builder.Services.AddScoped<DocumentImporter>();
await using var lambda = builder.Build();
lambda.MapEventHandler(
    split: (ImportBatch batch, ILambdaInvocationContext _) => batch.Items,
    handle: (ImportDocument item, DocumentImporter importer, CancellationToken token) =>
        importer.ImportAsync(item.Event, token));
```

No `AddEventProcessing` call or processor registration is required. The package supports the same
.NET target frameworks as core. Its matching core dependency supplies the source generator; the
optional package does not ship another analyzer.

## Callback families

The splitter runs once and returns `IEnumerable<TItem>`. The processor materializes it before any
handler starts. Null enumerable results and enumeration failures fail the invocation.

| Handler | Error mapper | Successful contribution | Response |
| --- | --- | --- | --- |
| `Task` | absent | absent | absent |
| `Task` | absent | source item | required |
| `Task` | `TFailure` | absent | required |
| `Task<TResult>` | absent | result | required |
| `Task<TResult>` | `TResult` | result | required |

`onError` is synchronous `(item, exception, processing) => value`. It maps errors from service
resolution and handler execution. A mapper that throws fails the invocation. Without a mapper,
item errors fail the invocation. Nullable response types may return null for normal core serialization.
A null returned handler task is an item error eligible for mapping.
Async mappers, nested awaitable handler results, and awaitable responses receive a generator diagnostic.
Put asynchronous recovery inside the item handler.

## Complete outcomes

Every response callback receives `IReadOnlyList<EventItemOutcome<TItem, TValue>>` plus the invocation
context. There is one immutable outcome per source position, in source order, regardless of completion
order. Duplicate items remain separate. Empty input calls the builder once with an empty list.

| Property | Meaning |
| --- | --- |
| `Index` | Zero-based source position |
| `Item` | Original source item, including null where the source type allows it |
| `Status` | `Succeeded`, `MappedError`, `Skipped`, or `Cancelled` |
| `HasValue` | A contribution exists, even if its value is null |
| `Value` | Handler, source-item, or mapped contribution |
| `Exception` | Original error for `MappedError`, otherwise null |

A handler-returned domain failure still has `Succeeded` status. The framework does not interpret
application result types. `Skipped` means the item never started; `Cancelled` means processing
cancellation interrupted admitted work. A successfully completed handler retains its result.

Applications own acknowledgement and retry. For example, this SQS response includes both mapped
failures and items that never completed:

```csharp
lambda.MapEventHandler(
    split: (SqsEnvelope<Order> batch, ILambdaInvocationContext _) => batch.Records,
    handle: async Task<ImportResult> (
        SqsEnvelope<Order>.SqsMessageEnvelope item,
        OrderImporter importer,
        CancellationToken token) => await importer.ImportAsync(item.BodyContent, token),
    onError: (item, error, processing) => new Retry(item.MessageId),
    createResponse: (outcomes, _) => new SQSBatchResponse(
        outcomes
            .Where(outcome => outcome.Status != EventItemStatus.Succeeded || outcome.Value is Retry)
            .Select(outcome => new SQSBatchResponse.BatchItemFailure
            {
                ItemIdentifier = outcome.Item.MessageId,
            })
            .ToList()));
```

Configure the SQS event source mapping to report partial batch failures. For FIFO ordering, use
sequential processing and stop when the application decides later records must wait. Include the
failed and unstarted records in the response. For stream sources, construct the source-specific
checkpoint from the complete outcomes. The package never chooses acknowledgements or retries.

## Binding and scopes

The item is the handler's first argument. Up to ten further parameters bind as follows:

- `ILambdaInvocationContext` or `ILambdaContext` receives the invocation context.
- `IEventProcessingContext` receives shared per-invocation processing controls.
- `CancellationToken` receives the processing token.
- Other arguments resolve from a fresh asynchronous item scope, including required, nullable,
  `[FromServices]`, and `[FromKeyedServices]` arguments.

A declared optional default applies when the service is missing. A registered zero or empty string
is retained. Each framework argument may appear once and cannot have service/event attributes.
The full incoming event type comes from the splitter, not `[FromEvent]` on a handler argument.

The scope remains alive through argument resolution, execution, and error mapping. Scope creation
or disposal failure fails the invocation. The processor stops admission and drains admitted handlers,
cancellation callbacks, and scope cleanup before returning. Return DTOs that remain usable after
scope disposal.

Typed delegates supply the parameter types used for binding. Attributes on the underlying handler
method still apply when its parameters accept broader types.

Method groups cannot infer dependency types for these overloads. Use a typed lambda, a typed delegate
local, or explicit generic arguments. A `Task<TResult>` handler cannot be passed to a no-result family.

## Options and cancellation

Defaults enable concurrency with at most ten active items per invocation. Scope disposal counts
against that limit. This is item concurrency inside one invocation.

```csharp
builder.Services.Configure<EventHandlerOptions>(options =>
{
    options.EnableConcurrency = false;
    options.MaxConcurrentExecutions = 1;
});
```

Options are captured and validated when mapping, after `Build()`. The maximum must be positive even
when concurrency is disabled. Configuration and options-validation exceptions propagate before any
callback runs.

`StopProcessing()` stops new admissions and lets admitted items finish. `CancelProcessing()` also
cancels the processing token. Both allow a response built from complete outcomes. Repeated calls are
safe. Pass the processing token into cancellable item work.

Invocation cancellation is separate and fails the invocation. It remains effective through cleanup.
The processor checks it immediately before response construction and before storing the response, so
cancellation during a synchronous response builder also suppresses its response. Invocation cancellation
takes precedence over concurrent item errors. Once the response is stored, core owns serialization.

## Registration and middleware

`MapEventHandler` consumes the same terminal-handler slot as `MapHandler`. A second mapping throws
before replacing provider properties. Existing custom providers retain precedence.

The generated interceptor creates one `HandleItem(item, itemServices, processing)` wrapper, calls
`EventHandlerFactory.Create(application.Services, ...)`, registers its terminal through `Handle`,
and emits normal event/final-response provider setup. Scheduling and error/cancellation state remain
internal package code. Core middleware, envelope extraction/packing, deserialization, and serialization
use their existing pipeline. Intermediate result types are not registered as response types.

See the [example project](https://github.com/LayeredCraft/minimal-lambda/tree/main/examples/MinimalLambda.Example.EventProcessor)
for all five families.
