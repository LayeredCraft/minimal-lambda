# BlueprintBaseName.1

MinimalLambda AWS Lambda function project configured for Native AOT.

This starter project contains:

- `Program.cs` - Lambda host setup, sample handler, and source-generated JSON serializer context.
- `BlueprintBaseName.1.csproj` - .NET project configured for AWS Lambda Native AOT deployment.
- `aws-lambda-tools-defaults.json` - default settings used by Visual Studio and `dotnet lambda` commands.
- `../../test/BlueprintBaseName.1.Tests` - integration tests using `MinimalLambda.Testing`.

The generated handler accepts a `string` Lambda event and returns the uppercase equivalent. Replace the event type, return type, and handler body in `Program.cs` to match your Lambda trigger.

## Prerequisites

- .NET SDK 8.0 or later.
- AWS credentials configured locally if you plan to deploy.
- Amazon.Lambda.Tools 5.6.0 or later for command-line deployment.
- Docker installed and running when publishing Native AOT from any OS other than Amazon Linux 2023.

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

## Native AOT notes

Native AOT compiles the Lambda function to a native executable. This can reduce cold starts and remove the need for the .NET runtime on the target platform.

This template enables Native AOT with these project settings:

- `PublishAot=true`
- `PublishTrimmed=true`
- `TrimMode=partial`
- `StripSymbols=true`
- `JsonSerializerIsReflectionEnabledByDefault=false`

When publishing with Native AOT, the build OS and architecture must match the Lambda target platform. AWS Lambda uses Amazon Linux 2023. AWS Lambda tooling can perform a container build with an Amazon Linux build image when Docker is installed.

## JSON serialization and trimming

Native AOT works best when JSON serialization is source generated. This template registers a `JsonSerializerContext` in `Program.cs`:

```csharp
builder.Services.AddLambdaSerializerWithContext<BlueprintBaseName__1JsonSerializerContext>();
```

Add every event, response, and DTO type used by your handlers to the context:

```csharp
[JsonSerializable(typeof(MyEvent))]
[JsonSerializable(typeof(MyResponse))]
public partial class BlueprintBaseName__1JsonSerializerContext : JsonSerializerContext;
```

Trimming removes code the compiler cannot see. Libraries that depend on reflection can fail at runtime if required members are trimmed. This template uses `TrimMode=partial` as a safer starter setting, but you should still test deployed AOT functions carefully.

For more trimming details, see <https://learn.microsoft.com/dotnet/core/deploying/trimming/trim-self-contained>.

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
- `msbuild-parameters` - additional MSBuild parameters used for publishing.

You can set profile and region when creating the project:

```bash
dotnet new mlambda-aot -n BlueprintBaseName.1 --profile default --region us-east-1
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
- Add `[JsonSerializable]` entries for your event, response, and DTO types.
- Register services with `builder.Services` in `Program.cs`.
- Add middleware with `lambda.UseMiddleware(...)` for logging, validation, or error handling.
- Update tests in `../../test/BlueprintBaseName.1.Tests` to cover your handler behavior.
