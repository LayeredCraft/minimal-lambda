# Registrations, profiles, rules, scopes, and shared values

How to make Compono use a specific value/factory/instance instead of
generating one from scratch, and how identity/sharing works across a
composed graph.

## `Register<T>()` — exact-type registration

```csharp
builder.Register<IClock>(context => new SystemClock());
builder.Register<int>(() => 42);
```

Pipeline stage 3, exact-type-keyed. **A second `Register<T>()` for the
same `T`** — direct, via a profile, or across two profiles — **is a
build-time `CompositionConfigurationException`, not last-write-wins.**
This is the single biggest AutoFixture-habit trap: AutoFixture
customizations happily re-customize the same type; Compono throws. If two
things both want to configure `T`, consolidate into one registration —
don't stack a second one hoping it overrides the first.

`UseServiceProvider(IServiceProvider provider)` is a stage-3 fallback,
tried only after every exact `Register<T>()` misses. Compono calls
`GetService(Type)` directly — it never creates, resolves from, or
disposes a scope on your behalf. Calling it twice is also a conflict.

## Type/member rules — `.For<T>()`

```csharp
builder.For<string>().Use("from-type-rule");
builder.For<Customer>().Member(x => x.Email).Use("literal@example.com");
builder.For<Order>().Member(x => x.PlacedAt)
    .Use(context => context.Resolve<IClock>().UtcNow);
```

A member rule always wins over a type rule for the same value
(specificity-based dispatch, not call order). `.Member(...)` requires a
**direct** property/field access expression — `x => x.Email.Length`
throws `ArgumentException` immediately at the `.Member(...)` call, not
deferred to composition time. A duplicate rule for the same type, or the
same (type, member) pair, is a build-time conflict, same as
`Register<T>()`.

Don't reuse a member rule across unrelated types hoping it'll apply
broadly — if a rule should really apply everywhere, use a type rule or
`Register<T>()` instead, not a copy-pasted member rule per type.

## Constructor selection — `.For<T>().UseConstructor<...>()`

A type with exactly one accessible constructor never needs this — Compono
selects it automatically. A type with more than one accessible
constructor and no selection is `CMP0001`. Fix it by naming the intended
constructor's own parameter types, in order:

```csharp
public sealed class Foo
{
    public Foo() { }
    public Foo(IBar bar, IBaz baz) { }
}

builder.For<Foo>().UseConstructor<IBar, IBaz>();
```

Compono still composes `IBar`/`IBaz` itself, recursively, exactly like an
unambiguous constructor's parameters — the generated code is a direct
`new Foo(bar, baz)`, never a stored/invoked delegate, never reflection.
Works identically for a type you own and a BCL/third-party type you don't
(`HttpClient`, `Exception`) — nothing is annotated on `Foo` itself.

**`Register<T>` vs. `UseConstructor<...>()` — do not conflate these:**

| | `Register<T>` | `UseConstructor<...>()` |
|---|---|---|
| Who builds it | You (a real runtime factory) | Compono (the generated plan) |
| Use when | You need a *specific, already-configured* value, or logic beyond plain construction | The type just has more than one constructor and Compono should keep composing it |

Never recommend `UseConstructor<...>()` for a case that actually needs a
specific pre-built value (e.g. an `HttpClient` wrapping a particular
`TestHttpHandler`-created handler) — there's no parameter-type list that
expresses "the exact instance my fixture already configured"; that stays
`Register<T>`/an interface wrapper. Never recommend
`[CompositionConstructor]` or any attribute on the composed type — not
shipped, explicitly rejected as the mechanism. Never recommend picking
"the constructor with the most parameters" or "the first one that
resolves" — always name the exact selection.

**Scope is compilation-wide, not per-profile**: a selection anywhere in
the compilation applies to every composition of that type. A second,
different selection for the same type anywhere in the compilation is
`CMP0033`; an identical repeated selection is harmless. A selection
matching no accessible constructor is `CMP0034`. Per-profile constructor
selection isn't supported — don't imply it is.

## `ICompositionProfile`

```csharp
public sealed class OrderTestProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder)
    {
        builder.UseNSubstitute();
        builder.Register<IClock>(_ => new FixedClock(DateTimeOffset.UtcNow));
    }
}

var composer = Composer.Create(b => b.AddProfile<OrderTestProfile>());
// or: b.AddProfile(new OrderTestProfile());
```

