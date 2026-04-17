# Contributing to MinimalLambda

Thank you for your interest in contributing to the MinimalLambda project! This document provides guidelines and instructions for contributing.

## Table of Contents

<!-- TOC -->
* [Contributing to MinimalLambda](#contributing-to-minimal-lambda)
  * [Table of Contents](#table-of-contents)
  * [Code of Conduct](#code-of-conduct)
  * [Getting Started](#getting-started)
  * [Development Setup](#development-setup)
  * [Code Formatting](#code-formatting)
  * [Making Changes](#making-changes)
  * [Commit Guidelines](#commit-guidelines)
  * [Pull Request Process](#pull-request-process)
  * [Code Style](#code-style)
  * [Testing](#testing)
  * [Documentation](#documentation)
  * [Target Frameworks](#target-frameworks)
  * [Central Package Management](#central-package-management)
  * [CI/CD Integration](#cicd-integration)
  * [Questions or Need Help?](#questions-or-need-help)
  * [License](#license)
<!-- TOC -->

## Code of Conduct

We are committed to providing a welcoming and inclusive environment for all contributors. Please be respectful and constructive in all interactions.

## Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally:
   ```bash
   git clone https://github.com/YOUR_USERNAME/minimal-lambda.git
   cd minimal-lambda
   ```
3. **Add the upstream remote**:
   ```bash
   git remote add upstream https://github.com/j-d-ha/minimal-lambda.git
   ```
4. **Create a feature branch** (see [Branching Strategy](#branching-strategy))

## Development Setup

### Prerequisites

- **.NET SDK 10.0+**
- **Node.js** (for commitlint & husky - local commit validation)
- **Task** (optional, for running automated tasks) - see [Taskfile.yml](/Taskfile.yml)
- **Git** with configured `user.name` and `user.email`

### Building the Project

```bash
# Install Node dependencies (commitlint & husky for commit validation)
npm install

# Set up git commit hooks (commit-msg validation)
task local:setup-git-hooks

# Restore dependencies
dotnet restore

# Build the solution
dotnet build

# Run tests
dotnet test

# Clean packages (if needed)
dotnet clean
```

## Code Formatting

Code formatting is automated using **CleanupCode** and **CSharpier**, ensuring consistent style across the project. All PRs are checked for formatting compliance in CI/CD.

### Formatting Tools

The project uses two complementary tools:

1. **CleanupCode**: JetBrains code cleanup tool
   - Applies code organization and style rules
   - Uses the "Built-in: Full Cleanup" profile configured in `MinimalLambda.sln.DotSettings`
   - Handles code structure and consistency

2. **CSharpier**: Opinionated C# code formatter
   - Enforces consistent formatting (similar to Prettier for C#)
   - Handles spacing, line breaks, and code layout

Tools can be installed using **NuGet**:
```bash
dotnet tool restore
```

### Running Format Commands

With [Task](https://taskfile.dev) installed:

```bash
# Format entire solution (both CleanupCode and CSharpier)
task format

# Run only CleanupCode
task format:cleanupcode

# Run only CSharpier
task format:csharpier
```

Always run `task format` before committing changes. Failing to format code may cause CI/CD checks to
fail, as the GitHub Actions workflow (`pr-build.yaml`) includes a code quality check that runs 
`task format` and validates no files were modified.

### IDE Integration

If you're using **JetBrains Rider** or **Visual Studio** with ReSharper:

- The solution includes `MinimalLambda.sln.DotSettings` with formatting rules
- Format on save can be configured in your IDE settings
- This ensures consistency even before running Task commands

## Making Changes

### Branching Strategy

Create branches following this naming convention:

```
feature/#<issue-number>-description     # New features
bug/description                         # Bug fixes (or bug/#<issue-number>-description)
chore/#<issue-number>-description       # Maintenance, tooling, build changes
docs/description                        # Documentation updates
```

**Example**:
```bash
git checkout -b feature/#42-add-custom-serializer
git checkout -b bug/on-shutdown-generator-trying-to-return-void
```

## Commit Guidelines

We follow [Conventional Commits](https://www.conventionalcommits.org/) format:

```
<type>(<optional scope>): <description>

[optional body]

[optional footer(s)]
```

### Commit Types

- **feat**: A new feature (e.g., `feat(source-generators): add OnShutdown support`)
- **fix**: A bug fix (e.g., `fix(#73): update MapHandler source generation`)
- **docs**: Documentation updates (e.g., `docs(openTelemetry): add XML documentation`)
- **refactor**: Code refactoring without feature changes (e.g., `refactor(source-generators): simplify logic`)
- **test**: Adding or updating tests (e.g., `test: add coverage for edge cases`)
- **chore**: Build, CI/CD, tooling, dependencies (e.g., `chore: update build targets`)
- **style**: Code style changes without logic changes (e.g., `style: simplify formatting`)

### Example Commits

```
feat(source-generators): add OnShutdown support for block lambdas and test coverage

Implements support for block lambda syntax in OnShutdown operations.
Includes comprehensive unit test coverage.
```

```
fix(#73): correct MapHandler detection in source generator

The source generator was not correctly identifying MapHandler methods
when using extension method syntax.
```

## Pull Request Process

1. **Ensure your branch is up to date**:
   ```bash
   git fetch upstream
   git rebase upstream/main
   ```

2. **Push to your fork**:
   ```bash
   git push origin your-branch-name
   ```

3. **Create a Pull Request** using the [PR template](/.github/pull_request_template.md):
   - **Title must follow Conventional Commits format** (validated by CI)
   - Format: `<type>(scope): <description>`
   - Valid types: `feat`, `fix`, `docs`, `refactor`, `test`, `chore`, `ci`, `style`
   - Reference related issues (e.g., `Closes #42`)
   - Complete the PR template checklist
   - Request review from maintainers

4. **Respond to feedback**:
   - Address review comments promptly
   - Push additional commits to the same branch
   - Avoid force-pushing after review has started

5. **Merge**:
   - Ensure all checks pass (CI/CD, tests, code review)
   - Maintainer will merge the PR
   - Your branch will be deleted after merge

## Code Style

### C# Conventions

- **Indentation**: 4 spaces
- **File-scoped namespaces**: Use `namespace MinimalLambda;` format
- **Nullable reference types**: Always enabled (`<Nullable>enable</Nullable>`)
- **Modern syntax**: Use records, top-level statements, and nullable annotations where appropriate
- **Line length**: Keep to 100 characters when practical

### XML Documentation

- **Document all public APIs** with XML documentation tags
- **Use only standard C# XML tags**: `<summary>`, `<param>`, `<returns>`, `<exception>`, `<remarks>`, `<example>`
- **Do NOT use unsupported tags** like `<strong>`, `<em>`, etc. (Use `<c>` for code/emphasis)
- **Example**:
  ```csharp
  /// <summary>
  /// Creates a new Lambda application builder.
  /// </summary>
  /// <param name="hostBuilder">The underlying host builder.</param>
  /// <returns>A configured <see cref="LambdaApplication"/> instance.</returns>
  /// <exception cref="ArgumentNullException">Thrown when <paramref name="hostBuilder"/> is null.</exception>
  public static LambdaApplication CreateBuilder(IHostBuilder hostBuilder) { }
  ```

## Testing

### Testing Framework

- **Framework**: xUnit v3
- **Assertions**: AwesomeAssertions (fluent assertions)
- **Mocking**: NSubstitute and AutoFixture
- **Test Location**: `/tests/` directory with project name pattern `*.UnitTests`

### Test Conventions

- **Test class naming**: `[SubjectClass]Test` (e.g., `LambdaApplicationTest`)
- **Test method naming**: Descriptive, following pattern `[Method]_[Condition]_[Expected]`
- **Test organization**: Use Arrange-Act-Assert pattern
- **Mark test subject**: Use `[TestSubject(typeof(...))]` attribute on test class

### Example Test

```csharp
[TestSubject(typeof(LambdaApplication))]
public class LambdaApplicationTest
{
    [Fact]
    public void RunAsync_WhenCalled_InvokesHandler()
    {
        // Arrange
        var mockHost = Substitute.For<IHost>();
        var application = new LambdaApplication(mockHost);

        // Act
        var result = await application.RunAsync();

        // Assert
        result.Should().BeSuccessful();
    }

    [Theory]
    [InlineData(null)]
    public void Constructor_WithNullHost_ThrowsArgumentNullException(IHost host)
    {
        // Act & Assert
        var act = () => new LambdaApplication(host);
        act.Should().Throw<ArgumentNullException>()
            .WithParameter(nameof(host));
    }
}
```

### Running Tests

```bash
# Run all tests
dotnet test

# Run tests for specific project
dotnet test tests/MinimalLambda.UnitTests

# Run with verbose output
dotnet test --verbosity detailed

# Run with coverage (MTP)
dotnet test --coverage --coverage-output-format cobertura
```

### Test Requirements

- **All public methods** should have corresponding unit tests
- **Happy path**: Test expected behavior with valid inputs
- **Error cases**: Test exception handling and validation
- **Edge cases**: Test boundary conditions and special cases
- **Coverage**: Aim for >80% code coverage on new features

## Documentation

### Project Documentation

- Update **README.md** for user-facing changes
- Add/update **XML documentation** for all public APIs
- Create/update docs in **/docs/claude/** for developer guides (if applicable)

### PR Documentation

- Include clear description of what changed and why
- Reference related issues
- Include examples for new features
- Document breaking changes prominently

### Documentation Standards

- **Clear and concise**: Write for developers unfamiliar with the code
- **Examples**: Provide code examples for complex features
- **Links**: Cross-reference related documentation
- **Keep current**: Update docs when implementation changes

## Target Frameworks

This project supports multiple .NET versions:

- **.NET 8.0** (supported)
- **.NET 9.0** (supported)
- **.NET 10.0 ** (primary target)

Test your changes against all supported frameworks:

```bash
dotnet build -f net8.0
dotnet build -f net9.0
```

## Central Package Management

Package versions are managed centrally in `Directory.Packages.props`. When adding dependencies:

1. Add the package version to `Directory.Packages.props`
2. Reference in project `.csproj` without version specifier
3. Document why the dependency is needed

## CI/CD Integration

All pull requests run through automated checks:

- **Build**: `dotnet build`
- **Tests**: `dotnet test`
- **Code analysis**: SonarCloud analysis

Ensure your changes pass all checks before requesting review.

## Questions or Need Help?

- **GitHub Issues**: For bug reports or feature requests
- **GitHub Discussions**: For questions or design discussions
- **PR Comments**: For implementation feedback

## License

By contributing to this project, you agree that your contributions will be licensed under the same MIT License as the project.

---

**Thank you for contributing to MinimalLambda!** 🚀
