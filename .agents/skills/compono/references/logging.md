# Compono.Logging

Only relevant if the project references `Compono.Logging`. `UseLogging()`
composes `ILogger`/`ILogger<T>` as a hand-written `CapturingLogger`/
`CapturingLogger<T>`, plus `CapturedLogEntry` and the fluent `Verify()`
surface.

```csharp
using Compono;
using Compono.Logging;
using Microsoft.Extensions.Logging;

var composer = Composer.Create(builder => builder.UseLogging());
var service = composer.Create<OrderService>();   // OrderService(ILogger<OrderService> logger, ...)
```

```csharp
public sealed class OrderServiceProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) =>
        builder.UseLogging().Share<ILogger<OrderService>>();
}

[Theory, Compose<OrderServiceProfile>]
public void RetriesLogAWarning(ILogger<OrderService> logger, OrderService service)
{
    service.PlaceOrder(...);
    logger.Verify().AtLevel(LogLevel.Warning).WithMessageContaining("retry").Once();
}
```

## When to use it

A composed type takes an `ILogger`/`ILogger<T>` constructor dependency
and the test wants to assert *what* was logged — level, message,
structured properties, exception, or scope. Concrete signs: the test
currently hand-rolls a fake `ILogger`, uses `NullLogger<T>.Instance` and
therefore can't assert anything about logging, or manually
`Register<ILogger<TSomething>>(...)`s a logger for one specific type.

## When NOT to use it

The test doesn't care about logging behavior at all — a bare
`NullLogger<T>.Instance`/an unconfigured `UseNSubstitute()` substitute is
fine when nothing in the test asserts against what was logged. Don't
introduce `Compono.Logging` just because a constructor happens to take an
`ILogger<T>` — only reach for it when the test actually needs to observe
logging behavior.

## Generation is on by default — do not suggest a manual opt-in

**This is the single most important thing to get right when this package
is referenced.** Unlike `Compono.TestDoubles` (pure opt-in via
`ComponoGeneratedTestDoubles`), `Compono.Logging` enables its generation
behavior automatically the moment the package is referenced — its own
packed `build`/`buildTransitive` MSBuild props asset defaults
`ComponoGeneratedLogging` to `true`. **Never tell a consumer to add
`<ComponoGeneratedLogging>true</ComponoGeneratedLogging>` to make
`UseLogging()` work** — if it isn't working, the cause is virtually never
a missing opt-in (there is none to add); check instead:

- Is `Compono.Logging` actually referenced (not just `Compono`)?
- Is the category type (`T` in `ILogger<T>`) reachable from a real
  composition root — an ordinary constructor parameter of a composed
  type, or a `[Compose]`/`[Compose<TProfile>]` theory-row parameter?
  Discovery is bounded to real roots, never a compilation-wide scan — an
  `ILogger<T>` reachable only through a hand-written `Register<T>(...)`
  factory's own internal `context.Resolve<ILogger<T>>()` call is a known,
  documented gap (ADR-0052 Finding B), not something to work around with
  a manual registration or reflection.
- Was `UseNSubstitute()` registered *before* `UseLogging()`? See
  "Registration order," below — `UseGeneratedTestDoubles()` ordering is
  never the cause, since it has no generated double to offer for
  `ILogger`/`ILogger<T>` while `ComponoGeneratedLogging` is enabled.

The **only** legitimate reason to write `ComponoGeneratedLogging` at all
is an explicit, deliberate opt-out:

```xml
<PropertyGroup>
  <ComponoGeneratedLogging>false</ComponoGeneratedLogging>
</PropertyGroup>
```

Generation happens inside the existing, shared `Compono.Generators`
(already embedded in `Compono.nupkg`) — `Compono.Logging` ships **no**
generator/analyzer DLL of its own. `CapturingLogger`/`CapturingLogger<T>`
are hand-written; the generator's only job is closing the generic
`CapturingLogger<T>` activation for each category type actually
discovered.

## Core usage vocabulary

- `builder.UseLogging(Action<LoggingOptions>? configure = null)` —
  registers the stage-6 provider. `LoggingOptions.MinimumLevel` (default
  `LogLevel.Trace`) is the only setting.
- `logger.GetCapturedEntries()` / `GetLastCapturedEntry()` /
  `ClearCapturedEntries()` — direct inspection, no assertion framework.
- `logger.Verify()` — fluent, one verb (matching
  `Compono.TestDoubles`/`Compono.Http`'s own `Verify()` vocabulary exactly
  — never suggest a two-verb `VerifyLog()...Verify()` shape, that was
  explicitly considered and rejected):
  `.AtLevel(level)`, `.WithEventId(id)`, `.WithException<TException>()`,
  `.WithMessageContaining(text)`, `.Matching(predicate)`, ending in
  `.Once()` / `.Never()` / `.Exactly(n)`.
- `new CapturingLogger<T>(options?)` / `new CapturingLogger(options?)` —
  direct construction, no composition required, identical behavior to a
  provider-composed instance.

## Structured properties and `MessageTemplate`

`CapturedLogEntry.Properties` (`IReadOnlyList<KeyValuePair<string, object?>>?`)
and `MessageTemplate` (the `"{OriginalFormat}"` entry, by name) are
derived from `State`, covering **both** an ordinary
`logger.LogInformation("...", args)` call and every `[LoggerMessage]`
source-generated call identically — never claim one style is unsupported
or needs special handling. A `null` structured value is preserved as
`null`, not stringified — that's why the value type is `object?`, not
the BCL's own non-nullable `object`.