- Pure configuration, applied synchronously exactly once.
- `AddProfile<TProfile>()` requires a parameterless constructor;
  `AddProfile(instance)` for one that doesn't.
- Multiple `AddProfile<...>()` calls all apply, in the order added.
- A profile applying itself (directly or through nesting) is a
  `CompositionConfigurationException` (`ProfileCycle`), immediately — not
  a silent no-op.
- A profile is configuration, not a base class, lifecycle hook, or place
  for assertions.

**Prefer several small, focused profiles** (`InfrastructureProfile`,
`DomainProfile`) named after the *concern* they configure, not the
consumer/test class that happens to use them — don't grow one giant
catch-all profile.

## Custom providers — matching on request shape, including name

`Register<T>()`/`.For<T>()` are exact-type-keyed. When a value genuinely
needs to vary by the **requesting parameter/member's own name**, not just
its type — several distinct values of the same declared type, chosen by
which parameter is asking — write a custom `ICompositionValueProvider`
instead:

```csharp
public sealed class UpsellPayloadProvider : ICompositionValueProvider
{
    public CompositionProviderResult TryProvide(in CompositionProviderRequest request, ICompositionContext context)
    {
        if (request.RequestedType != typeof(UpsellPayload))
            return CompositionProviderResult.NotHandled;

        return request.Name switch
        {
            "newGamePayload" => CompositionProviderResult.Handled(new UpsellPayload("new-game")),
            "lockedPackPayload" => CompositionProviderResult.Handled(new UpsellPayload("locked-pack")),
            _ => CompositionProviderResult.NotHandled,
        };
    }
}

builder.AddSemanticProvider(new UpsellPayloadProvider());
// or AddTestDoubleProvider(...), depending on what it produces
```

Providers run for each matching request. Return `NotHandled` when this
provider does not own the request so later pipeline stages can try; return
`Handled(value)` only for a value this provider deliberately supplies.
Keep providers stateless unless shared state is part of documented test
setup. Use `context.DeriveSeed()` for provider-owned random generation so a
seeded composition remains reproducible. Prefer `Register<T>()` or a
`.For<T>()` rule whenever exact type or member selection is sufficient:
custom providers are for request-shape decisions, not general configuration.

