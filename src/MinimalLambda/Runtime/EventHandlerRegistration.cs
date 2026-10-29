using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;

namespace MinimalLambda.Runtime;

/// <summary>Registers generated event-handler adapters with a Lambda invocation builder.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class EventHandlerRegistration
{
    /// <summary>Registers an event handler that produces no response contributions.</summary>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The item type produced by <paramref name="split" />.</typeparam>
    /// <param name="application">The invocation builder.</param>
    /// <param name="split">Splits the event into source items.</param>
    /// <param name="handle">The original handler delegate, used to validate the registration.</param>
    /// <param name="bind">Resolves item-handler arguments and returns the handler.</param>
    /// <returns>The same invocation builder.</returns>
    public static ILambdaInvocationBuilder Register<TEvent, TItem>(
        ILambdaInvocationBuilder application,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Delegate handle,
        Func<TItem, IServiceProvider, IEventProcessingContext, Func<Task>> bind)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(split);
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(bind);

        var runtime = new EventProcessorRuntime(application.Services);
        application.Handle(InvokeAsync);
        EnsureEventFeatureProvider<TEvent>(application);
        return application;

        Task InvokeAsync(ILambdaInvocationContext context) =>
            runtime.RunAsync<TEvent, TItem, object>(
                context,
                split,
                bind,
                selectSuccess: null,
                onError: null,
                complete: null);
    }

    /// <summary>Registers an event handler that contributes each successful source item.</summary>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The item type produced by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <param name="application">The invocation builder.</param>
    /// <param name="split">Splits the event into source items.</param>
    /// <param name="handle">The original handler delegate, used to validate the registration.</param>
    /// <param name="bind">Resolves item-handler arguments and returns the handler.</param>
    /// <param name="createResponse">Builds a response from successful source items.</param>
    /// <returns>The same invocation builder.</returns>
    public static ILambdaInvocationBuilder Register<TEvent, TItem, TResponse>(
        ILambdaInvocationBuilder application,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Delegate handle,
        Func<TItem, IServiceProvider, IEventProcessingContext, Func<Task>> bind,
        Func<IEnumerable<TItem>, ILambdaInvocationContext, TResponse> createResponse)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(split);
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(bind);
        ArgumentNullException.ThrowIfNull(createResponse);

        var runtime = new EventProcessorRuntime(application.Services);
        application.Handle(InvokeAsync);
        EnsureEventFeatureProvider<TEvent>(application);
        EnsureResponseFeatureProvider<TResponse>(application);
        return application;

        Task InvokeAsync(ILambdaInvocationContext context) =>
            runtime.RunAsync<TEvent, TItem, TItem>(
                context,
                split,
                bind,
                static (item, _) => item,
                onError: null,
                values => SetResponse(context, values, createResponse));
    }

    /// <summary>Registers an event handler that contributes mapped failures.</summary>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The item type produced by <paramref name="split" />.</typeparam>
    /// <typeparam name="TFailure">The mapped failure type.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <param name="application">The invocation builder.</param>
    /// <param name="split">Splits the event into source items.</param>
    /// <param name="handle">The original handler delegate, used to validate the registration.</param>
    /// <param name="bind">Resolves item-handler arguments and returns the handler.</param>
    /// <param name="onError">Maps an item-handler failure to a response contribution.</param>
    /// <param name="createResponse">Builds a response from mapped failures.</param>
    /// <returns>The same invocation builder.</returns>
    public static ILambdaInvocationBuilder Register<TEvent, TItem, TFailure, TResponse>(
        ILambdaInvocationBuilder application,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Delegate handle,
        Func<TItem, IServiceProvider, IEventProcessingContext, Func<Task>> bind,
        Func<TItem, Exception, IEventProcessingContext, TFailure> onError,
        Func<IEnumerable<TFailure>, ILambdaInvocationContext, TResponse> createResponse)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(split);
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(bind);
        ArgumentNullException.ThrowIfNull(onError);
        ArgumentNullException.ThrowIfNull(createResponse);

        var runtime = new EventProcessorRuntime(application.Services);
        application.Handle(InvokeAsync);
        EnsureEventFeatureProvider<TEvent>(application);
        EnsureResponseFeatureProvider<TResponse>(application);
        return application;

        Task InvokeAsync(ILambdaInvocationContext context) =>
            runtime.RunAsync<TEvent, TItem, TFailure>(
                context,
                split,
                bind,
                selectSuccess: null,
                onError: onError,
                complete: values => SetResponse(context, values, createResponse));
    }

    /// <summary>Registers an event handler that contributes successful results.</summary>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The item type produced by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The handler result type.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <param name="application">The invocation builder.</param>
    /// <param name="split">Splits the event into source items.</param>
    /// <param name="handle">The original handler delegate, used to validate the registration.</param>
    /// <param name="bind">Resolves item-handler arguments and returns the handler.</param>
    /// <param name="createResponse">Builds a response from successful handler results.</param>
    /// <returns>The same invocation builder.</returns>
    public static ILambdaInvocationBuilder Register<TEvent, TItem, TResult, TResponse>(
        ILambdaInvocationBuilder application,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Delegate handle,
        Func<TItem, IServiceProvider, IEventProcessingContext, Func<Task<TResult>>> bind,
        Func<IEnumerable<TResult>, ILambdaInvocationContext, TResponse> createResponse)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(split);
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(bind);
        ArgumentNullException.ThrowIfNull(createResponse);

        var runtime = new EventProcessorRuntime(application.Services);
        application.Handle(InvokeAsync);
        EnsureEventFeatureProvider<TEvent>(application);
        EnsureResponseFeatureProvider<TResponse>(application);
        return application;

        Task InvokeAsync(ILambdaInvocationContext context) =>
            runtime.RunAsync<TEvent, TItem, TResult>(
                context,
                split,
                bind,
                static (_, task) => ((Task<TResult>)task).GetAwaiter().GetResult(),
                onError: null,
                values => SetResponse(context, values, createResponse));
    }

    /// <summary>Registers an event handler that contributes successful and mapped results.</summary>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The item type produced by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The handler result and mapped failure type.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <param name="application">The invocation builder.</param>
    /// <param name="split">Splits the event into source items.</param>
    /// <param name="handle">The original handler delegate, used to validate the registration.</param>
    /// <param name="bind">Resolves item-handler arguments and returns the handler.</param>
    /// <param name="onError">Maps an item-handler failure to a response contribution.</param>
    /// <param name="createResponse">Builds a response from successful and mapped results.</param>
    /// <returns>The same invocation builder.</returns>
    public static ILambdaInvocationBuilder Register<TEvent, TItem, TResult, TResponse>(
        ILambdaInvocationBuilder application,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Delegate handle,
        Func<TItem, IServiceProvider, IEventProcessingContext, Func<Task<TResult>>> bind,
        Func<TItem, Exception, IEventProcessingContext, TResult> onError,
        Func<IEnumerable<TResult>, ILambdaInvocationContext, TResponse> createResponse)
    {
        ArgumentNullException.ThrowIfNull(application);
        ArgumentNullException.ThrowIfNull(split);
        ArgumentNullException.ThrowIfNull(handle);
        ArgumentNullException.ThrowIfNull(bind);
        ArgumentNullException.ThrowIfNull(onError);
        ArgumentNullException.ThrowIfNull(createResponse);

        var runtime = new EventProcessorRuntime(application.Services);
        application.Handle(InvokeAsync);
        EnsureEventFeatureProvider<TEvent>(application);
        EnsureResponseFeatureProvider<TResponse>(application);
        return application;

        Task InvokeAsync(ILambdaInvocationContext context) =>
            runtime.RunAsync<TEvent, TItem, TResult>(
                context,
                split,
                bind,
                static (_, task) => ((Task<TResult>)task).GetAwaiter().GetResult(),
                onError: onError,
                complete: values => SetResponse(context, values, createResponse));
    }

    private static void EnsureEventFeatureProvider<TEvent>(ILambdaInvocationBuilder application)
    {
        if (!application.Properties.ContainsKey(LambdaInvocationBuilder.EventFeatureProviderKey))
            application.Properties[LambdaInvocationBuilder.EventFeatureProviderKey] =
                application
                    .Services
                    .GetRequiredService<IEventFeatureProviderFactory>()
                    .Create<TEvent>();
    }

    private static void EnsureResponseFeatureProvider<TResponse>(
        ILambdaInvocationBuilder application)
    {
        if (!application.Properties.ContainsKey(LambdaInvocationBuilder.ResponseFeatureProviderKey))
            application.Properties[LambdaInvocationBuilder.ResponseFeatureProviderKey] =
                application
                    .Services
                    .GetRequiredService<IResponseFeatureProviderFactory>()
                    .Create<TResponse>();
    }

    private static void SetResponse<TValue, TResponse>(
        ILambdaInvocationContext context,
        IEnumerable<TValue> values,
        Func<IEnumerable<TValue>, ILambdaInvocationContext, TResponse> createResponse)
    {
        var response = createResponse(values, context);
        if (context.Features.Get<IResponseFeature>() is not IResponseFeature<TResponse>
            responseFeature)
            throw new InvalidOperationException(
                $"Response feature for type '{typeof(TResponse)}' is not available in the collection.");
        responseFeature.SetResponse(response);
    }
}
