# MinimalLambda Native AOT Function

This project contains a MinimalLambda AWS Lambda function configured for Native AOT.

## Run tests

```bash
dotnet test ../../test/Company.Project.Tests/Company.Project.Tests.csproj
```

## Native AOT

Native AOT compiles the Lambda function to a native executable for faster cold starts. Publishing Native AOT for AWS Lambda requires building for the same operating system and architecture used by Lambda. AWS Lambda tooling can perform a container build with an Amazon Linux build image when Docker is installed.

This template uses `TrimMode=partial`, matching AWS guidance for starter functions that may later use libraries that are not fully trim-safe. Test deployed AOT functions carefully because trim/AOT issues can appear at runtime when libraries depend on reflection.

## Deploy

Install the AWS Lambda .NET CLI tool if needed:

```bash
dotnet tool install -g Amazon.Lambda.Tools
```

Deploy from this project directory:

```bash
dotnet lambda deploy-function
```
