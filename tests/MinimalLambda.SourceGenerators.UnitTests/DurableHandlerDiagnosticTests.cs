#if NET10_0_OR_GREATER
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using MinimalLambda.SourceGenerators.Models;

namespace MinimalLambda.SourceGenerators.UnitTests;

public class DurableHandlerDiagnosticTests
{
    [Fact]
    public void DescriptorsMatchAdrContract()
    {
        var descriptors = new[]
        {
            Diagnostics.InvalidDurableInputCardinality,
            Diagnostics.InvalidDurableContextCardinality,
            Diagnostics.UnsupportedDurableParameter,
            Diagnostics.UnsupportedDurableReturnType,
            Diagnostics.MissingDurableSerializerRoot,
        };

        descriptors
            .Select(descriptor => descriptor.Id)
            .Should()
            .Equal("LH0007", "LH0008", "LH0009", "LH0010", "LH0011");
        descriptors
            .Take(4)
            .Should()
            .AllSatisfy(descriptor =>
            {
                descriptor.Category.Should().Be("MinimalLambda.Usage");
                descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
            });
        descriptors[4].Category.Should().Be("MinimalLambda.Configuration");
        descriptors[4].DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void BuildsStableTypedBindingModel()
    {
        var model = TransformSingle(
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
                ILambdaContext lambda,
                [FromEvent] (dynamic Value, int[] Items) input,
                IDurableContext durable,
                [FromKeyedServices("key")] IService service) => Task.FromResult("ok");

            interface IService { }
            """);

        model.DiagnosticInfos.Should().BeEmpty();
        model
            .InputCanonicalType
            .Should()
            .Be("global::System.ValueTuple<global::System.Object, global::System.Int32[]>");
        model.OutputCanonicalType.Should().Be("global::System.String");
        model.HasOutput.Should().BeTrue();
        model.HasAnyFromKeyedServices.Should().BeTrue();
        model
            .ParameterAssignments
            .Select(parameter => parameter.Source)
            .Should()
            .Equal(
                ParameterSource.Context,
                ParameterSource.Event,
                ParameterSource.DurableContext,
                ParameterSource.KeyedServices);
    }

    [Fact]
    public void ReportsCardinalityConflictsAndReturnInDeterministicOrder()
    {
        var diagnostics = Generate(
            """
            using System;
            using System.Threading.Tasks;
            using Amazon.Lambda.DurableExecution;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler(Handle);

            static ValueTask Handle(
                [FromEvent] IDurableContext first,
                [FromServices] IDurableContext second) => default;
            """);

        diagnostics
            .Select(diagnostic => diagnostic.Id)
            .Should()
            .Equal("LH0010", "LH0007", "LH0009", "LH0008", "LH0009");
        diagnostics[1]
            .GetMessage()
            .Should()
            .Be(
                "Durable handler must declare exactly one event input using '[FromEvent]'; found 0.");
        diagnostics[2]
            .GetMessage()
            .Should()
            .Contain("reserved context parameters cannot use event or service binding attributes");
        diagnostics[3].GetMessage().Should().Contain("found 2");
        diagnostics[0]
            .GetMessage()
            .Should()
            .Be(
                "Durable handler return type 'global::System.Threading.Tasks.ValueTask' is not supported; use 'Task' or 'Task<TOutput>' with a closed, nameable, accessible, non-transport output type.");
    }

    [Fact]
    public void ReportsMixedDuplicateEventInputsOnceAtExtraParameter()
    {
        var diagnostics = Generate(
            """
            using System.Threading.Tasks;
            using Amazon.Lambda.DurableExecution;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler(Handle);
            static Task Handle([FromEvent] string first, [Event] int second, IDurableContext context) => Task.CompletedTask;
            """);

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("LH0007");
        diagnostics[0].GetMessage().Should().EndWith("found 2.");
        diagnostics[0].Location.GetLineSpan().StartLinePosition.Line.Should().Be(7);
    }

    [Theory]
    [InlineData("[FromEvent] string input", "LH0008", "found 0")]
    [InlineData(
        "[FromEvent] string input, IDurableContext first, IDurableContext second",
        "LH0008",
        "found 2")]
    [InlineData("IDurableContext context", "LH0007", "found 0")]
    public void ReportsIsolatedCardinalityFailures(string parameters, string id, string message)
    {
        var diagnostics = Generate(
            $$"""
              using System.Threading.Tasks;
              using Amazon.Lambda.DurableExecution;
              using MinimalLambda;
              using MinimalLambda.Builder;

              var app = LambdaApplication.CreateBuilder().Build();
              app.MapDurableHandler(Handle);
              static Task Handle({{parameters}}) => Task.CompletedTask;
              """);

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be(id);
        diagnostics[0].GetMessage().Should().Contain(message);
    }

    [Theory]
    [InlineData("CancellationToken token", "CancellationToken is not bound automatically")]
    [InlineData(
        "[FromServices] CancellationToken token",
        "CancellationToken is not bound automatically")]
    [InlineData("Stream stream", "Stream transport types are reserved")]
    [InlineData(
        "DurableExecutionInvocationInput envelope",
        "outer durable envelope types are reserved")]
    public void ParameterReasonPrecedenceIsStable(string parameter, string reason)
    {
        var diagnostics = Generate(
            $$"""
              using System.IO;
              using System.Threading;
              using System.Threading.Tasks;
              using Amazon.Lambda.DurableExecution;
              using MinimalLambda;
              using MinimalLambda.Builder;

              var app = LambdaApplication.CreateBuilder().Build();
              app.MapDurableHandler(Handle);
              static Task Handle([FromEvent] string input, IDurableContext context, {{parameter}}) => Task.CompletedTask;
              """);

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("LH0009");
        diagnostics[0].GetMessage().Should().Contain(reason);
    }

    [Fact]
    public void RecursivelyRejectsTransportRootsButAllowsNestedDiTransport()
    {
        var diagnostics = Generate(
            """
            using System.Collections.Generic;
            using System.IO;
            using System.Threading.Tasks;
            using Amazon.Lambda.DurableExecution;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler(Handle);
            static Task<List<Stream>> Handle(
                [FromEvent] List<Stream> input,
                IDurableContext context,
                List<Stream> service) => null!;
            """);

        diagnostics.Select(diagnostic => diagnostic.Id).Should().Equal("LH0010", "LH0009");
    }

    [Theory]
    [InlineData("DurableExecutionInvocationInput", "LH0009")]
    [InlineData("DurableExecutionInvocationOutput", "LH0009")]
    [InlineData("System.Collections.Generic.List<DurableExecutionInvocationInput>", "LH0009")]
    [InlineData("System.Collections.Generic.List<DurableExecutionInvocationOutput>", "LH0009")]
    public void RejectsRawAndRecursivelyNestedInputEnvelopes(string inputType, string id)
    {
        var diagnostics = Generate(
            $$"""
              using System.Threading.Tasks;
              using Amazon.Lambda.DurableExecution;
              using MinimalLambda;
              using MinimalLambda.Builder;

              var app = LambdaApplication.CreateBuilder().Build();
              app.MapDurableHandler(Handle);
              static Task Handle([FromEvent] {{inputType}} input, IDurableContext context) => Task.CompletedTask;
              """);

        diagnostics.Select(diagnostic => diagnostic.Id).Should().Equal(id);
    }

    [Theory]
    [InlineData("DurableExecutionInvocationInput")]
    [InlineData("DurableExecutionInvocationOutput")]
    [InlineData("System.Collections.Generic.List<DurableExecutionInvocationInput>")]
    [InlineData("System.Collections.Generic.List<DurableExecutionInvocationOutput>")]
    [InlineData("System.Collections.Generic.List<System.IO.Stream>")]
    public void RejectsRawAndRecursivelyNestedOutputTransport(string outputType)
    {
        var diagnostics = Generate(
            $$"""
              using System.Threading.Tasks;
              using Amazon.Lambda.DurableExecution;
              using MinimalLambda;
              using MinimalLambda.Builder;

              var app = LambdaApplication.CreateBuilder().Build();
              app.MapDurableHandler(Handle);
              static Task<{{outputType}}> Handle([FromEvent] string input, IDurableContext context) => null!;
              """);

        diagnostics.Select(diagnostic => diagnostic.Id).Should().Equal("LH0010");
    }

    [Theory]
    [InlineData("ref int value")]
    [InlineData("in int value")]
    [InlineData("out int value")]
    public void RejectsByRefParametersWhenRoslynResolvesHandler(string parameter)
    {
        var diagnostics = Generate(
            $$"""
              using System.Threading.Tasks;
              using Amazon.Lambda.DurableExecution;
              using MinimalLambda;
              using MinimalLambda.Builder;

              var app = LambdaApplication.CreateBuilder().Build();
              app.MapDurableHandler(Handle);
              static Task Handle([FromEvent] string input, IDurableContext context, {{parameter}})
              {
                  {{(parameter.StartsWith("out", StringComparison.Ordinal) ? "value = 0;" : "")}}
                  return Task.CompletedTask;
              }
              """);

        diagnostics.Select(diagnostic => diagnostic.Id).Should().Equal("LH0009");
        diagnostics[0]
            .GetMessage()
            .Should()
            .Contain("ref, in, and out parameters are not supported");
    }

    [Theory]
    [InlineData("System.Span<int>")]
    [InlineData("System.ReadOnlySpan<char>")]
    public void RejectsRefLikeInputTypes(string inputType)
    {
        var diagnostics = Generate(
            $$"""
              using System.Threading.Tasks;
              using Amazon.Lambda.DurableExecution;
              using MinimalLambda;
              using MinimalLambda.Builder;

              var app = LambdaApplication.CreateBuilder().Build();
              app.MapDurableHandler(Handle);
              static Task Handle([FromEvent] {{inputType}} input, IDurableContext context) => Task.CompletedTask;
              """);

        diagnostics.Select(diagnostic => diagnostic.Id).Should().Equal("LH0009");
    }

    [Fact]
    public void RejectsInaccessibleNestedInputAndOutputTypes()
    {
        var diagnostics = Generate(
            """
            using System.Threading.Tasks;
            using Amazon.Lambda.DurableExecution;
            using MinimalLambda;
            using MinimalLambda.Builder;

            Entry.Map();
            internal static class Entry
            {
                private sealed class Hidden { }
                internal static void Map()
                {
                    var app = LambdaApplication.CreateBuilder().Build();
                    app.MapDurableHandler(Handle);
                }
                private static Task<Hidden> Handle([FromEvent] Hidden input, IDurableContext context) => null!;
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.Id).Should().Equal("LH0010", "LH0009");
    }

    [Fact]
    public void RejectsOpenTypesWhenRoslynProvidesConstructedModel()
    {
        var diagnostics = Generate(
            """
            using System.Threading.Tasks;
            using Amazon.Lambda.DurableExecution;
            using MinimalLambda;
            using MinimalLambda.Builder;

            Entry.Map<int>();
            internal static class Entry
            {
                internal static void Map<T>()
                {
                    var app = LambdaApplication.CreateBuilder().Build();
                    app.MapDurableHandler(Handle<T>);
                }
                private static Task<T> Handle<T>([FromEvent] T input, IDurableContext context) => Task.FromResult(input);
            }
            """);

        diagnostics.Select(diagnostic => diagnostic.Id).Should().Equal("LH0010", "LH0009");
        diagnostics
            .Should()
            .AllSatisfy(diagnostic => diagnostic.GetMessage().Should().Contain("closed"));
    }

    [Fact]
    public void FunctionPointerDisplayPreservesConventionAndRefModifiers()
    {
        var diagnostics = Generate(
            """
            using System.Threading.Tasks;
            using Amazon.Lambda.DurableExecution;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler(Handle);
            unsafe static Task Handle(
                [FromEvent] delegate* unmanaged[Cdecl]<ref int, out string, ref readonly byte, ref long> input,
                IDurableContext context) => Task.CompletedTask;
            """);

        diagnostics.Should().ContainSingle();
        diagnostics[0].Id.Should().Be("LH0009");
        diagnostics[0]
            .GetMessage()
            .Should()
            .Contain(
                "delegate* unmanaged[Cdecl]<ref global::System.Int32, out global::System.String, ref readonly global::System.Byte, ref global::System.Int64>");
    }

    [Fact]
    public void ConflictingReservedParameterDoesNotRunKeyedServiceBinding()
    {
        var model = TransformSingle(
            """
            using System.Threading.Tasks;
            using Amazon.Lambda.DurableExecution;
            using Microsoft.Extensions.DependencyInjection;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler(Handle);
            static Task Handle(
                [FromKeyedServices(null)] IDurableContext context) => Task.CompletedTask;
            """);

        model
            .DiagnosticInfos
            .Select(diagnostic => diagnostic.DiagnosticDescriptor.Id)
            .Should()
            .Equal("LH0007", "LH0009");
        model
            .ParameterAssignments
            .Should()
            .ContainSingle()
            .Which
            .IsFromKeyedService
            .Should()
            .BeFalse();
        model.ParameterAssignments[0].Assignment.Should().BeEmpty();
    }

    [Fact]
    public void SupportsLegacyEventAndRepeatedLambdaContexts()
    {
        var model = TransformSingle(
            """
            using System.Threading.Tasks;
            using Amazon.Lambda.Core;
            using Amazon.Lambda.DurableExecution;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler(Handle);
            static Task Handle(
                [Event] string input,
                ILambdaContext first,
                IDurableContext durable,
                ILambdaInvocationContext second,
                ILambdaContext third) => Task.CompletedTask;
            """);

        model.DiagnosticInfos.Should().BeEmpty();
        model
            .ParameterAssignments
            .Select(parameter => parameter.Source)
            .Should()
            .Equal(
                ParameterSource.Event,
                ParameterSource.Context,
                ParameterSource.DurableContext,
                ParameterSource.Context,
                ParameterSource.Context);
    }

    [Theory]
    [InlineData("void", "")]
    [InlineData("int", " => 0")]
    [InlineData("ValueTask", " => default")]
    [InlineData("ValueTask<int>", " => default")]
    public void RejectsUnsupportedReturnFamilies(string returnType, string body)
    {
        var source = $$"""
                       using System.Threading.Tasks;
                       using Amazon.Lambda.DurableExecution;
                       using MinimalLambda;
                       using MinimalLambda.Builder;

                       var app = LambdaApplication.CreateBuilder().Build();
                       app.MapDurableHandler(Handle);
                       static {{returnType}} Handle(
                           [FromEvent] string input,
                           IDurableContext context){{body}}{{(returnType == "void" ? " { }" : ";")}}
                       """;

        Generate(source).Select(diagnostic => diagnostic.Id).Should().ContainSingle("LH0010");
    }

    [Fact]
    public void ValidAndInvalidHandlersCoexistIndependently()
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
            static Task Valid([FromEvent] string input, IDurableContext context) => Task.CompletedTask;
            static Task Invalid(IDurableContext context) => Task.CompletedTask;
            """,
            includeDurableReferences: true);

        driver
            .GetRunResult()
            .Diagnostics
            .Select(diagnostic => diagnostic.Id)
            .Should()
            .ContainSingle("LH0007");
    }

    [Fact]
    public void UsesCompilationTreeOrderRatherThanFileNameOrder()
    {
        const string source = """
                              using MinimalLambda;
                              using MinimalLambda.Builder;

                              var app = LambdaApplication.CreateBuilder().Build();
                              app.MapDurableHandler(ZHandlers.Handle);
                              app.MapDurableHandler(AHandlers.Handle);
                              """;
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            source,
            includeDurableReferences: true,
            additionalSources:
            [
                ("Z.cs", """
                         using System.Threading.Tasks;
                         using Amazon.Lambda.DurableExecution;
                         internal static class ZHandlers
                         {
                             internal static Task Handle(IDurableContext context) => Task.CompletedTask;
                         }
                         """),
                ("A.cs", """
                         using System.Threading.Tasks;
                         using MinimalLambda.Builder;
                         internal static class AHandlers
                         {
                             internal static Task Handle([FromEvent] string input) => Task.CompletedTask;
                         }
                         """),
            ]);

        driver
            .GetRunResult()
            .Diagnostics
            .Select(diagnostic => diagnostic.Id)
            .Should()
            .Equal("LH0007", "LH0008");
    }

    [Fact]
    public void DeduplicatesDeclarationDiagnosticAcrossMappings()
    {
        var diagnostics = Generate(
            """
            using System.Threading.Tasks;
            using Amazon.Lambda.DurableExecution;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapDurableHandler(Handle);
            app.MapDurableHandler(Handle);
            static Task Handle(IDurableContext context) => Task.CompletedTask;
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
