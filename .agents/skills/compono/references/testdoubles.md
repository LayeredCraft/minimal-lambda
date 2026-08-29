# Compono.TestDoubles

Only relevant if the project references `Compono.TestDoubles`, sets
`<ComponoGeneratedTestDoubles>true</ComponoGeneratedTestDoubles>` in its own
`.csproj`, **and** calls `UseGeneratedTestDoubles()` when building the
composer. All three are required — the compile-time property alone only
generates the doubles, without `UseGeneratedTestDoubles()` nothing
registers them into the pipeline; the package reference alone does
nothing without the property set. Never suggest any one or two of the
three alone.

```csharp
var composer = Composer.Create(builder => builder.UseGeneratedTestDoubles());

var service = composer.Create<OrderService>();
service.Repository.Configure().CountAsync().Returns(Task.FromResult(4));
```

- `GeneratedTestDoubleProvider` runs at the test-double provider stage,
  same as `NSubstituteProvider`. It resolves a requested interface type to
  a **generated** double only if `Compono.Generators` actually emitted one
  for that interface at compile time. For an interface the compile-time
  opt-in never reached (project doesn't set
  `ComponoGeneratedTestDoubles=true`, or the interface was never requested
  anywhere the generator could discover it), `TryProvide` returns
  `NotHandled` — the pipeline moves on to the next registered provider
  (e.g. `NSubstituteProvider`, if also registered) exactly as it would if
  this provider weren't installed at all. It's only a genuine composition
  failure if no other provider claims the request either.
- **`Configure()`** — a generator-emitted extension bridge
  (`this IRepository`), reachable from **any namespace with no `using`
  needed** — every generated type lives in the global namespace by design.
  Don't add an import "just in case"; if `Configure()` doesn't resolve, the
  interface likely never got a generated double at all (check the
  compile-time opt-in is set and the interface is actually reached by
  something the generator's discovery walk covers — a
  `composer.Create<T>()`/`CreateMany<T>()` call site, a `[Compose]` theory/
  test method parameter, or a `[Composable]` declaration all feed the same
   closure walk).
- **`.Returns(...)`/`.Throws(...)`** per member configuration.
  Zero-argument `Configure().Member()` configures the member regardless of
  arguments. For eligible parameterized members, `Configure().Member(...)`
  accepts `Match<T>` arguments: literal values match by equality,
  `Match.Any<T>()` matches anything, and `Match.Is<T>(predicate)` matches
  by predicate.
- **`Verify()`** — parallel to and independent from `Configure()`, returning
  a verifier surface for `Never()`/`Once()`/`Exactly(n)`. Zero-argument
  `Verify().Member()` counts every call to that member. For eligible
  parameterized members, `Verify().Member(...)` uses the same `Match<T>`
  argument shape to perform argument-filtered verification.
- **Multi-entry argument-distinguished configuration** — for matching-
  eligible members, multiple `Configure().Member(...)` calls can coexist.
  Dispatch uses the most recently registered matching entry; precedence is
   registration order, not matcher-specificity ranking.
- **Full base-interface closure.** If `IRepository : IClock`, the generated
  double implements `IClock.UtcNow` too, configurable via
  `repository.Configure().UtcNow().Returns(...)` — not just `IRepository`'s
  own declared members.
- **Deterministic defaults** for any unconfigured member: primitives,
  nullable references, `Task`/`Task<T>`, `ValueTask`/`ValueTask<T>`, and
  known collection shapes return their deterministic default (empty
  collections, never `null`). `Task<T>`/`ValueTask<T>` recurse into `T` —
  `Task<int>` is fine, but `Task<Customer>` (a non-nullable reference `T`)
  may require explicit configuration rather than a default. See
  "Configuration-required members" below.

## Argument matching and filtered verification

Do not conflate argument matching with argument capture. Current
`Compono.TestDoubles` supports ordinary matcher-based configuration and
verification for eligible members; it does not expose an arbitrary call log
or invocation callback API.

### NSubstitute migration mapping

When migrating from NSubstitute, do not introduce a hand-written recording
fake merely because the old test uses `Arg.Is`, `Arg.Any`, `Received`, or
`DidNotReceive`. For an eligible generated-double member, translate the
concepts directly:

