namespace MinimalLambda.Options;

/// <summary>Provides default event-processor behavior for the Lambda host.</summary>
/// <remarks>
///     <para>
///         Options are captured when an event handler is mapped. Changes made after registration do
///         not affect that handler. Sequential processing is selected with
///         <see cref="EnableConcurrency" />; <see cref="MaxConcurrentExecutions" /> is the upper bound
///         when concurrency is enabled.
///     </para>
/// </remarks>
public sealed class EventHandlerOptions
{
    /// <summary>
    ///     Gets or sets a value indicating whether source items may be processed concurrently.
    ///     Defaults to <see langword="true" />.
    /// </summary>
    /// <remarks>
    ///     When disabled, each item finishes handling, error mapping, and scope disposal before the
    ///     next item starts. Asynchronous handlers are still awaited; the flag does not make
    ///     asynchronous work synchronous.
    /// </remarks>
    public bool EnableConcurrency { get; set; } = true;

    /// <summary>
    ///     Gets or sets the maximum number of item operations that may be active at once. Defaults to
    ///     10.
    /// </summary>
    /// <remarks>
    ///     The value must be positive, including when <see cref="EnableConcurrency" /> is
    ///     <see langword="false" />. There is no unlimited sentinel.
    /// </remarks>
    public int MaxConcurrentExecutions { get; set; } = 10;
}
