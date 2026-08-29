---
name: compono
description: >-
  **WORKFLOW SKILL** - Compono test-composition guidance for .NET/C# unit
  tests. Compono is a source-generated AutoFixture alternative
  (`composer.Create<T>()`/`CreateMany<T>()`, `[Composable]`,
  registrations, profiles, `[Shared]`, plus optional
  `Compono.XunitV3`/`Compono.TUnit`/`Compono.NSubstitute`/`Compono.Bogus`/`Compono.TestDoubles`/`Compono.DependencyInjection`/`Compono.Http`/`Compono.Logging`
  packages).
  USE FOR: writing/modifying/reviewing Compono tests, diagnosing
  `CMP0001`-`CMP0013` (errors), `CMP0020`-`CMP0032` and `CMP0035`-`CMP0037`
  (generated-test-double opt-in informational diagnostics), `CMP0033`-
  `CMP0034` (explicit constructor selection errors), `CMP0038`-`CMP0039`
  (Compono.Logging activation-generation diagnostics), or
  `CompositionException` failures, deciding on
  `[Composable]`/`Register<T>()`/`.For<T>()`/`[Shared]`, adding Compono
  when asked, migrating AutoFixture tests (`[Frozen]`, `AutoData`), any
  Compono/`Composer`/`[Compose]` question.
  DO NOT USE FOR: ordinary xUnit/NUnit/MSTest, NSubstitute, or Bogus work
  with no Compono package referenced; generic reflection/DI questions;
  production object construction.
  SCOPES TO: only load
  `xunit-v3.md`/`tunit.md`/`nsubstitute.md`/`bogus.md`/`testdoubles.md`/`dependencyinjection.md`/`http.md`/`logging.md`
  references when that package is referenced or requested.
license: MIT
metadata:
  author: LayeredCraft
  version: "0.1.0"
---

# Compono

Compono is a source-generated test-composition framework for modern
.NET — not a reflection-based fixture library. It looks similar to
AutoFixture on the surface (`Create<T>()`, `CreateMany<T>()`) but makes
different design choices throughout, and an agent relying on pretrained
AutoFixture habits will write code that doesn't compile, doesn't behave
as expected, or actively fights the framework. This skill exists to close
that gap — read the **Guardrails** section below before writing any
Compono code, then follow **Default workflow**.

## Detection

Check before assuming Compono is in play or absent — a project may use
some packages and not others.

| Signal | Where to look | Confidence | Meaning |
|---|---|---|---|
| `<PackageReference Include="Compono"` | any `.csproj` in the project | Definitive | Core Compono in use |
| `<PackageReference Include="Compono.XunitV3"` | `.csproj` | Definitive | `[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]`/`[Shared]` available — load `references/xunit-v3.md` |
| `<PackageReference Include="Compono.TUnit"` | `.csproj` | Definitive | `[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]`/`[Shared]` available — load `references/tunit.md` |
| `<PackageReference Include="Compono.NSubstitute"` | `.csproj` | Definitive | `UseNSubstitute()` available — load `references/nsubstitute.md` |
| `<PackageReference Include="Compono.Bogus"` | `.csproj` | Definitive | `UseBogus()`/`UseBogus<T>()` available — load `references/bogus.md` |
| `<PackageReference Include="Compono.TestDoubles"` or `UseGeneratedTestDoubles()` in `*.cs` | `.csproj`/`*.cs` | Definitive | Test-double intent present — load `references/testdoubles.md`, which explains that `UseGeneratedTestDoubles()` also needs `<ComponoGeneratedTestDoubles>true</ComponoGeneratedTestDoubles>` set (check separately; its absence is the most common setup mistake, not a reason to skip loading the reference) |
| `<PackageReference Include="Compono.DependencyInjection"` or `.AsServiceProvider()` in `*.cs` | `.csproj`/`*.cs` | Definitive | `row.AsServiceProvider()` available — load `references/dependencyinjection.md` |
| `<PackageReference Include="Compono.Http"` | `.csproj` | Definitive | `TestHttpHandler`/`OnGet`/`OnPost`/etc. available — load `references/http.md` |
| `<PackageReference Include="Compono.Logging"` or `UseLogging()` in `*.cs` | `.csproj`/`*.cs` | Definitive | `ILogger`/`ILogger<T>` compose via `UseLogging()`, `CapturingLogger`/`CapturingLogger<T>`, `Verify()` available — load `references/logging.md`. Generation is on by default once the package is referenced — never suggest a manual MSBuild opt-in step |
| `Composer.Create(`, `.Create<`, `.CreateMany<`, `CompositionBuilder` | `*.cs` | High | Core Compono API in active use |
| `[Compose]`, `[Compose<...>]`, `[Shared]` | `*.cs` | High | `Compono.XunitV3` or `Compono.TUnit` attributes in active use - check which package is referenced before assuming which |
| `ICompositionProfile` implementations | `*.cs` | Medium | Profile-based configuration convention already established — follow it rather than inventing a new one |
| `[Composable]` / `[assembly: Composable(` | `*.cs` | Medium | Discovery-gap workaround already in use somewhere in this codebase |
| No `Compono*` package reference anywhere | `.csproj` | — | Not a Compono project. Don't suggest Compono unless the user explicitly asks to adopt it. |

