# Installation

This guide walks you through installing `MinimalLambda` and configuring your project to build Lambda functions.

## System Requirements

Before you begin, ensure your development environment meets these requirements:

| Requirement         | Minimum Version                                       | Recommended |
| ------------------- | ----------------------------------------------------- | ----------- |
| .NET SDK            | 8.0                                                   | Latest LTS  |
| C# Language Version | 11                                                    | latest      |
| IDE                 | Visual Studio 2022 (17.8+), Rider 2023.3+, or VS Code | Latest      |
| AWS CLI             | 2.0+ (optional)                                       | Latest      |

!!! note "C# 11 Requirement"

    C# 11 or later is required for source generators and interceptors that power the framework's compile-time optimizations.

## Installing the NuGet Package

Choose your preferred installation method:

=== ".NET CLI"

    ```bash
    # Create a new console project
    dotnet new console -n MyFirstLambda
    cd MyFirstLambda

    # Add the MinimalLambda package
    dotnet add package MinimalLambda
    ```

    *Tip: the `examples/MinimalLambda.Example.HelloWorld` project in this repo shows a fully configured Lambda app if you prefer copying a working template.*

=== "Visual Studio"

    1. Right-click on your project in Solution Explorer
    2. Select **Manage NuGet Packages**
    3. Search for `MinimalLambda`
    4. Click **Install**

=== "Package Reference"

    Add this to your `.csproj` file:

    ```xml
    <ItemGroup>
      <PackageReference Include="MinimalLambda" Version="1.2.1-beta.1" />
    </ItemGroup>
    ```

## Project File Configuration

Configure your project file (`.csproj`) with the required settings for Lambda development.

### Required Settings

Open your `.csproj` file and ensure these properties are set:

```xml title="MyFirstLambda.csproj"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <!-- Lambda requires executable output -->
    <OutputType>Exe</OutputType>

    <!-- Target .NET 8 or later -->
    <TargetFramework>net8.0</TargetFramework>

    <!-- Use latest C# features (required for source generators) -->
    <LangVersion>latest</LangVersion>

    <!-- Mark as Lambda project (helps AWS tooling) -->
    <AWSProjectType>Lambda</AWSProjectType>

    <!-- Enable nullable reference types (recommended) -->
    <Nullable>enable</Nullable>

    <!-- Disable implicit usings for explicit control -->
    <ImplicitUsings>disable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

### Optional Settings (Recommended)

These optional settings improve the Lambda development experience:

```xml title="MyFirstLambda.csproj"
<PropertyGroup>
  <!-- Copy dependencies for Lambda Test Tool -->
  <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>

  <!-- Optimize for faster cold starts -->
  <PublishReadyToRun>true</PublishReadyToRun>

  <!-- Generate runtime config for local testing -->
  <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
</PropertyGroup>
```

### Complete Example

Here's a complete, minimal `.csproj` file for a Lambda function:

```xml title="MyFirstLambda.csproj" linenums="1"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>disable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <AWSProjectType>Lambda</AWSProjectType>
    <GenerateRuntimeConfigurationFiles>true</GenerateRuntimeConfigurationFiles>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
    <PublishReadyToRun>true</PublishReadyToRun>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MinimalLambda" Version="1.2.0" />
  </ItemGroup>
</Project>
```

!!! info "Source Generators"

    The `MinimalLambda` package ships an MSBuild target that automatically registers the required interceptor namespaces. No additional configuration is needed in your project file.

## Verifying Installation

Let's verify everything is set up correctly by creating a simple Lambda function.

### Create Program.cs

Create a `Program.cs` file with this minimal example:

```csharp title="Program.cs" linenums="1"
using MinimalLambda;

var builder = LambdaApplication.CreateBuilder();
var lambda = builder.Build();

lambda.MapHandler(([FromEvent] string input) => input.ToUpper());

await lambda.RunAsync();
```

### Build the Project

Run the build command:

=== ".NET CLI"

    ```bash
    dotnet build
    ```

=== "Visual Studio"

    Press `Ctrl+Shift+B` or select **Build** → **Build Solution**

### Expected Output

You should see output similar to:

```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

!!! success "Installation Successful"

    If the build succeeds, your installation is complete and you're ready to build Lambda functions!

## Package Overview

The `MinimalLambda` framework includes multiple packages for different use cases:

### Core Packages

| Package                         | Purpose                   | When to Use                                   |
| ------------------------------- | ------------------------- | --------------------------------------------- |
| **MinimalLambda**               | Core framework            | Required for all Lambda functions             |
| **MinimalLambda.Abstractions**  | Interfaces and contracts  | When creating custom extensions or middleware |
| **MinimalLambda.OpenTelemetry** | Observability integration | When you need distributed tracing and metrics |

### Envelope Packages

Envelope packages provide type-safe, strongly-typed event handling for specific AWS event sources:

| Package                                     | Event Source              | When to Use                              |
| ------------------------------------------- | ------------------------- | ---------------------------------------- |
| **MinimalLambda.Envelopes**                 | Infrastructure            | HTTP response builders (auto-referenced) |
| **MinimalLambda.Envelopes.Sqs**             | Amazon SQS                | Processing SQS queue messages            |
| **MinimalLambda.Envelopes.Sns**             | Amazon SNS                | Handling SNS notifications               |
| **MinimalLambda.Envelopes.ApiGateway**      | API Gateway               | Building REST/HTTP APIs                  |
| **MinimalLambda.Envelopes.Kinesis**         | Kinesis Data Streams      | Processing stream records                |
| **MinimalLambda.Envelopes.KinesisFirehose** | Kinesis Firehose          | Transforming Firehose data               |
| **MinimalLambda.Envelopes.Kafka**           | Apache Kafka / MSK        | Processing Kafka messages                |
| **MinimalLambda.Envelopes.CloudWatchLogs**  | CloudWatch Logs           | Processing log subscriptions             |
| **MinimalLambda.Envelopes.Alb**             | Application Load Balancer | ALB target Lambda functions              |

!!! info "Envelope Packages"

    You only need envelope packages if you're working with those specific event sources. For simple use cases, just `MinimalLambda` is sufficient. Learn more in the [Envelopes documentation](../features/envelopes.md).

## Troubleshooting

### Common Issues

#### C# Language Version Error

**Error**: `Feature 'interceptors' is not available in C# 10`

**Solution**: Set `<LangVersion>latest</LangVersion>` in your `.csproj` file.

#### Build Errors After Installation

**Error**: Various build errors after adding the package

**Solution**:

1. Verify your .NET SDK version: `dotnet --version`
2. Ensure it's .NET 8.0 or later
3. Clean and rebuild: `dotnet clean && dotnet build`

#### Missing OutputType

**Error**: Lambda function doesn't execute when deployed

**Solution**: Ensure `<OutputType>Exe</OutputType>` is set in your `.csproj`. Lambda functions must be executable console applications.

### Getting Help

If you encounter issues not covered here:

- Search or ask in [GitHub Discussions](https://github.com/j-d-ha/minimal-lambda/discussions)
- Report bugs in [GitHub Issues](https://github.com/j-d-ha/minimal-lambda/issues)

## Next Steps

Now that you have `MinimalLambda` installed and verified, you're ready to build your first Lambda function!

**→ Continue to [Your First Lambda](first-lambda.md)** to build a complete Lambda function step-by-step.

### Additional Resources

- [Core Concepts](core-concepts.md) – Understand the framework architecture
- [Examples](../examples/index.md) – Browse complete working examples
