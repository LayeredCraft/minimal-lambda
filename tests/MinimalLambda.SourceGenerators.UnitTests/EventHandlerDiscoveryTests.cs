using System.Text;
using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace MinimalLambda.SourceGenerators.UnitTests;

public class EventHandlerDiscoveryTests
{
    [Fact]
    public void GeneratesInterceptorsForAllFiftyFiveOverloads()
    {
        var source = BuildAllOverloadsSource();

        var (driver, compilation) = GeneratorTestHelpers.GenerateFromSource(source);
        var result = driver.GetRunResult();

        result
            .Diagnostics
            .Should()
            .BeEmpty(
                "all supported overloads should generate, but found:\n"
                + string.Join(
                    "\n",
                    result.Diagnostics.Select(diagnostic =>
                        $"  - {diagnostic.Id}: {diagnostic.GetMessage()}")));

        var tree = result
            .GeneratedTrees
            .Should()
            .ContainSingle(generated => generated.FilePath.EndsWith(
                "MinimalLambda.EventHandlers.g.cs",
                StringComparison.Ordinal))
            .Subject;
        CountOccurrences(tree.ToString(), "MapEventHandlerInterceptor").Should().Be(55);

        var errors = CompileWithGenerated(compilation, result)
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .ToList();
        errors
            .Should()
            .BeEmpty(
                "generated interceptors should compile, but found:\n"
                + string.Join(
                    "\n",
                    errors.Select(error => $"  - {error.Id}: {error.GetMessage()}")));
    }

    [Theory]
    [InlineData("(Item item) => Task.CompletedTask")]
    [InlineData("Handle")]
    [InlineData("handler")]
    [InlineData("LocalHandler")]
    public void RecognizesSupportedHandlerForms(string handler)
    {
        var source = $$"""
                       using System;
                       using System.Threading.Tasks;
                       using MinimalLambda;
                       using MinimalLambda.Builder;

                       internal static class Program
                       {
                           private static readonly Func<Item, Task> handler = Handle;

                           private static Task Handle(Item item) => Task.CompletedTask;

                           public static void Main()
                           {
                               Task LocalHandler(Item item) => Task.CompletedTask;
                               var app = LambdaApplication.CreateBuilder().Build();
                               app.MapEventHandler(
                                   split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                                   handle: {{handler}});
                           }
                       }

                       internal sealed class Batch
                       {
                           public Item[] Items => [];
                       }

                       internal sealed class Item;
                       """;

        RunGenerator(source).Diagnostics.Should().BeEmpty();
    }

    [Theory]
    [InlineData("object")]
    [InlineData("BaseItem")]
    [InlineData("IItem")]
    public void RecognizesTypedDelegateWhoseMethodAcceptsABaseItemType(string parameterType)
    {
        var (driver, compilation) = GeneratorTestHelpers.GenerateFromSource(
            $$"""
              using System;
              using System.Threading.Tasks;
              using MinimalLambda;
              using MinimalLambda.Builder;

              var app = LambdaApplication.CreateBuilder().Build();
              Func<Item, Task> handle = Handle;
              app.MapEventHandler<Batch, Item>(
                  split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                  handle: handle);

              static Task Handle({{parameterType}} item) => Task.CompletedTask;

              internal sealed class Batch
              {
                  public Item[] Items => [];
              }

              internal interface IItem;
              internal class BaseItem;
              internal sealed class Item : BaseItem, IItem;
              """);

        var result = driver.GetRunResult();
        result.Diagnostics.Should().BeEmpty();
        result
            .GeneratedTrees
            .Should()
            .ContainSingle(tree =>
                tree.FilePath.EndsWith(
                    "MinimalLambda.EventHandlers.g.cs",
                    StringComparison.Ordinal));
        CompileWithGenerated(compilation, result)
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should()
            .BeEmpty();
    }

