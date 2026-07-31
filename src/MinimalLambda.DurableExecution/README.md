# MinimalLambda.DurableExecution

Experimental, locally buildable AWS Lambda Durable Execution integration for MinimalLambda.

> 📚 **[View Full Documentation](https://layeredcraft.github.io/minimal-lambda/)**

## Requirements

- .NET 8 or .NET 10
- A compatible `MinimalLambda` version
- `Amazon.Lambda.DurableExecution` 1.0.0

## Installation

Install all three packages explicitly:

```bash
dotnet add package MinimalLambda
dotnet add package MinimalLambda.DurableExecution
dotnet add package Amazon.Lambda.DurableExecution --version 1.0.0
```

Keep the direct `MinimalLambda` reference so its single source generator runs for ordinary and
durable handlers; no separate durable generator package is required. The AWS Durable Execution
runtime already arrives transitively through `MinimalLambda.DurableExecution`, but transitive
package dependencies exclude analyzer assets. The direct `Amazon.Lambda.DurableExecution` reference
ensures its DE001-DE004 analyzers also run. The wrapper package does not duplicate the AWS runtime or
analyzer assemblies.

## Experimental API

`MapDurableHandler(Delegate)` is the compile-time interception target for durable handlers. Supported
handlers declare exactly one `[FromEvent]` workflow input, one `IDurableContext`, and return `Task`
or `Task<T>`. MinimalLambda invocation contexts and DI services can be additional parameters.

```csharp
using Amazon.Lambda.DurableExecution;
using MinimalLambda;
using MinimalLambda.Builder;

lambda.MapDurableHandler(async (
    [FromEvent] OrderRequest request,
    IDurableContext durable,
    IOrderService orders) =>
{
    return await durable.StepAsync(
        (_, cancellationToken) => orders.ProcessAsync(request, cancellationToken));
});
```

This API is experimental and may change before durable source-generation support is complete.
Calling it without a generated interceptor throws the same runtime fallback exception as
`MapHandler`.

Durable workflows can recover the exact MinimalLambda invocation context supplied to AWS by calling
`GetInvocationContext()`:

```csharp
using MinimalLambda.DurableExecution;

ILambdaInvocationContext invocation = durable.GetInvocationContext();
var requestId = invocation.AwsRequestId;
```

Until durable interception is available, or when raw envelope control is needed, ordinary
`MapHandler` remains the low-level escape hatch:

```csharp
using Amazon.Lambda.DurableExecution;
using MinimalLambda;
using MinimalLambda.Builder;

lambda.MapHandler(
    ([FromEvent] DurableExecutionInvocationInput envelope, ILambdaInvocationContext context) =>
        DurableFunction.WrapAsync<OrderRequest, OrderResult>(Workflow, envelope, context));
```

Local restore, build, and package validation do not prove cloud deployment or production-ready
NativeAOT support. Consult current project documentation and AWS runtime guidance before deployment.
