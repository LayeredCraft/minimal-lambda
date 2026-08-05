using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalLambda;
using MinimalLambda.Builder;
using MinimalLambda.DurableExecution;

var builder = LambdaApplication.CreateBuilder();
builder.Services.AddLambdaSerializerWithContext<TaskJsonContext>();
builder.Services.AddSingleton<WorkflowProbe>();

await using var lambda = builder.Build();
lambda.MapDurableHandler(HandleAsync);
await lambda.RunAsync();

static Task HandleAsync(
    [FromEvent] WorkflowInput input,
    IDurableContext durable,
    ILambdaContext lambdaContext,
    ILambdaInvocationContext invocationContext,
    [FromServices] WorkflowProbe probe)
{
    _ = durable.GetInvocationContext();
    _ = lambdaContext.AwsRequestId;
    _ = invocationContext.Serializer;
    probe.Observe(input.Name);
    return Task.CompletedTask;
}

internal sealed record WorkflowInput(string Name);

internal sealed class WorkflowProbe
{
    public void Observe(string value) => _ = value.Length;
}

[JsonSerializable(typeof(DurableExecutionInvocationInput))]
[JsonSerializable(typeof(DurableExecutionInvocationOutput))]
[JsonSerializable(typeof(WorkflowInput))]
internal partial class TaskJsonContext : JsonSerializerContext;
