# MinimalLambda Function

This project contains a MinimalLambda AWS Lambda function.

## Run tests

```bash
dotnet test ../../test/Company.Project.Tests/Company.Project.Tests.csproj
```

## Deploy

Install the AWS Lambda .NET CLI tool if needed:

```bash
dotnet tool install -g Amazon.Lambda.Tools
```

Deploy from this project directory:

```bash
dotnet lambda deploy-function
```
