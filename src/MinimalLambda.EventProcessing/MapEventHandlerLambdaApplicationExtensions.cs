using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Amazon.Lambda.Core;

namespace MinimalLambda.Builder;

/// <summary>
///     Overloads for <see cref="ILambdaInvocationBuilder.Handle(LambdaInvocationDelegate)" />
///     that process a batch of source items with generated dependency injection and optional
///     response construction.
/// </summary>
/// <remarks>
///     <para>
///         Source generation creates the wiring code, using compile-time interceptors to replace
///         these calls. Each started item receives its own asynchronous dependency injection scope.
///         Item handlers are asynchronous and return <see cref="Task" /> or
///         <see cref="Task{TResult}" />.
///     </para>
///     <para>
///         Handler parameters are item-first. After the item, <see cref="ILambdaInvocationContext" />,
///         <see cref="ILambdaContext" />, <see cref="IEventProcessingContext" />, and
///         <see cref="System.Threading.CancellationToken" /> receive framework values; all other
///         parameters resolve from the item scope. A handler accepts at most ten parameters after
///         the item, including framework context and cancellation-token parameters.
///     </para>
///     <para>
///         The response builder receives outcomes for every source position in source order.
///         Unstarted and locally cancelled items have explicit statuses; the processor does not add
///         placeholder results, validate response completeness, or retry items. These overloads
///         are mutually exclusive with each other and with <c>MapHandler</c> for the same
///         invocation handler at runtime.
///     </para>
///     <para>
///         Method groups do not supply dependency parameter types for generic inference. Use an
///         explicitly typed lambda, a typed delegate local, or specify every generic argument.
///     </para>
/// </remarks>
[ExcludeFromCodeCoverage]
public static class MapEventHandlerLambdaApplicationExtensions
{
    /// <summary>Registers an event-processor handler (N1, 0 additional parameters).</summary>
    /// <remarks>No result. Handler errors fail the invocation.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder MapEventHandler<TEvent, TItem>(
        this ILambdaInvocationBuilder application,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, Task> handle)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N1, 1 additional parameter).</summary>
    /// <remarks>No result. Handler errors fail the invocation.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder MapEventHandler<TEvent, TItem, TD0>(
        this ILambdaInvocationBuilder application,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, TD0, Task> handle)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N1, 2 additional parameters).</summary>
    /// <remarks>No result. Handler errors fail the invocation.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder MapEventHandler<TEvent, TItem, TD0, TD1>(
        this ILambdaInvocationBuilder application,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, TD0, TD1, Task> handle)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N1, 3 additional parameters).</summary>
    /// <remarks>No result. Handler errors fail the invocation.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder MapEventHandler<TEvent, TItem, TD0, TD1, TD2>(
        this ILambdaInvocationBuilder application,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, TD0, TD1, TD2, Task> handle)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N1, 4 additional parameters).</summary>
    /// <remarks>No result. Handler errors fail the invocation.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder MapEventHandler<TEvent, TItem, TD0, TD1, TD2, TD3>(
        this ILambdaInvocationBuilder application,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, TD0, TD1, TD2, TD3, Task> handle)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N1, 5 additional parameters).</summary>
    /// <remarks>No result. Handler errors fail the invocation.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder MapEventHandler<TEvent, TItem, TD0, TD1, TD2, TD3, TD4>(
        this ILambdaInvocationBuilder application,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, TD0, TD1, TD2, TD3, TD4, Task> handle)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N1, 6 additional parameters).</summary>
    /// <remarks>No result. Handler errors fail the invocation.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TD0, TD1, TD2, TD3, TD4, TD5>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, Task> handle)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N1, 7 additional parameters).</summary>
    /// <remarks>No result. Handler errors fail the invocation.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD6">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, Task> handle)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N1, 8 additional parameters).</summary>
    /// <remarks>No result. Handler errors fail the invocation.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD6">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD7">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7, Task> handle)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N1, 9 additional parameters).</summary>
    /// <remarks>No result. Handler errors fail the invocation.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD6">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD7">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD8">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7, TD8>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7, TD8, Task> handle)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N1, 10 additional parameters).</summary>
    /// <remarks>No result. Handler errors fail the invocation.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD6">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD7">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD8">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD9">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7, TD8, TD9>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7, TD8, TD9, Task> handle)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N2, 0 additional parameters).</summary>
    /// <remarks>No result. createResponse receives successfully handled source items.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder MapEventHandler<TEvent, TItem, TResponse>(
        this ILambdaInvocationBuilder application,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, Task> handle,
        Func<IReadOnlyList<EventItemOutcome<TItem, TItem>>, ILambdaInvocationContext, TResponse>
            createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N2, 1 additional parameter).</summary>
    /// <remarks>No result. createResponse receives successfully handled source items.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder MapEventHandler<TEvent, TItem, TResponse, TD0>(
        this ILambdaInvocationBuilder application,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, TD0, Task> handle,
        Func<IReadOnlyList<EventItemOutcome<TItem, TItem>>, ILambdaInvocationContext, TResponse>
            createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N2, 2 additional parameters).</summary>
    /// <remarks>No result. createResponse receives successfully handled source items.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder MapEventHandler<TEvent, TItem, TResponse, TD0, TD1>(
        this ILambdaInvocationBuilder application,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, TD0, TD1, Task> handle,
        Func<IReadOnlyList<EventItemOutcome<TItem, TItem>>, ILambdaInvocationContext, TResponse>
            createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N2, 3 additional parameters).</summary>
    /// <remarks>No result. createResponse receives successfully handled source items.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder MapEventHandler<TEvent, TItem, TResponse, TD0, TD1, TD2>(
        this ILambdaInvocationBuilder application,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, TD0, TD1, TD2, Task> handle,
        Func<IReadOnlyList<EventItemOutcome<TItem, TItem>>, ILambdaInvocationContext, TResponse>
            createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N2, 4 additional parameters).</summary>
    /// <remarks>No result. createResponse receives successfully handled source items.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResponse, TD0, TD1, TD2, TD3>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, Task> handle,
            Func<IReadOnlyList<EventItemOutcome<TItem, TItem>>, ILambdaInvocationContext, TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N2, 5 additional parameters).</summary>
    /// <remarks>No result. createResponse receives successfully handled source items.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResponse, TD0, TD1, TD2, TD3, TD4>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, Task> handle,
            Func<IReadOnlyList<EventItemOutcome<TItem, TItem>>, ILambdaInvocationContext, TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N2, 6 additional parameters).</summary>
    /// <remarks>No result. createResponse receives successfully handled source items.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResponse, TD0, TD1, TD2, TD3, TD4, TD5>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, Task> handle,
            Func<IReadOnlyList<EventItemOutcome<TItem, TItem>>, ILambdaInvocationContext, TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N2, 7 additional parameters).</summary>
    /// <remarks>No result. createResponse receives successfully handled source items.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD6">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResponse, TD0, TD1, TD2, TD3, TD4, TD5, TD6>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, Task> handle,
            Func<IReadOnlyList<EventItemOutcome<TItem, TItem>>, ILambdaInvocationContext, TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N2, 8 additional parameters).</summary>
    /// <remarks>No result. createResponse receives successfully handled source items.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD6">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD7">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResponse, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7, Task> handle,
            Func<IReadOnlyList<EventItemOutcome<TItem, TItem>>, ILambdaInvocationContext, TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N2, 9 additional parameters).</summary>
    /// <remarks>No result. createResponse receives successfully handled source items.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD6">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD7">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD8">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResponse, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7, TD8>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7, TD8, Task> handle,
            Func<IReadOnlyList<EventItemOutcome<TItem, TItem>>, ILambdaInvocationContext, TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N2, 10 additional parameters).</summary>
    /// <remarks>No result. createResponse receives successfully handled source items.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD6">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD7">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD8">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD9">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResponse, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7, TD8, TD9>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7, TD8, TD9, Task> handle,
            Func<IReadOnlyList<EventItemOutcome<TItem, TItem>>, ILambdaInvocationContext, TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N3, 0 additional parameters).</summary>
    /// <remarks>No result with recovery. createResponse receives mapped failures.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TFailure">The mapped failure value type.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder MapEventHandler<TEvent, TItem, TFailure, TResponse>(
        this ILambdaInvocationBuilder application,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, Task> handle,
        Func<TItem, Exception, IEventProcessingContext, TFailure> onError,
        Func<IReadOnlyList<EventItemOutcome<TItem, TFailure>>, ILambdaInvocationContext, TResponse>
            createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N3, 1 additional parameter).</summary>
    /// <remarks>No result with recovery. createResponse receives mapped failures.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TFailure">The mapped failure value type.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder MapEventHandler<TEvent, TItem, TFailure, TResponse, TD0>(
        this ILambdaInvocationBuilder application,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, TD0, Task> handle,
        Func<TItem, Exception, IEventProcessingContext, TFailure> onError,
        Func<IReadOnlyList<EventItemOutcome<TItem, TFailure>>, ILambdaInvocationContext, TResponse>
            createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N3, 2 additional parameters).</summary>
    /// <remarks>No result with recovery. createResponse receives mapped failures.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TFailure">The mapped failure value type.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TFailure, TResponse, TD0, TD1>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, Task> handle,
            Func<TItem, Exception, IEventProcessingContext, TFailure> onError,
            Func<IReadOnlyList<EventItemOutcome<TItem, TFailure>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N3, 3 additional parameters).</summary>
    /// <remarks>No result with recovery. createResponse receives mapped failures.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TFailure">The mapped failure value type.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TFailure, TResponse, TD0, TD1, TD2>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, Task> handle,
            Func<TItem, Exception, IEventProcessingContext, TFailure> onError,
            Func<IReadOnlyList<EventItemOutcome<TItem, TFailure>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N3, 4 additional parameters).</summary>
    /// <remarks>No result with recovery. createResponse receives mapped failures.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TFailure">The mapped failure value type.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TFailure, TResponse, TD0, TD1, TD2, TD3>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, Task> handle,
            Func<TItem, Exception, IEventProcessingContext, TFailure> onError,
            Func<IReadOnlyList<EventItemOutcome<TItem, TFailure>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N3, 5 additional parameters).</summary>
    /// <remarks>No result with recovery. createResponse receives mapped failures.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TFailure">The mapped failure value type.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TFailure, TResponse, TD0, TD1, TD2, TD3, TD4>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, Task> handle,
            Func<TItem, Exception, IEventProcessingContext, TFailure> onError,
            Func<IReadOnlyList<EventItemOutcome<TItem, TFailure>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N3, 6 additional parameters).</summary>
    /// <remarks>No result with recovery. createResponse receives mapped failures.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TFailure">The mapped failure value type.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TFailure, TResponse, TD0, TD1, TD2, TD3, TD4, TD5>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, Task> handle,
            Func<TItem, Exception, IEventProcessingContext, TFailure> onError,
            Func<IReadOnlyList<EventItemOutcome<TItem, TFailure>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N3, 7 additional parameters).</summary>
    /// <remarks>No result with recovery. createResponse receives mapped failures.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TFailure">The mapped failure value type.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD6">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TFailure, TResponse, TD0, TD1, TD2, TD3, TD4, TD5, TD6>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, Task> handle,
            Func<TItem, Exception, IEventProcessingContext, TFailure> onError,
            Func<IReadOnlyList<EventItemOutcome<TItem, TFailure>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N3, 8 additional parameters).</summary>
    /// <remarks>No result with recovery. createResponse receives mapped failures.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TFailure">The mapped failure value type.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD6">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD7">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TFailure, TResponse, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7, Task> handle,
            Func<TItem, Exception, IEventProcessingContext, TFailure> onError,
            Func<IReadOnlyList<EventItemOutcome<TItem, TFailure>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N3, 9 additional parameters).</summary>
    /// <remarks>No result with recovery. createResponse receives mapped failures.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TFailure">The mapped failure value type.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD6">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD7">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD8">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TFailure, TResponse, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7,
            TD8>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7, TD8, Task> handle,
            Func<TItem, Exception, IEventProcessingContext, TFailure> onError,
            Func<IReadOnlyList<EventItemOutcome<TItem, TFailure>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (N3, 10 additional parameters).</summary>
    /// <remarks>No result with recovery. createResponse receives mapped failures.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TFailure">The mapped failure value type.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD6">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD7">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD8">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD9">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TFailure, TResponse, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7,
            TD8,
            TD9>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7, TD8, TD9, Task> handle,
            Func<TItem, Exception, IEventProcessingContext, TFailure> onError,
            Func<IReadOnlyList<EventItemOutcome<TItem, TFailure>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R1, 0 additional parameters).</summary>
    /// <remarks>Result. createResponse receives successful results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder MapEventHandler<TEvent, TItem, TResult, TResponse>(
        this ILambdaInvocationBuilder application,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, Task<TResult>> handle,
        Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext, TResponse>
            createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R1, 1 additional parameter).</summary>
    /// <remarks>Result. createResponse receives successful results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder MapEventHandler<TEvent, TItem, TResult, TResponse, TD0>(
        this ILambdaInvocationBuilder application,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, TD0, Task<TResult>> handle,
        Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext, TResponse>
            createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R1, 2 additional parameters).</summary>
    /// <remarks>Result. createResponse receives successful results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResult, TResponse, TD0, TD1>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, Task<TResult>> handle,
            Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R1, 3 additional parameters).</summary>
    /// <remarks>Result. createResponse receives successful results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResult, TResponse, TD0, TD1, TD2>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, Task<TResult>> handle,
            Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R1, 4 additional parameters).</summary>
    /// <remarks>Result. createResponse receives successful results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResult, TResponse, TD0, TD1, TD2, TD3>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, Task<TResult>> handle,
            Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R1, 5 additional parameters).</summary>
    /// <remarks>Result. createResponse receives successful results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResult, TResponse, TD0, TD1, TD2, TD3, TD4>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, Task<TResult>> handle,
            Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R1, 6 additional parameters).</summary>
    /// <remarks>Result. createResponse receives successful results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResult, TResponse, TD0, TD1, TD2, TD3, TD4, TD5>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, Task<TResult>> handle,
            Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R1, 7 additional parameters).</summary>
    /// <remarks>Result. createResponse receives successful results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD6">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResult, TResponse, TD0, TD1, TD2, TD3, TD4, TD5, TD6>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, Task<TResult>> handle,
            Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R1, 8 additional parameters).</summary>
    /// <remarks>Result. createResponse receives successful results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD6">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD7">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResult, TResponse, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7, Task<TResult>> handle,
            Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R1, 9 additional parameters).</summary>
    /// <remarks>Result. createResponse receives successful results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD6">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD7">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD8">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResult, TResponse, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7,
            TD8>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7, TD8, Task<TResult>> handle,
            Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R1, 10 additional parameters).</summary>
    /// <remarks>Result. createResponse receives successful results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD6">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD7">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD8">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD9">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResult, TResponse, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7,
            TD8,
            TD9>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7, TD8, TD9, Task<TResult>> handle,
            Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R2, 0 additional parameters).</summary>
    /// <remarks>Result with recovery. createResponse receives successful and mapped results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder MapEventHandler<TEvent, TItem, TResult, TResponse>(
        this ILambdaInvocationBuilder application,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, Task<TResult>> handle,
        Func<TItem, Exception, IEventProcessingContext, TResult> onError,
        Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext, TResponse>
            createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R2, 1 additional parameter).</summary>
    /// <remarks>Result with recovery. createResponse receives successful and mapped results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder MapEventHandler<TEvent, TItem, TResult, TResponse, TD0>(
        this ILambdaInvocationBuilder application,
        Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
        Func<TItem, TD0, Task<TResult>> handle,
        Func<TItem, Exception, IEventProcessingContext, TResult> onError,
        Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext, TResponse>
            createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R2, 2 additional parameters).</summary>
    /// <remarks>Result with recovery. createResponse receives successful and mapped results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResult, TResponse, TD0, TD1>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, Task<TResult>> handle,
            Func<TItem, Exception, IEventProcessingContext, TResult> onError,
            Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R2, 3 additional parameters).</summary>
    /// <remarks>Result with recovery. createResponse receives successful and mapped results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResult, TResponse, TD0, TD1, TD2>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, Task<TResult>> handle,
            Func<TItem, Exception, IEventProcessingContext, TResult> onError,
            Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R2, 4 additional parameters).</summary>
    /// <remarks>Result with recovery. createResponse receives successful and mapped results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResult, TResponse, TD0, TD1, TD2, TD3>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, Task<TResult>> handle,
            Func<TItem, Exception, IEventProcessingContext, TResult> onError,
            Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R2, 5 additional parameters).</summary>
    /// <remarks>Result with recovery. createResponse receives successful and mapped results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResult, TResponse, TD0, TD1, TD2, TD3, TD4>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, Task<TResult>> handle,
            Func<TItem, Exception, IEventProcessingContext, TResult> onError,
            Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R2, 6 additional parameters).</summary>
    /// <remarks>Result with recovery. createResponse receives successful and mapped results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResult, TResponse, TD0, TD1, TD2, TD3, TD4, TD5>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, Task<TResult>> handle,
            Func<TItem, Exception, IEventProcessingContext, TResult> onError,
            Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R2, 7 additional parameters).</summary>
    /// <remarks>Result with recovery. createResponse receives successful and mapped results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD6">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResult, TResponse, TD0, TD1, TD2, TD3, TD4, TD5, TD6>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, Task<TResult>> handle,
            Func<TItem, Exception, IEventProcessingContext, TResult> onError,
            Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R2, 8 additional parameters).</summary>
    /// <remarks>Result with recovery. createResponse receives successful and mapped results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD6">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD7">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResult, TResponse, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7, Task<TResult>> handle,
            Func<TItem, Exception, IEventProcessingContext, TResult> onError,
            Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R2, 9 additional parameters).</summary>
    /// <remarks>Result with recovery. createResponse receives successful and mapped results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD6">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD7">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD8">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResult, TResponse, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7,
            TD8>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7, TD8, Task<TResult>> handle,
            Func<TItem, Exception, IEventProcessingContext, TResult> onError,
            Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }

    /// <summary>Registers an event-processor handler (R2, 10 additional parameters).</summary>
    /// <remarks>Result with recovery. createResponse receives successful and mapped results.</remarks>
    /// <typeparam name="TEvent">The deserialized Lambda event type.</typeparam>
    /// <typeparam name="TItem">The source item type returned by <paramref name="split" />.</typeparam>
    /// <typeparam name="TResult">The shared result type for successful and mapped items.</typeparam>
    /// <typeparam name="TResponse">The Lambda response type.</typeparam>
    /// <typeparam name="TD0">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD1">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD2">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD3">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD4">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD5">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD6">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD7">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD8">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <typeparam name="TD9">An additional handler input type. Framework context and token types are recognized by type.</typeparam>
    /// <param name="application">The builder to register the handler with.</param>
    /// <param name="split">Splits the event into source items. Called once per invocation.</param>
    /// <param name="handle">Processes one source item; the item is the first parameter.</param>
    /// <param name="onError">Maps a handler exception for one item to a result value.</param>
    /// <param name="createResponse">Builds the response from outcomes for every source position in source order.</param>
    /// <returns>The same <see cref="ILambdaInvocationBuilder" /> instance.</returns>
    /// <exception cref="InvalidOperationException">Thrown if interception does not occur.</exception>
    public static ILambdaInvocationBuilder
        MapEventHandler<TEvent, TItem, TResult, TResponse, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7,
            TD8,
            TD9>(
            this ILambdaInvocationBuilder application,
            Func<TEvent, ILambdaInvocationContext, IEnumerable<TItem>> split,
            Func<TItem, TD0, TD1, TD2, TD3, TD4, TD5, TD6, TD7, TD8, TD9, Task<TResult>> handle,
            Func<TItem, Exception, IEventProcessingContext, TResult> onError,
            Func<IReadOnlyList<EventItemOutcome<TItem, TResult>>, ILambdaInvocationContext,
                    TResponse>
                createResponse)
    {
        Debug.Fail("This method should have been intercepted at compile time!");
        throw new InvalidOperationException("This method is replaced at compile time.");
    }
}
