# MinimalLambda durable execution example

Typed durable handler with dependency injection, execution metadata, checkpointed step result, and source-generated JSON serialization.

```bash
dotnet build examples/MinimalLambda.Example.DurableExecution/MinimalLambda.Example.DurableExecution.csproj
```

`dotnet run` expects Lambda Runtime API and is not a standalone local workflow runner.

## Package for deployment

Install Amazon.Lambda.Tools. From this directory, produce deployment ZIP:

```bash
dotnet lambda package \
  --configuration Release \
  --output-package artifacts/MinimalLambda.Example.DurableExecution.zip
```

This sample uses Amazon.Lambda.Tools deployment defaults rather than SAM. Deploy with Amazon.Lambda.Tools 7.0.0 or later; `aws-lambda-tools-defaults.json` supplies managed `dotnet10`, `durable-execution-timeout`, retention, and `function-publish` settings. Supply a function name and execution role with `AWSLambdaBasicDurableExecutionRolePolicy` plus application-specific permissions:

```bash
dotnet lambda deploy-function <function-name> \
  --package artifacts/MinimalLambda.Example.DurableExecution.zip \
  --function-role arn:aws:iam::<account-id>:role/<durable-execution-role>
```

AWS configures `DurableConfig` from durable deployment settings. Follow AWS [Durable Execution deployment guide](https://docs.aws.amazon.com/lambda/latest/dg/durable-getting-started.html) for current IAM and deployment requirements. Durable invocations support a published version, alias, or `$LATEST`; prefer immutable version or alias for stable routing. Start and poll an execution with Amazon.Lambda.Tools 7.0.0 or later:

```bash
dotnet lambda invoke-function <function-name>:<version-or-alias> \
  --invoke-mode DurableExecution \
  --payload '{"OrderId":"order-123"}'
```

Consult current [AWS Durable Execution documentation](https://docs.aws.amazon.com/lambda/latest/dg/durable-getting-started.html) for deployment and IAM requirements. No AWS deployment was run for this example.

## Advanced low-level escape hatch

`MapDurableHandler` is preferred because normal handler stays typed and does not expose protocol envelopes or AWS client. If direct protocol control is required, replace that mapping (do not add a second mapping) with low-level `MapHandler` wiring:

```csharp
using Amazon.Lambda;
using Amazon.Lambda.DurableExecution;
using Microsoft.Extensions.DependencyInjection;
using MinimalLambda;
using MinimalLambda.Builder;

var builder = LambdaApplication.CreateBuilder();
builder.Services.AddLambdaSerializerWithContext<DurableExampleJsonContext>();
builder.Services.AddSingleton<IAmazonLambda, AmazonLambdaClient>();

await using var lambda = builder.Build();
lambda.MapHandler(
    async (
        [FromEvent] DurableExecutionInvocationInput envelope,
        ILambdaInvocationContext invocation,
        [FromServices] IAmazonLambda client) =>
        await DurableFunction.WrapAsync<OrderRequest, OrderResult>(
            LowLevelWorkflowAsync,
            envelope,
            invocation,
            client));

static async Task<OrderResult> LowLevelWorkflowAsync(
    OrderRequest request,
    IDurableContext durable)
{
    var step = await durable.StepAsync(
        (_, _) => Task.FromResult(
            new ProcessOrderStepResult($"Order {request.OrderId} processed")),
        name: "process-order");

    return new OrderResult(
        step.Message,
        durable.ExecutionContext.DurableExecutionArn,
        durable.LambdaContext.AwsRequestId);
}

await lambda.RunAsync();
```

Keep `DurableExecutionInvocationInput`, `DurableExecutionInvocationOutput`, handler input/output, and step result as explicit `JsonSerializable` roots in either approach.

Step bodies can be retried. Keep external side effects idempotent; checkpointing a step result does not provide exactly-once execution.