`CompositionProviderRequest.Name` carries the requesting constructor
parameter/required member/test-method-parameter's own name — this is a
**global rule** ("whenever anything asks for `UpsellPayload` named
`newGamePayload`, produce this"), evaluated for every matching request
across every test. Reserve this for the case that genuinely needs to
match on request shape rather than a fixed type — most AutoFixture
specimen builders migrate to a plain `Register<T>()` instead (see
`patterns-and-antipatterns.md`'s mapping table).

**Don't confuse this with `[Compose<TProfile, TConfig>]`'s profile
configuration arguments** (`xunit-v3.md`) — a `Name`-based provider is a
global rule keyed off the requesting parameter's name; a profile
configuration argument is a per-invocation value known only at one
specific test's call site. They solve different problems and aren't
interchangeable.

## Scopes and recursion

A type appearing twice in a graph is **not** automatically a cycle — a
genuine cycle is a type whose *construction is still in progress* when
re-requested. That fails fast with a path-annotated `CompositionException`
— there is no AutoFixture `OmitOnRecursionBehavior` equivalent. Break a
real self-reference with an explicit `Register<T>()` factory that
supplies the recursive edge deliberately (e.g. `null`, or a pre-built
instance) instead of asking Compono to silently omit it.

## `Share<T>()` — graph-wide sharing (core `Compono`)

`CompositionBuilder.Share<T>()` is the core, framework-independent
graph-wide sharing declaration — see
[ADR-0056](../../../docs/adr/0056-composition-builder-share-graph-wide-sharing.md).
Declared once (typically in a profile), it makes *every* request for `T`
anywhere in one root composition graph resolve to the same instance —
**no `[Shared]` attribute needed on the retrieving parameter**:

```csharp
public sealed class OrderProfile : ICompositionProfile
{
    public void Configure(CompositionBuilder builder) => builder.Share<Repository>();
}

[Theory]
[Compose<OrderProfile>]
public void ServiceUsesTheSharedRepository(Repository repository, OrderService service)
{
    // `repository` is an ordinary, undecorated parameter - reference-equal
    // to `service`'s own internally-composed Repository dependency.
}
```

- **Lazy**: the shared instance is created on first request, not eagerly
  when `Share<T>()` is called.
- **Idempotent**: calling it more than once for the same type - directly,
  or once via each of two profiles - changes nothing.
- **Composes with `Register<T>()` in either order**: a registered value
  plus `Share<T>()` shares the registered instance instead of a freshly
  generated one.
- **Lifetime boundary**: one root composition graph - one
  `Composer.Create<T>()` call, one item of a `CreateMany<T>(count)` batch,
  or one `CompositionRow`. Never shared across independent `Create<T>()`
  calls or across `CreateMany` items - don't suggest it as a way to share
  state between separate compositions.
- **Works under a plain `Composer.Create<T>()`/`CreateMany<T>()` call with
  no test framework involved at all**, as well as under `CompositionRow`
  (`Composer.CreateRow`, or `[Compose]` row binding) - unlike `[Shared]`,
  this is a core `Compono` concept, not a `Compono.XunitV3`/`Compono.TUnit`
  attribute.
- Disposal is explicitly out of scope, same as everywhere else in
  `Compono` today.

**Blast-radius warning**: adding `Share<T>()` to a profile several tests
already reuse changes sharing semantics for *every* graph composed with
that profile, silently, for any test structurally reaching the type more
than once - a materially larger blast radius than adding `[Shared]` to one
test method. Prefer a narrowly-scoped profile, or `[Shared]` on the one
test that needs it, over adding `Share<T>()` to a broadly-shared profile
without checking what else composes through it.

## `[Shared]` (`Compono.XunitV3`/`Compono.TUnit` only)

`Compono.XunitV3`:

```csharp
[Theory]
[Compose]
public void ServiceUsesTheSharedRepository(
    [Shared] Repository repository,
    OrderService service)
{
    // `service`'s internally-composed Repository dependency
    // is reference-equal to `repository`.
}
```

`Compono.TUnit` — same shape, `[Test]` instead of `[Theory]` (TUnit's own
attribute, not xUnit's):

```csharp
[Test]
[Compose]
public async Task ServiceUsesTheSharedRepository(
    [Shared] Repository repository,
    OrderService service)
{
    // `service`'s internally-composed Repository dependency
    // is reference-equal to `repository`.
}
```

- Type-keyed, not name-keyed: every parameter or nested dependency
  requesting exactly that type within the row reuses the same instance.
- `[Shared]` parameters resolve first, in declaration order, before
  non-shared parameters — so anything depending on the shared instance
  always sees it already available.
- Two `[Shared]` parameters of the same type on one method is an error —
  there's no way to know which one is "the" shared value.
- `[Shared]` (unlike `Share<T>()` above) only exists inside a `[Compose]`
  row — `Compono.XunitV3`'s `SharedAttribute` and `Compono.TUnit`'s own
  distinct `SharedAttribute` (same binding rules, duplicated rather than
  shared per ADR-0040's "Row-binding logic: duplicated, not extracted" —
  see `references/tunit.md`), whichever package the project references.
  Don't suggest `[Shared]` for a programmatic (non-`[Compose]`)
  composition — use `Share<T>()`, or a `Register<T>()` factory that
  returns the same captured instance, instead.

**Reach for `Share<T>()` when the sharing intent is reusable** — declared
once in a profile, applying to every test that uses it, with no
`[Shared]` attribute needed on the retrieving parameter. **Reach for
`[Shared]` for a one-off, single-test case** that doesn't warrant a
profile change. The two aren't mutually exclusive. **Don't overuse
either.** They're for a real identity requirement (the system under test
and the assertion need to reference the *same* instance), not "make
things consistent" and not a performance optimization — ordinary
composition is already cheap. When migrating from AutoFixture's
`[Frozen]`, audit each usage: many `[Frozen]` interface parameters were
only there to get a substitute in the first place, not to share it — once
`Compono.NSubstitute`'s `UseNSubstitute()` is active, composing an
interface already produces a substitute automatically, and no sharing
mechanism is needed unless identity actually matters.
