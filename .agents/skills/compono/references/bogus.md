# Compono.Bogus

Only relevant if the project references `Compono.Bogus`. Never suggest
`UseBogus()` if the package isn't referenced — install it first, only if
the user asks. Don't reach for Bogus just because it's installed —
realistic-looking data only matters when the test actually cares about
content shape (formats, human-readable output), not for values the
assertion ignores.

```csharp
var composer = Composer.Create(builder => builder.UseBogus());

builder.UseBogus(o =>
{
    o.Locale = "en";
    o.AddAlias("GivenName", BogusConvention.FirstName);
    o.AddConvention("Nickname", f => f.Internet.UserName());
});
```

## Member-name conventions (the default mechanism)

`BogusMemberNameProvider` runs at pipeline stage 5 (semantic providers).
It's an **exact-name, case-sensitive** match on `string`-typed members
only against a fixed allowlist — not fuzzy or NLP-based matching:

`FirstName`, `LastName`, `FullName`, `Email`, `PhoneNumber`,
`StreetAddress`, `City`, `State`, `PostalCode`, `CompanyName`.

A member named e.g. `Name` alone is **not** in the allowlist and won't
match anything — ambiguous names are deliberately not guessed. Extend the
allowlist with:

- `BogusOptions.AddAlias(string name, BogusConvention target)` — an
  extra exact name reusing a built-in generator.
- `BogusOptions.AddConvention(string name, Func<Faker, string> generate)`
  — an extra exact name with a fully custom generator (a fresh `Faker`
  per call — not shared/reused the way built-ins may be).

`BogusOptions.Locale` (default `"en"`) affects **only**
`BogusMemberNameProvider` — `UseBogus<T>()`/the member-rule sugar below
are independent and don't read it.

No per-type disambiguation exists: `Person.Name` and `Company.Name`
sharing the literal member name `Name` can't get different generators
from one package-wide alias — don't promise a user that's possible.

## Whole-object sugar — `UseBogus<T>()`

```csharp
builder.UseBogus<Customer>(faker => faker
    .RuleFor(x => x.Email, f => f.Internet.Email()));

builder.UseBogus<Customer>("en-GB", faker => faker
    .RuleFor(x => x.Email, f => f.Internet.Email()));
```

This is sugar over `builder.Register<T>(...)` (stage 3), not a new
pipeline stage. It builds `new Faker<T>(locale).UseSeed(context.DeriveSeed())`
**before** invoking your `configureFaker` callback — seeding happens
before your rules run, which matters if a rule eagerly draws randomness
at configuration time (e.g. `RuleFor(x => x.Id, f.Random.Guid())`
evaluated once vs. per-generation — check Bogus's own `Faker<T>` docs for
that distinction, Compono doesn't change it).

Because it's `Register<T>()` under the hood, calling `UseBogus<T>()`
twice for the same `T`, or combining it with a direct `Register<T>()` for
the same type, is the same build-time conflict described in
`registrations-profiles-and-scopes.md`.

## Member-rule sugar — `.UseBogus(...)`

```csharp
builder.For<Customer>().Member(x => x.Email)
    .UseBogus(f => f.Internet.Email());

builder.For<Customer>().Member(x => x.Email)
    .UseBogus(f => f.Internet.Email(), locale: "en-GB");
```

Member-rule-scoped sugar over `.Use(...)` for a single field, when a
whole-`Faker<T>` isn't warranted.

## Coexistence with `Compono.NSubstitute`

`UseBogus()`/`UseNSubstitute()` call order never matters — they claim
disjoint pipeline stages (5 vs. 6) with zero reference between the two
packages in either direction. Don't worry about which one to call first
in a profile.
