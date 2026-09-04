using System.Diagnostics.CodeAnalysis;

namespace MinimalLambda;

/// <summary>
///     Provides extension methods for safely accessing typed event and response data from the
///     <see cref="IFeatureCollection" /> on <see cref="ILambdaInvocationContext" />.
/// </summary>
public static class FeatureLambdaInvocationContextExtensions
{
    extension(ILambdaInvocationContext context)
    {
        /// <summary>Gets the typed event data from the <see cref="IEventFeature" /> in the Lambda context.</summary>
        /// <typeparam name="T">The type of event data to retrieve.</typeparam>
        /// <returns>The typed event data, or null if not found or not of the specified type.</returns>
        public T? GetEvent<T>()
        {
            ArgumentNullException.ThrowIfNull(context);

            return context.Features.Get<IEventFeature>() is IEventFeature<T> eventFeature
                ? eventFeature.GetEvent(context)
                : default;
        }

        /// <summary>
        ///     Attempts to get the typed event data from the <see cref="IEventFeature" /> in the Lambda
        ///     context.
        /// </summary>
        /// <typeparam name="T">The type of event data to retrieve.</typeparam>
        /// <param name="eventT">The typed event data, or null if not found or not of the specified type.</param>
        /// <returns>True if the event data was found and is of the specified type; otherwise false.</returns>
        public bool TryGetEvent<T>([NotNullWhen(true)] out T? eventT)
        {
            ArgumentNullException.ThrowIfNull(context);

            eventT = context.GetEvent<T>();
            return eventT is not null;
        }

        /// <summary>Gets the typed event data from the <see cref="IEventFeature" /> in the Lambda context.</summary>
        /// <typeparam name="T">The type of event data to retrieve.</typeparam>
        /// <returns>The typed event data.</returns>
        /// <exception cref="InvalidOperationException">
        ///     Thrown when the event data is not found or not of the
        ///     specified type.
        /// </exception>
        public T GetRequiredEvent<T>()
        {
            ArgumentNullException.ThrowIfNull(context);

            if (!context.TryGetEvent<T>(out var eventT))
                throw new InvalidOperationException(
                    $"Lambda event of type '{typeof(T).FullName}' is not available in the context.");

            return eventT;
        }

        /// <summary>
        ///     Gets the typed response data from the <see cref="IResponseFeature" /> in the Lambda
        ///     context.
        /// </summary>
        /// <typeparam name="T">The type of response data to retrieve.</typeparam>
        /// <returns>The typed response data, or null if not found or not of the specified type.</returns>
        public T? GetResponse<T>()
        {
            ArgumentNullException.ThrowIfNull(context);

            return context.Features.Get<IResponseFeature>() is IResponseFeature<T> responseFeature
                ? responseFeature.GetResponse()
                : default;
        }

        /// <summary>
        ///     Attempts to get the typed response data from the <see cref="IResponseFeature" /> in the
        ///     Lambda context.
        /// </summary>
        /// <typeparam name="T">The type of response data to retrieve.</typeparam>
        /// <param name="responseT">The typed response data, or null if not found or not of the specified type.</param>
        /// <returns>True if the response data was found and is of the specified type; otherwise false.</returns>
        public bool TryGetResponse<T>([NotNullWhen(true)] out T? responseT)
        {
            ArgumentNullException.ThrowIfNull(context);

            responseT = context.GetResponse<T>();
            return responseT is not null;
        }

        /// <summary>
        ///     Gets the typed response data from the <see cref="IResponseFeature" /> in the Lambda
        ///     context.
        /// </summary>
        /// <typeparam name="T">The type of response data to retrieve.</typeparam>
        /// <returns>The typed response data.</returns>
        /// <exception cref="InvalidOperationException">
        ///     Thrown when the response data is not found or not of
        ///     the specified type.
        /// </exception>
        public T GetRequiredResponse<T>()
        {
            ArgumentNullException.ThrowIfNull(context);

            if (!context.TryGetResponse<T>(out var responseT))
                throw new InvalidOperationException(
                    $"Lambda response of type '{typeof(T).FullName}' is not available in the context.");

            return responseT;
        }

        /// <summary>
        ///     Ensures the raw Lambda event stream is seekable, buffering it into memory first if
        ///     necessary. Call from middleware before reading
        ///     <see cref="IInvocationDataFeature.EventStream" /> directly (for example, to log the raw
        ///     payload), then reset its <c>Position</c> to <c>0</c> afterward so event deserialization
        ///     can still consume it.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        ///     Thrown when no <see cref="IInvocationDataBufferingFeature" /> is available in the
        ///     context.
        /// </exception>
        public void EnableEventBuffering()
        {
            ArgumentNullException.ThrowIfNull(context);

            context.Features.GetRequired<IInvocationDataBufferingFeature>().EnableBuffering();
        }
    }
}
