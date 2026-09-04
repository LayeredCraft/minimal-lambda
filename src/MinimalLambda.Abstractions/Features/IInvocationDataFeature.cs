namespace MinimalLambda;

/// <summary>
///     Provides access to the invocation event and response data for a Lambda function
///     invocation.
/// </summary>
/// <remarks>
///     This feature encapsulates the raw input (event) and output (response) data for a Lambda
///     invocation. Implementations should manage the lifecycle of these streams appropriately.
/// </remarks>
public interface IInvocationDataFeature : IDisposable
{
    /// <summary>Gets the stream containing the Lambda invocation event data.</summary>
    /// <value>A readable stream containing the serialized Lambda event.</value>
    Stream EventStream { get; }

    /// <summary>Gets or sets the stream that will contain the Lambda invocation response data.</summary>
    /// <value>
    ///     A writable stream where the Lambda response should be written. The stream can be replaced if
    ///     needed to redirect response data to a different destination.
    /// </value>
    Stream ResponseStream { get; set; }
}
