using System.Collections.Concurrent;
using System.Text.Json.Serialization;
using Amazon;
using Amazon.Lambda.Core;
using Amazon.Lambda.DurableExecution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MinimalLambda;
using MinimalLambda.Builder;

AWSConfigs.AWSRegion = RegionEndpoint.USEast1.SystemName;

var builder = LambdaApplication.CreateBuilder();

builder.Services.AddLambdaSerializerWithContext<DurableLambdaJsonContext>();
builder.Services.AddSingleton<IDurableGreetingService, DurableGreetingService>();
builder.Services.AddSingleton<DurableMiddlewareProbe>();

await using var lambda = builder.Build();

lambda.UseMiddleware(async (context, next) =>
{
    var probe = context.ServiceProvider.GetRequiredService<DurableMiddlewareProbe>();
    probe.RecordBefore();

    try
    {
        await next(context);
    }
    finally
    {
        probe.RecordAfter();
    }
});

lambda.MapDurableHandler(HandleAsync);

await lambda.RunAsync();

static Task<DurableResult> HandleAsync(
    [FromEvent] DurableRequest request,
    IDurableContext durable,
    ILambdaInvocationContext invocation,
    [FromServices] ILambdaSerializer serializer,
    [FromServices] IDurableGreetingService service,
    [FromServices] DurableMiddlewareProbe probe)
{
    if (request.ShouldFail)
    {
        throw new InvalidOperationException("durable fixture failure");
    }

    return Task.FromResult(
        new DurableResult(
            service.CreateMessage(request.Name),
            durable.ExecutionContext.DurableExecutionArn,
            probe.BeforeCount == 1 && probe.AfterCount == 0,
            ReferenceEquals(serializer, invocation.Serializer)
            && ReferenceEquals(serializer, durable.LambdaContext.Serializer)));
}

public class DurableLambda;

internal sealed record DurableRequest(string Name, bool ShouldFail);

internal sealed record DurableResult(
    string Message,
    string ExecutionArn,
    bool MiddlewareEntered,
    bool SerializerIdentityPreserved);

internal interface IDurableGreetingService
{
    string CreateMessage(string name);
}

internal sealed class DurableGreetingService : IDurableGreetingService
{
    public string CreateMessage(string name) => $"Hello {name}!";
}

internal sealed class DurableMiddlewareProbe
{
    private readonly ConcurrentQueue<string> events = new();
    private int afterCount;
    private int beforeCount;

    public int BeforeCount => Volatile.Read(ref beforeCount);

    public int AfterCount => Volatile.Read(ref afterCount);

    public IReadOnlyCollection<string> Events => events.ToArray();

    public void RecordBefore()
    {
        Interlocked.Increment(ref beforeCount);
        events.Enqueue("before");
    }

    public void RecordAfter()
    {
        Interlocked.Increment(ref afterCount);
        events.Enqueue("after");
    }
}

[JsonSerializable(typeof(DurableExecutionInvocationInput))]
[JsonSerializable(typeof(DurableExecutionInvocationOutput))]
[JsonSerializable(typeof(DurableRequest))]
[JsonSerializable(typeof(DurableResult))]
internal partial class DurableLambdaJsonContext : JsonSerializerContext;
