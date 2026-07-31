using System.Text.Json.Serialization;
using Amazon.Lambda.DurableExecution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalLambda;
using MinimalLambda.Builder;

var builder = LambdaApplication.CreateBuilder();

builder.Services.AddLambdaSerializerWithContext<DurableExampleJsonContext>();
builder.Services.AddSingleton<IOrderService, OrderService>();

await using var lambda = builder.Build();

lambda.MapDurableHandler(HandleOrderAsync);

await lambda.RunAsync();

static async Task<OrderResult> HandleOrderAsync(
    [FromEvent] OrderRequest request,
    IDurableContext durable,
    [FromServices] IOrderService orders)
{
    var step = await durable.StepAsync(
        (_, cancellationToken) => orders.ProcessAsync(request.OrderId, cancellationToken),
        name: "process-order");

    return new OrderResult(
        step.Message,
        durable.ExecutionContext.DurableExecutionArn,
        durable.LambdaContext.AwsRequestId);
}

internal sealed record OrderRequest(string OrderId);

internal sealed record OrderResult(string Message, string ExecutionArn, string AwsRequestId);

internal sealed record ProcessOrderStepResult(string Message);

internal interface IOrderService
{
    Task<ProcessOrderStepResult> ProcessAsync(string orderId, CancellationToken cancellationToken);
}

internal sealed class OrderService : IOrderService
{
    public Task<ProcessOrderStepResult> ProcessAsync(
        string orderId,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new ProcessOrderStepResult($"Order {orderId} processed"));
    }
}

[JsonSerializable(typeof(DurableExecutionInvocationInput))]
[JsonSerializable(typeof(DurableExecutionInvocationOutput))]
[JsonSerializable(typeof(OrderRequest))]
[JsonSerializable(typeof(OrderResult))]
[JsonSerializable(typeof(ProcessOrderStepResult))]
internal partial class DurableExampleJsonContext : JsonSerializerContext;
