using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalLambda;
using MinimalLambda.Builder;
using MinimalLambda.DurableExecution;

var builder = LambdaApplication.CreateBuilder();
builder.Services.AddLambdaSerializerWithContext<TypedJsonContext>();
builder.Services.AddSingleton<IGreetingService, GreetingService>();

await using var lambda = builder.Build();
lambda.MapDurableHandler(HandleAsync);
await lambda.RunAsync();

static Task<WorkflowOutput> HandleAsync(
    [FromEvent] WorkflowInput input,
    IDurableContext durable,
    ILambdaContext lambdaContext,
    ILambdaInvocationContext invocationContext,
    [FromServices] IGreetingService greetingService)
{
    _ = durable.GetInvocationContext();
    _ = lambdaContext.AwsRequestId;
    _ = invocationContext.Serializer;
    return Task.FromResult(new WorkflowOutput(greetingService.Create(input.Name)));
}

internal sealed record WorkflowInput(string Name);

internal sealed record WorkflowOutput(string Message);

internal interface IGreetingService
{
    string Create(string name);
}

internal sealed class GreetingService : IGreetingService
{
    public string Create(string name) => $"Hello, {name}!";
}

[JsonSerializable(typeof(DurableExecutionInvocationInput))]
[JsonSerializable(typeof(DurableExecutionInvocationOutput))]
[JsonSerializable(typeof(WorkflowInput))]
[JsonSerializable(typeof(WorkflowOutput))]
internal partial class TypedJsonContext : JsonSerializerContext;