| NSubstitute | Compono.TestDoubles |
|---|---|
| `Arg.Is<T>(predicate)` | `Match.Is<T>(predicate)` |
| `Arg.Any<T>()` | `Match.Any<T>()` |
| literal argument | literal argument (equality match) |
| `Received(1).Member(...)` | `Verify().Member(...).Once()` |
| `Received(n).Member(...)` | `Verify().Member(...).Exactly(n)` |
| `DidNotReceive().Member(...)` | `Verify().Member(...).Never()` |

Real AWS Secrets Manager Provider migration shapes:

```csharp
configurationBuilder.Verify()
    .Add(Match.Is<IConfigurationSource>(source => source is SecretsManagerConfigurationSource))
    .Once();
```

```csharp
secretsManager.Configure()
    .GetSecretValueAsync(
        Match.Is<GetSecretValueRequest>(request => request.SecretId == secretName),
        Match.Any<CancellationToken>())
    .Returns(Task.FromResult(response));
```

### Eligibility boundary

Argument-aware `Configure().Member(...)`/`Verify().Member(...)` is generated
for a member only when the member is eligible: it is not overloaded, its
real parameters do not reference the member's own open generic type
parameter, its real parameters are usable as generic type arguments, the
derived generated field names do not collide, and its generated extension
would not be hidden by an inherited `object` member. If a member is scoped
out of argument-awareness, keep using the existing argument-independent
surface (`Configure().Member()` / `Verify().Member()`) or choose another
test seam/provider when the test genuinely needs argument distinction.

Overloaded members keep the discriminator-only shape described below: their
arguments select the overload at compile time; they are not matchers.

## Multiple response configurations per member

For matching-eligible members, each `Configure().Member(...)` call appends a
new response configuration. Dispatch walks matching entries from newest to
oldest and uses the first match:

```csharp
repository.Configure()
    .Withdraw(Match.Any<string>(), Match.Any<decimal>(), Match.Any<bool>())
    .Returns(false);
repository.Configure()
    .Withdraw("acct-1", Match.Any<decimal>(), Match.Any<bool>())
    .Returns(true);
```

`Withdraw("acct-1", ...)` returns `true`; other accounts return `false`.
There is no matcher-specificity ranking — if two entries both match, the
one configured later wins.

## Sequential/call-count-based responses

`ReturnConfigBuilder<T>.ReturnsSequence(...)` (ADR-0054) configures a
different outcome per call, consumed in order; the final outcome repeats
once the sequence is exhausted. It coexists with the argument-matching
surface above — sequence state belongs to whichever entry the call
matched, so two argument-distinguished entries on the same member each own
an independent ordinal:

```csharp
repository.Configure().CountAsync()
    .ReturnsSequence(
        SequenceOutcome.Throw(new TimeoutException("attempt 1 fails")),
        SequenceOutcome.Throw(new TimeoutException("attempt 2 fails")),
        Task.FromResult(42));

await repository.CountAsync(); // throws TimeoutException("attempt 1 fails")
await repository.CountAsync(); // throws TimeoutException("attempt 2 fails")
await repository.CountAsync(); // 42
await repository.CountAsync(); // 42 (exhausted - repeats the final outcome)
```

Each element is a `SequenceOutcome<T>`: an ordinary `T` value converts to
it implicitly (`1`, `Task.FromResult(42)`, `false`), and an exception
outcome is spelled explicitly with `SequenceOutcome.Throw(exception)` —
there is no implicit conversion from `Exception`, since that is silently
wrong for a `T` that is itself `Exception` or a base/derived type of it.
Call recording (`Verify().Member(...).Exactly(n)`) is independent of
response consumption — a throwing call still counts. Reconfiguring the
same entry (`Configure()` again) replaces the sequence and resets its
ordinal; `Returns(...)`/`Throws(...)` on the same builder clear any
configured sequence, and vice versa.

## Overloaded members (v2)

An overloaded interface member gets its own per-overload `Configure()`
surface instead of an all-or-nothing rejection (see
`docs/adr/0044-compono-testdoubles-v2-overloads-generics-verification.md`) —
the generated configuration extension for an overloaded member takes the
same real parameter types the interface overload declares, purely so
ordinary C# overload resolution picks the right one. The values themselves
are still discarded and are **not** argument matchers. `Verify()` reuses
this same per-overload surface - `Verify().Speak("hi")` selects the same
overload-specific counter `Configure().Speak("hi")` would:

