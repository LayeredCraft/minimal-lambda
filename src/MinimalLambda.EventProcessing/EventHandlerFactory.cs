using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MinimalLambda.EventProcessing;

/// <summary>Constructs terminal delegates used by generated event mappings.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class EventHandlerFactory
{
    /// <summary>Creates an event terminal and validates its callbacks and effective options.</summary>
    /// <typeparam name="TEvent">The Event type.</typeparam>
    /// <typeparam name="TItem">The Item type.</typeparam>
    /// <param name="services">The mapping services.</param>
    /// <param name="split">The mapping split.</param>
    /// <param name="handle">The mapping handle.</param>
    /// <returns>A terminal invocation delegate.</returns>
    public static LambdaInvocationDelegate Create<TEvent, TItem>(
        IServiceProvider services,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, IServiceProvider, IEventProcessingContext, Task> handle)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(split);
        ArgumentNullException.ThrowIfNull(handle);
        var runtime = CreateRuntime(services);
        return context => runtime.RunAsync<TEvent, TItem, object>(
            context,
            split,
            (item, provider, processing) => handle(item, provider, processing),
            null,
            null,
            null);
    }

    /// <summary>Creates an event terminal and validates its callbacks and effective options.</summary>
    /// <typeparam name="TEvent">The Event type.</typeparam>
    /// <typeparam name="TItem">The Item type.</typeparam>
    /// <typeparam name="TResponse">The Response type.</typeparam>
    /// <param name="services">The mapping services.</param>
    /// <param name="split">The mapping split.</param>
    /// <param name="handle">The mapping handle.</param>
    /// <param name="createResponse">The mapping createResponse.</param>
    /// <returns>A terminal invocation delegate.</returns>
    public static LambdaInvocationDelegate Create<TEvent, TItem, TResponse>(
        IServiceProvider services,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, IServiceProvider, IEventProcessingContext, Task> handle,
        Func<IReadOnlyList<EventItemOutcome<TItem, TItem>>, ILambdaInvocationContext, TResponse>
            createResponse)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(split);
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(createResponse);
        var runtime = CreateRuntime(services);
        return context => runtime.RunAsync<TEvent, TItem, TItem>(
            context,
            split,
            (item, provider, processing) => handle(item, provider, processing),
            static (item, _) => item,
            null,
            values => SetResponse(context, createResponse(values, context)));
    }

    /// <summary>Creates an event terminal and validates its callbacks and effective options.</summary>
    /// <typeparam name="TEvent">The Event type.</typeparam>
    /// <typeparam name="TItem">The Item type.</typeparam>
    /// <typeparam name="TFailure">The Failure type.</typeparam>
    /// <typeparam name="TResponse">The Response type.</typeparam>
    /// <param name="services">The mapping services.</param>
    /// <param name="split">The mapping split.</param>
    /// <param name="handle">The mapping handle.</param>
    /// <param name="onError">The mapping onError.</param>
    /// <param name="createResponse">The mapping createResponse.</param>
    /// <returns>A terminal invocation delegate.</returns>
    public static LambdaInvocationDelegate Create<TEvent, TItem, TFailure, TResponse>(
        IServiceProvider services,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, IServiceProvider, IEventProcessingContext, Task> handle,
        Func<TItem, Exception, IEventProcessingContext, TFailure> onError,
        Func<IReadOnlyList<EventItemOutcome<TItem, TFailure>>, ILambdaInvocationContext, TResponse>
            createResponse)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(split);
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(onError);
        ArgumentNullException.ThrowIfNull(createResponse);
        var runtime = CreateRuntime(services);
        return context => runtime.RunAsync<TEvent, TItem, TFailure>(
            context,
            split,
            (item, provider, processing) => handle(item, provider, processing),
            null,
            onError,
            values => SetResponse(context, createResponse(values, context)));
    }

    /// <summary>Creates an event terminal and validates its callbacks and effective options.</summary>
    /// <typeparam name="TEvent">The Event type.</typeparam>
    /// <typeparam name="TItem">The Item type.</typeparam>
    /// <typeparam name="TResult">The Result type.</typeparam>
    /// <typeparam name="TResponse">The Response type.</typeparam>
    /// <param name="services">The mapping services.</param>
    /// <param name="split">The mapping split.</param>
    /// <param name="handle">The mapping handle.</param>
    /// <param name="createResponse">The mapping createResponse.</param>
    /// <returns>A terminal invocation delegate.</returns>
    public static LambdaInvocationDelegate Create<TEvent, TItem, TResult, TResponse>(
        IServiceProvider services,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, IServiceProvider, IEventProcessingContext, Task<TResult>> handle,
        Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext, TResponse>
            createResponse)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(split);
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(createResponse);
        var runtime = CreateRuntime(services);
        return context => runtime.RunAsync<TEvent, TItem, TResult>(
            context,
            split,
            (item, provider, processing) => handle(item, provider, processing),
            static (_, task) => ((Task<TResult>)task).GetAwaiter().GetResult(),
            null,
            values => SetResponse(context, createResponse(values, context)));
    }

    /// <summary>Creates an event terminal and validates its callbacks and effective options.</summary>
    /// <typeparam name="TEvent">The Event type.</typeparam>
    /// <typeparam name="TItem">The Item type.</typeparam>
    /// <typeparam name="TResult">The Result type.</typeparam>
    /// <typeparam name="TResponse">The Response type.</typeparam>
    /// <param name="services">The mapping services.</param>
    /// <param name="split">The mapping split.</param>
    /// <param name="handle">The mapping handle.</param>
    /// <param name="onError">The mapping onError.</param>
    /// <param name="createResponse">The mapping createResponse.</param>
    /// <returns>A terminal invocation delegate.</returns>
    public static LambdaInvocationDelegate Create<TEvent, TItem, TResult, TResponse>(
        IServiceProvider services,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, IServiceProvider, IEventProcessingContext, Task<TResult>> handle,
        Func<TItem, Exception, IEventProcessingContext, TResult> onError,
        Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext, TResponse>
            createResponse)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(split);
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(onError);
        ArgumentNullException.ThrowIfNull(createResponse);
        var runtime = CreateRuntime(services);
        return context => runtime.RunAsync<TEvent, TItem, TResult>(
            context,
            split,
            (item, provider, processing) => handle(item, provider, processing),
            static (_, task) => ((Task<TResult>)task).GetAwaiter().GetResult(),
            onError,
            values => SetResponse(context, createResponse(values, context)));
    }

    private static EventProcessorRuntime CreateRuntime(IServiceProvider services)
    {
        var configured = services.GetService<IOptions<EventHandlerOptions>>();
        var options = configured is null ? new EventHandlerOptions() : configured.Value;
        ArgumentNullException.ThrowIfNull(options);
        var validation = new EventHandlerOptionsValidator().Validate(
            Microsoft.Extensions.Options.Options.DefaultName,
            options);
        if (validation.Failed)
            throw new OptionsValidationException(
                Microsoft.Extensions.Options.Options.DefaultName,
                typeof(EventHandlerOptions),
                validation.Failures);
        return new EventProcessorRuntime(
            options.EnableConcurrency ? options.MaxConcurrentExecutions : 1,
            services.GetRequiredService<IServiceScopeFactory>(),
            services.GetService<ILoggerFactory>()?.CreateLogger("MinimalLambda.EventProcessing"));
    }

    private static void SetResponse<TResponse>(ILambdaInvocationContext context, TResponse response)
    {
        context.CancellationToken.ThrowIfCancellationRequested();
        if (context.Features.Get<IResponseFeature>() is not IResponseFeature<TResponse> feature)
            throw new InvalidOperationException(
                $"Response feature for type '{typeof(TResponse)}' is not available in the collection.");
        feature.SetResponse(response);
    }
}
