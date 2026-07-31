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
        ///         handler to the AWS Lambda Durable Execution protocol. A supported handler declares exactly
        ///         one <see cref="FromEventAttribute" /> workflow input and one AWS
        ///         <c>IDurableContext</c>, and returns <see cref="Task" /> or
        ///         <see cref="Task{TResult}" />. Invocation contexts and dependency-injection services can be
        ///         additional parameters.
        ///     </para>
        ///     <para>
        ///         A compile-time interceptor replaces this call; invoking this method directly at runtime is
        ///         unsupported.
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
