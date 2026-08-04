#if MINIMALLAMBDA_DURABLE
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
    public void ReportsMultipleDurableContextsAndSuppressesDurableAdapter()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            """
            using System.Threading.Tasks;
            using Amazon.Lambda.DurableExecution;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler(Handle);
            static Task Handle(IDurableContext first, IDurableContext second) => Task.CompletedTask;
            """,
            includeDurableReferences: true);

        // Act
        var result = driver.GetRunResult();

        // Assert
        result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "LH0007");
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void TreatsCancellationTokenAsSpecialBinding()
    {
        var model = TransformSingle(
            """
            using System.Threading;
            using System.Threading.Tasks;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler(Handle);
            static Task Handle(CancellationToken cancellationToken) => Task.CompletedTask;
            """);

        model.DiagnosticInfos.Should().BeEmpty();
        model
            .ParameterAssignments
            .Should()
            .ContainSingle()
            .Which
            .Should()
            .Match<DurableHandlerParameterInfo>(parameter =>
                parameter.Source == ParameterSource.CancellationToken
                && parameter.Assignment == "context.CancellationToken");
    }

    [Fact]
    public void ReportsInaccessibleHandlerTypesAndSuppressesDurableAdapter()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            """
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            internal static class Handlers
            {
                private sealed record Request;

                internal static void Map(ILambdaInvocationBuilder app) =>
                    app.MapDurableHandler(Handle);

                private static Task Handle([FromEvent] Request request) => Task.CompletedTask;
            }
            """,
            includeDurableReferences: true);

        // Act
        var result = driver.GetRunResult();

        // Assert
        result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "LH0007");
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void ReportsInaccessibleOutputTypeAndSuppressesDurableAdapter()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            """
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            internal static class Handlers
            {
                private sealed record Result;

                internal static void Map(ILambdaInvocationBuilder app) =>
                    app.MapDurableHandler(Handle);

                private static Task<Result> Handle() => Task.FromResult(new Result());
            }
            """,
            includeDurableReferences: true);

        // Act
        var result = driver.GetRunResult();

        // Assert
        result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "LH0007");
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void ReportsUnboundTypeParametersAndSuppressesDurableAdapter()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            """
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            internal static class Handlers<T>
            {
                internal static void Map(ILambdaInvocationBuilder app) =>
                    app.MapDurableHandler(Handle);

                private static Task Handle([FromEvent] T request) => Task.CompletedTask;
            }
            """,
            includeDurableReferences: true);

        // Act
        var result = driver.GetRunResult();

        // Assert
        result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "LH0007");
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void ReportsByReferenceParametersAndSuppressesDurableAdapter()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            """
            using System;
            using System.Threading.Tasks;
            using MinimalLambda.Builder;

            delegate Task RefHandler(ref int value);

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler((RefHandler)Handle);
            static Task Handle(ref int value) => Task.CompletedTask;
            """,
            includeDurableReferences: true);

        // Act
        var result = driver.GetRunResult();

        // Assert
        result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "LH0007");
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void ReportsFileLocalContainingTypeAndSuppressesDurableAdapter()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            """
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            file static class Handlers
            {
                public sealed record Request;

                internal static void Map(ILambdaInvocationBuilder app) =>
                    app.MapDurableHandler(Handle);

                private static Task Handle([FromEvent] Request request) => Task.CompletedTask;
            }
            """,
            includeDurableReferences: true);

        var result = driver.GetRunResult();

        result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "LH0007");
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void ReportsPointerParameterAndSuppressesDurableAdapter()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            """
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            internal static unsafe class Handlers
            {
                internal static void Map(ILambdaInvocationBuilder app) =>
                    app.MapDurableHandler(Handle);

                private static Task Handle([FromEvent] int* request) => Task.CompletedTask;
            }
            """,
            includeDurableReferences: true,
            allowUnsafe: true);

        var result = driver.GetRunResult();

        result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "LH0007");
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void ReportsRefLikeParameterAndSuppressesDurableAdapter()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            """
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            internal static class Handlers
            {
                internal ref struct Request;

                internal static void Map(ILambdaInvocationBuilder app) =>
                    app.MapDurableHandler(Handle);

                private static Task Handle([FromEvent] Request request) => Task.CompletedTask;
            }
            """,
            includeDurableReferences: true);

        var result = driver.GetRunResult();

        result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "LH0007");
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void ReportsEveryEventParameterAfterFirst()
    {
        var model = TransformSingle(
            """
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler(Handle);
            static Task Handle([FromEvent] string first, [Event] int second) => Task.CompletedTask;
            """);

        model
            .DiagnosticInfos
            .Select(diagnostic => diagnostic.DiagnosticDescriptor.Id)
            .Should()
            .Equal("LH0002");
    }

    [Fact]
    public void ReportsInaccessibleExplicitCustomDelegateAndSuppressesDurableAdapter()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
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

        var result = driver.GetRunResult();

        result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "LH0007");
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void ReportsCustomDelegateWrappedAsDelegateAndSuppressesDurableAdapter()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            """
            using System;
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            delegate Task DurableHandler(string input);

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler((Delegate)(DurableHandler)Handle);
            static Task Handle([FromEvent] string input) => Task.CompletedTask;
            """,
            includeDurableReferences: true);

        var result = driver.GetRunResult();

        result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "LH0007");
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void ReportsMismatchedExplicitDelegateAndSuppressesDurableAdapter()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            """
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            delegate Task ContravariantHandler(string input);

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler((ContravariantHandler)Handle);
            static Task Handle([FromEvent] object input) => Task.CompletedTask;
            """,
            includeDurableReferences: true);

        var result = driver.GetRunResult();

        result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "LH0007");
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void ReportsAnonymousOutputTypeAndSuppressesDurableAdapter()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            """
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler(() => Task.FromResult(new { Value = 42 }));
            """,
            includeDurableReferences: true);

        var result = driver.GetRunResult();

        result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "LH0007");
        result.GeneratedTrees.Should().BeEmpty();
    }

    [Fact]
    public void ReportsRefReturnAndSuppressesDurableAdapter()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            """
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            Entry.Map();

            internal delegate ref Task RefHandler();

            internal static class Entry
            {
                private static Task task = Task.CompletedTask;

                internal static void Map()
                {
                    var app = LambdaApplication.CreateBuilder().Build();
                    app.MapDurableHandler((RefHandler)Handle);
                }

                private static ref Task Handle() => ref task;
            }
            """,
            includeDurableReferences: true);

        var result = driver.GetRunResult();

        result.Diagnostics.Should().ContainSingle(diagnostic => diagnostic.Id == "LH0007");
        result.GeneratedTrees.Should().BeEmpty();
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
