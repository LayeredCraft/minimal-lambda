using System.Text.Json.Serialization;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using MinimalLambda.Builder;
using MinimalLambda.Runtime;

namespace MinimalLambda.Benchmarks;

public class InvocationBenchmarks
{
    private readonly TestLambdaContext _context =
        new() { RemainingTime = TimeSpan.FromMinutes(15) };

    private readonly byte[] _payload = "{\"Value\":42}"u8.ToArray();
    private LambdaApplication _completed = null!;
    private LambdaApplication _suspended = null!;
    private LambdaApplication _serialized = null!;
    private Func<Stream, ILambdaContext, Task<Stream>> _completedHandler = null!;
    private Func<Stream, ILambdaContext, Task<Stream>> _suspendedHandler = null!;
    private Func<Stream, ILambdaContext, Task<Stream>> _serializedHandler = null!;

    [Params(0, 3)]
    public int MiddlewareCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkConfig.ReportRuntime();
        _completed = CreateApplication();
        _completed.MapHandler(() => Task.CompletedTask);
        _completedHandler = CreateHandler(_completed);

        _suspended = CreateApplication();
        _suspended.MapHandler(async () =>
        {
            await Task.Yield();
        });
        _suspendedHandler = CreateHandler(_suspended);

        _serialized = CreateApplication();
        _serialized.MapHandler(([FromEvent] BenchmarkPayload value) => value);
        _serializedHandler = CreateHandler(_serialized);
    }

    [Benchmark]
    public Task Completed() => Invoke(_completedHandler);

    [Benchmark]
    public Task Suspended() => Invoke(_suspendedHandler);

    [Benchmark]
    public Task Serialize() => Invoke(_serializedHandler);

    private async Task Invoke(Func<Stream, ILambdaContext, Task<Stream>> handler)
    {
        using var input = new MemoryStream(_payload, writable: false);
        using var output = await handler(input, _context).ConfigureAwait(false);
    }

    private LambdaApplication CreateApplication()
    {
        var builder =
            LambdaApplication.CreateBuilder(
                new LambdaApplicationOptions { DisableDefaults = true });
        builder.Services.AddLambdaSerializerWithContext<BenchmarkJsonContext>();
        var app = builder.Build();
        for (var index = 0; index < MiddlewareCount; index++)
            app.Use(next => async context => await next(context).ConfigureAwait(false));
        return app;
    }

    private static Func<Stream, ILambdaContext, Task<Stream>>
        CreateHandler(LambdaApplication app) =>
        app
            .Services
            .GetRequiredService<ILambdaHandlerFactory>()
            .CreateHandler(CancellationToken.None);

    [GlobalCleanup]
    public void Cleanup()
    {
        _completed.Dispose();
        _suspended.Dispose();
        _serialized.Dispose();
    }
}

public sealed record BenchmarkPayload(int Value);

[JsonSerializable(typeof(BenchmarkPayload))]
[JsonSerializable(typeof(int[]))]
public partial class BenchmarkJsonContext : JsonSerializerContext;
