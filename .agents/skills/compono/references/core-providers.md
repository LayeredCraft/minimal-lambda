# Core value generation

Use this before assuming a type needs a registration, test-double provider,
or `[Composable]`. Core Compono source-generates plans for concrete types
reached from a local `Create<T>()`/`CreateMany<T>()` call site. Provider-
resolved leaves such as interfaces, abstract classes, and delegates are not
constructed through constructor selection; they need an applicable
registration or provider at runtime.

## Ordinary composition

- A concrete type needs exactly one accessible constructor. More than one
  is `CMP0001`; no registration disambiguates a directly composed type.
- Constructor parameters and assignable required members become nested
  composition requests. A required member without an accessible generated
  initializer/setter is `CMP0007`.
- Every ordinary generated value derives from composition seed and path.
  Same seed, configuration, and Compono version produce same output; do not
  assert a seed's incidental generated literal across versions.
- A nullability mismatch for one discovered type or one closed collection
  can be a compile-time conflict (`CMP0010`/`CMP0011`). Keep requests
  consistent rather than suppressing diagnostic.

## Collections

Supported collection roots are arrays, `List<T>`, `IReadOnlyList<T>`,
`HashSet<T>`, and `Dictionary<TKey, TValue>`. Unsupported roots or element
shapes report `CMP0006`; inaccessible element/key types report `CMP0012`.

`CreateMany<T>(count)` produces independent root compositions, not one
collection member from a shared graph. `count` zero returns an empty list;
negative `count` throws `ArgumentOutOfRangeException` immediately. With a
fixed seed, items retain stable positions when count grows.

## Choose smallest mechanism

- Let core composition build ordinary concrete graph values.
- Use a member rule for one assertion-relevant member.
- Use `Register<T>()` for one exact-type value or factory.
- Use a semantic/test-double provider only when request shape requires it.
  See `registrations-profiles-and-scopes.md`.
- Read `diagnostics.md` for any build diagnostic or runtime failure. Do not
  add reflection as a fallback.
