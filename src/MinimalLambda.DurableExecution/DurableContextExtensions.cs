using Amazon.Lambda.DurableExecution;

namespace MinimalLambda.DurableExecution;

/// <summary>
///     Provides MinimalLambda invocation context access for AWS Lambda durable execution contexts.
/// </summary>
public static class DurableContextExtensions
{
    extension(IDurableContext context)
    {
        /// <summary>
        ///     Gets the MinimalLambda invocation context associated with this durable execution.
        /// </summary>
        /// <remarks>
        ///     MinimalLambda supplies its physical invocation context as the durable context's Lambda context
        ///     when adapting a durable handler. This method preserves that exact context instance. AWS owns
        ///     replay and creates a new physical Lambda invocation for a replay, so this context and its
        ///     dependency-injection scope must not be treated as logical workflow state. Its cancellation token
        ///     represents the physical invocation; using it to cancel the root workflow can produce a terminal
        ///     durable failure. Prefer cancellation tokens supplied to durable operation callbacks.
        /// </remarks>
        /// <returns>
        ///     Exact <see cref="ILambdaInvocationContext" /> instance stored in
        ///     <see cref="IDurableContext.LambdaContext" />.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        ///     Thrown when the durable context is <see langword="null" />.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        ///     Thrown when <see cref="IDurableContext.LambdaContext" /> is not a MinimalLambda
        ///     <see cref="ILambdaInvocationContext" />.
        /// </exception>
        /// <seealso cref="IDurableContext.LambdaContext" />
        public ILambdaInvocationContext GetInvocationContext()
        {
            ArgumentNullException.ThrowIfNull(context);

            return context.LambdaContext as ILambdaInvocationContext
                ?? throw new InvalidOperationException(
                    "MinimalLambda invocation context is not available on this durable context.");
        }
    }
}
