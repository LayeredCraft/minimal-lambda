# MinimalLambda.Templates

`dotnet new` templates for MinimalLambda AWS Lambda projects.

> 📚 **[View Full Documentation](https://j-d-ha.github.io/minimal-lambda/)**

## Overview

MinimalLambda.Templates provides ready-to-build AWS Lambda project templates for the MinimalLambda framework. The templates create a Lambda function project, an integration test project, and AWS Lambda deployment defaults using the same `src/` and `test/` layout as the AWS .NET Lambda blueprints.

Use it to:

- **Start new Lambda projects**: Generate a complete function folder with `src/<Name>` and `test/<Name>.Tests`
- **Add functions to existing repositories**: Use the standard `dotnet new` `-o .` option to write into the current solution folder
- **Choose standard or Native AOT**: Start with regular JIT deployment or AOT-friendly project settings
- **Test immediately**: Generated test projects use `MinimalLambda.Testing` and xUnit
- **Deploy with AWS tooling**: Generated projects include `aws-lambda-tools-defaults.json`

## Installation

Install the template package from NuGet:

```bash
dotnet new install MinimalLambda.Templates
```

Update to the latest published version by running the same command again:

```bash
dotnet new install MinimalLambda.Templates
```

Uninstall the templates:

```bash
dotnet new uninstall MinimalLambda.Templates
```

List installed MinimalLambda templates:

```bash
dotnet new list mlambda
```

## Local template development

From the repository root, pack and install the templates from your local checkout:

```bash
dotnet pack src/MinimalLambda.Templates/MinimalLambda.Templates.csproj -c Release
dotnet new install ./src/MinimalLambda.Templates --force
dotnet new list mlambda
```

Run a full standard-template smoke test:

```bash
mkdir -p /tmp/minimal-lambda-template-test
cd /tmp/minimal-lambda-template-test
rm -rf FullRunStandard
dotnet new mlambda -n FullRunStandard --profile default --region us-east-1
dotnet test FullRunStandard/test/FullRunStandard.Tests/FullRunStandard.Tests.csproj
```

Run a full Native AOT-template smoke test:

```bash
mkdir -p /tmp/minimal-lambda-template-test
cd /tmp/minimal-lambda-template-test
rm -rf FullRunAot
dotnet new mlambda-aot -n FullRunAot --profile default --region us-east-1
dotnet test FullRunAot/test/FullRunAot.Tests/FullRunAot.Tests.csproj
```

Run an existing-solution smoke test:

```bash
mkdir -p /tmp/minimal-lambda-template-test/ExistingSolutionRun
cd /tmp/minimal-lambda-template-test/ExistingSolutionRun
dotnet new sln -n ExistingSolutionRun
dotnet new mlambda -n AddedFunction -o . --profile default --region us-east-1
dotnet sln add src/AddedFunction/AddedFunction.csproj
dotnet sln add test/AddedFunction.Tests/AddedFunction.Tests.csproj --include-references false
dotnet test
```

## Quick Start

Create a new Lambda folder from a parent directory:

```bash
dotnet new mlambda -n MyLambda
cd MyLambda
dotnet test test/MyLambda.Tests/MyLambda.Tests.csproj
```

Generated layout:

```text
MyLambda/
  src/MyLambda/
    MyLambda.csproj
    Program.cs
    aws-lambda-tools-defaults.json
    README.md
  test/MyLambda.Tests/
    MyLambda.Tests.csproj
    LambdaTests.cs
```

Add a Lambda to an existing repository or solution folder:

```bash
dotnet new mlambda -n MyLambda -o .
dotnet sln add src/MyLambda/MyLambda.csproj
dotnet sln add test/MyLambda.Tests/MyLambda.Tests.csproj
```

This creates `src/MyLambda` and `test/MyLambda.Tests` beside your existing solution instead of nesting them under a new `MyLambda` folder.

## Templates

| Template                                                    | Short name    | Use when                                     |
| ----------------------------------------------------------- | ------------- | -------------------------------------------- |
| MinimalLambda AWS Lambda Function                           | `mlambda`     | Starting a standard MinimalLambda function   |
| MinimalLambda AWS Lambda Function configured for Native AOT | `mlambda-aot` | Starting a Native AOT MinimalLambda function |

## Template Options

Both templates support AWS profile and region replacement in `aws-lambda-tools-defaults.json`:

```bash
dotnet new mlambda -n MyLambda --profile default --region us-east-1
dotnet new mlambda-aot -n MyAotLambda --profile prod --region eu-west-1
```

Use the same options when generating into an existing repository:

```bash
dotnet new mlambda -n MyLambda -o . --profile default --region us-east-1
dotnet new mlambda-aot -n MyAotLambda -o . --profile prod --region eu-west-1
```

## Central Package Management

Like the built-in .NET and AWS Lambda templates, MinimalLambda templates emit versioned `PackageReference` items. If your repository uses Central Package Management, move those versions into `Directory.Packages.props` after generation.

Add or update these package versions at the repository root:

```xml
<ItemGroup>
  <PackageVersion Include="MinimalLambda" Version="2.6.0-beta.1" />
  <PackageVersion Include="MinimalLambda.Testing" Version="2.6.0-beta.1" />
  <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.6.0" />
  <PackageVersion Include="xunit" Version="2.9.3" />
  <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
</ItemGroup>
```

Then remove `Version="..."` from the generated `PackageReference` items in the app and test `.csproj` files.

## Native AOT

Create a new Native AOT Lambda folder:

```bash
dotnet new mlambda-aot -n MyAotLambda
```

Add a Native AOT Lambda to an existing repository or solution folder:

```bash
dotnet new mlambda-aot -n MyAotLambda -o .
dotnet sln add src/MyAotLambda/MyAotLambda.csproj
dotnet sln add test/MyAotLambda.Tests/MyAotLambda.Tests.csproj
```

The AOT template includes:

- `PublishAot=true`
- `PublishTrimmed=true`
- `TrimMode=partial`
- `JsonSerializerIsReflectionEnabledByDefault=false`
- A generated `JsonSerializerContext` in `Program.cs`

## Deploy

Install the AWS Lambda .NET CLI tool if needed:

```bash
dotnet tool install -g Amazon.Lambda.Tools
```

Deploy from the generated function project directory:

```bash
cd src/MyLambda
dotnet lambda deploy-function
```

For the new-folder templates, first enter the generated folder:

```bash
cd MyLambda/src/MyLambda
dotnet lambda deploy-function
```

## Other Packages

Additional packages in the minimal-lambda framework for runtime hosting, testing, observability, and event source handling.

| Package                                                                                                       | NuGet                                                                                                                                                          | Downloads                                                                                                                                                            |
| ------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| [**MinimalLambda**](../MinimalLambda/README.md)                                                               | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.svg)](https://www.nuget.org/packages/MinimalLambda)                                                     | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.svg)](https://www.nuget.org/packages/MinimalLambda/)                                                     |
| [**MinimalLambda.Abstractions**](../MinimalLambda.Abstractions/README.md)                                     | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Abstractions.svg)](https://www.nuget.org/packages/MinimalLambda.Abstractions)                           | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Abstractions.svg)](https://www.nuget.org/packages/MinimalLambda.Abstractions/)                           |
| [**MinimalLambda.OpenTelemetry**](../MinimalLambda.OpenTelemetry/README.md)                                   | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.OpenTelemetry.svg)](https://www.nuget.org/packages/MinimalLambda.OpenTelemetry)                         | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.OpenTelemetry.svg)](https://www.nuget.org/packages/MinimalLambda.OpenTelemetry/)                         |
| [**MinimalLambda.Testing**](../MinimalLambda.Testing/README.md)                                               | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Testing.svg)](https://www.nuget.org/packages/MinimalLambda.Testing)                                     | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Testing.svg)](https://www.nuget.org/packages/MinimalLambda.Testing/)                                     |
| [**MinimalLambda.Templates**](README.md)                                                                      | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Templates.svg)](https://www.nuget.org/packages/MinimalLambda.Templates)                                 | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Templates.svg)](https://www.nuget.org/packages/MinimalLambda.Templates/)                                 |
| [**MinimalLambda.Envelopes**](../Envelopes/MinimalLambda.Envelopes/README.md)                                 | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes)                                 | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes/)                                 |
| [**MinimalLambda.Envelopes.Sqs**](../Envelopes/MinimalLambda.Envelopes.Sqs/README.md)                         | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Sqs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sqs)                         | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Sqs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sqs/)                         |
| [**MinimalLambda.Envelopes.ApiGateway**](../Envelopes/MinimalLambda.Envelopes.ApiGateway/README.md)           | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.ApiGateway.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.ApiGateway)           | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.ApiGateway.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.ApiGateway/)           |
| [**MinimalLambda.Envelopes.Sns**](../Envelopes/MinimalLambda.Envelopes.Sns/README.md)                         | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Sns.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sns)                         | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Sns.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Sns/)                         |
| [**MinimalLambda.Envelopes.Kinesis**](../Envelopes/MinimalLambda.Envelopes.Kinesis/README.md)                 | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Kinesis.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kinesis)                 | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Kinesis.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kinesis/)                 |
| [**MinimalLambda.Envelopes.KinesisFirehose**](../Envelopes/MinimalLambda.Envelopes.KinesisFirehose/README.md) | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.KinesisFirehose.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.KinesisFirehose) | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.KinesisFirehose.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.KinesisFirehose/) |
| [**MinimalLambda.Envelopes.Kafka**](../Envelopes/MinimalLambda.Envelopes.Kafka/README.md)                     | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Kafka.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kafka)                     | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Kafka.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Kafka/)                     |
| [**MinimalLambda.Envelopes.CloudWatchLogs**](../Envelopes/MinimalLambda.Envelopes.CloudWatchLogs/README.md)   | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.CloudWatchLogs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.CloudWatchLogs)   | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.CloudWatchLogs.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.CloudWatchLogs/)   |
| [**MinimalLambda.Envelopes.Alb**](../Envelopes/MinimalLambda.Envelopes.Alb/README.md)                         | [![NuGet](https://img.shields.io/nuget/v/MinimalLambda.Envelopes.Alb.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Alb)                         | [![Downloads](https://img.shields.io/nuget/dt/MinimalLambda.Envelopes.Alb.svg)](https://www.nuget.org/packages/MinimalLambda.Envelopes.Alb/)                         |

## License

This project is licensed under the MIT License. See [LICENSE](../../LICENSE) for details.
