# Event processing

Use for MapEventHandler, per-item DI, batch responses, processing controls, and EventHandlerOptions.

Install `MinimalLambda.EventProcessing`. Import `MinimalLambda.EventProcessing` for context, options,
factory, and outcomes; mapping extensions remain in `MinimalLambda.Builder`. Core alone has no event
processing APIs or registration. No AddEventProcessing call or processor DI registration is required.
The matching core package supplies the generator. Do not add a second analyzer.

## Mapping

The splitter is `Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>>`, called once. Its output
is materialized before handling. The splitter selects the full event type. Null output fails invocation.
The handler is item-first, returning Task or Task<TResult>, with up to ten further parameters.

Framework bindings are ILambdaInvocationContext/ILambdaContext, IEventProcessingContext, and the
processing CancellationToken. Other arguments resolve from the item scope, including keyed/nullable
services. Declared optional defaults apply only when a service is missing. Do not use FromEvent to
select the incoming batch. Use a typed lambda/delegate or explicit generics for method groups.

Five families: no response; Task plus source-item response; Task plus mapped-failure response;
Task<TResult> plus response; Task<TResult> plus synchronous error mapper and response.
The error mapper is `(item, exception, processing) => TValue`. Async mapper contributions, nested
awaitable handler results, and awaitable responses are diagnostic errors.

Typed delegates supply binding types. Attributes on the underlying handler method still apply
when its parameters accept broader types.

## Complete outcomes

Response callback: `(IReadOnlyList<EventItemOutcome<TItem, TValue>>, ILambdaInvocationContext) => TResponse`.
Every source position appears once in order, including duplicates and nullable items. Outcomes expose
Index, Item, Status, HasValue, Value, Exception. HasValue distinguishes a null value from no contribution.
Statuses are Succeeded, MappedError, Skipped, Cancelled. Handler domain results are not interpreted.
A mapped error retains the original exception. Empty batches build one response with an empty list.

Applications own acknowledgement/retry decisions. For SQS, include mapped/domain failures and
skipped/cancelled items as appropriate, using original Item.MessageId. Configure partial batch failure
reporting. For FIFO use sequential handling and StopProcessing when later records must wait; include
unstarted records in the retry response. For stream checkpoints inspect complete outcomes and choose
the checkpoint in application code. Never compact outcomes into values before deciding retries.

## Runtime policies

Fresh async scope per admitted item, alive through DI, handling, mapper, and awaited cleanup. DI
resolution/constructor failures and sync/async handler failures reach onError. Null handler tasks are
item errors. Missing/throwing mapper fails invocation; never recursively map mapper failures.
Scope creation/disposal failures fail invocation. Drain admitted work and cleanup before returning.
Return DTOs that survive scope disposal. First fatal error wins; additional failures are logged.
Invocation cancellation takes precedence.

Options default to concurrency enabled, maximum 10. Optional Configure<EventHandlerOptions> before
Build changes them. Mapping after Build captures and validates effective options, including defaults;
positive maximum required even in sequential mode. Never hide configuration exceptions.

StopProcessing stops admission; CancelProcessing also cancels admitted work through the processing
token. Both can yield partial responses with complete outcomes. Invocation cancellation fails the
invocation through cleanup and suppresses response construction/storage. Cleanup counts toward the
concurrency cap. Repeated controls and reentrant cancellation callbacks must drain safely.

## Generated boundary

One HandleItem(item, itemServices, processing) wrapper, called and awaited inside item error handling.
Public EventHandlerFactory.Create accepts built IServiceProvider and typed callbacks, constructs the
internal processor once, returns LambdaInvocationDelegate. Interceptor calls application.Handle before
normal conditional event/final-response provider setup. Duplicate mappings preserve prior providers.
Keep scheduling, batching, state and response construction in package code. No new core feature APIs,
processing feature, Bind/Invoke stages, BoundArguments, runtime reflection, or generated algorithms.

Validate real serialization/envelopes/middleware, keyed/optional/framework bindings, ordered outcomes,
DI mapping, fatal cleanup, cancellation races, packed consumers and Native AOT. See
`docs/features/event-processor.md` and `designs/event-processing-contracts.md` for full contracts.
