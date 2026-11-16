using System.Reflection;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Environments;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.CsProj;
using BenchmarkDotNet.Toolchains.DotNetCli;
using MinimalLambda.Runtime;

namespace MinimalLambda.Benchmarks;

public class BenchmarkConfig : ManualConfig
{
    internal static void ReportRuntime()
    {
        var method = typeof(LambdaHostedService).GetMethod(
            "ExecuteAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Console.WriteLine(
            $"Core async lowering: {method.MethodImplementationFlags}; runtime: {Environment.Version}");
    }

    public BenchmarkConfig()
    {
        Add(DefaultConfig.Instance);
        AddDiagnoser(MemoryDiagnoser.Default);
        var common = Job.Default.WithArguments(
        [
            new MsBuildArgument("/p:GeneratePackageOnBuild=false"),
            new MsBuildArgument("/p:MinimalLambdaRuntimeAsync=false")
        ]);
        AddJob(common.WithRuntime(CoreRuntime.Core10_0).WithId("NET10"));

        var toolchain =
            CsProjCoreToolchain.From(new NetCoreAppSettings("net11.0", null, ".NET 11.0"));
        // shortcut: BDN 0.15.8 needs a known moniker; the toolchain runs net11.0. Remove when BDN
        // supports .NET 11.
        var net11 = common.WithRuntime(CoreRuntime.Core10_0).WithToolchain(toolchain);
        AddJob(net11.WithId("NET11"));
        AddJob(
            net11
                .WithArguments(
                [
                    new MsBuildArgument("/p:GeneratePackageOnBuild=false"),
                    new MsBuildArgument("/p:MinimalLambdaRuntimeAsync=true")
                ])
                .WithId("NET11-RuntimeAsync"));
    }
}
