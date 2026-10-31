using System.Collections;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Microsoft.Extensions.DependencyInjection;
using MinimalLambda;
using MinimalLambda.Builder;
using MinimalLambda.EventProcessing;

await RunFixture(false);
await RunFixture(true);
Console.WriteLine("Packed event fixture passed: defaults, configured options, middleware, JSON response, ordered outcomes, async scopes.");

static async Task RunFixture(bool configured)
{
    var builder = LambdaApplication.CreateBuilder();
    builder.Services.AddLambdaSerializerWithContext<EventJsonContext>();
    builder.Services.AddScoped<ScopeProbe>();
    if (configured)
    {
        builder.Services.Configure<EventHandlerOptions>(o => o.MaxConcurrentExecutions = 1);
        builder.Services.AddKeyedSingleton<string>("label", "");
    }
    await using var app = builder.Build();
    SQSEvent? observed = null;
    app.UseMiddleware(async (context, next) =>
    {
        observed = context.GetRequiredEvent<SQSEvent>();
        await next(context);
    });
    Func<SQSEvent.SQSMessage, ScopeProbe, IEventProcessingContext, int, string, Task<string>> handle = Handle;
    Task<string> Handle(SQSEvent.SQSMessage item, ScopeProbe _, IEventProcessingContext processing, int optional = 42, [FromKeyedServices("label")] string label = "fallback")
        {
            if (optional != 42 || label != (configured ? "" : "fallback")) throw new Exception("Optional default lost");
            if (item.Body == "retry") throw new InvalidOperationException("retry");
            if (item.Body == "stop") processing.StopProcessing();
            return Task.FromResult(item.MessageId);

    }
    app.MapEventHandler(
        split: (SQSEvent batch, ILambdaInvocationContext _) =>
        {
            if (!ReferenceEquals(batch, observed)) throw new Exception("Full event identity changed");
            return batch.Records;
        },
        handle: handle,
        onError: (item, _, _) => item.MessageId,
        createResponse: (outcomes, _) => new Response(outcomes.Where(o => o.Status != EventItemStatus.Succeeded)
            .Select(o => o.Item.MessageId).ToArray()));
    using var data = new Data(configured
        ? "{\"Records\":[{\"messageId\":\"a\",\"body\":\"ok\"},{\"messageId\":\"b\",\"body\":\"retry\"},{\"messageId\":\"c\",\"body\":\"stop\"},{\"messageId\":\"d\",\"body\":\"retry\"}]}"
        : "{\"Records\":[{\"messageId\":\"a\",\"body\":\"ok\"},{\"messageId\":\"b\",\"body\":\"retry\"}]}");
    var features = new Features();
    features.Set<IInvocationDataFeature>(data);
    foreach (var pair in app.Properties)
        if (pair.Value is IFeatureProvider provider)
        {
            if (provider.TryCreate(typeof(IEventFeature), out var input)) features.Set((IEventFeature)input!);
            if (provider.TryCreate(typeof(IResponseFeature), out var output)) features.Set((IResponseFeature)output!);
        }
    var invocation = new Invocation(app.Services, features);
    await app.Build()(invocation);
    features.Get<IResponseFeature>()!.SerializeToStream(invocation);
    var response = JsonSerializer.Deserialize(data.ResponseStream, EventJsonContext.Default.Response)!;
    var expected = configured ? new[] { "b", "d" } : new[] { "b" };
    if (!response.Retry.SequenceEqual(expected)) throw new Exception("Incorrect serialized response");
}

internal sealed record Response(string[] Retry);
internal sealed class ScopeProbe : IAsyncDisposable
{
    public async ValueTask DisposeAsync() => await Task.Yield();
}
internal sealed class Data(string json) : IInvocationDataFeature
{
    public Stream EventStream { get; } = new MemoryStream(Encoding.UTF8.GetBytes(json));
    public Stream ResponseStream { get; set; } = new MemoryStream();
    public void Dispose() { EventStream.Dispose(); ResponseStream.Dispose(); }
}
internal sealed class Features : IFeatureCollection
{
    private readonly Dictionary<Type, object> values = new();
    public void Set<T>(T instance) => values[typeof(T)] = instance!;
    public T? Get<T>() => values.TryGetValue(typeof(T), out var value) ? (T)value : default;
    public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => values.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
internal sealed class Invocation(IServiceProvider services, IFeatureCollection features) : ILambdaInvocationContext
{
    public CancellationToken CancellationToken => default;
    public IFeatureCollection Features => features;
    public IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
    public IDictionary<string, object?> Properties { get; } = new Dictionary<string, object?>();
    public IServiceProvider ServiceProvider => services;
    public string AwsRequestId => "packed-event";
    public IClientContext ClientContext => null!;
    public ICognitoIdentity Identity => null!;
    public string InvokedFunctionArn => "arn:aws:lambda:us-east-1:123456789012:function:test";
    public string FunctionName => "test";
    public string FunctionVersion => "1";
    public ILambdaLogger Logger => null!;
    public string LogGroupName => "test";
    public string LogStreamName => "test";
    public int MemoryLimitInMB => 128;
    public TimeSpan RemainingTime => TimeSpan.FromMinutes(1);
}
[JsonSerializable(typeof(SQSEvent))]
[JsonSerializable(typeof(Response))]
internal partial class EventJsonContext : JsonSerializerContext;
