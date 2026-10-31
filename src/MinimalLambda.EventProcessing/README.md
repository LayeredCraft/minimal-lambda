# MinimalLambda.EventProcessing

Optional scoped event processing for MinimalLambda. Install this package and import
`MinimalLambda.EventProcessing` with `MinimalLambda.Builder`.

MapEventHandler splits a full event into source items. Each item runs in its own asynchronous DI
scope. Response builders receive ordered outcomes for every source position, including skipped and
cancelled work. Application code chooses acknowledgement and retry policy.

No AddEventProcessing call is required. Optional Configure<EventHandlerOptions> before Build controls
concurrency. Effective options are validated at mapping time. The matching core dependency supplies
the source generator; this package does not duplicate the analyzer.

DI and handler failures reach the synchronous error mapper. Scope creation/disposal failures fail the
invocation. Invocation cancellation remains effective through cleanup. Core handles normal
serialization, middleware, and envelopes.

See https://layeredcraft.github.io/minimal-lambda/features/event-processor/ for usage and callback contracts.
