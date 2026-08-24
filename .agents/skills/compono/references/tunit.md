# Compono.TUnit

Only relevant if the project references `Compono.TUnit`. Requires real
TUnit (`TUnit`/`TUnit.Core` + Microsoft Testing Platform runner). Depends
on `Compono` (the source generator flows through transitively).

The full attribute family has shipped: `[Compose]`, `[Compose<TProfile>]`,
and `[Compose<TProfile, TConfig>]`, method-parameter-only — see ADR-0040
for the full design.

## `[Compose]`

```csharp
[Test]
[Compose]
public async Task ComposedValuesAreProducedForEveryParameter(int quantity, string productName) { }

[Test]
[Compose(42, "widget")]           // inline binds positionally left-to-right
public async Task InlineValuesAreUsedDirectly(int quantity, string productName) { }

[Test]
[Compose(42)]                     // quantity inline, productName composed
public async Task MixesInlineAndComposedValues(int quantity, string productName) { }

[Test]
[Compose(Seed = 4219)]
public async Task ReproducesTheSameComposedValues(Order order) { }
```

- Inline values bind **positionally**, never by parameter name.
- `Seed` is a plain non-negative `int`; negative throws immediately,
  before any row state is reported.
- `[Shared]` parameters compose first, in declaration order, before
  non-shared parameters — a distinct attribute type from
  `Compono.XunitV3.SharedAttribute`, with identical binding rules
  (duplicated per ADR-0040's "Row-binding logic: duplicated, not
  extracted" section).
- A passing row reports its seed back as a `Compono.Seed` custom property
  (`TestContext.Current.Metadata.TestDetails.CustomProperties`) —
  TUnit's own place for this, distinct from `Compono.XunitV3`'s trait
  mechanism. Check it in test output before asking for a re-run.
- Composition happens at data-generation time, not a separate discovery
  pass.

## `[Compose<TProfile>]`

```csharp
[Test]
[Compose<OrderTestProfile>]
public async Task Creates_service(
    [Shared] IOrderRepository repository,
    OrderService service,
    CreateOrder command)
{
}
```

Same behavior as `[Compose]`, but applies `TProfile.Configure` to the
row's builder first — this is how a test picks up
`UseNSubstitute()`/`UseBogus()`/registrations for that specific test.

## `[Compose<TProfile, TConfig>]`

```csharp
public enum RepositoryKind { Player, Game }

public sealed record RepositoryConfig(RepositoryKind Repository);

public sealed class RepositoryProfile : ICompositionProfile
{
    public RepositoryProfile(RepositoryConfig config) => Config = config;
    public RepositoryConfig Config { get; }
    public void Configure(CompositionBuilder builder) =>
        builder.Register<IRepository>(_ => RepositoryFactory.Create(Config.Repository));
}

[Test]
[Compose<RepositoryProfile, RepositoryConfig>(RepositoryKind.Player)]
public async Task Handles_PlayerRepository(IRepository repository) { }
```

Use this when a profile needs a value only known at **this specific
test's call site** - not a fixed, default-constructed profile the way
`[Compose<TProfile>]` always is. `TConfig`'s constructor arguments here
(**profile configuration arguments**) are a completely different binding
target from this file's inline values above - they never bind to the
test method's own parameters, all of which are still composed in full.

- `TConfig` must have exactly one public constructor; `TProfile` must have
  exactly one public constructor accepting exactly one `TConfig`-typed
  parameter. Either shape being wrong is a clear `CompositionException`
  raised during composer/profile initialization (`ApplyProfile`, inside
  the base class's cached `Lazy<Composer>`) - before `BindingPlan` is
  ever built, not a compile error (`[Compose<TProfile>]`'s `new()`
  constraint doesn't carry over to this form - see
  `docs/adr/0036-parameterized-composition-profile-selection.md`).
- **Use the strongest attribute-legal type for each argument** - an
  `enum` for a finite choice, `typeof(...)` for a CLR type, `bool`/numeric
  where that's already the real meaning. `params object?[]` is a binding
  mechanism C# attribute rules force, not a reason to design `TConfig`
  around magic strings.
- **This is not the same problem as name-based value selection.** A value
  that varies by which parameter/member is *asking* (not by test call
  site) is a `CompositionProviderRequest.Name`-matching custom
  `ICompositionValueProvider` question - see
  `registrations-profiles-and-scopes.md`. Don't reach for
  `[Compose<TProfile, TConfig>]` for that case, and don't reach for a
  custom provider for this one.
- **Don't reach for this form by default.** If a fixed, default-constructed
  profile already covers it, the plain `[Compose<TProfile>]` form is
  enough - reserve this one for a value that's genuinely different per
  call site and needs to reach configuration logic running *inside* the
  profile.

## Disposal — read before assuming automatic cleanup

TUnit disposes a `[Compose]`-composed **root** method argument itself,
automatically, once the test completes. A non-`[Shared]` dependency
**nested** inside a composed argument is disposed by no one — TUnit's own
nested-object disposal tracking is scoped to `IAsyncInitializer`-registered
properties, not a general graph walk. Don't compose a cross-test-shared
disposable as `[Compose]`/`[Shared]` either — TUnit's shared-value
reference counting has no provenance awareness of where a value came
from. See ADR-0040's "Diagnostics, disposal, and seed observability"
section for the full reasoning — don't assume `Compono.XunitV3`'s own
disposal story (no automatic disposal at all, PR #24) carries over
unchanged; the two packages differ here because TUnit's own execution
model differs from xUnit v3's.

## Hard constraint: one Compose-family attribute per method

`[Compose]` and `[Compose<TProfile>]` are both `ComposeAttribute`
subclasses. Two **different** Compose-family attributes on one method
(e.g. `[Compose]` + `[Compose<ProfileA>]`) *compile* but throw
`CompositionException` at data-generation time, not compile time —
`BindingPlan.ValidateSignature` resolves the method's own `MethodInfo`
(via a parameter's `ReflectionInfo.Member`, or - for a zero-parameter
method, which has no parameter to read that from - an arity-aware
`GetMethods()` filter matched on name, zero declared parameters, *and*
generic arity together, not a plain `GetMethod(name, Type.EmptyTypes)`
call, which would throw `AmbiguousMatchException` for a class declaring
both a zero-parameter `Run()` and a zero-parameter-but-generic `Run<T>()`)
and counts `ComposeAttribute`-derived attributes on it. The identical
attribute type twice on one method **is** a compiler error
(`AllowMultiple=false`).

**There is no equivalent of stacking multiple data-source attributes on
one method.** If a test needs several independent inline+composed
combinations, split into separate `[Test]`/`[Arguments]` methods — don't
try to layer multiple Compose-family attributes to get that effect.

## No fixture object

There's nothing like AutoFixture's `IFixture` to hold onto across a test
class.

## Real examples in this repo

- `test/Compono.TUnit.SampleTests/CompositionTests.cs` — a plain
  `[Compose]`-composed `OrderService` through the real packaged
  `Compono.TUnit -> Compono` dependency (not a `ProjectReference`).
- `test/Compono.TUnit.SampleTests/SharedTests.cs` — `[Shared] Repository
  repository, OrderService service`.
- `test/Compono.TUnit.SampleTests/DisposalTests.cs` — the root-disposed
  vs. nested-not-disposed proof, using a plain purpose-built
  `IDisposable` type, not a mocking-library substitute.
- `test/Compono.TUnit.SampleTests/NSubstituteTests.cs` —
  `[Compose<NSubstituteTestProfile>] async Task Saves_order([Shared]
  IOrderRepository repository, CreateOrderHandler handler, PlaceOrder
  command)`.