    [Fact]
    public void RecognizesTypedDelegateLocalAndExplicitGenericArguments()
    {
        var result = RunGenerator(
            """
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();

            Func<Item, Probe, Task<int>> handle = Handle;
            app.MapEventHandler(
                split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                handle: handle,
                createResponse: (IEnumerable<int> results, ILambdaInvocationContext _) => new Response());

            app.MapEventHandler<Batch, Item, int, Response, Probe>(
                (Batch batch, ILambdaInvocationContext _) => batch.Items,
                Handle);

            static Task<int> Handle(Item item, Probe probe) => Task.FromResult(probe.Value);

            internal sealed class Batch
            {
                public Item[] Items => [];
            }

            internal sealed class Item;

            internal sealed class Probe
            {
                public int Value => 1;
            }

            internal sealed class Response;
            """);

        result.Diagnostics.Should().BeEmpty();
        result
            .GeneratedTrees
            .Should()
            .ContainSingle(tree => tree.FilePath.EndsWith(
                "MinimalLambda.EventHandlers.g.cs",
                StringComparison.Ordinal));
    }

    [Fact]
    public void ReportsCompileErrorForUntypedMethodGroupWithUnknownDependency()
    {
        var errors = CompileErrors(
            """
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapEventHandler(
                split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                handle: Handle);

            static Task Handle(Item item, Probe probe) => Task.CompletedTask;

            internal sealed class Batch
            {
                public Item[] Items => [];
            }

            internal sealed class Item;

            internal sealed class Probe;
            """);

        // Method groups cannot supply the dependency input type. The compiler reports the failed
        // method-group conversion (CS1503) instead of the bare inference failure (CS0411) once the
        // full overload set is visible, so both are accepted here.
        errors.Should().Contain(id => id == "CS0411" || id == "CS1503");
    }

    [Fact]
    public void RejectsConcreteTaskResultWhereAUnionResultIsRequired()
    {
        var errors = CompileErrors(
            """
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapEventHandler(
                split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                handle: HandleSuccess,
                createResponse: (IEnumerable<Outcome> results, ILambdaInvocationContext _) => new Response());

            static Task<Success> HandleSuccess(Item item) => Task.FromResult(new Success());

            internal sealed class Batch
            {
                public Item[] Items => [];
            }

            internal sealed class Item;

            internal abstract class Outcome;

            internal sealed class Success : Outcome;

            internal sealed class Retry : Outcome;

            internal sealed class Response;
            """);

        errors.Should().NotBeEmpty();
    }

    [Fact]
    public void RejectsAsyncVoidHandlerAtCompileTime()
    {
        var errors = CompileErrors(
            """
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapEventHandler(
                split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                handle: Handle);

            static async void Handle(Item item) => await Task.CompletedTask;

            internal sealed class Batch
            {
                public Item[] Items => [];
            }

            internal sealed class Item;
            """);

        errors.Should().NotBeEmpty();
    }

    [Fact]
    public void RecognizesPositionalAndReorderedArguments()
    {
        var result = RunGenerator(
            """
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();

            app.MapEventHandler(
                (Batch batch, ILambdaInvocationContext _) => batch.Items,
                Handle,
                (IEnumerable<Item> items, ILambdaInvocationContext _) => new Response());

            app.MapEventHandler(
                createResponse: (IEnumerable<Item> items, ILambdaInvocationContext _) => new Response(),
                handle: Handle,
                split: (Batch batch, ILambdaInvocationContext _) => batch.Items);

            static Task Handle(Item item) => Task.CompletedTask;

            internal sealed class Batch
            {
                public Item[] Items => [];
            }

            internal sealed class Item;

            internal sealed class Response;
            """);

        result.Diagnostics.Should().BeEmpty();
        var generated = result
            .GeneratedTrees
            .Should()
            .ContainSingle(tree => tree.FilePath.EndsWith(
                "MinimalLambda.EventHandlers.g.cs",
                StringComparison.Ordinal))
            .Subject
            .ToString();
        generated.Split("MapEventHandlerInterceptor").Length.Should().Be(3);
    }

    [Fact]
    public void ReportsInvalidKeyedServiceKeyOnHandlerParameter()
    {
        var result = RunGenerator(
            """
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapEventHandler(
                split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                handle: (Item _, [FromKeyedServices(new[] { "a", "b" })] Service _) => Task.CompletedTask);

            internal sealed class Batch
            {
                public Item[] Items => [];
            }

            internal sealed class Item;

            internal sealed class Service;
            """);

        result.Diagnostics.Should().ContainSingle().Which.Id.Should().Be("LH0003");
    }

