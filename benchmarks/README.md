# MinimalLambda benchmarks

Run from the repository root with the stable .NET 11 SDK and both .NET 10 and .NET 11 runtimes installed:

```bash
dotnet run --project benchmarks/MinimalLambda.Benchmarks -c Release -f net11.0 -- --filter '*InvocationBenchmarks*' '*BatchBenchmarks*' '*FeatureBenchmarks*'
```

The suite measures complete invocations, zero or three middleware layers, JSON deserialization and serialization, batches of one or 32 items, feature lookup, and response-stream replacement. Batch handlers use `Task.Yield()` to exercise suspended work with a concurrency limit of four. `CreateBuilderBenchmarks` remains available through the benchmark filter.

Three jobs compare the same source:

| Job | Target | Compiler lowering |
| --- | --- | --- |
| `NET10` | .NET 10 | Classic async |
| `NET11` | .NET 11 | Classic async |
| `NET11-RuntimeAsync` | .NET 11 | Runtime Async |

BenchmarkDotNet 0.15.8 does not recognize the .NET 11 runtime moniker. The .NET 11 jobs use an explicit `net11.0` toolchain and a known moniker for BenchmarkDotNet's SDK checks and build ordering. Its configuration summary can therefore say `Runtime=.NET 10.0`; the process environment header and setup diagnostics report the actual runtime. Setup also prints the core library's `ExecuteAsync` implementation flags: `IL` for classic async and `Async` for Runtime Async.

## Runtime Async

Runtime Async remains disabled by default. To evaluate repository builds:

```bash
dotnet test MinimalLambda.sln -c Release -p:MinimalLambdaRuntimeAsync=true
task build:aot-check
```

The property applies only to `net11.0` projects. Compiler feature changes participate in the incremental compile cache, so switching modes rebuilds affected assemblies. The AOT task publishes .NET 10 and both .NET 11 modes. CI tests both modes and validates stable and preview package manifests.

This is a repository build option. It does not recompile installed NuGet assemblies or automatically opt client projects into Runtime Async.

## Release preparation measurements

Measured on November 15, 2026, on an Apple M3 Max running macOS 27.0.1, SDK 11.0.100, .NET 10.0.13, and .NET 11.0.0. These are short local JIT measurements, with one launch, two warmups, five measured iterations, and a 100 ms iteration target. They do not measure AWS cold starts or Native AOT execution.

```bash
dotnet run --project benchmarks/MinimalLambda.Benchmarks -c Release -f net11.0 -- --filter '*InvocationBenchmarks*' '*BatchBenchmarks*' '*FeatureBenchmarks*' --warmupCount 2 --iterationCount 5 --iterationTime 100 --launchCount 1
```

The allocation baseline uses commit `3a481e47` with the same benchmark code and benchmark access to internals. Run each revision from its own checkout directory so BenchmarkDotNet resolves the correct project. Both .NET 10 and .NET 11 showed these allocations per operation:

| Operation | Before | After |
| --- | ---: | ---: |
| Cached feature lookup | 32 B | 0 B |
| First provider lookup | 248 B | 248 B |
| Replace response stream before first read | 96 B | 32 B |

The response-stream benchmark keeps a non-inlined factory boundary so the JIT cannot remove the original stream initialization as dead code. No pooling or stream ownership changes are involved.

For .NET 11, Runtime Async increased allocations in the suspended workloads:

| Workload | Classic async | Runtime Async |
| --- | ---: | ---: |
| Completed invocation, no middleware | 992 B | 992 B |
| Suspended invocation, no middleware | 1,558 B | 2,256 B |
| Batch of 32 suspended items | 16.09 KB | 24.18 KB |

Timing results varied by workload and had wide confidence intervals in some cases. These measurements support keeping Runtime Async opt-in for this release. Repeat longer runs on the deployment architecture before changing the default.
