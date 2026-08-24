# Composition model

How `composer.Create<T>()` actually works, when `[Composable]` is (and
isn't) needed, and how determinism/seeding fits in. Read this before
composing any type, and before telling a user their type "needs an
attribute" — most don't.

## `Composer`

`Composer` is immutable once built:

```csharp
var composer = Composer.Create(); // no config
var composer = Composer.Create(builder =>
{
    builder.UseNSubstitute();
    builder.UseBogus();
    builder.AddProfile<MyProfile>();
});
```

Config is validated and frozen at `Create()` time — a `Composer` is never
reconfigured after that. **Build one `Composer` per test/suite and reuse
it**; rebuilding it per assertion is a documented common mistake, not a
style choice — mainly because it revalidates the configuration on every
call for no reason.

Reuse does **not** by itself make an *unseeded* composer's calls
correlated or reproducible relative to each other. The seed logic lives
inside `Create<T>()`/`CreateMany<T>()` themselves — each call evaluates
`_configuration.Seed ?? CompositionSeed.Generate()`, so on an unseeded
composer, **every individual `Create<T>()`/`CreateMany<T>()` call draws
its own fresh random root seed**, whether that call happens on a
long-lived reused `Composer` instance or a freshly rebuilt one — reuse
doesn't change this. The only way to get reproducible/correlated values
across multiple `Create<T>()` calls is `WithSeed(...)` at configuration
time, which makes `_configuration.Seed` non-null so every call on that
composer reuses the same configured seed instead of generating a new
one.

`ICompositionContext` is what a registration factory or custom provider
uses to resolve *its own* nested dependencies:

```csharp
builder.Register<OrderService>(context => new OrderService(context.Resolve<IClock>()));
```

## Entry points

```csharp
T Create<T>()
IReadOnlyList<T> CreateMany<T>(int count)
CompositionRow CreateRow(Type declaringType)
```

- `Create<T>()` — one root composition, its own scope/path.
- `CreateMany<T>(count)` — `count` **fully independent** root
  compositions, not one `List<T>` member. `count: 0` → empty list, never
  null. Negative `count` → `ArgumentOutOfRangeException` immediately.
- `CreateRow(Type)` — one composition scope shared across several sibling
  top-level requests (same seed/shared-scope/path root). This is the
  primitive `Compono.XunitV3`'s `[Compose]` builds on; you won't normally
  call it directly outside an integration.

## `[Composable]`

**Most types never need it.** The generator discovers any type reachable
from a `Create<T>()`/`CreateMany<T>()` call site in the compilation —
directly, or transitively through constructor parameters. Discovery walks
call sites, not attributes, by default.

`[Composable]` is a narrow opt-in fallback for when that walk can't reach
a type:

```csharp
[Composable]
public class OnlyReachedIndirectly { /* ... */ }

// or, for a type you can't annotate directly (e.g. it lives in a
// referenced assembly):
[assembly: Composable(typeof(SomeExternalType))]
```

Use it when a type is:
- Used only as a `[Compose]` theory parameter reached indirectly and the
  generator's local call-site walk doesn't cover it, or
- Owned by a referenced assembly you can't add the attribute to directly
  (use the assembly-level form).

`AllowMultiple = true`; repeated requests for the same type dedupe.
**`CMP0008`**: assembly-level `[Composable]` with no `typeof(...)`
argument is a compile error — always pass the type.

**Do not** apply `[Composable]` broadly "to be safe," and do not expect
it to behave like a DI `[Injectable]`-style universal marker. It's an
opt-in for a discovery gap, not a general annotation.

## No reflection, ever, on the default path

Composition is 100% source-generated: the generator picks the
constructor, requests each parameter/required member via a descriptor-
based `ICompositionContext.Resolve<T>(...)` overload, and emits real,
debuggable C#. That overload is generated-code-only — don't write it by
hand in a registration or profile; use the plain `context.Resolve<T>()`
overload shown above instead. `Activator.CreateInstance` never appears in
the default path. This is why
constructor ambiguity is a **compile-time** concern (`CMP0001`), not a
runtime one the way it is in a reflection-based fixture library — there
is no way to disambiguate at runtime, and no registration rescues a
directly-composed type with more than one accessible constructor from
`CMP0001`. See `diagnostics.md`.

## Determinism and seeding

Every composed value derives from a seed. Same seed + same config + same
Compono version ⇒ same output — **not** guaranteed across Compono
versions, so never assert `"seed X produces value Y"` as a permanent
check.

- `builder.WithSeed(int seed)` — sets the composer's root seed. Calling
  it twice is a build-time config conflict.
- `[Compose(Seed = 4219)]` (in `Compono.XunitV3`) — same idea per theory
  row. Must be non-negative; negative throws immediately.
- `context.DeriveSeed() : int` — on-demand, path-derived seed for a
  provider or registration factory that needs its own determinism (this
  is what `Compono.Bogus`'s `UseBogus<T>()` uses internally).
- `CreateMany<T>(count)` forks seeds per item off a stable `"CreateMany"`
  key — items 0-2 of `CreateMany(3)` and `CreateMany(10)` (same root
  seed) are identical; independent of `count`.
- Path derivation uses structured segments (kind + ordinal, never
  parameter *name*) — renaming a parameter without reordering it never
  changes what gets generated.

**Reproducing a failure**: catch `CompositionException`, read
`.Diagnostic` (nullable — some failures, like `HashSet<T>`/`Dictionary`
unique-value exhaustion, have none) whose `ToString()` includes the tree
path and a `Seed:` line. In `Compono.XunitV3`, `[Compose]` always appends
`Seed: ...` to the exception message regardless of whether `.Diagnostic`
is present, and every row carries a `Compono.Seed` xUnit trait
unconditionally (pass or fail) — check the trait/output before asking the
user to re-run anything.

**Don't** pin a seed as a general test-writing habit — leave it unset by
default; use `Seed =` only to reproduce a specific investigated failure,
then feel free to remove it once fixed.

## Discovery and dispatch (for context, rarely user-facing)

Dispatch is a generated module-initializer populating a closed-generic
static field per type (`PlanCache<Customer>.Instance = ...`) — a field
read, not a dictionary lookup. You won't write this code by hand; it
matters mainly for understanding why composing a brand-new type "just
works" the first time you call `Create<T>()` on it (the generator saw the
call site at compile time) versus why an indirectly-reached type might
need `[Composable]`.
