# Diagnostics

Two completely different failure classes — don't confuse them:

- **Compile-time**: `CMP0001`-`CMP0013` (errors — fail `dotnet build`) and
  `CMP0020`-`CMP0032` (informational — never fail the build, only relevant
  if `ComponoGeneratedTestDoubles=true` is set, whether or not
  `Compono.TestDoubles` is referenced), both emitted by
  `Compono.Generators` (a Roslyn analyzer). Look up the code below.
- **Runtime**: `CompositionException`, thrown from `composer.Create<T>()`
  or a `[Compose]` theory row when the code compiled fine but the
  pipeline couldn't satisfy a request — most commonly a missing provider
  for an interface/abstract/delegate type. Read the tree path and seed
  (below), don't guess from the root type alone.

Always check *which* class you're looking at first: a red squiggle /
build failure is compile-time (this doc's table); a test that compiled
and then threw is runtime (the tree-path section).

## Compile-time: CMP0001-CMP0013

| Code | Meaning | Fix |
|---|---|---|
| CMP0001 | Ambiguous construction — the type has more than one accessible constructor | Reduce to one accessible constructor, or compose an interface/wrapper instead (interfaces are always provider-resolved, never routed through constructor selection) — no registration rescues this |
| CMP0002 | No accessible constructor at all (only `private`, or a `static` type) | Give it an accessible constructor, or compose something else |
| CMP0003 | (Historical/rare) — interfaces, abstract classes, and delegates are always classified provider-resolved today, both at root and member position, so this shouldn't surface for those. A missing provider for one is a *runtime* `CompositionException`, not this diagnostic. | Install/configure a provider: `UseNSubstitute()`, `Register<T>()`, or `.For<T>()` |
| CMP0004 | Unsupported constructor parameter kind — `ref`/`out`/`ref readonly`, ref struct, pointer, or function-pointer parameter (`in` parameters ARE supported) | Remove/change the parameter kind, or `Register<T>()` by hand |
| CMP0005 | Type argument isn't closed — an open generic type parameter reached a `Create<T>()` call | Supply a concrete closed type; open-generic registration isn't supported — there's no configuration that makes an open type composable |
| CMP0006 | Type argument shape unsupported — not a named type and not one of the supported collection roots (e.g. `int[,]`, pointer types) | Use a named type, or one of the 5 supported collection roots (array, `List<T>`, `IReadOnlyList<T>`, `HashSet<T>`, `Dictionary<TKey,TValue>`) |
| CMP0007 | Unsupported required-member kind — ref struct/pointer member type, or not assignable from generated code (no accessible init/set, or a readonly/inaccessible field) | Change the type/accessor, set it via a `Register<T>()` factory, or add a constructor annotated `[SetsRequiredMembers]` |
| CMP0008 | Assembly-level `[Composable]` used with no type argument | `[assembly: Composable(typeof(SomeType))]` — always pass the type |
| CMP0009 | Type argument is a `ref struct` (e.g. `Span<T>`) — can never be a generic type argument at all | No workaround; compose the wrapping non-ref-struct type instead |
| CMP0010 | The same type was discovered multiple times with conflicting nullability metadata across call sites | Make every request for the type use consistent nullability |
| CMP0011 | The same closed collection type was discovered with conflicting element/key nullability | Make every member/parameter of that collection type consistent |
| CMP0012 | A collection's element/key type isn't accessible (private/protected) from the generated collection-plan type | Use an accessible element/key type |
| CMP0013 | A `[Compose]`-attributed parameter type isn't accessible (private/protected) from the generated row-binding dispatch type | Use an accessible parameter type, or widen the type's accessibility |

This is the complete core diagnostic set — CMP0001 through CMP0013, no
more, no fewer. `CMP0020`-`CMP0032` (below) are real too, but belong to
generated test doubles, not core composition. If something references a
`CMP00xx` code outside these two ranges, it isn't real; don't invent one.

## Compile-time, generated-test-double opt-in only: CMP0020-CMP0032

Only relevant if the project sets
`ComponoGeneratedTestDoubles=true` — see `references/testdoubles.md`. The
generator is embedded in core `Compono` and gates discovery solely on that
MSBuild property (`ComponoIncrementalGenerator.Initialize`); these codes
can surface even in a project that never references `Compono.TestDoubles`
at all (the runtime package is only required for
`UseGeneratedTestDoubles()` to actually resolve a request to the generated
double — see the "Both gates are required" note in
`references/testdoubles.md`). Every code here is `DiagnosticSeverity.Info`,
not `Error`: it never fails `dotnet build`. Classes and delegates never
appear here at all — `LeafTypeClassifier` only admits interfaces as
generated-double candidates.

Most codes (`CMP0020`, `CMP0021`, `CMP0023`-`CMP0028`, `CMP0031`) report
that an entire interface leaf couldn't get a generated double at all — it
falls back to the ordinary runtime-provider path (`UseNSubstitute()`,
`Register<T>()`, `.For<T>()`, or a runtime `CompositionException` if
nothing else handles it). A narrower, v2 subset (`CMP0022`, `CMP0029`,
`CMP0030`) is scoped to **one overload or identity instead** — the double
still generates, every other member keeps its own `Configure()`/`Verify()`
surface, and only the colliding/unsupported one falls back to a
deterministic default. `CMP0032` is a third shape: interface-scoped like
the first group, but purely informational about a member that *does* get
a real `Configure()`/`Verify()` surface — see its own row. Each row below
says which applies.

| Code | Scope | Meaning |
|---|---|---|
| CMP0020 | Whole interface | Not accessible to a top-level generated type (the interface itself, or a private/protected nested interface reached through it) |
| CMP0021 | Whole interface | An unsupported member kind — indexer, event, a genuinely unimplemented static abstract member, `__arglist` method. A static abstract member already resolved by a more-derived interface's own concrete implementation (C#'s "most specific implementation" rule — the `IAmazonS3`/`IAmazonService` shape) does **not** trigger this code at all (ADR-0046). (A generic method is supported as of v2 unless its return type depends on its own type parameter — that's `CMP0031`, not this code.) |
| CMP0022 | One identity | A **diamond collision** — the exact same full signature independently declared by two different base interfaces, so the two identities can't be told apart. A genuine C# overload (same name, *different* signature) is unaffected — it gets its own per-overload `Configure()`/`Verify()` surface instead (v2) |
| CMP0023 | Whole interface | The interface declares its own `Configure`/`Verify` member that would shadow the generated bridge — any non-method member of that name (property/field/event, which always wins over an extension), or a method *callable with zero arguments* (broader than zero-parameter: `Configure(int mode = 0)` and `Configure(params int[] modes)` both collide too). A required-parameter method like `Verify(int mode)` is **not** callable with zero arguments and doesn't collide — the generated bridge stays usable |
| CMP0024 | Whole interface | A member's generated configuration extension collides with an inherited `object` member (`ToString`/`GetHashCode`/`GetType`; `Equals` collides only for a non-generic, single-*required*-parameter overload whose parameter isn't ref-like — `Equals<T>(T)` stays distinguishable by explicit type argument, and `Equals(Span<int>)` has no reference conversion to `object` at all) |
| CMP0025 | Whole interface | An unsupported return shape: ref-like, by-ref-returning, or pointer/function-pointer **always**; a non-nullable reference type (or `Task<T>`/`ValueTask<T>` wrapping one) with no deterministic default **only when the member also has no `Configure()`/`Verify()` surface for an unrelated reason** — a diamond collision, a zero-argument-extension collision, an overloaded `ref`/`out`/`in` parameter, or (method only) an `object`-member collision. Otherwise that shape generates as **configuration-required** instead (`CMP0032`) — see `references/testdoubles.md` |
| CMP0026 | Whole interface | An unsupported parameter shape. A pointer or function-pointer parameter, at any nesting depth (even inside an array, e.g. `int*[]`), is **always** this code — it requires the method to be declared `unsafe`, which this feature never emits, regardless of whether a same-named sibling exists. A `ref`/`out`/`in` parameter is this code only on a *solo* member (no same-named sibling) — a sibling present routes to `CMP0030` instead, **except** an `out` parameter whose own type has no deterministic default (e.g. a non-nullable reference type), which stays this code even with a sibling present since there's no constructible fallback body at all. Also this code for a generic method's own type parameter used as `T?` (constrained or not) |
| CMP0027 | Whole interface | A set-only property — nothing could observe a value written through it |
| CMP0028 | Whole interface | The same interface was discovered multiple times with conflicting generic-argument nullability across call sites |
| CMP0029 | Colliding identities only | Two or more same-named members of *equal generic arity* (a property vs. a method, or two methods with different real parameter lists) whose generated configuration extensions are each genuinely zero-parameter, so they'd collide (`CS0111`) — the colliding identities fall back; any sibling overload with its own real parameter list, or with a *different* generic arity (e.g. a zero-parameter `M` alongside `M<T>()`, or `M<T>()` alongside `M<T, U>()`), is unaffected — the generator groups candidates by `(Name, GenericArity)` before checking for a collision |
| CMP0030 | One overload | A `ref`/`out`/`in` parameter on a member that *does* have a same-named sibling — this overload dispatches via a deterministic default with no `Configure()` surface; its sibling overloads are unaffected. Doesn't apply to an `out` parameter with no deterministic default of its own — that's `CMP0026` (whole interface) even with a sibling present |
| CMP0031 | Whole interface | A generic method whose return type references its own type parameter anywhere in its symbol graph (`T Get<T>()`, `Task<T> GetAsync<T>()`) — no constructible fallback body. A generic method whose return type *doesn't* depend on its own type parameter (`ILogger<T>`'s `Log<TState>`) is supported, not this code |
| CMP0032 | Interface-scoped count, not blocking | One or more members have no deterministic default but generate as **configuration-required** instead of rejecting the interface — each throws `Compono.TestDoubleNotConfiguredException` if invoked before `Configure().Member(...).Returns(...)`/`.Throws(...)`. One diagnostic per interface (a count of how many members), not one per member — the exact member is already named by the exception at the point it's actually invoked unconfigured |

