using AwesomeAssertions;

namespace MinimalLambda.SourceGenerators.UnitTests;

public sealed class EventHandlerCallbackShapeTests
{
    [Theory]
    [InlineData("async (item, error, processing) => { await Task.Yield(); }")]
    [InlineData("AsyncError")]
    [InlineData("callback")]
    public void RejectsAsyncVoidErrorCallbacks(string callback)
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            $$"""
              using System;
              using System.Threading.Tasks;
              using MinimalLambda;
              using MinimalLambda.Builder;
              using MinimalLambda.EventProcessing;
              var app = LambdaApplication.CreateBuilder().Build();
              Action<string, Exception, IEventProcessingContext> callback = AsyncError;
              app.MapEventHandler(split: (string[] batch, ILambdaInvocationContext _) => batch,
                  handle: (string item) => Task.CompletedTask, onError: {{callback}});
              static async void AsyncError(string item, Exception error, IEventProcessingContext processing)
              {
                  await Task.Yield();
              }
              """);
        driver
            .GetRunResult()
            .Diagnostics
            .Should()
            .Contain(d => d.Id == "LH0008" && d.GetMessage().Contains("synchronous"));
    }

    [Fact]
    public void RejectsDiscardedHandlerResultsWithNoResponseErrorCallback()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            """
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.Builder;
            using MinimalLambda.EventProcessing;
            var app = LambdaApplication.CreateBuilder().Build();
            app.MapEventHandler(split: (string[] batch, ILambdaInvocationContext _) => batch,
                handle: Handle, onError: (item, error, processing) => { });
            static Task<int> Handle(string item) => Task.FromResult(1);
            """);
        driver.GetRunResult().Diagnostics.Should().Contain(d => d.Id == "LH0011");
    }

    [Theory]
    [InlineData(
        "(string item) => Task.CompletedTask",
        "async (item, error, processing) => { await Task.Yield(); return 1; }")]
    [InlineData(
        "(string item) => Task.FromResult(Task.FromResult(1))",
        "(item, error, processing) => Task.FromResult(1)")]
    [InlineData("(string item) => Task.FromResult<object>(new object())", "mapperDelegate")]
    public void RejectsAwaitableContributions(string handler, string mapper)
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            $$"""
              using System;
              using System.Threading.Tasks;
              using MinimalLambda;
              using MinimalLambda.EventProcessing;
              using MinimalLambda.Builder;
              using MinimalLambda.EventProcessing;
              var app = LambdaApplication.CreateBuilder().Build();
              Func<string, Exception, IEventProcessingContext, object> mapperDelegate = AsyncMapper;
              app.MapEventHandler(split: (string[] batch, ILambdaInvocationContext _) => batch,
                  handle: {{handler}}, onError: {{mapper}}, createResponse: (outcomes, _) => "response");
              static Task<int> AsyncMapper(string item, System.Exception error, IEventProcessingContext processing) => Task.FromResult(1);
              """);
        driver
            .GetRunResult()
            .Diagnostics
            .Should()
            .Contain(d => d.Id == "LH0008" && d.GetMessage().Contains("awaitable"));
    }

    [Fact]
    public void RejectsAsyncResponseHiddenByObjectDelegate()
    {
        var (driver, _) = GeneratorTestHelpers.GenerateFromSource(
            """
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using MinimalLambda;
            using MinimalLambda.EventProcessing;
            using MinimalLambda.Builder;
            using MinimalLambda.EventProcessing;
            var app = LambdaApplication.CreateBuilder().Build();
            Func<IReadOnlyList<EventItemOutcome<string, string>>, ILambdaInvocationContext, object> response = AsyncResponse;
            app.MapEventHandler(split: (string[] batch, ILambdaInvocationContext _) => batch,
                handle: (string _) => Task.CompletedTask, createResponse: response);
            static Task<string> AsyncResponse(IReadOnlyList<EventItemOutcome<string, string>> items, ILambdaInvocationContext context) => Task.FromResult("response");
            """);
        driver
            .GetRunResult()
            .Diagnostics
            .Should()
            .Contain(d => d.Id == "LH0008" && d.GetMessage().Contains("awaitable TResponse"));
    }
}
