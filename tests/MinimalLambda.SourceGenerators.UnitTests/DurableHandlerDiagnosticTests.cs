#if NET10_0_OR_GREATER
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using MinimalLambda.SourceGenerators.Models;

namespace MinimalLambda.SourceGenerators.UnitTests;

public class DurableHandlerDiagnosticTests
{
    [Fact]
    public void AllowsHandlersToOmitTheEventAndDurableContext()
    {
        var model = TransformSingle(
            """
            using System.Threading.Tasks;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler(Handle);
            static Task Handle() => Task.CompletedTask;
            """);

        model.DiagnosticInfos.Should().BeEmpty();
        model.InputType.Should().Be("global::System.Object");
        model.ParameterAssignments.Should().BeEmpty();
    }

    [Fact]
    public void TreatsOptionalDurableContextAndEventAsSpecialBindings()
    {
        var model = TransformSingle(
            """
            using System.Threading.Tasks;
            using Amazon.Lambda.DurableExecution;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler(Handle);
            static Task Handle([FromEvent] string input, IDurableContext durable) => Task.CompletedTask;
            """);

        model.DiagnosticInfos.Should().BeEmpty();
        model
            .ParameterAssignments
            .Select(parameter => parameter.Source)
            .Should()
            .Equal(ParameterSource.Event, ParameterSource.DurableContext);
    }

    [Fact]
    public void ReportsOnlyUnsupportedReturnFamilies()
    {
        var diagnostics = Generate(
            """
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler(Handle);
            static int Handle() => 42;
            """);

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("LH0007");
    }

    private static DurableMethodInfo TransformSingle(string source)
    {
        var (_, compilation) = GeneratorTestHelpers.GenerateFromSource(
            source,
            includeDurableReferences: true);
        var invocation =
            compilation
                .SyntaxTrees
                .Single()
                .GetRoot()
                .DescendantNodes()
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>()
                .Single(node =>
                    node
                        .Expression
                        .ToString()
                        .EndsWith("MapDurableHandler", StringComparison.Ordinal));
        return HandlerSyntaxProvider
            .Transform(
                invocation,
                compilation.GetSemanticModel(invocation.SyntaxTree),
                CancellationToken.None)
            .Should()
            .BeOfType<DurableMethodInfo>()
            .Subject;
    }

    private static Diagnostic[] Generate(string source)
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            source,
            includeDurableReferences: true);
        return driver.GetRunResult().Diagnostics.ToArray();
    }
}
#endif
