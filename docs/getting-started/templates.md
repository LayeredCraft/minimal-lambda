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

Run tests:

```bash
dotnet test MyLambda/test/MyLambda.Tests/MyLambda.Tests.csproj
```

## Native AOT Lambda

Create a Native AOT-ready MinimalLambda function:

```bash
dotnet new mlambda-aot -n MyAotLambda
```

The AOT template includes a `JsonSerializerContext`, `PublishAot`, `TrimMode=partial`, and `aws-lambda-tools-defaults.json` settings for AWS Lambda deployment.

Run tests:

```bash
dotnet test MyAotLambda/test/MyAotLambda.Tests/MyAotLambda.Tests.csproj
```

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
