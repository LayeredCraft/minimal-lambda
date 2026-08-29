# Compono.Http

Only relevant if the project references `Compono.Http`. One public type,
`TestHttpHandler` (an `HttpMessageHandler` subclass), plus its
`HttpResponseRegistration`/`HttpResponseRegistrationBuilder` return types
and `UnmatchedHttpRequestException`.

```csharp
[Theory, Compose]
public async Task GetAsync_ReturnsCustomer([Shared] TestHttpHandler handler)
{
    var registration = handler.OnGet("/v1/customers/42")
        .RespondJson(new CustomerDto("42", "Ada Lovelace"));

    using var client = handler.CreateClient(new Uri("https://api.example.com/"));
    var customer = await client.GetFromJsonAsync<CustomerDto>("/v1/customers/42");

    registration.Verify().Once();
}
```

## When to use it

A test deliberately needs to exercise the real HTTP client pipeline —
`real HttpClient -> TestHttpHandler -> configured HTTP response` — not an
application-level abstraction. Concrete signs: the type under test is
built directly on `HttpClient`, the test cares about URI/method/header/
request construction, request serialization, or `DelegatingHandler`
behavior, or the codebase has a hand-written/reflection-based
`HttpMessageHandler` fake that `Compono.Http` should replace.

## When NOT to use it

The production seam is already an ordinary application interface
(`ICustomerApi`, `IWeatherService`, ...) and the test doesn't care that
it's HTTP-backed specifically — stay with `Compono.TestDoubles`/
`Compono.NSubstitute` there. Never special-case `HttpClient`/
`HttpMessageHandler` through `Compono.TestDoubles`/`Compono.NSubstitute`
instead of `Compono.Http` — that boundary is architectural (ADR-0051), not
a v1-only limitation that might later be lifted.

## Core usage vocabulary

- `handler.OnGet(path)` / `OnPost(path)` / `OnPut(path)` / `OnPatch(path)` /
  `OnDelete(path)` — two overloads. `OnGet(string path)` is the normal,
  common-case one: an exact equality match against the request URI's
  path+query, e.g. `handler.OnGet("/v1/customers/42")` — use this for the
  ordinary case, it's what preserves the literal path in a `Verify()`
  failure message. `OnGet(Match<string> path)` is for
  `handler.OnGet(Match.Any<string>())` (any path) or
  `handler.OnGet(Match.Is<string>(p => p.StartsWith("/v1/")))` (a
  predicate) — its `Verify()` description is deliberately generic
  ("matching a custom path condition"), since `Match<T>` exposes no way to
  tell `Any()` from `Is(...)` from the outside (ADR-0051 Amendment 1);
  never claim a `Match<string>`-based registration's failure message names
  the actual path or predicate.
- `handler.When(req => ...)` — whole-request predicate (method, URI,
  headers, content type together). The only mechanism for matching on
  anything beyond method+path — there is **no** dedicated header/query/
  body matcher DSL.
- Every match finalizes with `.Respond(HttpStatusCode)`,
  `.RespondText(content, mediaType, encoding)`,
  `.RespondJson(value, options?)`, `.RespondJson(value, jsonTypeInfo)`, or
  `.Throws(exception)` — each returns the `HttpResponseRegistration`
  handle, capture it for verification:
  ```csharp
  var registration = handler.OnPost("/v1/orders").RespondJson(order);
  ...
  registration.Verify().Once();   // .Never() / .Exactly(n) also available
  ```

## Matching semantics

Registrations are evaluated **last-registered-first, first match wins** —
register a broad fallback first, a specific override after it:

```csharp
handler.When(_ => true).Respond(HttpStatusCode.NotFound);   // fallback, registered first
handler.OnGet("/v1/customers/42").RespondJson(customer);    // override, registered second, wins
```

Getting registration order backwards silently breaks this — the fallback
would otherwise win for every request, including the one meant to hit the
specific override.

## Unmatched behavior

Strict by default — a request matching no registration throws
`UnmatchedHttpRequestException` (naming the method and URI), never a
fabricated response. There is no loose-mode switch. Want a fallback?
Configure one explicitly: `handler.When(_ => true).Respond(...)`. The
unmatched request still appears in `handler.Requests`.

## Verification vs. request inspection

Two different questions, two different APIs — never conflate them:

- `registration.Verify().Once()` / `.Never()` / `.Exactly(n)` — "how many
  times did *this configured behavior* match." Reuses core `Compono`'s
  `CallVerifier` unchanged.
- `handler.Requests` (`IReadOnlyList<HttpRequestMessage>`) — "what did the
  system under test actually send," every request in arrival order,
  matched or not, snapshotted fresh on every access.

Never suggest reconstructing a verification predicate or an expression-
based `Verify(...)` API — that shape was considered and rejected in
ADR-0051.

## Lifetime

`TestHttpHandler` is caller-owned. `[Shared]` gives identity/reuse across
composed parameters in one test — **not** disposal; Compono does not
currently dispose `[Shared]`-composed `IDisposable` values, and this
package doesn't add that. `handler.CreateClient(...)` always builds the
`HttpClient` with `disposeHandler: false`, so disposing a client never
disposes the handler — dispose both yourself:

```csharp
using var client = handler.CreateClient(baseAddress);
```

Never teach or imply automatic composition-scope disposal for
`TestHttpHandler` — if a future core ADR changes that, this file updates
then, not preemptively.

## `IHttpClientFactory` boundary

`Compono.Http` is not an `IHttpClientFactory` mocking package and ships no
`Microsoft.Extensions.Http` helper. For that seam, use a tiny
project-local fake — it's a single-method interface, no special machinery
needed — or `Compono.TestDoubles`/`Compono.NSubstitute` if the project
already uses one of those for other doubles:

```csharp
private sealed class FakeHttpClientFactory(HttpClient client) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => client;
}
```

Never suggest a `Microsoft.Extensions.Http` helper `Compono.Http` doesn't
ship.

## JSON / AOT

`RespondJson(value, options)` is the ergonomic runtime-metadata path — it
carries the normal `RequiresDynamicCode`/`RequiresUnreferencedCode`
trimming/AOT warnings at the consumer's own call site, propagated
honestly rather than suppressed. `RespondJson(value, jsonTypeInfo)` (a
source-generated `JsonSerializerContext`'s metadata) is the AOT-safe path
— prefer it in an AOT/trim-sensitive project. Never claim all
`RespondJson` usage is automatically AOT-safe — only the `JsonTypeInfo<T>`
overload is.
