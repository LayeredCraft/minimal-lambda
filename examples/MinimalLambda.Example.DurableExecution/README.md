# MinimalLambda durable execution example

Typed durable handler with dependency injection, execution metadata, checkpointed step result, and source-generated JSON serialization.

```bash
dotnet build examples/MinimalLambda.Example.DurableExecution/MinimalLambda.Example.DurableExecution.csproj
```

`dotnet run` expects Lambda Runtime API and is not a standalone local workflow runner. Deployment assets are included for static validation. Follow [Durable Execution Deployment](../../docs/guides/durable-execution-deployment.md) for local package validation, deployment syntax, IAM, qualified invocation, and evidence limits. No AWS deployment was run for this example.

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