## Scope semantics

Real scope tracking via `LoggerExternalScopeProvider` — not a no-op.
`CapturedLogEntry.Scopes` is outermost-to-innermost, a snapshot fixed at
capture time. Never claim scopes are unsupported or that
`BeginScope(...)` is a no-op in this package.

## `MinimumLevel` — real filtering

An entry below `MinimumLevel` is never captured at all — never claim it's
merely an `IsEnabled()` opinion layered over a complete capture stream.
`LogLevel.None` is never an enabled/capturable level regardless of
`MinimumLevel`; `MinimumLevel = LogLevel.None` disables capture entirely.
There is no `FakeLogger`-style per-level `ControlLevel` toggle — a single
threshold is the whole v1 surface, don't invent a richer one.

## Four distinct failure conditions — never conflate them

1. **Non-Compono.Logging `ILogger`** — `logger.Verify()`/etc. called on
   an `ILogger` that isn't a `CapturingLogger`/`CapturingLogger<T>`
   (`NullLogger<T>.Instance`, an NSubstitute substitute, a
   `Compono.TestDoubles` double) → `InvalidOperationException` at the
   call site. Usually means registration order is backwards (below).
2. **Missing generated activation at runtime** — `LoggingProvider`
   recognized a closed `ILogger<T>` request but no generator-discovered
   activation exists for that category → a *different*
   `InvalidOperationException`, thrown at composition time. Means the
   category type isn't reachable from a real composition root.
3. **`CMP0038`** (compile-time, `Info`) — `ComponoGeneratedLogging` is
   enabled but `Compono.Logging`'s own runtime types couldn't be
   resolved. Only happens if the property was forced `true` without the
   package actually referenced — never something an ordinary consumer
   who just installs the package hits.
4. **`CMP0039`** (compile-time, `Info`) — a closed `ILogger<T>` category
   type is private/protected and can't be named by the generated
   top-level activation (mirrors `CMP0012`'s identical collection-element
   accessibility check). Composition still compiles; only that one
   category's activation is withheld, falling back to condition 2 above
   if actually requested at runtime.

Never present these as the same condition, and never suggest a fix for
one when the actual symptom matches another.

## Registration order (stage-6 precedence)

`UseLogging()` shares stage 6 with `UseNSubstitute()`/
`UseGeneratedTestDoubles()` — the existing, `Accepted` first-registered-
wins rule applies unchanged (no new precedence mechanism). What differs is
which providers can actually produce `ILogger`/`ILogger<T>` at all, per
ADR-0055 Amendment 4:

- **`UseGeneratedTestDoubles()`** — when `ComponoGeneratedLogging` is
  enabled, `Compono.TestDoubles` never generates a double for
  `ILogger`/`ILogger<T>` at all (Amendment 4: those types are
  Logging-owned). `GeneratedTestDoubleProvider` therefore has nothing to
  offer them regardless of registration order — order between
  `UseLogging()` and `UseGeneratedTestDoubles()` is **not observable** for
  `ILogger`/`ILogger<T>`.
- **`UseNSubstitute()`** — untouched by Amendment 4; it can independently
  produce an `ILogger`/`ILogger<T>` substitute without any generated
  factory. Registration order still matters here:

  ```csharp
  builder.UseLogging().UseNSubstitute();   // ILogger<T> -> CapturingLogger<T>
  builder.UseNSubstitute().UseLogging();   // ILogger<T> -> an NSubstitute substitute instead
  ```

  If a composed type depends on both an `ILogger<T>` and another interface
  resolved by `UseNSubstitute()`, register `UseLogging()` first. The
  reverse order is an explicit, documented consequence — never diagnose it
  as a bug, and never suggest a priority/specificity mechanism that
  doesn't exist.

## `Share<T>()` and `[Shared]`

A composed `ILogger<T>` participates in
[`Share<T>()`](../../../docs/adr/0056-composition-builder-share-graph-wide-sharing.md)
like any other composed type — no logging-specific mechanism. Prefer
`Share<T>()`, declared once in a profile (as above), over `[Shared]` on
every theory that needs to observe a captured logger — an ordinary,
undecorated `ILogger<OrderService> logger` parameter then gets back the
exact composed instance for assertion, with **no `[Shared]` attribute**.
`[Shared]` still works unchanged for a one-off case that doesn't warrant a
profile. Never suggest `Share<T>()` shares across separate `Create<T>()`
calls or is composer-wide — it's graph-scoped, per ADR-0056.

## `LoggingFactoryRegistry` — generator infrastructure, not usage API

Real, public API (required for the same cross-assembly reason
`Compono.TestDoubles`' `GeneratedTestDoubleRegistry` is public — generated
code lives in the consumer's own assembly). Never present it as part of
normal usage — a consumer's code should never call it directly. If it
shows up in generated-API-reference output, that's expected; it does not
belong in an ordinary composition/verification example.

## v1 boundaries — do not invent capability beyond these

No `ILoggerFactory` composition, no Serilog/provider-specific behavior,
no test-runner output capture/routing, no DI integration
(`services.AddCompono...`-style), no cross-scope structured-property
flattening/searching, no category-string constructor for the non-generic
`CapturingLogger`, no dependency on
`Microsoft.Extensions.Diagnostics.Testing`. If asked whether one of these
exists, say plainly that it doesn't rather than improvising a plausible-
looking API.
