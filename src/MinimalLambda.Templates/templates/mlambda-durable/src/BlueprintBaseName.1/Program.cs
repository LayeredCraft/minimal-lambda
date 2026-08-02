using System.Text.Json.Serialization;
using Amazon.Lambda.DurableExecution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalLambda;
using MinimalLambda.Builder;

var builder = LambdaApplication.CreateBuilder();
builder.Services.AddLambdaSerializerWithContext<BlueprintBaseName__1JsonContext>();
builder.Services.AddSingleton<GreetingService>();

await using var lambda = builder.Build();

lambda.MapDurableHandler(async (
    [FromEvent] GreetingRequest request,
    IDurableContext durable,
    [FromServices] GreetingService greetings) =>
{
    var result = await durable.StepAsync(
        (_, cancellationToken) => greetings.CreateAsync(request.Name, cancellationToken),
        name: "create-greeting");

    return new GreetingResponse(result.Message, durable.ExecutionContext.DurableExecutionArn);
});

await lambda.RunAsync();

internal sealed record GreetingRequest(string Name);

internal sealed record GreetingResponse(string Message, string ExecutionArn);

internal sealed record GreetingStepResult(string Message);

internal sealed class GreetingService
{
    public Task<GreetingStepResult> CreateAsync(string name, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(new GreetingStepResult($"Hello, {name}!"));
    }
}

[JsonSerializable(typeof(DurableExecutionInvocationInput))]
[JsonSerializable(typeof(DurableExecutionInvocationOutput))]
[JsonSerializable(typeof(GreetingRequest))]
[JsonSerializable(typeof(GreetingResponse))]
[JsonSerializable(typeof(GreetingStepResult))]
internal partial class BlueprintBaseName__1JsonContext : JsonSerializerContext;
