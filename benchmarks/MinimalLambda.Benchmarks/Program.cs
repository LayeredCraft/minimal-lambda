using BenchmarkDotNet.Running;
using MinimalLambda.Benchmarks;

BenchmarkSwitcher.FromAssembly(typeof(BenchmarkConfig).Assembly).Run(args, new BenchmarkConfig());
