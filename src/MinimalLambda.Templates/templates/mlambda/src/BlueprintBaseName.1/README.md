# BlueprintBaseName.1

MinimalLambda AWS Lambda function project.

This starter project contains:

- `Program.cs` - Lambda host setup and sample handler.
- `BlueprintBaseName.1.csproj` - .NET project configured for AWS Lambda deployment.
- `aws-lambda-tools-defaults.json` - default settings used by Visual Studio and `dotnet lambda` commands.
- `../../test/BlueprintBaseName.1.Tests` - integration tests using `MinimalLambda.Testing`.

The generated handler accepts a `string` Lambda event and returns the uppercase equivalent. Replace the event type, return type, and handler body in `Program.cs` to match your Lambda trigger.

## Prerequisites

- .NET SDK 8.0 or later.
- AWS credentials configured locally if you plan to deploy.
- Amazon.Lambda.Tools for command-line deployment.

Install Amazon.Lambda.Tools if needed:

```bash
dotnet tool install -g Amazon.Lambda.Tools
```

Update Amazon.Lambda.Tools if already installed:

```bash
dotnet tool update -g Amazon.Lambda.Tools
```

## Restore and build

From this project directory:

```bash
dotnet restore
dotnet build
```

## Run tests

From this project directory:

```bash
dotnet test ../../test/BlueprintBaseName.1.Tests/BlueprintBaseName.1.Tests.csproj
```

Or from the generated repository/folder root:

```bash
dotnet test test/BlueprintBaseName.1.Tests/BlueprintBaseName.1.Tests.csproj
```

## Central Package Management

This template emits versioned `PackageReference` items. If your repository uses Central Package Management, move those versions into `Directory.Packages.props` and remove `Version="..."` from the generated app and test project references.

Add or update these entries at the repository root:

```xml
<ItemGroup>
  <PackageVersion Include="MinimalLambda" Version="2.6.0-beta.1" />
  <PackageVersion Include="MinimalLambda.Testing" Version="2.6.0-beta.1" />
  <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.6.0" />
  <PackageVersion Include="xunit" Version="2.9.3" />
  <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.5" />
</ItemGroup>
```

## Configure AWS deployment defaults

`aws-lambda-tools-defaults.json` contains deployment defaults for `dotnet lambda` and the AWS Toolkit for Visual Studio:

- `profile` - AWS credentials profile.
- `region` - AWS region.
- `function-runtime` - Lambda runtime.
- `function-memory-size` - memory in MB.
- `function-timeout` - timeout in seconds.
- `function-handler` - executable assembly name for this MinimalLambda app.

You can set profile and region when creating the project:

```bash
dotnet new mlambda -n BlueprintBaseName.1 --profile default --region us-east-1
```

Or edit `aws-lambda-tools-defaults.json` before deployment.

## Deploy from the command line

From this project directory:

```bash
dotnet lambda deploy-function
```

The AWS Lambda .NET CLI reads `aws-lambda-tools-defaults.json` for default deployment settings and prompts for any missing values.

Useful commands:

```bash
dotnet lambda help
dotnet lambda list-functions
dotnet lambda invoke-function BlueprintBaseName.1 --payload "hello world"
```

## Deploy from Visual Studio

With the AWS Toolkit for Visual Studio installed:

1. Right-click the project in Solution Explorer.
2. Select **Publish to AWS Lambda**.
3. Review values from `aws-lambda-tools-defaults.json`.
4. Publish the function.

After deployment, use the AWS Explorer Lambda Function View to test invocations, configure event sources, update runtime configuration, and view CloudWatch logs.

## Next steps

- Replace the sample `string` handler with your event type.
- Register services with `builder.Services` in `Program.cs`.
- Add middleware with `lambda.UseMiddleware(...)` for logging, validation, or error handling.
- Update tests in `../../test/BlueprintBaseName.1.Tests` to cover your handler behavior.
