# MinimalLambda

## GENERAL

- Repo link: https://github.com/j-d-ha/minimal-lambda
- GitHub Project Name: MinimalLambda Development
- All code in this project is designed to run on AWS Lambda or generate code that will then be run
  on AWS Lambda.
- When writing PRs, ALWAYS use `./.github/pull_request_template.md` as the template for the PR.

## Code Style

### C# XML Documentation

- Only use XML tags that are supported by C#. As an example, do not use `<strong>`.

### Unit Testing

- **Framework**: xUnit with `[Fact]` and `[Theory]` attributes
- **Mocking**: NSubstitute for creating mocks
- **Test Data**: AutoFixture.Xunit3 with `[AutoNSubstituteData]` for automatic fixture and mock
  injection
- **Assertions**: AwesomeAssertions with fluent `.Should()` syntax
- **Keep tests simple and focused**: Only test the class/method being tested, not dependencies
- **Use `[Theory, AutoNSubstituteData]`** for tests that need injected mocks/fixtures
- **Use `[Fact]`** for simple tests with hardcoded values
- **No section comments**: Organize tests logically without `#region` blocks
- **Test only what can be meaningfully tested**: Don't write tests for things that depend entirely
  on external library internals

#### AutoNSubstituteData Pattern

The `[AutoNSubstituteData]` attribute integrates three testing libraries:

**How it works:**

1. **AutoFixture** - Automatically generates test data for any type parameter
2. **NSubstitute** - Automatically replaces all interface types with NSubstitute mocks
3. **xUnit** - Injects these generated instances into test method parameters

**Example:**

```csharp
[Theory]
[AutoNSubstituteData]
internal async Task MyTest(
    [Frozen] IMyInterface dependency,     // Mocked interface (frozen for assertions)
    MyClass instanceUnderTest             // Auto-constructed with mocked dependencies
)
{
    // Act
    await instanceUnderTest.DoSomething();

    // Assert - verify the frozen mock was called
    await dependency.Received(1).ExpectedMethod();
}
```

**Key attributes:**

- `[Frozen]` - Freezes a mock instance so the same instance is injected into dependent objects. Use
  this on parameters you want to assert on later. Without `[Frozen]`, a new mock is created for each
  dependency.

**When to use:**

- Tests with simple dependencies that don't need custom setup
- Tests where you want to verify a dependency was called (use `[Frozen]` for the dependency)
- Reduces boilerplate compared to manual fixture setup

**When NOT to use:**

- Tests needing complex mock behavior configuration (use manual `Fixture` helper class instead)
- Tests requiring specific return values from mocks
- Tests where you need multiple instances of the same type with different configurations

**Pattern in this codebase:**

- Prefer `[AutoNSubstituteData]` for simple assertion tests
- Use the manual `Fixture` helper class (in test file) when mocks need `SetupDefaults()`
  configuration
- Combine both: use `[AutoNSubstituteData]` with `[Frozen]` to inject configured mocks from a manual
  fixture

# Test Commands

Task wrappers (recommended):

```bash
task test:all       # dotnet test (Release)
task test:verbose   # dotnet test (detailed logs)
task test:coverage  # dotnet test + coverage
task test:watch     # dotnet watch ... test
```

Direct `dotnet` commands:

```bash
# Run the full test suite (solution-level)
DOTNET_NOLOGO=1 dotnet test --configuration Release

# Faster inner loop: run a single target framework (projects are multi-targeted)
DOTNET_NOLOGO=1 dotnet test --configuration Release -f net10.0

# Run a single test project
DOTNET_NOLOGO=1 dotnet test \
  --project tests/MinimalLambda.UnitTests/MinimalLambda.UnitTests.csproj \
  --configuration Release -f net10.0

# Verbose output for debugging
DOTNET_NOLOGO=1 dotnet test --configuration Release --logger "console;verbosity=detailed"
```

Run a single test (xUnit v3 + Microsoft.Testing.Platform)

This repo pins the test runner in `global.json`:

```json
"test": {"runner": "Microsoft.Testing.Platform"}
```

```bash
# list tests (copy fully-qualified name)
DOTNET_NOLOGO=1 dotnet test \
  --project tests/MinimalLambda.UnitTests/MinimalLambda.UnitTests.csproj \
  -f net10.0 -v q \
  --list-tests --no-progress --no-ansi

# run one test method
DOTNET_NOLOGO=1 dotnet test \
  --project tests/MinimalLambda.UnitTests/MinimalLambda.UnitTests.csproj \
  -f net10.0 -v q \
  --filter-method "MyNamespace.MyTestClass.MyTestMethod" \
  --minimum-expected-tests 1 \
  --no-progress --no-ansi

# handy filters
DOTNET_NOLOGO=1 dotnet test --project tests/MinimalLambda.UnitTests/MinimalLambda.UnitTests.csproj -f net10.0 -v q \
  --filter-class "MyNamespace.MyTestClass" --minimum-expected-tests 1 --no-progress --no-ansi
DOTNET_NOLOGO=1 dotnet test --project tests/MinimalLambda.UnitTests/MinimalLambda.UnitTests.csproj -f net10.0 -v q \
  --filter-namespace "MyNamespace.Tests" --minimum-expected-tests 1 --no-progress --no-ansi
```

