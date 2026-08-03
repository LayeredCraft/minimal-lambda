using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace MinimalLambda.Builder;

/// <summary>
///     Provides durable handler registration extensions for
///     <see cref="ILambdaInvocationBuilder" />.
/// </summary>
[ExcludeFromCodeCoverage]
public static class MapDurableHandlerLambdaApplicationExtensions
{
    extension(ILambdaInvocationBuilder application)
    {
        /// <summary>
        ///     Registers an AWS Lambda Durable Execution handler with automatic dependency injection and
        ///     serialization.
        /// </summary>
        /// <remarks>
        ///     <para>
        ///         Source generation creates wiring code that resolves handler dependencies and adapts the
        ///         handler to the AWS Lambda Durable Execution protocol. A handler can optionally declare a
        ///         <see cref="FromEventAttribute" /> workflow input and an AWS <c>IDurableContext</c>.
        ///         It returns <see cref="Task" /> or <see cref="Task{TResult}" />. Invocation contexts and
        ///         dependency-injection services can be additional parameters.
        ///     </para>
        ///     <para>
        ///         Invocation contexts, dependency-injection scopes, and middleware belong to one physical
        ///         Lambda invocation. Middleware runs again when AWS replays a workflow. AWS owns durable
        ///         context creation, checkpoints, replay, suspension, and durable status mapping;
        ///         MinimalLambda owns physical invocation hosting, dependency injection, middleware, outer
        ///         envelope serialization, and root handler cancellation tokens supplied by the physical
        ///         invocation. Durable operation callbacks receive distinct SDK cancellation tokens for step work.
        ///     </para>
        ///     <para>
        ///         A compile-time interceptor must replace this call; invoking this fallback directly at
        ///         runtime is unsupported and throws <see cref="InvalidOperationException" />.
        ///     </para>
        /// </remarks>
        /// <param name="handler">
        ///     Durable handler delegate that will be intercepted and replaced at compile time by the source
        ///     generator.
        /// </param>
        /// <returns>
        ///     Current <see cref="ILambdaInvocationBuilder" /> instance for method chaining.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        ///     Thrown if the call was not replaced by source-generated code at compile time.
        /// </exception>
        /// <seealso cref="ILambdaInvocationBuilder.Handle(LambdaInvocationDelegate)" />
        /// <seealso cref="MapHandlerLambdaApplicationExtensions.MapHandler(ILambdaInvocationBuilder, Delegate)" />
        public ILambdaInvocationBuilder MapDurableHandler(Delegate handler)
        {
            Debug.Fail("This method should have been intercepted at compile time!");
            throw new InvalidOperationException("This method is replaced at compile time.");
        }
    }
}