Fix: for a whole-interface code, address the interface's shape if you want
a generated double, or otherwise ignore it — the interface still works
exactly as it did before `ComponoGeneratedTestDoubles` existed, just
without a generated double for that one leaf. For a scoped code
(`CMP0022`/`CMP0029`/`CMP0030`), nothing needs fixing unless you want a
`Configure()`/`Verify()` surface for that specific identity — the rest of
the double already has one. For `CMP0032`, nothing needs fixing either —
it's informational, telling you which members need
`Configure().Member(...).Returns(...)`/`.Throws(...)` before their first
call, same as any other member's `Configure()` surface, just without a
usable deterministic default to fall back on if you forget.

## Runtime: `CompositionException` tree path and seed

```text
Unable to compose CreateOrderHandler.

CreateOrderHandler
└── IOrderProcessor processor
    └── OrderValidator validator
        └── IRuleProvider rules

No registration, semantic provider, test-double provider, built-in
provider, or generated plan could satisfy IRuleProvider.

Seed: 8451203967726193045
```

Read top-down — it always names the exact failing **nested** dependency
(`IRuleProvider` here), not just the root type (`CreateOrderHandler`).
Don't start debugging from the root; find the leaf the tree points at.

`CompositionDiagnostic` exposes `RootType`, `FailedType`, `Path`,
`Trace`, `Seed`, `Message` programmatically if you need to inspect it in
code rather than read the printed form. It's nullable on the exception —
some failures (e.g. `HashSet<T>`/`Dictionary` unique-value exhaustion via
`UniqueValueResolver`) have no structured diagnostic, only the exception
message with `Seed:` appended.

