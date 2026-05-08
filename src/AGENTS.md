# AGENTS.md (MinimalLambda src)

## Runtime constraints

Code under `src/` is Lambda-first and should stay AOT/trimming-friendly.

- Avoid reflection-heavy or dynamic code unless required and guarded.
- Guard dynamic paths with runtime capability checks when needed.
- Minimize allocations on hot paths.
- Prefer `sealed` for public classes unless inheritance is intended.
- Prefer `internal` for implementation details.
- Use `ArgumentNullException.ThrowIfNull(arg)` for null guards.
- Use `InvalidOperationException` for invalid runtime state.
- Follow local `ConfigureAwait(false)` patterns.

## C# syntax

C# 14 extension blocks are valid syntax. Do not rewrite them to old `this` extension methods.

```csharp
public static class MyExtensions
{
    extension(string value)
    {
        public int WordCount() => value.Split().Length;
    }
}
```

Rules:

- Extension blocks go inside static classes.
- Use `extension(Type receiver)` syntax.
- Members inside access receiver directly.
- Properties/operators are supported.
- `extension(ref Type receiver)` is valid for value types.

## Source generators

- Keep generated output deterministic.
- Avoid APIs that break trimming/AOT unless guarded.
- Snapshot updates live under `tests/MinimalLambda.SourceGenerators.UnitTests/Snapshots/`.