```csharp
public interface IResponseBuilder
{
    void Speak(string? text);
    void Speak(params ISsml[] parts);
}

builder.Configure().Speak("hello").Throws(new InvalidOperationException());
builder.Configure().Speak(new ISsml[] { ssml }).Throws(new InvalidOperationException());
```

`.Speak(...)` alone only selects an overload's configuration handle -
nothing is configured on the double until `.Returns(...)`/`.Throws(...)`
is chained, same as any non-overloaded `Configure()` call.

Two things still don't get a surface: a **diamond collision** (the exact
same signature independently declared by two different base interfaces —
nothing to disambiguate) and a `ref`/`out`/`in` parameter's own overload
(falls back to a deterministic default, informational `CMP0030`) — in both
cases only that one identity loses its surface, every other member and
overload of the interface is unaffected.

A base interface's abstract declaration resolved by a more-derived
interface's own concrete (default-interface-member) redeclaration via `new`
is **not** a diamond collision (ADR-0044 Amendment 20) - the dominant
(derived) declaration gets a real `Configure()`/`Verify()` surface, and its
unconfigured fallback runs the interface's own real body instead of a
computed default; the losing (base) declaration purely forwards to it, so
both interface views share one call-recording state. See
`docs/packages/compono-testdoubles.md`'s "Default interface members" section
for the full example.

### Overload-safe argument matching (ADR-0044 Amendment 21)

The discriminator-only surface above still selects an overload by real
argument *type*, not by argument *content*. When a test needs to distinguish
calls to the **same overload** by their actual argument values, an eligible
overload (real parameters, no `ref`/`out`/`in`, not a self-referencing
generic parameter - the same eligibility conditions as the non-overloaded
matching surface above) also gets a second, matching-specific member name -
`<Member>Matching` - taking real `Match<T>` parameters directly:

```csharp
public interface IAmazonDynamoDB
{
    Task<DeleteItemResponse> DeleteItemAsync(DeleteItemRequest request, CancellationToken cancellationToken);
    Task<DeleteItemResponse> DeleteItemAsync(string tableName, CancellationToken cancellationToken);
}

client.Configure()
    .DeleteItemAsync(fallbackRequest, CancellationToken.None)
    .Returns(Task.FromResult(fallbackResponse));
client.Configure()
    .DeleteItemAsyncMatching(Match.Is<DeleteItemRequest>(x => x.TableName == "special"), Match.Any<CancellationToken>())
    .Returns(Task.FromResult(specialResponse));

client.Verify()
    .DeleteItemAsyncMatching(Match.Is<DeleteItemRequest>(x => x.TableName == "special"), Match.Any<CancellationToken>())
    .Once();
```

`DeleteItemAsyncMatching(...)` is a **configuration/verification-side alias
only** - it is never itself an independently-dispatched method the SUT can
call. Both it and the unchanged `DeleteItemAsync(realArgs, ...)`
discriminator surface attach to the **same real overload**'s entries/call
log: registration order gives precedence (last-matching-registration-wins,
same reverse-scan rule as "Multiple response configurations per member"
above), so a broad discriminator-only response registered first and a
narrower `.Matching(...)` override registered after it compose exactly like
two entries on a non-overloaded member would. `Verify().DeleteItemAsync(realArgs, ...)`
still reports the overload's total real call count, now backed by the same
call log. A literal argument on the `Matching`-named surface converts to
`Match<T>` exactly like it does everywhere else (Amendment 18) - it's
rejected only when two sibling overloads share the same `<Member>Matching`
name AND the literal is ambiguously convertible to both of their `Match<T>`
types (e.g. `Get(int)`/`Get(long)` called as `GetMatching(5)`, a real
`CS0121`), not as a blanket rule.

