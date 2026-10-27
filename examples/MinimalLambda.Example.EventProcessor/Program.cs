using Amazon.Lambda.S3Events;
using Microsoft.Extensions.Hosting;
using MinimalLambda;
using MinimalLambda.Builder;
using MinimalLambda.Envelopes.Sqs;

var builder = LambdaApplication.CreateBuilder();

var lambda = builder.Build();

// No per-item result or Lambda response.
lambda.MapEventHandler(
    (S3Event input, ILambdaInvocationContext _) => input.Records,
    (S3Event.S3EventNotificationRecord item) => Task.CompletedTask);

// One injected dependency. The splitter's TItem is the handler's first parameter.
lambda.MapEventHandler(
    (S3Event input, ILambdaInvocationContext _) => input.Records,
    (S3Event.S3EventNotificationRecord item, MyService service) =>
    {
        service.DoStuff(item.S3.Object.Key);
        return Task.CompletedTask;
    });

// Two injected dependencies. The item remains the first handler parameter.
lambda.MapEventHandler(
    (S3Event input, ILambdaInvocationContext _) => input.Records,
    EventHandlers.HandleS3RecordAsync);

// A per-item result and a typed batch response.
lambda.MapEventHandler(
    (SqsEnvelope<MyEvent> input, ILambdaInvocationContext _) => input.Records,
    (SqsEnvelope<MyEvent>.SqsMessageEnvelope item, MyService service) =>
    {
        service.DoStuff(item.BodyContent?.Message ?? string.Empty);
        return Task.FromResult(EventOutcome.Success);
    },
    (result, _)
        => new ExampleResponse(
            result
                .Items
                .Where(item => item.Result == EventOutcome.Retry)
                .Select(item => item.Item.MessageId)
                .ToList()));

// The error interceptor returns the same TItemResult as the item handler.
lambda.MapEventHandler(
    (SqsEnvelope<MyEvent> input, ILambdaInvocationContext _) => input.Records,
    (SqsEnvelope<MyEvent>.SqsMessageEnvelope item, MyService service) =>
    {
        service.DoStuff(item.BodyContent?.Message ?? string.Empty);
        return Task.FromResult(EventOutcome.Success);
    },
    ( _,  _,  _)
        => EventOutcome.Retry,
    (result, _)
        => new ExampleResponse(
            result
                .Items
                .Where(item => item.Result == EventOutcome.Retry)
                .Select(item => item.Item.MessageId)
                .ToList()));

// Start the Lambda runtime loop.
await lambda.RunAsync();

public record MyEvent(string Message);

public enum EventOutcome
{
    Success,
    Retry,
}

public sealed record EventItemResult<TItem, TItemResult>(TItem Item, TItemResult Result);

public sealed class EventBatchResult<TItem, TItemResult>
{
    public IReadOnlyList<EventItemResult<TItem, TItemResult>> Items { get; } = [];
}

public sealed record ExampleResponse(IReadOnlyList<string?> RetryMessageIds);

// Required so the generated test project can reference this top-level Program.
public partial class Program;

public static class Extension
{
    extension(LambdaApplication lambda)
    {
        public LambdaApplication MapEventHandler<TEvent, TItem>(
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, Task> handle,
            EventHandlerOptions? options = null)
            => lambda;

        public LambdaApplication MapEventHandler<TEvent, TItem, TDependency>(
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TDependency, Task> handle,
            EventHandlerOptions? options = null)
            => lambda;

        public LambdaApplication MapEventHandler<TEvent, TItem, TDependency1, TDependency2>(
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TDependency1, TDependency2, Task> handle,
            EventHandlerOptions? options = null)
            => lambda;

        public LambdaApplication
            MapEventHandler<TEvent, TItem, TItemResult, TResponse, TDependency>(
                Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
                Func<TItem, TDependency, Task<TItemResult>> handle,
                Func<EventBatchResult<TItem, TItemResult>, ILambdaInvocationContext, TResponse>
                    createResponse,
                EventHandlerOptions? options = null)
            => lambda;

        public LambdaApplication
            MapEventHandler<TEvent, TItem, TItemResult, TResponse, TDependency>(
                Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
                Func<TItem, TDependency, Task<TItemResult>> handle,
                Func<TItem, Exception, ILambdaInvocationContext, TItemResult> onError,
                Func<EventBatchResult<TItem, TItemResult>, ILambdaInvocationContext, TResponse>
                    createResponse,
                EventHandlerOptions? options = null)
            => lambda;
    }
}

public sealed class EventHandlerOptions
{
    public int MaxConcurrentExecutions { get; init; } = 10;
}

public class MyService
{
    public void DoStuff(string input) { }
}

public class MyOtherService
{
    public void DoStuff(string input) { }
}

public static class EventHandlers
{
    public static Task HandleS3RecordAsync(
        S3Event.S3EventNotificationRecord item,
        MyService service,
        MyOtherService otherService)
    {
        service.DoStuff(item.S3.Object.Key);
        otherService.DoStuff(item.EventName);
        return Task.CompletedTask;
    }
}
