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

`Program.cs` uses inline `MapDurableHandler` registration. Durable handlers require one
`[FromEvent]` input, one `IDurableContext`, and return `Task` or `Task<T>`.

Use cancellation token supplied by durable operation callbacks. Do not declare root
`CancellationToken` on durable handler: it belongs to physical Lambda invocation, while Durable
Execution may replay workflow across invocations.

Keep `DurableExecutionInvocationInput`, `DurableExecutionInvocationOutput`, workflow input/output,
and operation result types as explicit `JsonSerializable` roots.

## Deploy

`aws-lambda-tools-defaults.json` sets managed `dotnet10` runtime and durable execution defaults.
Create function as Durable Execution function; deployment and invocation permissions follow current
[AWS Durable Execution documentation](https://docs.aws.amazon.com/lambda/latest/dg/durable-getting-started.html).

```bash
dotnet lambda deploy-function
```

## Central Package Management

If repository uses Central Package Management, move generated package versions to
`Directory.Packages.props` and remove `Version="..."` from project references.
