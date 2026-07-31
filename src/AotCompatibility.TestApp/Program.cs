using System.Text.Json.Serialization;
using Amazon.Lambda.DurableExecution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalLambda;
using MinimalLambda.Builder;

var builder = LambdaApplication.CreateBuilder();
builder.Services.AddLambdaSerializerWithContext<AotJsonSerializerContext>();

await using var lambda = builder.Build();
lambda.MapDurableHandler(HandleAsync);
await lambda.RunAsync();

static async Task<DurableWorkflowOutput> HandleAsync(
    [FromEvent] DurableWorkflowInput input,
    IDurableContext durable)
{
    var stepResult = await durable.StepAsync<DurableStepResult>(
        (_, _) => Task.FromResult(new DurableStepResult(input.Name.Trim())),
        name: "normalize-name");

    return new DurableWorkflowOutput($"Hello, {stepResult.NormalizedName}!");
}

internal sealed record DurableWorkflowInput(string Name);

internal sealed record DurableWorkflowOutput(string Message);

internal sealed record DurableStepResult(string NormalizedName);

[JsonSerializable(typeof(DurableExecutionInvocationInput))]
[JsonSerializable(typeof(DurableExecutionInvocationOutput))]
[JsonSerializable(typeof(DurableWorkflowInput))]
[JsonSerializable(typeof(DurableWorkflowOutput))]
[JsonSerializable(typeof(DurableStepResult))]
internal partial class AotJsonSerializerContext : JsonSerializerContext;
