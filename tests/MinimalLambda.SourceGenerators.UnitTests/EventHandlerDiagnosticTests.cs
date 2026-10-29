using AwesomeAssertions;
using Microsoft.CodeAnalysis;

namespace MinimalLambda.SourceGenerators.UnitTests;

public class EventHandlerDiagnosticTests
{
    [Fact]
    public void ReportsUnsupportedSignatureForInaccessibleItemType()
    {
        var result = RunGenerator(
            """
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            internal static class Program
            {
                private sealed class Item;

                public static void Main()
                {
                    var app = LambdaApplication.CreateBuilder().Build();
                    app.MapEventHandler(
                        split: (Batch batch, ILambdaInvocationContext _) => GetItems(),
                        handle: (Item item) => Task.CompletedTask);
                }

                private static Item[] GetItems() => [];
            }

            internal sealed class Batch;
            """);

        result
            .Diagnostics
            .Should()
            .NotBeEmpty()
            .And
            .OnlyContain(diagnostic =>
                diagnostic.Id == "LH0008"
                && diagnostic.Severity == DiagnosticSeverity.Error
                && diagnostic.Location.GetLineSpan().Path == "InputFile.cs");
        result
            .GeneratedTrees
            .Should()
            .NotContain(tree => tree.FilePath.EndsWith(
                "MinimalLambda.EventHandlers.g.cs",
                StringComparison.Ordinal));
    }

    [Fact]
    public void ReportsCompilerErrorForRefLambdaParameter()
    {
        var (_, compilation) = GeneratorTestHelpers.GenerateFromSource(
            """
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapEventHandler<Batch, Item, int>(
                split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                handle: (Item item, ref int count) => Task.CompletedTask);

            internal sealed class Batch
            {
                public Item[] Items => [];
            }

            internal sealed class Item;
            """);

        compilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should()
            .Contain(diagnostic => diagnostic.Id == "CS1677");
    }

    [Fact]
    public void ReportsUnsupportedSignatureForStreamEventAndAwaitableResponse()
    {
        var result = RunGenerator(
            """
            using System.Collections.Generic;
            using System.IO;
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapEventHandler(
                split: (Stream stream, ILambdaInvocationContext _) => System.Linq.Enumerable.Empty<Item>(),
                handle: (Item item) => Task.CompletedTask,
                createResponse: (IEnumerable<Item> items, ILambdaInvocationContext _) => Task.FromResult(new Response()));

            internal sealed class Item;

            internal sealed class Response;
            """);

        result
            .Diagnostics
            .Should()
            .NotBeEmpty()
            .And
            .OnlyContain(diagnostic => diagnostic.Id == "LH0008");
    }

    [Fact]
    public void ReportsUnsupportedBindingForEventAttributeOnItemParameter()
    {
        var result = RunGenerator(
            """
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapEventHandler(
                split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                handle: ([FromEvent] Item item) => Task.CompletedTask);

            internal sealed class Batch
            {
                public Item[] Items => [];
            }

            internal sealed class Item;
            """);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("LH0009");
        diagnostic.Location.GetLineSpan().Path.Should().Be("InputFile.cs");
    }

    [Fact]
    public void ReportsUnsupportedBindingForDuplicateProcessingContext()
    {
        var result = RunGenerator(
            """
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapEventHandler(
                split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                handle: (Item item, IEventProcessingContext first, IEventProcessingContext second) =>
                    Task.CompletedTask);

            internal sealed class Batch
            {
                public Item[] Items => [];
            }

            internal sealed class Item;
            """);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("LH0009");
        diagnostic.Location.GetLineSpan().Path.Should().Be("InputFile.cs");
    }

    [Fact]
    public Task SnapshotsDuplicateCancellationTokensAndDiscardedTaskResult()
    {
        const string source = """
                              using System.Threading;
                              using System.Threading.Tasks;
                              using MinimalLambda;
                              using MinimalLambda.Builder;

                              var app = LambdaApplication.CreateBuilder().Build();
                              app.MapEventHandler<Batch, Item, CancellationToken, CancellationToken>(
                                  split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                                  handle: Handle);

                              static Task<int> Handle(Item item, CancellationToken first, CancellationToken second) =>
                                  Task.FromResult(item.Id);

                              internal sealed class Batch
                              {
                                  public Item[] Items => [];
                              }

                              internal sealed class Item
                              {
                                  public int Id { get; set; }
                              }
                              """;
        var (driver, compilation) = GeneratorTestHelpers.GenerateFromSource(source);

        compilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should()
            .BeEmpty("the diagnostic snapshot should use compiler-valid source");

        var diagnostics = driver
            .GetRunResult()
            .Diagnostics
            .Select(diagnostic =>
            {
                var span = diagnostic.Location.GetLineSpan();
                return new
                {
                    diagnostic.Id,
                    Severity = diagnostic.Severity.ToString(),
                    Message = diagnostic.GetMessage(),
                    FilePath = span.Path,
                    StartLine = span.StartLinePosition.Line + 1,
                    StartColumn = span.StartLinePosition.Character + 1,
                    EndLine = span.EndLinePosition.Line + 1,
                    EndColumn = span.EndLinePosition.Character + 1,
                };
            })
            .OrderBy(diagnostic => diagnostic.Id, StringComparer.Ordinal)
            .ToArray();

        diagnostics.Select(diagnostic => diagnostic.Id).Should().Equal("LH0009", "LH0011");
        diagnostics
            .Select(diagnostic => diagnostic.Severity)
            .Should()
            .OnlyContain(severity => severity == DiagnosticSeverity.Error.ToString());
        diagnostics
            .Select(diagnostic => diagnostic.FilePath)
            .Should()
            .OnlyContain(path => path == "InputFile.cs");

        return Verifier.Verify(diagnostics).UseDirectory("Snapshots").DisableDiff();
    }

