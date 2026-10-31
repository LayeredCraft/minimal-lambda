using AwesomeAssertions;

namespace MinimalLambda.SourceGenerators.UnitTests;

public sealed class EventHandlerCallbackShapeTests
{
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
