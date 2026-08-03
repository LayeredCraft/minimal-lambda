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

static Task HandleAsync(
    IDurableContext durable,
    [FromServices] GreetingService greetingService)
{
    _ = durable.GetInvocationContext();
    _ = greetingService.Create("durable");
    return Task.CompletedTask;
}

internal sealed class GreetingService
{
    public string Create(string name) => $"Hello, {name}!";
}

[JsonSerializable(typeof(DurableExecutionInvocationInput))]
[JsonSerializable(typeof(DurableExecutionInvocationOutput))]
[JsonSerializable(typeof(object))]
internal partial class AotJsonContext : JsonSerializerContext;
