# Event-processing design

The accepted API and error/cancellation policies are in [event-processing-contracts.md](event-processing-contracts.md).
This replaces the earlier unshipped design. Beads epic `ml-ny2` tracks implementation and validation.

`MinimalLambda.EventProcessing` owns public mapping contracts, options, outcomes, the generated-code
factory, and internal scheduling/runtime code. It depends on matching core; core has no processing
services or APIs. The existing core analyzer recognizes the optional package and emits one item wrapper.

The interceptor calls the public factory with application services and typed callbacks, registers the
returned terminal using `application.Handle`, then conditionally registers normal event/final-response
providers. It rejects duplicate mappings before mutating providers. The splitter selects the full event
type. Intermediate item results never select the response provider.

The factory validates effective options at mapping time, resolves scope/logging dependencies, and
constructs the internal processor once. Each invocation gets fresh state. Each admitted item gets an
async scope alive through argument resolution, handler execution, error mapping, and awaited disposal.
Service resolution and handler errors reach onError; creation/disposal failures fail the invocation.

Response callbacks receive immutable ordered outcomes for every source position. Skipped and cancelled
items remain visible. Application code decides acknowledgements and retries. Invocation cancellation
remains effective through cleanup and is checked before constructing and storing the response.

Core serialization, features, envelopes, and middleware remain unchanged. There is no processing
feature, required service-registration call, generated worker algorithm, or separate binding stage.