In the rare case a real interface member is literally named
`<Overload>Matching` and its own generated `Configure()` extension
signature would otherwise collide with the alias's, Compono disambiguates
automatically with a deterministic fallback name, the same way it already
does for other generated names that collide - no diagnostic, no dropped
capability, both surfaces stay independently reachable. A real *generic*
member sharing a closed-instantiation signature with the alias is a softer,
non-blocking case: that specific closed instantiation is reachable only via
an explicit type argument on the real member's own name, never implicitly.

## Generic methods (v2)

A generic method is supported when its return type doesn't reference its
own type parameter (Requirement 2) - `ILogger<T>`'s `Log<TState>`/
`BeginScope<TState>` is the motivating shape. The explicit implementation
stays generic (type parameters copied, constraints left unstated - they're
inherited automatically and redeclaring them is `CS0460`); the
`Configure()` extension itself stays **non-generic** for a solo generic
member - one backing slot covers every closed instantiation:

```csharp
public interface ILoggerLike
{
    void Log<TState>(int logLevel, TState state, Exception? exception);
    IDisposable? BeginScope<TState>(TState state) where TState : notnull;
}

logger.Configure().Log().Throws(new InvalidOperationException());
logger.Configure().BeginScope().Returns(myScope);
```

**Overloaded and generic together** (Amendment 1): the configuration
extension becomes generic too, purely for compile-time overload selection
- the backing slot still doesn't vary per closed type. This extension
*does* carry its constraint clauses verbatim (it's an ordinary standalone
generic method, not an interface implementation). An explicit type
argument is needed at the call site whenever ordinary overload-resolution
betterness rules wouldn't otherwise pick that overload (same as a real
call to the interface member itself).

A generic method whose return type depends directly on its own type
parameter (`T Get<T>()`, `Task<T> GetAsync<T>()`, and supported nullable
variants) can have per-closed-`T` configuration. Argument matching is
available for that shape only when the member's parameters are otherwise
matching-eligible and do not use the method's own open type parameter.

Still unsupported: unsupported generic-return shapes beyond the documented
per-closed-`T` cases, value-type-constrained `T?` (`System.Nullable<T>`),
and parameter shapes the generator cannot represent without reflection or
boxing. See `diagnostics.md` before guessing a workaround.

## Call verification (v2+)

`Verify()` — parallel to and independent from `Configure()`, returning a
 distinct wrapper so the two never collide — asserts how many times a member
was actually called. `Never()`/`Once()`/`Exactly(n)` are the terminal
count assertions. For argument-aware members, filtering happens in the
generated `Verify().Member(...)` extension before the terminal
 `CallVerifier` is returned:

```csharp
repository.Configure().CountAsync().Returns(Task.FromResult(5));
var order = await service.PlaceAsync(3);
repository.Verify().CountAsync().Once();
repository.Verify().Save().Once();
repository.Verify().UtcNow().Never(); // never read in this call path
```

```csharp
repository.Verify()
    .Save(Match.Is<Order>(order => order.Id == expectedId))
    .Once();
```

A failing assertion throws `Compono.TestDoubleVerificationException` (a
plain exception, not a framework assertion type). A call counts whether it
hits configured, default, or thrown behavior.

