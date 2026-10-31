# Event-processing contracts

Accepted for ml-ny2.1 on 2026-10-30. The epic's locked decisions are unchanged.

## Ownership and delivery

`MinimalLambda.EventProcessing` depends on the matching core version. Public processing types
use `MinimalLambda.EventProcessing`; interception extensions remain in `MinimalLambda.Builder`
for discoverability. The existing core-delivered generator recognizes the optional assembly.
The optional package ships no second analyzer. Core and general abstractions contain no processing APIs,
options registrations, or processing features. All four core TFMs are supported.

## Outcomes

`EventItemOutcome<TItem, TValue>` is immutable and contains `Index`, `Item`, `Status`,
`HasValue`, `Value`, and `Exception`. Status is `Succeeded`, `MappedError`, `Skipped`, or
`Cancelled`. Index is zero-based source position. Equal or identical items remain distinct entries.
`HasValue` distinguishes a genuine null contribution from no contribution. `Exception` is set
only for mapped errors. A handler returning a domain failure is still `Succeeded`; only the
application understands domain success. Invocation failure produces no response or outcome callback.

Split output is materialized once before handlers start. Null enumerables fail the invocation;
nullable items and values are retained. Empty input calls the response builder once with an empty list.
A response builder may return null when its response type allows it; core serializes that typed null normally.
The builder receives `IReadOnlyList<EventItemOutcome<TItem, TValue>>` in source order after all
admitted work and cleanup finish. It may enumerate repeatedly. Unstarted items are `Skipped`;
admitted work interrupted by processing cancellation is `Cancelled`. Successful work remains successful
even if cancellation was requested concurrently. Application code owns acknowledgement and retry.

## Callback families

Each mapping takes `Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split` and an
item-first handler returning `Task` or `Task<TResult>`. Up to ten additional parameters bind
services or framework values. The five compiler-selected families are:

| Family | Handler | Mapper | Outcome value |
| --- | --- | --- | --- |
| No response | Task | absent | no response callback |
| Source item | Task | absent | source item on success |
| Failures only | Task | TFailure | value only on mapped errors |
| Results | Task<TResult> | absent | handler result |
| Results with errors | Task<TResult> | TResult | handler or mapper result |

Response callbacks are synchronous `Func<IReadOnlyList<EventItemOutcome<TItem, TValue>>,
ILambdaInvocationContext, TResponse>`. Error callbacks are synchronous
`Func<TItem, Exception, IEventProcessingContext, TValue>`. Awaitable mapper contributions,
nested handler task results, and awaitable responses are rejected with an actionable generator
diagnostic. Async error mappers are deliberately unsupported; put async recovery in the handler.
Required callbacks cannot be null. A null task from a handler is an item error eligible for mapping.

Example response policies:

```csharp
// Source-item family
createResponse: (outcomes, _) => outcomes.Where(x => x.Status != EventItemStatus.Succeeded)
    .Select(x => x.Item.Id).ToArray()
// Failures-only and result families can inspect HasValue before reading Value.
// The no-response family omits both response and error callbacks.
```

## Binding, errors, and lifetime

Each item gets an asynchronous scope, alive through argument resolution, execution, and error mapping.
Scope creation is an invocation failure because no item provider exists. DI resolution, synchronous
handler throws, asynchronous faults, and null handler tasks reach onError with the original exception.
Absent or throwing mappers fail the invocation; mapper failures are never recursively mapped.
Scope disposal always fails the invocation. Drain admitted work and cleanup before propagating errors;
preserve the first fatal exception and log additional failures. Invocation cancellation takes precedence.
Optional nullable services retain null fallback. Declared optional defaults are honored, including keyed
and value-type defaults; generated resolution distinguishes missing services from default-valued services.

StopProcessing stops admission without cancelling admitted items. CancelProcessing also cancels the
processing token. Invocation cancellation suppresses response construction through cleanup, including
cancellation callback draining. Check immediately before and after the synchronous response builder;
cancellation during construction suppresses storing its response. Once stored, core owns serialization.

## Factory and generated output

`EventHandlerFactory.Create` accepts the built `IServiceProvider`, split, the typed wrapper,
optional mapper, and optional response callback. Its five overloads mirror the families above.
Wrappers are `Func<TItem, IServiceProvider, IEventProcessingContext, Task>` or the corresponding
`Task<TResult>` shape. The factory validates callbacks and effective options at mapping time,
resolves scope/logging dependencies, and constructs an internal processor once. No required
AddEventProcessing call or processor DI registration exists. Configure<EventHandlerOptions> is optional.
Configuration failures propagate. Options are copied at mapping time.

```csharp
ArgumentNullException.ThrowIfNull(handle);
var terminal = EventHandlerFactory.Create<Batch, Item, int, Response>(
    application.Services, split, HandleItem, onError, createResponse);
application.Handle(terminal); // Reject duplicates before changing provider properties.
// Same conditional provider registration as MapHandler, for Batch and Response only.
return application;

Task<int> HandleItem(Item item, IServiceProvider itemServices, IEventProcessingContext processing)
{
    var importer = itemServices.GetRequiredKeyedService<Importer>("primary");
    return handle(item, importer, processing.CancellationToken);
}
```

The runtime invokes and awaits HandleItem inside item-error handling. There is no generated binder,
BoundArguments, worker loop, or cancellation state machine. The splitter determines Batch; the final
response callback determines Response. Core serialization and middleware remain unchanged.
