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
    public void ReportsUnsupportedSignatureForRefParameter()
    {
        var result = RunGenerator(
            """
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapEventHandler(
                split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                handle: (Item item, ref int count) => count++;

            internal sealed class Batch
            {
                public Item[] Items => [];
            }

            internal sealed class Item;
            """);

        result.Diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == "LH0008");
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

        result.Diagnostics.Should().OnlyContain(diagnostic => diagnostic.Id == "LH0008");
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