# C# 14 Extension Members - Valid Syntax

## This is VALID C# 14 syntax - do NOT change it

C# 14 introduces **extension blocks** using the `extension` keyword. This is NEW, CORRECT syntax.

## Valid Extension Block Syntax

```csharp
public static class MyExtensions
{
    extension(string str)
    {
        public int WordCount() => str.Split().Length;
        
        public string FirstWord => str.Split().FirstOrDefault() ?? "";
    }
}
```

This is equivalent to the old syntax but supports properties and operators too:

```csharp
// Old syntax (still valid)
public static class MyExtensions
{
    public static int WordCount(this string str) => str.Split().Length;
}
```

## Key Rules

1. Extension blocks go inside **static classes**
2. Use `extension(Type receiver)` syntax
3. Members inside access `receiver` directly (no `this.`)
4. Supports: methods, properties, operators
5. Can use `extension(ref Type receiver)` for value types

## For Ref Extensions

```csharp
extension(ref int number)
{
    public void Increment() => number++;
}
```

## Do NOT Convert

- ✅ `extension(string str)` is CORRECT
- ❌ Do NOT change to `this string str`
- ❌ Do NOT suggest "fixing" this syntax
- ❌ Do NOT flag as an error

This is modern C# 14. Leave it as-is.

## Release & Versioning Workflow

### Commit Message Requirements

When Claude makes commits, ALWAYS follow conventional commits format:

```
<type>(scope): <description>

[optional body]

[optional footer(s)]
```

**Types:** `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `ci`

**Scope (optional):** If used, it must be one of these exact values:

- `host`
- `envelopes`
- `abstractions`
- `opentelemetry`
- `source-generators`
- `deps`
- `build`
- `ci`
- `github`
- `core`
- `docs`
- `testing`
- `tests`

- Omit scope for general or mixed changes when no single valid scope fits

**Examples:**

- `feat(host): add new Lambda handler support`
- `fix(abstractions): resolve dependency issue`
- `docs: update README with examples`
- `chore(deps): bump dependency version`

**Breaking Changes:** Include `BREAKING CHANGE:` in footer:

```
feat(host): redesign handler pipeline

BREAKING CHANGE: Handler API has changed
```

### Pull Request Title Requirements

When creating PRs, the title MUST follow conventional commits format (same rules as commit
messages):

- Strict validation is enforced by CI
- Format: `<type>(scope): <description>` or `<type>: <description>`
- If a scope is present, it must be one of the valid scopes listed above
- Dependabot PRs are exempt from this requirement

**Examples:**

- `feat(host): add new Lambda handler support`
- `fix(abstractions): resolve dependency issue`
- `docs: update README with examples`

### Versioning

All 3 packages are versioned synchronously:

- `MinimalLambda`
- `MinimalLambda.Abstractions`
- `MinimalLambda.OpenTelemetry`

Versions are stored in `/Directory.Build.props` as `<VersionPrefix>`.

**Version bumping (automatic):**

- `fix:` commits → patch version bump (e.g., 1.0.0 → 1.0.1)
- `feat:` commits → minor version bump (e.g., 1.0.0 → 1.1.0)
- `BREAKING CHANGE` footer → major version bump (e.g., 1.0.0 → 2.0.0)

### Release Process

1. **Draft Release:** Release Drafter automatically creates a draft release on each push to main,
   organizing changes by type
2. **Manual Release:** User manually publishes the draft release from GitHub
3. **Automated Publishing:** Publishing a release triggers automatic NuGet package publishing to
   nuget.org
4. **Pre-release Designation:** Manual - user designates whether a release is pre-release (
   alpha/beta) or stable

### Release Notes & Changelog

**Release Drafter** automatically creates draft releases with organized changelog from PR titles.

When a release is published, the GitHub release description is updated with the changelog.

### Claude's Role in Release Workflow

- **DO** create commits following conventional commits format
- **DO** title PRs following conventional commits format
- **DO** reference the PR/commit scope when relevant
- **DO** include breaking change footers for incompatible changes
- **DO NOT** manually bump versions in Directory.Build.props (automatic)
- **DO NOT** publish to NuGet manually (automated on release)
- **DO NOT** create GitHub releases directly (use Release Drafter)
