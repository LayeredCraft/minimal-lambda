#if MINIMALLAMBDA_DURABLE
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MinimalLambda.SourceGenerators.Models;
using WellKnownType = MinimalLambda.SourceGenerators.WellKnownTypes.WellKnownTypeData.WellKnownType;

namespace MinimalLambda.SourceGenerators.UnitTests;

public class DurableHandlerDiscoveryTests
{
    public static TheoryData<string> SupportedHandlerForms =>
        new()
        {
            "async () => await Task.CompletedTask",
            "async () => { await Task.CompletedTask; }",
            "HandleAsync",
            "LocalHandler",
            "Handler",
        };

    [Theory]
    [MemberData(nameof(SupportedHandlerForms))]
    public void RecognizesSupportedHandlerForms(string handler)
    {
        var source = $$"""
                       using System;
                       using System.Threading.Tasks;
                       using MinimalLambda;
                       using MinimalLambda.Builder;

                       internal static class Program
                       {
                           private static readonly Delegate Handler = (Func<Task>)HandleAsync;

                           private static Task HandleAsync() => Task.CompletedTask;

                           public static void Main()
                           {
                               Task LocalHandler() => Task.CompletedTask;
                               var lambda = LambdaApplication.CreateBuilder().Build();
                               lambda.MapDurableHandler({{handler}});
                           }
                       }
                       """;

        TransformDurableCall(source, includeDurableReferences: true)
            .Should()
            .BeOfType<DurableMethodInfo>();
    }

    [Fact]
    public void EmitsDurableAdapterForKnownExtensionMethod()
    {
        const string source = """
                              using System.Threading.Tasks;
                              using MinimalLambda;
                              using MinimalLambda.Builder;

                              var lambda = LambdaApplication.CreateBuilder().Build();
                              lambda.MapDurableHandler(() => Task.CompletedTask);
                              """;

        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            source,
            includeDurableReferences: true);

        driver
            .GetRunResult()
            .GeneratedTrees
            .Should()
            .ContainSingle(tree => tree.FilePath.EndsWith(
                "MinimalLambda.DurableHandlers.g.cs",
                StringComparison.Ordinal));
    }

    [Fact]
    public void IgnoresSameNamedMethodFromConsumerAssemblyWithoutDurableReferences()
    {
        const string source = """
                              using System;
                              using MinimalLambda;
                              using MinimalLambda.Builder;

                              namespace MinimalLambda.Builder
                              {
                                  internal static class MapDurableHandlerLambdaApplicationExtensions
                                  {
                                      extension(ILambdaInvocationBuilder builder)
                                      {
                                          public void MapDurableHandler(Delegate handler) { }
                                      }
                                  }
                              }

                              internal static class Program
                              {
                                  public static void Main()
                                  {
                                      var lambda = LambdaApplication.CreateBuilder().Build();
                                      lambda.MapDurableHandler(() => { });
                                  }
                              }
                              """;

        TransformDurableCall(source, includeDurableReferences: false).Should().BeNull();

        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(source);
        driver.GetRunResult().Diagnostics.Should().BeEmpty();
        driver.GetRunResult().GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void ResolvesAwsDurableSymbolsFromConsumerCompilation()
    {
        const string source = """
                              using MinimalLambda;

                              _ = LambdaApplication.CreateBuilder();
                              """;
        var (_, compilation) = GeneratorTestHelpers.GenerateFromSource(
            source,
            includeDurableReferences: true);
        var types = WellKnownTypes.WellKnownTypes.GetOrCreate(compilation);

        WellKnownType[] durableTypes =
        [
            WellKnownType.Amazon_Lambda_DurableExecution_IDurableContext,
            WellKnownType.Amazon_Lambda_DurableExecution_DurableFunction,
            WellKnownType.Amazon_Lambda_DurableExecution_DurableExecutionInvocationInput,
            WellKnownType.Amazon_Lambda_DurableExecution_DurableExecutionInvocationOutput,
        ];

        foreach (var durableType in durableTypes)
            types
                .Get(durableType)
                .ContainingAssembly
                .Name
                .Should()
                .Be("Amazon.Lambda.DurableExecution");
    }

    [Fact]
    public void PrefersAwsDurableAssemblyWhenConsumerSpoofsMetadataName()
    {
        const string source = """
                              namespace Amazon.Lambda.DurableExecution
                              {
                                  internal interface IDurableContext { }
                              }
                              """;
        var (_, compilation) = GeneratorTestHelpers.GenerateFromSource(
            source,
            includeDurableReferences: true);
        var types = WellKnownTypes.WellKnownTypes.GetOrCreate(compilation);

        types
            .Get(WellKnownType.Amazon_Lambda_DurableExecution_IDurableContext)
            .ContainingAssembly
            .Name
            .Should()
            .Be("Amazon.Lambda.DurableExecution");
    }

    [Fact]
    public void OrdinaryMapHandlerStillUsesOrdinaryModel()
    {
        const string source = """
                              using MinimalLambda;
                              using MinimalLambda.Builder;

                              var lambda = LambdaApplication.CreateBuilder().Build();
                              lambda.MapHandler(() => "ok");
                              """;

        var (_, compilation) = GeneratorTestHelpers.GenerateFromSource(source);
        var invocation = FindInvocation(compilation, "MapHandler");

        HandlerSyntaxProvider
            .Transform(
                invocation,
                compilation.GetSemanticModel(invocation.SyntaxTree),
                CancellationToken.None)
            .Should()
            .BeOfType<MapHandlerMethodInfo>();
    }

    private static IMethodInfo? TransformDurableCall(string source, bool includeDurableReferences)
    {
        var (_, compilation) = GeneratorTestHelpers.GenerateFromSource(
            source,
            includeDurableReferences: includeDurableReferences);
        var invocation = FindInvocation(compilation, "MapDurableHandler");

        return HandlerSyntaxProvider.Transform(
            invocation,
            compilation.GetSemanticModel(invocation.SyntaxTree),
            CancellationToken.None);
    }

    private static InvocationExpressionSyntax FindInvocation(
        Compilation compilation,
        string methodName) =>
        compilation
            .SyntaxTrees
            .Single()
            .GetRoot()
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Single(invocation =>
                invocation.Expression.ToString().EndsWith(methodName, StringComparison.Ordinal));
}
#endif
