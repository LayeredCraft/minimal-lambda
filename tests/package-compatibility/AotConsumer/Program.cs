using System.Text.Json.Serialization;
using Amazon.Lambda.DurableExecution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalLambda;
using MinimalLambda.Builder;
using MinimalLambda.DurableExecution;

var builder = LambdaApplication.CreateBuilder();
builder.Services.AddLambdaSerializerWithContext<AotJsonContext>();
builder.Services.AddSingleton<GreetingService>();

await using var lambda = builder.Build();
lambda.MapDurableHandler(HandleAsync);
await lambda.RunAsync();

static Task<WorkflowOutput> HandleAsync(
    [FromEvent] WorkflowInput input,
    IDurableContext durable,
    [FromServices] GreetingService greetingService)
{
    _ = durable.GetInvocationContext();
    return Task.FromResult(new WorkflowOutput(greetingService.Create(input.Name)));
}

internal sealed record WorkflowInput(string Name);

internal sealed record WorkflowOutput(string Message);

internal sealed class GreetingService
{
    public string Create(string name) => $"Hello, {name}!";
}

[JsonSerializable(typeof(DurableExecutionInvocationInput))]
[JsonSerializable(typeof(DurableExecutionInvocationOutput))]
[JsonSerializable(typeof(WorkflowInput))]
[JsonSerializable(typeof(WorkflowOutput))]
internal partial class AotJsonContext : JsonSerializerContext;