**Still deliberately minimal** — `Never`/`Once`/`Exactly(n)` only, no
`AtLeast`/`AtMost`, no `ReceivedCalls()`-style enumeration, no call-order
verification. An eligible overload's `<Member>Matching(Match<T>...)` surface
supports argument matching; same-name matcher-wrapped overload configuration
does not. If a test needs anything this page doesn't cover (call-order
verification, `ReturnsForAnyArgs`, etc.), use `Compono.NSubstitute` for that
interface instead — the two providers can coexist (see "Precedence with
`Compono.NSubstitute`" below).

## Argument matching and argument-filtered verification (v3)

For a member that is the only overload of its name in the interface, has
no real parameter referencing the member's own open generic type
parameter, has no real parameter of a ref-like type (`Span<T>` and
similar can't be a generic type argument), has no derived internal field
name colliding with another member's, and isn't a one-parameter `Equals`
(its extension would share arity with the inherited `object.Equals(object)`
and never actually be reachable) — five conditions, all required
(`docs/adr/0048-testdoubles-argument-matching-and-call-verification.md`
and its Amendment 1) — `Configure()`/`Verify()` accept `Compono.Match<T>`
per parameter instead of just the return value: a literal (equality
match), `Match.Any<T>()` (matches anything, same as omitting a matcher),
or `Match.Is<T>(predicate)`:

```csharp
repository.Configure()
    .Withdraw("acct-1", Match.Any<decimal>(), Match.Is<bool>(allowed => allowed))
    .Returns(true);

repository.Withdraw("acct-1", 50m, overdraftAllowed: true);  // true — every matcher satisfied
repository.Withdraw("acct-2", 50m, overdraftAllowed: true);  // falls through — accountId doesn't match

repository.Verify()
    .Withdraw(Match.Is<string>(id => id == "acct-1"), Match.Any<decimal>(), Match.Any<bool>())
    .Once();
```

An eligible member also keeps its original zero-argument `Configure()`/
`Verify()` spelling (`repository.Configure().Withdraw().Returns(...)`,
argument-independent, exactly v1/v2's shape) — the two aren't mutually
exclusive, and a member with no real parameters only ever had the
zero-argument form to begin with. A call whose arguments don't satisfy a
configured matcher is treated identically to an unconfigured member
(falls through to a computed default, or to "Configuration-required
members"'s throwing behavior below) — not a distinct failure mode.

**Why this doesn't apply to an overloaded member.** A real compiler spike
(ADR-0048's Decision Outcome) proved that wrapping every overload's
parameters in a matcher type breaks C#'s own overload resolution
unpredictably for several realistic parameter-type families (base/derived
class hierarchies, `string[]` vs. `IEnumerable<string>`, even plain `int`
vs. `long` widening) — there's no reliable per-family fix, so argument
matching is scoped out entirely for any member with more than one
overload. An overloaded member's `Configure()`/`Verify()` stay exactly
the per-overload discriminator shape above, unchanged. The same reasoning
excludes a generic method whose real parameters reference its own type
parameter (an `ILogger<TState>.Log<TState>`-shaped member) — a per-member
call log can't hold an open type parameter's value, so that shape keeps
its existing argument-independent `Configure()`/`Verify()` too, exactly
as it already worked.

**Why `Match<T>`, not `Arg<T>`.** `Compono.Arg` would collide with
`NSubstitute.Arg` for any consumer whose own namespace nests under
`Compono` (this repo's own samples convention) or who combines `Compono`
with `Compono.NSubstitute` directly — confirmed with a real failing build
during this feature's implementation, not a theoretical concern. `Match`
avoids the collision entirely and names the actual Compono concept
(matching an argument), rather than borrowing NSubstitute's own
vocabulary.

## Multiple response configurations per member (v3)

An eligible member (see above) — or a closed-instantiation-eligible
member (a generic method whose return type *is* its own sole type
parameter, or the sole type argument of `Task<T>`/`ValueTask<T>` including
the `T?` forms when `T : class`; see
`docs/packages/compono-testdoubles.md`'s "Per-closed-instantiation
configuration" section) — isn't limited to one `Configure()` call. Each
call **appends** a new, independent response configuration instead of
overwriting the previous one — a broad default and one or more narrower,
argument-distinguished overrides can coexist on the same member in the
same test:

```csharp
repository.Configure()
    .Withdraw(Match.Any<string>(), Match.Any<decimal>(), Match.Any<bool>())
    .Returns(false);
repository.Configure()
    .Withdraw("acct-1", Match.Any<decimal>(), Match.Any<bool>())
    .Returns(true);

repository.Withdraw("acct-1", 50m, overdraftAllowed: true);  // true — the more specific entry
repository.Withdraw("acct-9", 50m, overdraftAllowed: true);  // false — falls through to the default entry
```

**Precedence: last matching registration wins.** A call dispatches to the
*most recently registered* `Configure()` entry whose matchers all match —
registration order, not matcher "specificity", decides which entry wins
when more than one entry could match the same call. There's no comparison
between matchers (a `Match.Is<T>(predicate)` entry is never treated as
"more specific" than a `Match.Any<T>()` entry, for example) — if two
entries could both match a call, whichever was configured later wins,
full stop. This keeps dispatch simple and its outcome fully determined by
the order `Configure()` calls appear, with no ranking heuristic to reason
about.

**Compatibility note (pre-1.0).** Before this capability existed, a
second `Configure()` call on the same member *overwrote* the first —
observable as the second call always winning, since only one
configuration could exist at a time. That's now a special case of
"last matching registration wins": a second call still wins whenever it
could have won before (it's always the most recently registered, and an
argument-independent `Configure()` call always matches), so ordinary,
single- or sequential-override usage is unaffected. What changes is that
the *first* configuration is no longer discarded — it's still reachable
by any call the second configuration's matchers don't cover, rather than
falling through to the member's deterministic default. This is an
intentional pre-1.0 semantic correction, not a breaking change to guard
against: the previous overwrite behavior was never separately documented
as guaranteed, and every existing single-`Configure()`-call usage keeps
its exact same observable behavior.

**What this deliberately doesn't do.** No matcher-specificity ranking
(see above). Sequential/call-count-based responses use `ReturnsSequence(...)`
(see "Sequential/call-count-based responses"). No `Returns(Func<...>)`
callback responses. Verification (`Verify()`) is completely unaffected — it
stays a count over the member's shared call log, independent of how many
response configurations exist.

## Configuration-required members (v2)

A member returning a non-nullable reference type (or `Task<T>`/
`ValueTask<T>` wrapping one) with no deterministic default used to reject
the *whole interface* (v1's `CMP0025`). As of v2
(`docs/adr/0045-testdoubles-configuration-required-members.md`), that
member instead generates as **configuration-required**, provided it would
otherwise have a real `Configure()`/`Verify()` surface — it throws
`Compono.TestDoubleNotConfiguredException` if invoked before
`Configure().Member(...).Returns(...)`/`.Throws(...)`, rather than falling
back to a computed default:

```csharp
context.Configure().AwsRequestId().Returns("test-request-id");
```

**Migration implication, not just a new feature:** when migrating a test
off `Compono.NSubstitute`, "the interface now generates" is no longer
proof every member is safe to call unconfigured — some members that used
to block the whole interface now generate *and* require explicit setup
before use. Check the generator's `CMP0032` diagnostic (one per interface,
a count) to know how many members on a given interface need
`Configure(...)` before the test exercises them; don't assume every
generated member has a usable default just because generation succeeded.
This applies identically to sync/async/property members and to a fluent
self-returning member (`IResponseBuilder`-shaped) — none of those get
special-cased, all follow the same rule.

## The #1 AutoFixture/NSubstitute-habit trap: matching is not capture

`Compono.TestDoubles` is not a general-purpose mocking framework, but it
does support argument matching and argument-filtered verification for the
eligible member shapes above. The remaining boundary is stronger behavior
that needs access to the actual invocation as a first-class value:

- true argument capture for later arbitrary inspection outside a generated
  `Verify().Member(Match...)` count assertion;
- invocation-aware callback responses (`Returns(call => ...)`,
  `Returns(Func<CallInfo, T>)`, or "invoke this delegate argument and use
  its result");
- callback side effects based on the actual invocation;
- call-order verification;
- strict mode, partial substitutes, recursive auto-configuration;
- classes, delegates, indexers, events, and other unsupported shapes listed
  below.

If a test only needs "this member was called once with an argument matching
this predicate," use `Verify().Member(Match.Is<T>(...)).Once()`. If it
needs to store every argument for arbitrary later inspection, run code from
a callback, or invoke a delegate argument, that is a different capability;
use an existing project-local fake or `Compono.NSubstitute` where the
project intentionally keeps that dependency, and treat any real
`Compono.NSubstitute`-can/`Compono.TestDoubles`-cannot case as roadmap
 evidence under ADR-0042 Amendment 2.

## Unsupported shapes are compile-time diagnostics, not silent gaps

**Classes and delegates are not test-double candidates at all** —
`LeafTypeClassifier` only ever admits interfaces for generated-double
eligibility, so neither one is diagnosed here or falls back to this
package's provider; a concrete class still composes through ordinary
constructor selection, and a delegate leaf stays provider-resolved (a
runtime `CompositionException` if no provider handles it, not a `CMP002x`
diagnostic).

**`Microsoft.Extensions.Logging.ILogger`/`ILogger<T>` are also never
test-double candidates when `Compono.Logging`'s generation
(`ComponoGeneratedLogging`) is enabled** — ADR-0055 Amendment 4: those two
types become Logging-owned abstractions, excluded from
`Compono.TestDoubles`-eligibility entirely (no double, no `Verify()`
extension for them), regardless of `ComponoGeneratedTestDoubles`. This
fixes a real compile-time collision — without it, this package's generated,
exact-typed `Verify(this ILogger<T>)` extension silently wins C# overload
resolution over `Compono.Logging`'s own `Verify(this ILogger)`, breaking
`Compono.Logging`'s verification API. If `Compono.Logging` isn't
referenced, or its generation is explicitly disabled
(`ComponoGeneratedLogging=false`), `ILogger`/`ILogger<T>` behave exactly as
any other interface for this package — unchanged.

For an eligible **interface**, indexers, events, a genuinely unimplemented
 static abstract member, a generic method whose return type depends on its
 own type parameter, a generic type parameter used as `T?` (constrained or
 not), and a handful of narrower shapes (set-only properties,
 pointer/function-pointer parameters or returns, ref-like returns) can
 withhold generated-double support at compile time. Whole-interface codes
 fall back to the ordinary runtime-provider path, same as an interface the
 compile-time opt-in never reached. Scoped codes only withhold the affected
 member's `Configure()`/`Verify()` surface or DIM fallback; `CMP0032` is an
 informational count of configuration-required members. All
 generated-test-double diagnostics (`CMP0020`-`CMP0032` and
 `CMP0035`-`CMP0037`) are informational — see `diagnostics.md` for each
 code's exact scope and disposition before guessing a fix. Overloaded
 members, a `ref`/`out`/`in` parameter, and a generic method independent of
 its own type parameter are narrower now (see above) — only the specific
 colliding/unsupported overload loses its surface, not the whole interface.
 A non-nullable-reference return with no deterministic default no longer
 rejects the whole interface either (v2, see "Configuration-required
 members" above) — unless it also lacks a `Configure()` surface for one of
 those other reasons, in which case it still does.

A static abstract member declared on a base interface but already resolved
by a more-derived interface's own concrete implementation (C#'s "most
specific implementation" rule — the `IAmazonS3`/`IAmazonService` shape) is
**not** a genuinely unimplemented member at all and doesn't reject anything;
only a static abstract member with no override anywhere in the interface's
hierarchy still whole-interface-rejects (ADR-0046).

## Precedence with `Compono.NSubstitute`

```csharp
var composer = Composer.Create(builder => builder
    .UseGeneratedTestDoubles()
    .UseNSubstitute());
```

Both providers can be registered together. Registration order decides which
one resolves an interface request first — `UseGeneratedTestDoubles()`
registered before `UseNSubstitute()` means any interface the generator
emitted a double for resolves to the generated double; an interface that
never got a generated double falls through to `NSubstituteProvider` (or to
composition failure if neither provider claims it). This is the same "tried
in registration order" contract every provider already follows — no
special-cased precedence logic exists between these two specifically.

## Combining with `[Shared]`

`Compono.XunitV3`:

```csharp
[Theory]
[Compose<GeneratedTestDoubleProfile>]
public async Task Saves_order([Shared] IRepository repository, OrderService service)
{
    repository.Configure().CountAsync().Returns(Task.FromResult(4));
    var order = await service.PlaceAsync(6);
    repository.Verify().Save(Match.Is<Order>(saved => saved.Id == order.Id)).Once();
}
```

`Compono.TUnit` — same shape, `[Test]` instead of `[Theory]`:

```csharp
[Test]
[Compose<GeneratedTestDoubleProfile>]
public async Task Saves_order([Shared] IRepository repository, OrderService service)
{
    repository.Configure().CountAsync().Returns(Task.FromResult(4));
    var order = await service.PlaceAsync(6);
    repository.Verify().Save(Match.Is<Order>(saved => saved.Id == order.Id)).Once();
}
```

`[Shared]` (in `Compono.XunitV3` or `Compono.TUnit`) is what lets you both
configure/verify a double *and* have it wired into the composed system under
test — see `registrations-profiles-and-scopes.md`. Without `[Shared]`, a
double-typed parameter and a double nested inside another composed type
would be two different generated-double instances.