    [Fact]
    public void IgnoresSameNamedMapEventHandlerFromConsumerAssembly()
    {
        var result = RunGenerator(
            """
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            namespace MinimalLambda.Builder
            {
                internal static class MapEventHandlerLambdaApplicationExtensions
                {
                    extension(ILambdaInvocationBuilder builder)
                    {
                        public void MapEventHandler(
                            System.Func<Batch, ILambdaInvocationContext, System.Collections.Generic.IEnumerable<Item>> split,
                            System.Func<Item, Task> handle) { }
                    }
                }
            }

            internal static class Program
            {
                public static void Main()
                {
                    var app = LambdaApplication.CreateBuilder().Build();
                    app.MapEventHandler(
                        split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                        handle: (Item item) => Task.CompletedTask);
                }
            }

            internal sealed class Batch
            {
                public Item[] Items => [];
            }

            internal sealed class Item;
            """);

        result.Diagnostics.Should().BeEmpty();
        result.GeneratedTrees.Should().BeEmpty();
    }

    private static GeneratorDriverRunResult RunGenerator(string source)
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(source);
        return driver.GetRunResult();
    }

    private static IReadOnlyList<string> CompileErrors(string source)
    {
        var (_, compilation) = GeneratorTestHelpers.GenerateFromSource(source);
        return compilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.Id)
            .Distinct()
            .ToList();
    }

    private static Compilation CompileWithGenerated(
        Compilation originalCompilation,
        GeneratorDriverRunResult result)
    {
        var parseOptions = originalCompilation.SyntaxTrees.First().Options;
        var generatedTrees = result
            .GeneratedTrees
            .Select(tree => CSharpSyntaxTree.ParseText(
                tree.GetText(),
                (CSharpParseOptions)parseOptions,
                tree.FilePath))
            .ToArray();
        return originalCompilation.AddSyntaxTrees(generatedTrees);
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }

    private static string BuildAllOverloadsSource()
    {
        var builder = new StringBuilder();
        builder.AppendLine(
            """
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            internal sealed class Batch
            {
                public Item[] Items => [];
            }

            internal sealed class Item
            {
                public int Id { get; set; }
            }

            internal sealed class Response;
            """);
        for (var index = 0; index < 10; index++)
            builder.AppendLine($"internal sealed class Probe{index};");

        builder.AppendLine("internal static class Program");
        builder.AppendLine("{");
        builder.AppendLine("    public static void Main()");
        builder.AppendLine("    {");
        builder.AppendLine("        var app = LambdaApplication.CreateBuilder().Build();");

        for (var arity = 0; arity <= 10; arity++)
        {
            var parameters = BuildProbeParameters(arity);

            builder.AppendLine(
                $$"""
                          app.MapEventHandler(
                              split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                              handle: (Item item{{parameters}}) => Task.CompletedTask);
                  """);
            builder.AppendLine(
                $$"""
                          app.MapEventHandler(
                              split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                              handle: (Item item{{parameters}}) => Task.CompletedTask,
                              createResponse: (IEnumerable<Item> items, ILambdaInvocationContext _) => new Response());
                  """);
            builder.AppendLine(
                $$"""
                          app.MapEventHandler(
                              split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                              handle: (Item item{{parameters}}) => Task.CompletedTask,
                              onError: (Item item, Exception exception, IEventProcessingContext processing) => item.Id,
                              createResponse: (IEnumerable<int> failures, ILambdaInvocationContext _) => new Response());
                  """);
            builder.AppendLine(
                $$"""
                          app.MapEventHandler(
                              split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                              handle: (Item item{{parameters}}) => Task.FromResult(item.Id),
                              createResponse: (IEnumerable<int> results, ILambdaInvocationContext _) => new Response());
                  """);
            builder.AppendLine(
                $$"""
                          app.MapEventHandler(
                              split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                              handle: (Item item{{parameters}}) => Task.FromResult(item.Id),
                              onError: (Item item, Exception exception, IEventProcessingContext processing) => item.Id,
                              createResponse: (IEnumerable<int> results, ILambdaInvocationContext _) => new Response());
                  """);
        }

        builder.AppendLine("    }");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string BuildProbeParameters(int arity)
    {
        var builder = new StringBuilder();
        for (var index = 0; index < arity; index++)
            builder.Append($", Probe{index} probe{index}");
        return builder.ToString();
    }
}
