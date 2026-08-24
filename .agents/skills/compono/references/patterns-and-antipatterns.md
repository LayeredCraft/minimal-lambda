# Patterns, antipatterns, and migrating from AutoFixture

Use this when reviewing existing Compono usage, deciding whether an
approach is idiomatic, or converting AutoFixture-based tests. These are
drawn from real dogfooding evidence
(`docs/research/0001-autofixture-comparison.md`,
`docs/migrating-from-autofixture.md`), not speculation.

## Antipatterns to flag in review

1. **Asserting on incidental composed values.** Only assert exact values
   that were explicitly pinned (inline `[Compose(42, ...)]`, a member
   rule, or `[Shared]` reference equality). For ordinarily-composed
   values, assert shape (`Should().NotBeNullOrWhiteSpace()`), not an
   exact literal that happened to come out of composition.
2. **Hardcoding "seed X ⇒ value Y" as a permanent assertion.** Not
   guaranteed stable across Compono versions.
3. **Pinning a seed as a general test-writing habit.** `Seed =` is for
   reproducing a specific investigated failure, then removing it once
   fixed — not a default on every `[Compose]`.
4. **Overusing `[Shared]`** for consistency or as an assumed performance
   optimization rather than a genuine identity requirement.
5. **One giant catch-all profile** instead of several small,
   concern-named ones (`InfrastructureProfile`, `DomainProfile`).
6. **Inflating the global collection-size default "just in case."** Set
   it per-member instead: `.For<T>().Member(x => x.Y).WithCollectionSize(n)`.
7. **Retry-looping a `CompositionException`** as if it were flaky-test
   noise — it's deterministic and reproducible from its own seed.
8. **Trying to recreate AutoFixture infrastructure that has no Compono
   equivalent** — see the mapping table below. `IFixture`,
   `IRequestSpecification`/`NamedRequest`, `OmitOnRecursionBehavior`, and
   `ConfigureMembers`-style substitute auto-configuration are removed
   entirely, with no replacement concept. Don't reinvent them as
   project-local helpers; adjust the test instead.
9. **Stacking multiple Compose-family attributes** on one test method —
   see `xunit-v3.md`; split into separate methods instead.
10. **Composing an ambiguous-constructor BCL type directly** (e.g.
    `HttpClient`, `Exception` — both seen in real migrations). `CMP0001`
    has no registration-based escape hatch — wrap in an app-owned
    interface/factory, or (for `Exception` specifically) compose the
    message as a `string` and hand-construct
    `new Exception(message)` in Arrange.
11. **Mechanically converting every `[Frozen]` to `[Shared]`.** Audit
    each one — many `[Frozen]` interface parameters existed only to
    obtain a substitute, not to share it; once `UseNSubstitute()` is
    active, composing an interface already produces a substitute, no
    `[Shared]` required unless identity genuinely matters.
12. **Relying on unstubbed NSubstitute member defaults** the way
    `ConfigureMembers = true` allowed. Stub explicitly.
13. **Mechanism-named tests** (`ComposesAndAssertsOnX`) instead of
    behavior-named ones. Keep `[Shared]` parameter names ordinary (not
    `sharedRepository`); keep test-only domain types named for the
    domain, not prefixed `Test`/`Fake`/`Mock` unless they're genuinely
    hand-written doubles.
14. **Reusing one member rule across unrelated types** hoping it applies
    broadly — use a type rule or `Register<T>()` if it should really be
    global.
15. **Recommending a combinatorial set of profile subclasses, a per-test
    `Composer.Create(...)` escape hatch, invented ambient/global scenario
    state, or AutoFixture retention** for a parameterized custom
    `AutoDataAttribute` (constructor args driving customization logic —
    e.g. `PersistenceAutoData(repositoryName)`) — the correct answer is
    `[Compose<TProfile, TConfig>]`, see `xunit-v3.md` and the mapping
    table below. Don't propose the workarounds `[Compose<TProfile,
    TConfig>]` exists to eliminate.
16. **Passing a bare string to a `[Compose<TProfile, TConfig>]` argument**
    when the value is really a finite choice or a CLR type — prefer an
    `enum`/`typeof(...)` instead; see `xunit-v3.md`'s "no stringly typed
    configuration" guidance.
17. **Reaching for `[Compose<TProfile, TConfig>]` to solve a name-based
    value-selection problem, or reaching for a custom
    `ICompositionValueProvider` to solve a call-site-configuration
    problem** — these are two different questions (see `xunit-v3.md`),
    not two names for the same mechanism.

## AutoFixture → Compono concept mapping

| AutoFixture | Compono | Notes |
|---|---|---|
| `fixture.Create<T>()` | `composer.Create<T>()` | Same shape, different guarantees — see `composition-model.md` |
| `fixture.CreateMany<T>()` | `composer.CreateMany<T>(count)` | Independent instances, not a shared `List<T>` |
| `[Frozen]` | `[Shared]` (`Compono.XunitV3` only) | Audit each usage — see antipattern 11 above |
| `AutoNSubstituteCustomization` | `builder.UseNSubstitute()` | No `ConfigureMembers` equivalent — see antipattern 12 and `nsubstitute.md` |
| `fixture.Customize<T>(...)` | `builder.Register<T>()` / `.For<T>().Use()` | Re-customizing the same type is a build-time conflict, not override |
| `[AutoData]`/`[InlineAutoData]` | `[Compose]` / inline args on `[Compose(...)]` | Only one Compose-family attribute per method — see `xunit-v3.md` |
| Parameterized custom `AutoDataAttribute` (constructor args driving customization logic) | `[Compose<TProfile, TConfig>]` | `TConfig`'s constructor args, not the test method's parameters — see `xunit-v3.md`. Use an enum/`typeof(...)`, not a bare string |
| `OmitOnRecursionBehavior` | *(none)* | Real cycles fail fast; break them with an explicit `Register<T>()` |
| `IFixture` | *(none)* | No fixture-holder object; configure via `[Compose<TProfile>]`/`ICompositionProfile` per test |
| `IRequestSpecification`/`NamedRequest` | *(none)* | No equivalent request-matching abstraction |
| Reflection-based construction | Source-generated plans | No reflection fallback — see Guardrails in `SKILL.md` |

## Idiomatic patterns to encourage in review

- Small, concern-named profiles applied via `AddProfile<T>()`.
- Member rules for the one or two values a test cares about; ordinary
  composition for everything else.
- `[Shared]` reserved for genuine identity requirements, paired with an
  explicit assertion against the shared instance.
- Interfaces/abstractions introduced around ambiguous-constructor BCL
  types rather than fighting `CMP0001`.
- Deterministic seeds used only transiently, during investigation of a
  specific failure — not left pinned in committed code.
