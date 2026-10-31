namespace MinimalLambda.EventProcessing;

/// <summary>
///     Provides processing controls and cancellation state for one event-processor invocation.
/// </summary>
/// <remarks>
///     <para>
///         There is one processing context per processor invocation. Item handlers and error mappers
///         share it. It is injected as a framework parameter, not resolved from dependency injection,
///         and must not be retained beyond its owning invocation.
///     </para>
///     <para>
///         <see cref="StopProcessing" /> prevents new items from being admitted while admitted work
///         finishes normally. <see cref="CancelProcessing" /> also stops admissions and signals
///         <see cref="CancellationToken" /> so admitted handlers can observe cooperative cancellation.
///         Both operations are thread-safe and idempotent. Stopping can be escalated to cancellation;
///         cancellation cannot be reversed.
///     </para>
///     <para>
///         Cancellation is cooperative. Neither operation forcibly interrupts a running handler,
///         undoes side effects, or guarantees that an active handler stops. Already completed results
///         are retained and passed to the response builder.
///     </para>
/// </remarks>
public interface IEventProcessingContext
{
    /// <summary>Gets the original Lambda invocation context.</summary>
    ILambdaInvocationContext Invocation { get; }

    /// <summary>
    ///     Gets the token that links the invocation cancellation token with
    ///     <see cref="CancelProcessing" /> requests made during handling.
    /// </summary>
    /// <remarks>
    ///     Handlers receive this token through a <see cref="System.Threading.CancellationToken" />
    ///     parameter. Use <see cref="Invocation" /> to observe the original invocation token.
    /// </remarks>
    CancellationToken CancellationToken { get; }

    /// <summary>Stops admitting new source items while admitted work finishes normally.</summary>
    /// <remarks>
    ///     This method does not signal <see cref="CancellationToken" /> and does not fail the
    ///     invocation. Calling it after processing has completed has no effect.
    /// </remarks>
    void StopProcessing();

    /// <summary>
    ///     Stops admitting new source items and signals <see cref="CancellationToken" /> so admitted
    ///     handlers can cancel cooperatively.
    /// </summary>
    /// <remarks>
    ///     Admitted handlers, cancellation callbacks, and scope disposal are drained before the
    ///     response is built. Calling this method after processing has completed has no effect.
    /// </remarks>
    void CancelProcessing();
}
