# Compono.XunitV3

Only relevant if the project references `Compono.XunitV3`. Requires real
xUnit v3 (`xunit.v3` + Microsoft Testing Platform runner) — not xUnit v2.
Depends on `Compono` (the source generator flows through transitively).

## `[Compose]`

```csharp
[Theory]
[Compose]
public void ComposedValuesAreProducedForEveryParameter(int quantity, string productName) { }

[Theory]
[Compose(42, "widget")]           // inline binds positionally left-to-right
public void InlineValuesAreUsedDirectly(int quantity, string productName) { }

[Theory]
[Compose(42)]                     // quantity inline, productName composed
public void MixesInlineAndComposedValues(int quantity, string productName) { }

[Theory]
[Compose(Seed = 4219)]
public void ReproducesTheSameComposedValues(Order order) { }
```

- Inline values bind **positionally**, never by parameter name.
- `Seed` is a plain non-negative `int`; negative throws immediately.
- `[Shared]` parameters compose first, in declaration order, before
  non-shared parameters — see `registrations-profiles-and-scopes.md`.
- Every row carries a `Compono.Seed` xUnit trait unconditionally, pass or
  fail — check it in test output before asking for a re-run.
- Composition happens at execution time, not discovery time — there's no
  separate "composed values shown in the test explorer" pass.

## `[Compose<TProfile>]`

```csharp
[Theory]
[Compose<OrderTestProfile>]
public void Creates_service(
    [Shared] IOrderRepository repository,
    OrderService service,
    CreateOrder command)
{
}
```

Same behavior as `[Compose]`, but applies `TProfile.Configure` to the
row's builder first — this is how a theory picks up
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

[Theory]
[Compose<RepositoryProfile, RepositoryConfig>(RepositoryKind.Player)]
public void Handles_PlayerRepository(IRepository repository) { }
```

Use this when a profile needs a value only known at **this specific
test's call site** - not a fixed, default-constructed profile the way
`[Compose<TProfile>]` always is. `TConfig`'s constructor arguments here
(**profile configuration arguments**) are a completely different binding
target from this file's inline values above - they never bind to the
test method's own parameters, all of which are still composed in full.

- `TConfig` must have exactly one public constructor; `TProfile` must have
  exactly one public constructor accepting exactly one `TConfig`-typed
  parameter. Either shape being wrong is a clear, cached
  `CompositionException` at binding-plan-construction time, not a compile
  error (`[Compose<TProfile>]`'s `new()` constraint doesn't carry over to
  this form - see `docs/adr/0036-parameterized-composition-profile-selection.md`).
- **Use the strongest attribute-legal type for each argument** - an
  `enum` for a finite choice, `typeof(...)` for a CLR type, `bool`/numeric
  where that's already the real meaning. `params object?[]` is a binding
  mechanism C# attribute rules force, not a reason to design `TConfig`
  around magic strings. Flag `[Compose<TProfile, TConfig>("SomeString")]`
  in review the same way you'd flag any other stringly-typed value
  standing in for a finite choice.
- **This is not the same problem as name-based value selection.** A value
  that varies by which parameter/member is *asking* (not by test call
  site) is a `CompositionProviderRequest.Name`-matching custom
  `ICompositionValueProvider` question - see
  `registrations-profiles-and-scopes.md`. Don't reach for
  `[Compose<TProfile, TConfig>]` for that case, and don't reach for a
  custom provider for this one.
- **Don't reach for this form by default.** If the "parameter" a
  migrated AutoFixture attribute takes is really just obtaining a
  substitute, or a single fixed value that never actually varies across
  real call sites, the plain forms already cover it - reserve this one
  for a value that's genuinely different per call site and needs to
  reach configuration logic running *inside* the profile.

## Hard constraint: one Compose-family attribute per method

`[Compose]` and `[Compose<TProfile>]` are both `DataAttribute` subclasses.
Two **different** Compose-family attributes on one method (e.g.
`[Compose]` + `[Compose<ProfileA>]`) *compile* but throw
`CompositionException` at data-binding time, not compile time — the
signature is only validated once xUnit actually asks the attribute for
its row data. The identical attribute type twice on one method **is** a
compiler error (`AllowMultiple=false`).

**There is no equivalent of stacking multiple `[InlineAutoData(...)]`
rows on one method.** If a test needs several independent inline+composed
combinations, split into separate `[Theory]`/`[InlineData]` methods —
don't try to layer multiple Compose-family attributes to get that effect.

## No fixture object

There's nothing like AutoFixture's `IFixture` to hold onto across a test
class. Configuration is per-test via `[Compose<TProfile>]`; don't invent
a shared fixture-holder pattern to route around this.

## Real examples in this repo

- `test/Compono.XunitV3.SampleTests/SharedTests.cs` — `[Shared] Repository
  repository, OrderService service, CreateOrder command`.
- `test/Compono.XunitV3.SampleTests/NSubstituteTests.cs` —
  `[Compose<NSubstituteTestProfile>] async Task Saves_order([Shared]
  IOrderRepository repository, CreateOrderHandler handler, PlaceOrder
  command)`.
- `test/Compono.XunitV3.SampleTests/BogusTests.cs` — a profile combining
  `UseBogus().UseNSubstitute()`, composing a `Customer` with `required
  string FirstName/LastName/Email` matched via Bogus conventions.
- `test/Compono.XunitV3.SampleTests/FailingCompositionTests.cs` — a
  deliberately failing `[Compose(Seed = 24601)]` test, useful as a
  reference for what the real `dotnet test` failure output looks like.