## Troubleshooting workflow

1. Is this a build failure or a test-run failure? Build → compile-time
   table above. Test-run → tree path below.
2. For a runtime failure: read the tree path to the exact failing type,
   not the root.
3. Read the message under the tree — it names which pipeline stages were
   tried and missed (registration, semantic provider, test-double
   provider, built-in provider, generated plan).
4. Fix by adding what's missing at the stage that should have supplied
   it — a `Register<T>()`, a `UseNSubstitute()`/`UseBogus()` if the
   package is referenced, or a `.For<T>()` rule. Don't work around the
   failure with reflection or a different fixture library (see the
   Guardrails in `SKILL.md`).
5. To reproduce locally: for a `Compono.XunitV3` row failure, the printed
   `Seed:` value plugs directly into `[Compose(Seed = ...)]` — that path
   is always `int`-range by construction. For a plain programmatic
   `composer.Create<T>()` failure, `CompositionDiagnostic.Seed` is a
   `ulong` (an unseeded composer draws a full random 64-bit value) and
   both `builder.WithSeed(int seed)` and `[Compose(Seed = ...)]` are
   `int`-typed — **if the printed seed exceeds `int.MaxValue`, there is
   currently no public API to paste it back in and get the exact same
   failure again.** Don't claim otherwise. What actually works: switch to
   an explicit `WithSeed(someChosenIntValue)` *before* re-running, so the
   next occurrence of the failure (if it reproduces at all with a
   different seed) is pinned and reproducible going forward — this finds
   *a* reproduction of the same underlying bug, not a replay of that
   exact original run. If the failure doesn't reproduce under a new seed,
   treat that as a data point about the failure's cause (e.g. it may
   depend on which specific random values were drawn) rather than
   assuming the investigation is complete. Remove any pinned seed once
   the fix is verified — don't leave it pinned as a permanent habit.
6. A `CompositionException` from Compono's own generated plans and
   built-in providers is deterministic, not flaky — don't wrap it in a
   retry, investigate. But check what's actually in the failing path
   first: a consumer-supplied `Register<T>()` factory, custom provider,
   or a configured `IServiceProvider` fallback can still do
   non-deterministic things (clock/random reads, I/O, a transient
   throw). If the failure traces through one of those, the "just
   reproduce it with the seed" assumption doesn't hold — inspect that
   code path directly instead.
