# BlueprintBaseName.1

MinimalLambda AWS Lambda Durable Execution function.

## Prerequisites

- .NET SDK 10.0 or later.
- AWS credentials for deployment.
- Amazon.Lambda.Tools 7.0.0 or later.

## Restore and build

```bash
dotnet restore
dotnet build
```

## Durable handler

`Program.cs` uses inline `MapDurableHandler` registration. `[FromEvent]` input and
`IDurableContext` are optional; durable handlers return `Task` or `Task<T>`.

A root `CancellationToken` parameter receives physical Lambda-invocation cancellation. It is not a
Durable Execution operation token; use cancellation supplied to durable operation callbacks for step work.

Keep `DurableExecutionInvocationInput`, `DurableExecutionInvocationOutput`, workflow input/output,
and operation result types as explicit `JsonSerializable` roots.

## Deploy

`aws-lambda-tools-defaults.json` sets managed `dotnet10` runtime and durable execution defaults.
`dotnet lambda deploy-function` creates an execution role when one does not exist and attaches AWS
managed policy `AWSLambdaBasicDurableExecutionRolePolicy`. If you supply a custom role with
`--function-role`, attach that policy before deployment; add application-specific permissions as needed.
Deployment and invocation permissions follow current [AWS Durable Execution documentation](https://docs.aws.amazon.com/lambda/latest/dg/durable-getting-started.html).

```bash
dotnet lambda deploy-function

# Custom role:
# dotnet lambda deploy-function --function-role arn:aws:iam::123456789012:role/durable-execution-role
```

## Central Package Management

If repository uses Central Package Management, move generated package versions to
`Directory.Packages.props` and remove `Version="..."` from project references.
