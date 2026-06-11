# Project Templates

MinimalLambda ships `dotnet new` templates for starting AWS Lambda projects with the same layout and deployment defaults used by AWS Lambda tooling.

## Install templates

```bash
dotnet new install MinimalLambda.Templates
```

To update later, run the install command again. To remove the templates:

```bash
dotnet new uninstall MinimalLambda.Templates
```

## Basic Lambda

Create a standard MinimalLambda function and an integration test project:

```bash
dotnet new mlambda -n MyLambda
```

When run from a parent folder, the template creates a new `MyLambda` folder:

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

Run tests:

```bash
dotnet test MyLambda/test/MyLambda.Tests/MyLambda.Tests.csproj
```

To add a function to an existing repository or solution folder, generate into the current directory:

```bash
dotnet new mlambda -n MyLambda -o .
dotnet sln add src/MyLambda/MyLambda.csproj
dotnet sln add test/MyLambda.Tests/MyLambda.Tests.csproj
```

This creates `src/MyLambda` and `test/MyLambda.Tests` beside your existing solution instead of nesting them under a new `MyLambda` folder.

## Native AOT Lambda

Create a Native AOT-ready MinimalLambda function:

```bash
dotnet new mlambda-aot -n MyAotLambda
```

Use `-o .` when adding Native AOT to an existing repository or solution folder:

```bash
dotnet new mlambda-aot -n MyAotLambda -o .
dotnet sln add src/MyAotLambda/MyAotLambda.csproj
dotnet sln add test/MyAotLambda.Tests/MyAotLambda.Tests.csproj
```

The AOT template includes a `JsonSerializerContext`, `PublishAot`, `TrimMode=partial`, and `aws-lambda-tools-defaults.json` settings for AWS Lambda deployment.

Run tests:

```bash
dotnet test MyAotLambda/test/MyAotLambda.Tests/MyAotLambda.Tests.csproj
```

## Available templates

| Template          | Short name    | Use when                                     |
| ----------------- | ------------- | -------------------------------------------- |
| Standard Lambda   | `mlambda`     | Starting a standard MinimalLambda function   |
| Native AOT Lambda | `mlambda-aot` | Starting a Native AOT MinimalLambda function |

## AWS profile and region

Both templates accept `--profile` and `--region`. These values replace placeholders in `aws-lambda-tools-defaults.json`.

```bash
dotnet new mlambda -n MyLambda --profile default --region us-east-1
```

## Deploy

Install the AWS Lambda .NET CLI tool if needed:

```bash
dotnet tool install -g Amazon.Lambda.Tools
```

Deploy from the generated project directory:

```bash
cd MyLambda/src/MyLambda
dotnet lambda deploy-function
```

If you generated into an existing repository with `-o .`, use `cd src/MyLambda` instead.
