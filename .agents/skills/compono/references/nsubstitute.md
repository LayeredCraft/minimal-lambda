# Compono.NSubstitute

Only relevant if the project references `Compono.NSubstitute`. Never
suggest `UseNSubstitute()` if the package isn't referenced — install it
first, only if the user asks.

```csharp
var composer = Composer.Create(builder => builder.UseNSubstitute());

// configured:
builder.UseNSubstitute(o => o.SubstituteAbstractClasses = false);
```

- `NSubstituteProvider` runs at pipeline stage 6 (test-double providers).
  It handles any request where the requested type is substitutable:
  `IsInterface`, or a delegate type (`IsSubclassOf(MulticastDelegate)`),
  or — when `SubstituteAbstractClasses` is `true` (the default) — an
  unsealed, non-interface, non-delegate abstract class.
- It produces a bare `Substitute.For([requestedType], [])` — nothing
  more.
- `NSubstituteOptions.SubstituteAbstractClasses` (`bool`, default
  `true`). Turning it off means an abstract-class request throws
  `CompositionException` instead of being substituted or constructed
  directly — abstract types are **always** provider-resolved, they never
  silently fall back to direct construction.

## The #1 AutoFixture-habit trap: no member auto-configuration

Every substitute Compono produces is a bare `Substitute.For<T>()`. There
is **no** equivalent of `AutoNSubstituteCustomization { ConfigureMembers
= true }`. An unstubbed member that returns `Task<T>` returns
NSubstitute's own default (`Task.FromResult<T>(default)`), **not** a
recursively-composed value.

If you're migrating a test that relied on `ConfigureMembers = true`
implicitly returning composed values from unstubbed members, expect
`NullReferenceException`s on first run — stub the members that matter
explicitly, per-test, rather than looking for a global auto-configure
switch (there isn't one).

## Combining with `[Shared]`

```csharp
[Theory]
[Compose<NSubstituteTestProfile>]
public async Task Saves_order(
    [Shared] IOrderRepository repository,
    CreateOrderHandler handler,
    PlaceOrder command)
{
    // `repository` is the exact substitute `handler` was composed with —
    // assert against it directly (e.g. repository.Received(1).Save(...)).
}
```

`[Shared]` is what lets you both assert against a substitute *and* have
it wired into the composed system under test — see
`registrations-profiles-and-scopes.md`. Without `[Shared]`, a
substitute-typed parameter and a substitute nested inside another
composed type would be two different `Substitute.For<T>()` instances.
