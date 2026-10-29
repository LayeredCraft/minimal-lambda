using AwesomeAssertions;
using Microsoft.CodeAnalysis;

namespace MinimalLambda.SourceGenerators.UnitTests;

public class EventHandlerEmitterTests
{
    [Fact]
    public Task EmitsRepresentativeAdapterForEveryFamily() =>
        GeneratorTestHelpers.Verify(
            """
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();

            app.MapEventHandler(
                split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                handle: (Item item) => Task.CompletedTask);

            app.MapEventHandler(
                split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                handle: (Item item) => Task.CompletedTask,
                createResponse: (IEnumerable<Item> items, ILambdaInvocationContext _) => new Response());

            app.MapEventHandler(
                split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                handle: (Item item) => Task.CompletedTask,
                onError: (Item item, Exception exception, IEventProcessingContext _) => item.Id,
                createResponse: (IEnumerable<int> failures, ILambdaInvocationContext _) => new Response());

            app.MapEventHandler(
                split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                handle: (Item item) => Task.FromResult(item.Id),
                createResponse: (IEnumerable<int> results, ILambdaInvocationContext _) => new Response());

            app.MapEventHandler(
                split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                handle: (Item item) => Task.FromResult(item.Id),
                onError: (Item item, Exception exception, IEventProcessingContext _) => item.Id,
                createResponse: (IEnumerable<int> results, ILambdaInvocationContext _) => new Response());

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

    [Fact]
    public Task EmitsContextTokenAndKeyedServiceBindings() =>
        GeneratorTestHelpers.Verify(
            """
            using System;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;
            using Amazon.Lambda.Core;
            using Microsoft.Extensions.DependencyInjection;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();

            app.MapEventHandler(
                split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                handle: async Task<int> (
                    Item item,
                    ILambdaContext lambdaContext,
                    IEventProcessingContext processing,
                    CancellationToken token,
                    [FromServices] Service service,
                    [FromKeyedServices("primary")] Service keyed) =>
                {
                    await Task.Yield();
                    _ = lambdaContext.AwsRequestId;
                    _ = processing.CancellationToken;
                    token.ThrowIfCancellationRequested();
                    return item.Id + service.Value + keyed.Value;
                },
                onError: (item, _, _) => item.Id,
                createResponse: (IEnumerable<int> results, ILambdaInvocationContext _) => new Response());

            internal sealed class Batch
            {
                public Item[] Items => [];
            }

            internal sealed class Item
            {
                public int Id { get; set; }
            }

            internal sealed class Service
            {
                public int Value => 1;
            }

            internal sealed class Response;
            """);

    [Fact]
    public Task EmitsTypedDelegateAndExplicitGenericMethodGroupBindings() =>
        GeneratorTestHelpers.Verify(
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
                Handle,
                (IEnumerable<int> results, ILambdaInvocationContext _) => new Response());

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

    [Fact]
    public Task EmitsNullableAndOptionalServiceResolution() =>
        GeneratorTestHelpers.Verify(
            """
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using Microsoft.Extensions.DependencyInjection;
            using MinimalLambda;
            using MinimalLambda.Builder;

            var app = LambdaApplication.CreateBuilder().Build();
            app.MapEventHandler(
                split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                handle: (
                    Item item,
                    NullableService? nullableService,
                    [FromServices] string? optionalService = null) => Task.CompletedTask);

            internal sealed class Batch
            {
                public Item[] Items => [];
            }

            internal sealed class Item;

            internal sealed class NullableService;
            """);

    [Fact]
    public void CoexistsWithInvocationLifecycleAndMiddlewareGeneratedFiles()
    {
        const string source = """
                              using System.Collections.Generic;
                              using System.Threading.Tasks;
                              using MinimalLambda;
                              using MinimalLambda.Builder;

                              var app = LambdaApplication.CreateBuilder().Build();
                              app.OnInit(() => Task.CompletedTask);
                              app.MapHandler(() => "hello");
                              app.MapEventHandler(
                                  split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                                  handle: (Item item) => Task.CompletedTask,
                                  createResponse: (IEnumerable<Item> items, ILambdaInvocationContext _) => new Response());

                              internal sealed class Batch
                              {
                                  public Item[] Items => [];
                              }

                              internal sealed class Item;

                              internal sealed class Response;
                              """;

        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(source);
        var result = driver.GetRunResult();

        result.Diagnostics.Should().BeEmpty();
        result
            .GeneratedTrees
            .Select(tree => tree.FilePath.Split('/').Last())
            .Should()
            .Contain(
            [
                "MinimalLambda.InvocationHandlers.g.cs",
                "MinimalLambda.EventHandlers.g.cs",
                "MinimalLambda.OnInitHandlers.g.cs",
            ]);
    }

    [Fact]
    public void EmitsSingleDeterministicFileForMultipleSites()
    {
        const string source = """
                              using System.Collections.Generic;
                              using System.Threading.Tasks;
                              using MinimalLambda;
                              using MinimalLambda.Builder;

                              var app = LambdaApplication.CreateBuilder().Build();
                              app.MapEventHandler(
                                  split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                                  handle: (Item item) => Task.CompletedTask);
                              app.MapEventHandler(
                                  split: (Batch batch, ILambdaInvocationContext _) => batch.Items,
                                  handle: (Item item) => Task.CompletedTask,
                                  createResponse: (IEnumerable<Item> items, ILambdaInvocationContext _) => new Response());

                              internal sealed class Batch
                              {
                                  public Item[] Items => [];
                              }

                              internal sealed class Item;

                              internal sealed class Response;
                              """;

        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(source);
        var result = driver.GetRunResult();

        result.Diagnostics.Should().BeEmpty();
        result
            .GeneratedTrees
            .Should()
            .ContainSingle(tree => tree.FilePath.EndsWith(
                "MinimalLambda.EventHandlers.g.cs",
                StringComparison.Ordinal));

        var generated = result.GeneratedTrees.Single().ToString();
        generated
            .IndexOf("MapEventHandlerInterceptor0", StringComparison.Ordinal)
            .Should()
            .BeLessThan(generated.IndexOf("MapEventHandlerInterceptor1", StringComparison.Ordinal));
    }
}
