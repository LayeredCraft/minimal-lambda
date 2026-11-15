using System.Text.Json;
using Amazon.Lambda.Core;
using Amazon.Lambda.TestUtilities;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.DependencyInjection;
using MinimalLambda.Builder;
using MinimalLambda.EventProcessing;
using MinimalLambda.Runtime;

namespace MinimalLambda.Benchmarks;

public class BatchBenchmarks
{
    private readonly TestLambdaContext _context =
        new() { RemainingTime = TimeSpan.FromMinutes(15) };

    private LambdaApplication _app = null!;
    private byte[] _payload = null!;
    private Func<Stream, ILambdaContext, Task<Stream>> _handler = null!;

    [Params(1, 32)]
    public int ItemCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkConfig.ReportRuntime();
        _payload = JsonSerializer.SerializeToUtf8Bytes(
            Enumerable.Range(0, ItemCount).ToArray(),
            BenchmarkJsonContext.Default.Int32Array);
        var builder =
            LambdaApplication.CreateBuilder(
                new LambdaApplicationOptions { DisableDefaults = true });
        builder.Services.AddLambdaSerializerWithContext<BenchmarkJsonContext>();
        builder.Services.Configure<EventHandlerOptions>(options =>
            options.MaxConcurrentExecutions = 4);
        _app = builder.Build();
        _app.MapEventHandler<int[], int>(
            static (items, _) => items,
            static async _ => await Task.Yield());
        _handler =
            _app
                .Services
                .GetRequiredService<ILambdaHandlerFactory>()
                .CreateHandler(CancellationToken.None);
    }

    [Benchmark]
    public async Task ProcessBatch()
    {
        using var input = new MemoryStream(_payload, writable: false);
        using var output = await _handler(input, _context).ConfigureAwait(false);
    }

    [GlobalCleanup]
    public void Cleanup() => _app.Dispose();
}
