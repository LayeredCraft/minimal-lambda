# Compono.DependencyInjection

Only relevant if the project references `Compono.DependencyInjection`. One
public member: `row.AsServiceProvider()`, an extension method on
`CompositionRow` returning a plain `System.IServiceProvider`.

```csharp
var row = composer.CreateRow(typeof(QuestionFormTests));
var provider = row.AsServiceProvider();

var apiClient = provider.GetRequiredService<IApiClient>();
apiClient.Configure().GetQuestions().Returns(questions);   // Compono.TestDoubles
```

- **What it's for**: surfacing what Compono has explicitly registered or
  can provide as a plain `IServiceProvider`, for an ecosystem that already
  accepts one — most commonly a **fallback** provider (e.g. bUnit's
  `Services.AddFallbackServiceProvider(...)`), so a consumer doesn't have
  to manually enumerate and register every dependency a system under test
  might ask for. This package is not framework-specific and has no
  third-party dependency of its own — it doesn't reference bUnit, ASP.NET
  Core, or any hosting model, and `GetRequiredService<T>()` in the example
  above is the standard `Microsoft.Extensions.DependencyInjection`
  extension method, which the *consumer's* own app/test host almost
  certainly already references — this package doesn't need to.
- **What it can resolve**: this row's existing scope values (already
  `[Shared]`/`ResolveShared` elsewhere in the same row), exact
  registrations (`builder.Register<T>(...)`), and configuration
  rules/semantic/test-double providers — `Compono.TestDoubles`,
  `Compono.NSubstitute`, or a custom `ICompositionValueProvider`, all
  treated identically (provider-neutral).
- **What it deliberately can't resolve** — never present these as bugs,
  they're a permanent scope boundary: a configured `UseServiceProvider(...)`
  external provider (consulting it here could silently flatten a
  legitimately transient/scoped external registration into "cached
  forever"), and ordinary generated-plan composition of an arbitrary
  concrete type with no registration or provider (that dispatch needs the
  target type known at compile time — a runtime `Type` can't reach it
  without reflection, which this repo rules out by default). A
  `GetService(Type)` call for either returns `null` — the same "nothing
  could handle it" outcome as any other unregistered type, not an error.
- **Stable per-`Type` identity** — the first successful `GetService(Type)`
  call for a given type is cached by the returned provider instance; every
  later call for that same type returns the identical object. This is what
  lets a test configure a double once and have something else resolving
  through the same provider (a rendered UI component, a second dependent
  service) observe that exact instance. A miss is **not** cached — a type
  unsatisfiable on one call can still be satisfied by a later one if the
  row's own configuration changes in between.
- **Concurrent calls are safe but not fixed-seed deterministic across
  different types** — no races or corruption, and two same-type callers
  never see different instances. But when two DIFFERENT types are
  requested concurrently for the first time, which one's resolution runs
  first isn't fixed — a randomness-dependent factory/provider (one
  calling `ctx.DeriveSeed()` or doing nested composition) can derive a
  different value across runs on the same seed in that specific
  situation. Sequential resolution is unaffected. Never claim full
  fixed-seed reproducibility here without this caveat.
- **Not** a general-purpose DI container: no `services.AddCompono()`, no
  `Composer`/`IComposer` registration into an application's own DI
  container, no `IServiceScope`/`IServiceScopeFactory` integration, no
  automatic bulk registration of every composed value. If asked for any of
  these, say they don't exist rather than approximating with what's here —
  see `SKILL.md`'s Guardrails section.
- **Does not own or dispose anything it resolves and caches** — if a
  resolved value implements `IDisposable`/`IAsyncDisposable`, disposing it
  is the caller's responsibility, exactly as it would be for a value the
  caller constructed by hand. This matches `CompositionRow`/`Composer`'s
  own lack of any disposal contract; this bridge does not introduce one.
  Never suggest the provider will clean up a resolved value on its own.
