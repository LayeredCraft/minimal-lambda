using System.Runtime.CompilerServices;
using BenchmarkDotNet.Attributes;

namespace MinimalLambda.Benchmarks;

public class FeatureBenchmarks
{
    private readonly DefaultFeatureCollection _features = new([]);
    private readonly IFeatureProvider[] _providers = [new StringFeatureProvider()];

    [GlobalSetup]
    public void Setup()
    {
        BenchmarkConfig.ReportRuntime();
        _features.Set("cached");
    }

    [Benchmark]
    public string? CachedLookup() => _features.Get<string>();

    [Benchmark]
    public string? FirstProviderLookup() => new DefaultFeatureCollection(_providers).Get<string>();

    [Benchmark]
    public IInvocationDataFeature ReplaceResponseStream()
    {
        var feature = CreateFeature();
        feature.ResponseStream = Stream.Null;
        return feature;
    }

    // Keep the factory boundary so the JIT cannot discard the original stream initialization.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static IInvocationDataFeature CreateFeature() =>
        new InvocationDataFeature { EventStream = Stream.Null };

    private sealed class StringFeatureProvider : IFeatureProvider
    {
        public bool TryCreate(Type type, out object? feature)
        {
            feature = type == typeof(string) ? "provided" : null;
            return feature is not null;
        }
    }
}
