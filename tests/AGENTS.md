# AGENTS.md (MinimalLambda tests)

## Test stack

- xUnit v3: `[Fact]`, `[Theory]`.
- Assertions: AwesomeAssertions `.Should()`.
- Mocking: NSubstitute.
- Test data: AutoFixture + AutoNSubstitute via `[AutoNSubstituteData]`.
- Prefer Arrange / Act / Assert comments.
- Keep tests simple and focused. Test class/method behavior, not dependencies.
- Do not write tests for behavior owned entirely by external library internals.

## AutoNSubstituteData pattern

Use `[Theory, AutoNSubstituteData]` for tests needing fixture data or mocks.
Use `[Fact]` for simple hardcoded cases.

`[Frozen]` freezes generated instance so same mock is injected into system under test and available for assertions.

```csharp
[Theory]
[AutoNSubstituteData]
internal async Task MyTest(
    [Frozen] IMyInterface dependency,
    MyClass instanceUnderTest
)
{
    // Act
    await instanceUnderTest.DoSomething();

    // Assert
    await dependency.Received(1).ExpectedMethod();
}
```

Prefer `[AutoNSubstituteData]` for simple dependency assertions.
Use manual `Fixture` helper class in test file when mocks need defaults or complex setup.

## Commands

```bash
task test:all
task test:verbose
task test:coverage
task test:watch
```

```bash
DOTNET_NOLOGO=1 dotnet test --configuration Release
DOTNET_NOLOGO=1 dotnet test --configuration Release -f net10.0
DOTNET_NOLOGO=1 dotnet test \
  --project tests/MinimalLambda.UnitTests/MinimalLambda.UnitTests.csproj \
  --configuration Release -f net10.0
```

## Single test commands

Repo uses xUnit v3 on Microsoft.Testing.Platform.

```bash
DOTNET_NOLOGO=1 dotnet test \
  --project tests/MinimalLambda.UnitTests/MinimalLambda.UnitTests.csproj \
  -f net10.0 -v q \
  --list-tests --no-progress --no-ansi

DOTNET_NOLOGO=1 dotnet test \
  --project tests/MinimalLambda.UnitTests/MinimalLambda.UnitTests.csproj \
  -f net10.0 -v q \
  --filter-method "MyNamespace.MyTestClass.MyTestMethod" \
  --minimum-expected-tests 1 \
  --no-progress --no-ansi

DOTNET_NOLOGO=1 dotnet test --project tests/MinimalLambda.UnitTests/MinimalLambda.UnitTests.csproj -f net10.0 -v q \
  --filter-class "MyNamespace.MyTestClass" --minimum-expected-tests 1 --no-progress --no-ansi

DOTNET_NOLOGO=1 dotnet test --project tests/MinimalLambda.UnitTests/MinimalLambda.UnitTests.csproj -f net10.0 -v q \
  --filter-namespace "MyNamespace.Tests" --minimum-expected-tests 1 --no-progress --no-ansi
```