    [Fact]
    public void ReportsUnresolvedHandlerForReassignedDelegateLocal()
    {
        var result = RunGenerator(
            """
            using System;
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            Func<Item, Task> handle = Handle;
            handle = Handle;
            app.MapEventHandler(
                split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                handle: handle);

            static Task Handle(Item item) => Task.CompletedTask;

            internal sealed class Batch
            {
                public Item[] Items => [];
            }

            internal sealed class Item;
            """);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("LH0010");
        diagnostic.Location.GetLineSpan().Path.Should().Be("InputFile.cs");
    }

    [Theory]
    [InlineData("(handle, replacement) = (replacement, handle);")]
    [InlineData("(unused, (handle, replacement)) = (0, (replacement, handle));")]
    public void ReportsUnresolvedHandlerForDeconstructionReassignment(string assignment)
    {
        var (driver, compilation) = GeneratorTestHelpers.GenerateFromSource(
            $$"""
              using System;
              using System.Threading.Tasks;
              using Microsoft.Extensions.DependencyInjection;
              using MinimalLambda;
              using MinimalLambda.Builder;

              var app = LambdaApplication.CreateBuilder().Build();
              Func<Item, Probe, Task> handle = First;
              Func<Item, Probe, Task> replacement = Second;
              int unused;
              {{assignment}}
              app.MapEventHandler<Batch, Item, Probe>(
                  split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                  handle: handle);

              static Task First(Item item, [FromKeyedServices("blue")] Probe probe) => Task.CompletedTask;
              static Task Second(Item item, Probe probe) => Task.CompletedTask;

              internal sealed class Batch
              {
                  public Item[] Items => [];
              }

              internal sealed class Item;
              internal sealed class Probe;
              """);

        compilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Should()
            .BeEmpty();
        var result = driver.GetRunResult();
        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("LH0010");
        diagnostic.Location.GetLineSpan().Path.Should().Be("InputFile.cs");
        result
            .GeneratedTrees
            .Should()
            .NotContain(tree =>
                tree.FilePath.EndsWith(
                    "MinimalLambda.EventHandlers.g.cs",
                    StringComparison.Ordinal));
    }

    [Fact]
    public void ReportsDiscardedResultWhenMethodGroupReturnsTaskOfTForNoResultOverload()
    {
        var result = RunGenerator(
            """
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapEventHandler(
                split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                handle: Handle);

            static Task<int> Handle(Item item) => Task.FromResult(item.Id);

            internal sealed class Batch
            {
                public Item[] Items => [];
            }

            internal sealed class Item
            {
                public int Id { get; set; }
            }
            """);

        var diagnostic = result.Diagnostics.Should().ContainSingle().Subject;
        diagnostic.Id.Should().Be("LH0011");
        diagnostic.Location.GetLineSpan().Path.Should().Be("InputFile.cs");
    }

    [Fact]
    public void InvalidRegistrationDoesNotSuppressValidRegistration()
    {
        var result = RunGenerator(
            """
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapEventHandler(
                split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                handle: ([FromEvent] Item item) => Task.CompletedTask);
            app.MapEventHandler(
                split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                handle: (Item item) => Task.CompletedTask);

            internal sealed class Batch
            {
                public Item[] Items => [];
            }

            internal sealed class Item;
            """);

        result.Diagnostics.Select(diagnostic => diagnostic.Id).Should().Equal("LH0009");
        var tree = result
            .GeneratedTrees
            .Should()
            .ContainSingle(generated => generated.FilePath.EndsWith(
                "MinimalLambda.EventHandlers.g.cs",
                StringComparison.Ordinal))
            .Subject;
        CountOccurrences(tree.ToString(), "MapEventHandlerInterceptor").Should().Be(1);
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

    private static GeneratorDriverRunResult RunGenerator(string source)
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(source);
        return driver.GetRunResult();
    }
}
