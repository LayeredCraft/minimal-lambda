using AwesomeAssertions;
using Microsoft.CodeAnalysis;

namespace MinimalLambda.SourceGenerators.UnitTests;

public class DurableHandlerEmitterTests
{
    [Fact]
    public Task EmitsTaskAdapterThatCompiles() =>
        GeneratorTestHelpers.Verify(
            """
            using System.Threading.Tasks;
            using Amazon.Lambda.DurableExecution;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler(Handle);

            static Task Handle([FromEvent] string input, IDurableContext durable) => Task.CompletedTask;
            """,
            includeDurableReferences: true);

    [Fact]
    public Task PreservesAccessibleCustomDelegateType() =>
        GeneratorTestHelpers.Verify(
            """
            using System.Threading.Tasks;
            using Amazon.Lambda.DurableExecution;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler((DurableHandler)Handle);

            static Task Handle([FromEvent] string input, IDurableContext durable) => Task.CompletedTask;

            public delegate Task DurableHandler(string input, IDurableContext durable);
            """,
            includeDurableReferences: true);

    [Fact]
    public void PreservesCustomDelegateHiddenByReadonlyDelegateField()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            """
            using System;
            using System.Threading.Tasks;
            using Amazon.Lambda.DurableExecution;
            using MinimalLambda;
            using MinimalLambda.Builder;

            internal delegate Task DurableHandler([FromEvent] string input, IDurableContext durable);

            internal static class Program
            {
                private static readonly Delegate Handler = (DurableHandler)Handle;

                internal static void Main()
                {
                    var app = LambdaApplication.CreateBuilder().Build();
                    app.MapDurableHandler(Handler);
                }

                private static Task Handle([FromEvent] string input, IDurableContext durable) => Task.CompletedTask;
            }
            """,
            includeDurableReferences: true);

        var result = driver.GetRunResult();
        result.Diagnostics.Should().BeEmpty();
        GetDurableSource(result).Should().Contain("(global::DurableHandler)null!");
    }

    [Fact]
    public Task FallsBackToInferredSignatureForInaccessibleCustomDelegate() =>
        GeneratorTestHelpers.Verify(
            """
            using System.Threading.Tasks;
            using Amazon.Lambda.DurableExecution;
            using MinimalLambda;
            using MinimalLambda.Builder;

            Entry.Map();

            internal static class Entry
            {
                private delegate Task DurableHandler(string input, IDurableContext durable);

                internal static void Map()
                {
                    var app = LambdaApplication.CreateBuilder().Build();
                    app.MapDurableHandler((DurableHandler)Handle);
                }

                private static Task Handle([FromEvent] string input, IDurableContext durable) => Task.CompletedTask;
            }
            """,
            includeDurableReferences: true);

    [Fact]
    public Task EmitsTaskOfTAdapterThatCompiles() =>
        GeneratorTestHelpers.Verify(
            """
            using System.Threading.Tasks;
            using Amazon.Lambda.DurableExecution;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler(Handle);

            static Task<int> Handle([FromEvent] string input, IDurableContext durable) => Task.FromResult(input.Length);
            """,
            includeDurableReferences: true);

    [Fact]
    public Task EmitsOrderedContextDiAndKeyedBindingsInsideWorkflowClosure() =>
        GeneratorTestHelpers.Verify(
            """
            using System.Threading.Tasks;
            using Amazon.Lambda.Core;
            using Amazon.Lambda.DurableExecution;
            using Microsoft.Extensions.DependencyInjection;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler(Handle);

            static Task<string> Handle(
                IService required,
                ILambdaInvocationContext invocation,
                [FromEvent] string input,
                ILambdaContext lambda,
                IService? optional = null,
                IDurableContext durable = null!,
                [FromKeyedServices("key")] IService keyed = null!) => Task.FromResult(input);

            interface IService { }
            """,
            includeDurableReferences: true);

    [Fact]
    public void SuppressesInvalidAdapterButEmitsValidAdapter()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            """
            using System.Threading.Tasks;
            using Amazon.Lambda.DurableExecution;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler(Valid);
            app.MapDurableHandler(Invalid);

            static Task Valid([FromEvent] string input, IDurableContext durable) => Task.CompletedTask;
            static string Invalid([FromEvent] string input, IDurableContext durable) => input;
            """,
            includeDurableReferences: true);

        var result = driver.GetRunResult();
        result.Diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("LH0010");
        var durableSource = GetDurableSource(result);
        durableSource.Should().Contain("MapDurableHandlerInterceptor0");
        durableSource.Should().NotContain("MapDurableHandlerInterceptor1");
        durableSource.Should().Contain("global::System.Threading.Tasks.Task (string arg0");
        durableSource.Should().NotContain("string (string arg0");
    }

    [Fact]
    public void WarningOnlyAdapterStillEmits()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            """
            using System.Text.Json.Serialization;
            using System.Threading.Tasks;
            using Amazon.Lambda.DurableExecution;
            using Microsoft.Extensions.DependencyInjection;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var builder = LambdaApplication.CreateBuilder();
            builder.Services.AddLambdaSerializerWithContext<AppJsonContext>();
            var app = builder.Build();
            app.MapDurableHandler(Handle);

            static Task Handle([FromEvent] string input, IDurableContext durable) => Task.CompletedTask;

            [JsonSerializable(typeof(string))]
            partial class AppJsonContext : JsonSerializerContext { }
            """,
            includeDurableReferences: true);

        var result = driver.GetRunResult();
        result.Diagnostics.Select(diagnostic => diagnostic.Id).Should().Contain("LH0011");
        GetDurableSource(result).Should().Contain("MapDurableHandlerInterceptor0");
    }

    [Fact]
    public void OrdersAdaptersBySyntaxTreeThenMapSpan()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            """
            using System.Threading.Tasks;
            using Amazon.Lambda.DurableExecution;
            using MinimalLambda;
            using MinimalLambda.Builder;

            internal static class Program
            {
                public static void Main()
                {
                    var app = LambdaApplication.CreateBuilder().Build();
                    app.MapDurableHandler(SecondInFile);
                    app.MapDurableHandler(FirstInFile);
                }

                private static Task SecondInFile([FromEvent] string input, IDurableContext durable) => Task.CompletedTask;
                private static Task FirstInFile([FromEvent] int input, IDurableContext durable) => Task.CompletedTask;
            }
            """,
            includeDurableReferences: true,
            additionalSources:
            [
                ("EarlierDiscovery.cs", """
                                        using System.Threading.Tasks;
                                        using Amazon.Lambda.DurableExecution;
                                        using MinimalLambda;
                                        using MinimalLambda.Builder;

                                        internal static class Other
                                        {
                                            internal static void Map()
                                            {
                                                var app = LambdaApplication.CreateBuilder().Build();
                                                app.MapDurableHandler(Handle);
                                            }

                                            private static Task Handle([FromEvent] long input, IDurableContext durable) => Task.CompletedTask;
                                        }
                                        """),
            ]);

        var result = driver.GetRunResult();
        result.Diagnostics.Should().BeEmpty();
        var source = GetDurableSource(result);
        var first = source.IndexOf("Task (string arg0", StringComparison.Ordinal);
        var second = source.IndexOf("Task (int arg0", StringComparison.Ordinal);
        var third = source.IndexOf("Task (long arg0", StringComparison.Ordinal);
        first.Should().BeGreaterThan(-1);
        second.Should().BeGreaterThan(first);
        third.Should().BeGreaterThan(second);
    }

    private static string GetDurableSource(GeneratorDriverRunResult result) =>
        result
            .Results
            .SelectMany(generator => generator.GeneratedSources)
            .Single(source => source.HintName == "MinimalLambda.DurableHandlers.g.cs")
            .SourceText
            .ToString();
}