Don't hardcode an assumed version scheme or `--prerelease` requirement —
it changes independently of this skill. Check the [installation guide](https://layeredcraft.github.io/compono/getting-started/installation/)
or actual NuGet listing for the current install command instead of guessing
from a remembered version pattern.

**Adopting Compono in a project that doesn't have it yet**: only do this
when the user explicitly asks. Add the `Compono` package (plus
`Compono.XunitV3` if the project uses xUnit v3 theories, `Compono.TUnit`
if it uses TUnit, `Compono.NSubstitute`/`Compono.Bogus` only if the user
wants those). Don't retrofit existing
passing tests to use Compono unprompted — that's a scope decision for the
user to make test-by-test, not something to do as a drive-by.

## Default workflow

### High-priority `Compono.TestDoubles` matching guardrail

If the project references `Compono.TestDoubles`, calls
`UseGeneratedTestDoubles()`, mentions generated doubles, or asks to migrate
NSubstitute `Arg.Is`/`Arg.Any`/`Received`/`DidNotReceive` usage to current
TestDoubles, **read `references/testdoubles.md` before answering**. Do not
answer from memory: older Compono guidance said generated doubles had no
argument matching, but that is stale. Current TestDoubles supports
`Configure()`, `Verify()`, literal equality matching, `Match.Any<T>()`,
`Match.Is<T>(predicate)`, argument-filtered `Never()`/`Once()`/`Exactly(n)`,
and multi-entry argument-distinguished response configuration for eligible
member shapes. Translate NSubstitute vocabulary directly where eligible:
`Arg.Is<T>` → `Match.Is<T>`, `Arg.Any<T>()` → `Match.Any<T>()`,
`Received(1)` → `Verify().Member(...).Once()`, `Received(n)` →
`Verify().Member(...).Exactly(n)`, and `DidNotReceive()` →
`Verify().Member(...).Never()`. Never invent non-existent TestDoubles APIs
such as `CallsTo(...)`, `ReceivedCalls()`, or `[ComponoTest]`, and never
recommend a hand-written recording fake solely because the old test used
NSubstitute argument matchers. True argument capture, invocation-aware
callback responses/side effects, and call-order verification are different
capabilities; use project-local fake/roadmap guidance for those boundaries
instead of claiming `Match<T>` solves them.

1. **Detect** — run the table above. Know which packages are actually
   installed before recommending any API from them.
2. **Inspect** the type under test and its collaborators — concrete class
   with one accessible constructor? Interface/abstract/delegate? Does it
   already have `[Composable]`? Is there an existing `ICompositionProfile`
   this codebase already uses?
   Read `references/core-providers.md` when ordinary generated values,
   collection behavior, nullability, or built-in support boundaries affect
   the decision.
3. **Decide** whether Compono is appropriate at all — see **When not to
   use Compono** below — then which mechanism fits:
   - An ordinary value, composed from scratch each time → let Compono
     generate it, no configuration needed.
   - A specific fixed value needed for an assertion → inline value
     (`[Compose(42, "widget")]`) or a member rule
     (`.For<T>().Member(x => x.Y).Use(...)`), not a post-hoc mutation
     after `Create<T>()`.
   - The *same instance* needs to be shared across the composed graph and
     the test body → `Share<T>()` (`CompositionBuilder.Share<T>()`,
     declared once, typically in a profile) for a reusable, profile-level
     sharing intent — every request for that type anywhere in the graph
     participates automatically, with **no `[Shared]` attribute needed**;
     `[Shared]` (in `Compono.XunitV3` or `Compono.TUnit`, whichever the
     project references) for a one-off, single-test case that doesn't
     warrant a profile change. See
     `references/registrations-profiles-and-scopes.md`. Don't reach for
     either just to "make things consistent" or as a perceived performance
     win; ordinary composition is already cheap. Adding `Share<T>()` to a
     profile several tests already reuse changes sharing semantics for
     *every* graph composed with that profile, silently, for any test
     structurally reaching the type more than once — a materially larger
     blast radius than adding `[Shared]` to one test method; don't add it
     to a shared profile without considering that.
   - Interface/abstract-class/delegate needs a real test double →
     `Compono.NSubstitute`'s `UseNSubstitute()`, not a hand-rolled stub,
      if that package is referenced. An **interface** leaf that should be
     source-generated/AOT-safe → `Compono.TestDoubles`'s
     `UseGeneratedTestDoubles()`, if that package is referenced and the
     compile-time opt-in is set. Current generated doubles support
     `Configure()`, `Verify()`, literal equality matching, `Match.Any<T>()`,
     `Match.Is<T>(predicate)`, argument-filtered `Never()`/`Once()`/
     `Exactly(n)`, and multi-entry argument-distinguished response
     configuration for eligible member shapes. Do not mistake NSubstitute
     vocabulary (`Arg.Is`, `Arg.Any`, `Received`, `DidNotReceive`) for a
     reason to invent a hand-written recording fake; translate it to the
     generated-double surface where the member shape is eligible. True
     argument capture, invocation-aware callback responses/side effects,
     call-order verification, classes, delegates, and other explicitly
     unsupported shapes remain outside current `Compono.TestDoubles`
      support — see `references/testdoubles.md`.
   - A test deliberately needs to exercise the real HTTP client pipeline
     (real `HttpClient` → `TestHttpHandler` → configured response) rather
     than substitute an application-level interface away →
     `Compono.Http`'s `TestHttpHandler`, if that package is referenced —
     see `references/http.md`. Don't reach for this when the seam is
     already an ordinary interface the test doesn't specifically care is
     HTTP-backed — that stays the NSubstitute/TestDoubles bullet above.
   - A composed type takes an `ILogger`/`ILogger<T>` dependency and the
     test wants to assert what was logged (level, message, structured
     properties, exception, scope) → `UseLogging()` composing a
     `CapturingLogger`/`CapturingLogger<T>`, if `Compono.Logging` is
     referenced — see `references/logging.md`. Generation is on by
     default once the package is referenced; never suggest a manual
     `ComponoGeneratedLogging=true` opt-in step, and never suggest a
     hand-rolled fake `ILogger` when this package is already referenced.
     If the composed type also depends on another interface via
     `UseNSubstitute()`/`UseGeneratedTestDoubles()`, register
     `UseLogging()` first — see `references/logging.md`'s registration-
     order note.
   - A `string` member needs a realistic value (email, name, address) →
     `Compono.Bogus`'s member-name conventions or `UseBogus(...)`, if
     that package is referenced. Don't reach for Bogus everywhere — plain
     generated values are fine when realism doesn't matter to the test.
   - Cross-test/cross-project reusable setup → an `ICompositionProfile`,
     not a copy-pasted builder lambda in every test.
   - A value only known at a *specific test's call site* that must
     influence configuration logic running *inside* a profile (not a
     top-level test parameter) → `[Compose<TProfile, TConfig>]` (in
     `Compono.XunitV3` or `Compono.TUnit`, whichever the project
     references) — see `references/xunit-v3.md` or `references/tunit.md`
     to match. Prefer an enum/`typeof(...)` over a bare
     string for the argument. Don't confuse this with a
     `CompositionProviderRequest.Name`-based custom provider
     (`references/registrations-profiles-and-scopes.md`), which solves a
     different (name-based, not call-site) selection problem.
4. **Check `[Composable]` necessity** — see
   `references/composition-model.md`'s Discovery section. Most types need
   nothing; only add it when the type has no local `Create<T>()`/
   `CreateMany<T>()` call site the generator can walk from (e.g. it's only
   ever reached indirectly, or it lives in a referenced assembly you can't
   annotate directly). Never add `[Composable]` speculatively across a
   type hierarchy "just in case."
5. **Act** — write the composition call, registration, or profile change.
   Prefer existing project conventions (an established profile, an
   existing member-rule pattern) over introducing a new mechanism for the
   same problem.
6. **Compile and run.** A compile-time failure is a `CMP0001`-`CMP0013`
   diagnostic from `Compono.Generators` — look it up in
   `references/diagnostics.md` before guessing a fix. If
   `ComponoGeneratedTestDoubles=true` is set — the generator is embedded in
   core `Compono.Generators`, so this can surface even without
   `Compono.TestDoubles` referenced — a `CMP0020`-`CMP0032` or
   `CMP0035`-`CMP0037` diagnostic is informational, not a failure. Most of
   them mean that one interface leaf fell back to the ordinary
   runtime-provider path, not that the build broke — but `CMP0022`,
   `CMP0029`, and `CMP0030` are narrower: they withhold a `Configure()`/
   `Verify()` surface for just one overload or colliding identity while the
   rest of the double still generates normally, so don't assume the whole
   interface fell back without checking which code fired (see
   `references/diagnostics.md`). `CMP0033`-`CMP0034` (explicit constructor
   selection, ADR-0002 Amendment 3 / ADR-0052 Part B) are always-on
   **errors**, not informational — they surface whenever
   `builder.For<T>().UseConstructor<...>()` is used. A test-time failure is
   a `CompositionException` — read its tree-shaped path and `Seed:` line
   (also see `references/diagnostics.md`) to find exactly which nested
   dependency failed, rather than guessing from the root type.

## Guardrails

These are hard rules, not preferences. Compono's whole design point is
*not* being a reflection-based fixture library — violating these
undermines the reason Compono exists in this project.

- **Never introduce runtime reflection as a workaround.** No
  `Activator.CreateInstance`, no constructor/property reflection, no
  "just reflect over the type" fallback when composition fails. Compono
  has no reflection fallback today — a composition failure means the
  generator needs a supported shape, or a provider/registration needs to
  be added. Reflection is excluded from the default architecture by
  design (ADR-0001); it is not a valid escape hatch even for "just this
  one test."
- **Never silently substitute AutoFixture** (or another fixture library)
  because it's more familiar or because a Compono composition is
  failing. If Compono genuinely can't do something a test needs, say so
  explicitly and let the user decide — don't quietly reach for a
  different library.
- **Never re-register or re-customize the same type to "fix" a build
  error.** A second `Register<T>()` for the same `T` (directly, via a
  profile, or across two profiles) is a build-time conflict, not
  last-write-wins like AutoFixture customizations. If a registration
  conflicts, that's a signal to consolidate, not to add another one.
- **Never mark broad swathes of a production model `[Composable]`
  "to be safe."** It's a narrow discovery-gap opt-in, not a general
  "make this type composable" marker — see Detection above and
  `references/composition-model.md`.
- **Never treat a `CompositionException` as flaky-test noise to retry —
  but check what's actually in the failing path first.** Compono's own
  generated plans and built-in providers are deterministic and
  reproducible from the seed. A consumer-supplied `Register<T>()`
  factory, a custom provider, or a native `IServiceProvider` fallback can
  still do non-deterministic things (clock/random reads, I/O, a
  transient throw) — if the failing path runs through one of those,
  inspect it before assuming the seed alone explains or reproduces the
  failure.
- **Never hardcode "seed X produces value Y" as a permanent assertion.**
  Determinism holds for a given Compono version, not across versions.
  Only assert on values you explicitly pinned (inline values, member
  rules, `[Shared]` reference equality).
- **Never bypass a `CMP0001`-`CMP0013` compile error by working around
  the generator** (e.g. hand-writing a plan, suppressing the diagnostic,
  or switching the type to be constructed manually elsewhere just to
  dodge it). Fix the underlying shape, or compose an interface/wrapper
  instead when the diagnostic's fix column says so — see
  `references/diagnostics.md`. **For `CMP0001` specifically (a type with
  more than one accessible constructor), the preferred fix is
  `builder.For<T>().UseConstructor<T1, ...>()`** (ADR-0002 Amendment 3 /
  ADR-0052) — name the intended constructor's own parameter types, in
  order, and Compono keeps composing them exactly as it would for an
  unambiguous type. This works for a type you own **and** for a BCL/
  third-party type you don't (`HttpClient`, `Exception`) — it requires no
  annotation on the type being composed, only a call in your own
  profile/composer configuration. See
  [Registrations and Rules: "Constructor selection"](https://github.com/LayeredCraft/compono/blob/main/docs/concepts/registrations-and-rules.md#constructor-selection-fortuseconstructor)
  and `references/registrations-profiles-and-scopes.md`.
  - **Never recommend adding `[CompositionConstructor]` or any
    Compono-specific attribute to the composed type** — that was never
    shipped, and product direction explicitly rejected it as the
    mechanism (a test-composition library must not require production
    code to carry test-only annotations). Never recommend greedy/
    "most-parameters"/first-resolvable-constructor guessing either —
    `UseConstructor<...>()` is always explicit, never a heuristic.
  - **`UseConstructor<...>()` is not a substitute for `Register<T>`.** Use
    it only when Compono should keep *composing* the type from scratch.
    If the test needs one *specific, already-configured* instance
    (e.g. an `HttpClient` wrapping a particular `TestHttpHandler`-created
    handler) rather than something Compono could build from a
    parameter-type list, that's still `Register<T>`/an interface wrapper
    — don't tell a user to reach for `UseConstructor<...>()` there; see
    "When not to use Compono" below for exactly which shape still needs
    the older workaround.
  - Scope is compilation-wide, not per-profile: a selection applies to
    every composition of that type in the compilation. A conflicting
    selection is `CMP0033`; a selection matching no accessible
    constructor is `CMP0034`.
- **Never assume a runtime reflection compatibility mode exists.** It's
  explicitly undecided/future work, not shipped API — don't tell a user
  they can "opt into reflection fallback."
- **Never claim or write code against a Compono integration package that
  hasn't shipped — but distinguish "no dedicated package" from "no
  capability."** Only `Compono`, `Compono.XunitV3`, `Compono.TUnit`,
  `Compono.NSubstitute`, `Compono.Bogus`, `Compono.TestDoubles`,
  `Compono.DependencyInjection`, `Compono.Http`, and `Compono.Logging` ship
  as packages today
  (`Compono.TUnit`
  ships the full attribute family —
  `[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]`/`[Shared]`,
  see `references/tunit.md`; `Compono.TestDoubles` requires both the
  package reference and the `ComponoGeneratedTestDoubles` compile-time
  opt-in, see `references/testdoubles.md`; `Compono.DependencyInjection`
  ships exactly one member, `row.AsServiceProvider()` — a configured-
  resolution `IServiceProvider` bridge, see `references/dependencyinjection.md`;
  `Compono.Http` ships `TestHttpHandler`, a reflection-free
  `HttpMessageHandler`-based test double for `HttpClient`-consuming code —
  does not ship `IHttpClientFactory`/named-client integration, see
  `references/http.md`; `Compono.Logging` ships `UseLogging()` and
  `CapturingLogger`/`CapturingLogger<T>` — generation is on by default
  once the package is referenced, no `ComponoGeneratedLogging` opt-in
  needed (unlike `Compono.TestDoubles`' opt-in `ComponoGeneratedTestDoubles`),
  see `references/logging.md`)
  — there is no `Compono.NUnit`, `Compono.MSTest`, `Compono.FakeItEasy`, or
  `Compono.Moq`, and never invent a plausible-looking API for one. That
  doesn't always mean the underlying capability is unsupported, though:
  core `Composer.Create<T>()`/`CreateMany<T>()` work inside any test
  framework's test body today, including NUnit/MSTest, with no
  framework-specific package required — just without `Compono.XunitV3`'s
  `[Compose]`/`[Shared]`/row convenience. Likewise, don't overstate what
  `Compono.DependencyInjection` itself is: it's a narrow, one-direction
  bridge (`row.AsServiceProvider()`, reaching only scope/exact-
  registration/provider-backed values — never a configured
  `UseServiceProvider(...)`, never ordinary generated-plan composition of
  an arbitrary concrete type) — there's still no `services.AddCompono()`,
  no `Composer`/`IComposer` registration into an app's own DI container,
  and no richer/auto-registering DI package; that broader idea remains
  unshipped, a distinct thing from what `Compono.DependencyInjection`
  actually does. When asked whether Compono supports one of these, say
  precisely what does and doesn't exist rather than a blanket "no." For
  current candidate status (idea / admitted candidate / deferred), point
  at <https://layeredcraft.github.io/compono/roadmap/future-packages/> —
  not a repo-relative `docs/` path, since an installed skill payload
  (`npx skills add`) only includes `SKILL.md` and `references/`, not the
  rest of this repository.

## When not to use Compono

Compono is not always the right tool. Prefer explicit, hand-built test
data when:

- The test's whole point *is* a specific, meaningful value (e.g. testing
  a validation boundary at exactly `Age = 18`) — write it literally,
  don't compose it and then override it.
- The setup is one or two trivial values — a composed call adds
  indirection without saving anything real.
- The type has an ambiguous-constructor BCL shape (e.g. `HttpClient`,
  `Exception`, both with multiple accessible constructors) and Compono
  should just keep constructing it plainly — that's `CMP0001`, fixed with
  `builder.For<T>().UseConstructor<...>()` (ADR-0002 Amendment 3/ADR-0052
  Part B; see `references/registrations-profiles-and-scopes.md`'s
  "Constructor selection" section) — don't reach for an interface wrapper
  or hand construction for this case anymore, that's the stale
  pre-`UseConstructor` workaround.
  `UseConstructor<...>()` does **not** help the different, still-open
  case: you need a *specific, already-configured* instance, not just "some
  accessible constructor." That's still `Register<T>`/an interface
  wrapper/hand construction — e.g. `Exception`: compose the message as a
  `string` parameter, then `new Exception(message)` by hand in Arrange
  (preserves randomized-message behavior, one added line, no readability
  loss); `HttpClient` wrapping one specific, already-configured
  `HttpMessageHandler` a fixture built: a real interface wrapper
  (`IHttpClientProvider`), since no parameter-type list expresses "the
  exact handler my fixture already configured" — see
  `docs/concepts/registrations-and-rules.md`'s `Register<T>` vs.
  `UseConstructor<...>()` table. This distinction, not the shape of the
  BCL type itself, is what decides which mechanism applies — classification
  history: [ADR-0002 Amendment 1](https://github.com/LayeredCraft/compono/blob/main/docs/adr/0002-constructor-selection-algorithm.md#amendment-1-2026-08-04-cmp0001-observed-against-a-real-ambiguous-bcl-type-no-change-made),
  [RESEARCH-0003](https://github.com/LayeredCraft/compono/blob/main/docs/research/0003-structured-logging-exception-constructor-ambiguity.md),
  [migration guide](https://github.com/LayeredCraft/compono/blob/main/docs/migrating-from-autofixture.md#known-differences-and-limitations).
  **`Compono.Http` doesn't change the specific-instance case** — composing
  an already-configured, real `HttpClient` value via
  `Composer.Create<HttpClient>()` still needs `Register<HttpClient>`/an
  interface wrapper. What `Compono.Http` answers is the *adjacent*
  question: testing code that *consumes* an `HttpClient`/
  `HttpMessageHandler`. If that's the actual goal, use `Compono.Http`'s
  `TestHttpHandler` (see `references/http.md`) instead of hand-wrapping an
  interface just to fake HTTP responses — don't conflate the two.
  Registration-aware discovery (Compono recognizing a `Register<T>` and
  treating it as a compile-time leaf) remains a separate, unresolved
  roadmap item (ADR-0052 Part A) — don't imply it's shipped.
- A collaborator's realistic *content* doesn't matter to the assertion —
  don't reach for `Compono.Bogus` just because it's installed.

## References

Load only what the Detection table says is relevant to the current task.

| File | Read when... |
|---|---|
| `references/core-providers.md` | Deciding whether core Compono can generate an ordinary value, collection, or required member without a registration/provider |
| `references/composition-model.md` | Composing a type, deciding on `[Composable]`, understanding generated-plan discovery, or anything about determinism/seeding |
| `references/registrations-profiles-and-scopes.md` | Using `Register<T>()`, `.For<T>().Use()`/`.Member()`, `ICompositionProfile`, `Share<T>()`, `[Shared]`, or debugging a recursion/registration-conflict error |
| `references/diagnostics.md` | A `CMP0001`-`CMP0013` build error, a `CMP0033`-`CMP0034` constructor-selection build error, a `CMP0020`-`CMP0032` or `CMP0035`-`CMP0037` informational diagnostic (surfaces whenever `ComponoGeneratedTestDoubles=true` is set, whether or not `Compono.TestDoubles` is referenced), a `CMP0038`-`CMP0039` Compono.Logging activation-generation diagnostic, or a runtime `CompositionException` needs diagnosing |
| `references/xunit-v3.md` | `Compono.XunitV3` is referenced — `[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]`/`[Shared]` theory work |
| `references/tunit.md` | `Compono.TUnit` is referenced — `[Compose]`/`[Compose<TProfile>]`/`[Compose<TProfile, TConfig>]`/`[Shared]` test-method work |
| `references/nsubstitute.md` | `Compono.NSubstitute` is referenced — `UseNSubstitute()` work |
| `references/bogus.md` | `Compono.Bogus` is referenced — `UseBogus()`/`UseBogus<T>()` work |
| `references/testdoubles.md` | `Compono.TestDoubles` is referenced or `UseGeneratedTestDoubles()` is called — `UseGeneratedTestDoubles()`/generated `Configure()`/`Verify()` work, `Match<T>` argument matching and multiple-response-per-member for eligible members, including diagnosing a missing `ComponoGeneratedTestDoubles` opt-in |
| `references/dependencyinjection.md` | `Compono.DependencyInjection` is referenced or `.AsServiceProvider()` is called — `row.AsServiceProvider()`, its stable-identity/caching contract, and what it deliberately can't resolve |
| `references/http.md` | `Compono.Http` is referenced — `TestHttpHandler`/matching/verification/lifetime work |
| `references/logging.md` | `Compono.Logging` is referenced or `UseLogging()` is called — `ILogger`/`ILogger<T>` composition, `CapturingLogger`/`CapturingLogger<T>`, structured properties, scope semantics, `Verify()`, and the `ComponoGeneratedLogging` default-on/opt-out behavior |
| `references/patterns-and-antipatterns.md` | Reviewing existing Compono usage for correctness, migrating from AutoFixture, or unsure whether an approach is idiomatic |
