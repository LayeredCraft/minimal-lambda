using System.ComponentModel;
using MinimalLambda.Builder;

namespace MinimalLambda;

/// <summary>Infrastructure used by generated durable handler adapters.</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public static class DurableTerminalInfrastructure
{
    private const string RegistrationKey = "__MinimalLambdaDurableTerminal";
    private static readonly object RegistrationMarker = new();

    /// <summary>Registers that an invocation pipeline requires a durable terminal.</summary>
    /// <param name="builder">Invocation pipeline builder.</param>
    /// <exception cref="ArgumentNullException"><paramref name="builder" /> is <see langword="null" />.</exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Register(ILambdaInvocationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Properties[RegistrationKey] = RegistrationMarker;
    }

    /// <summary>Enters the durable terminal for an invocation.</summary>
    /// <remarks>
    ///     Generated adapters must call this method before invoking the durable terminal body.
    /// </remarks>
    /// <param name="context">Current invocation context.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">
    ///     The durable terminal is not registered or has already been entered for this invocation.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Enter(ILambdaInvocationContext context) => GetState(context).Enter();

    /// <summary>Marks the durable terminal as completed for an invocation.</summary>
    /// <remarks>
    ///     Generated adapters must set the durable response feature before calling this method.
    /// </remarks>
    /// <param name="context">Current invocation context.</param>
    /// <exception cref="ArgumentNullException"><paramref name="context" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">
    ///     The durable terminal is not registered, has not been entered, has already completed, or its
    ///     lifecycle was violated.
    /// </exception>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public static void Complete(ILambdaInvocationContext context) => GetState(context).Complete();

    internal static bool IsRegistered(IDictionary<string, object?> properties) =>
        properties.TryGetValue(RegistrationKey, out var marker)
        && ReferenceEquals(marker, RegistrationMarker);

    internal static void Validate(ILambdaInvocationContext context) => GetState(context).Validate();

    private static DurableTerminalState GetState(ILambdaInvocationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context is not IDurableTerminalContext { DurableTerminalState: { } state })
            throw new InvalidOperationException(
                "The durable terminal lifecycle is not registered for this invocation.");

        return state;
    }
}

internal interface IDurableTerminalContext
{
    DurableTerminalState? DurableTerminalState { get; }
}

internal sealed class DurableTerminalState
{
    internal const string DuplicateExecutionMessage =
        "The durable terminal can only be executed once per invocation.";

    internal const string IncompleteMessage =
        "The durable terminal did not complete before the invocation pipeline returned.";

    internal const string LifecycleViolationMessage =
        "The durable terminal lifecycle was violated.";

    internal const string MissingMessage = "The durable terminal was not executed.";

    private int _status;

    public void Enter()
    {
        if (Interlocked.CompareExchange(
                ref _status,
                (int)DurableTerminalStatus.Running,
                (int)DurableTerminalStatus.NotStarted)
            == (int)DurableTerminalStatus.NotStarted)
            return;

        Interlocked.Exchange(ref _status, (int)DurableTerminalStatus.Violated);
        throw new InvalidOperationException(DuplicateExecutionMessage);
    }

    public void Complete()
    {
        if (Interlocked.CompareExchange(
                ref _status,
                (int)DurableTerminalStatus.Completed,
                (int)DurableTerminalStatus.Running)
            == (int)DurableTerminalStatus.Running)
            return;

        Interlocked.Exchange(ref _status, (int)DurableTerminalStatus.Violated);
        throw new InvalidOperationException(LifecycleViolationMessage);
    }

    public void Validate()
    {
        var status = (DurableTerminalStatus)Volatile.Read(ref _status);

        if (status == DurableTerminalStatus.Completed)
            return;

        Interlocked.Exchange(ref _status, (int)DurableTerminalStatus.Violated);

        throw status switch
        {
            DurableTerminalStatus.NotStarted => new InvalidOperationException(MissingMessage),
            DurableTerminalStatus.Running => new InvalidOperationException(IncompleteMessage),
            _ => new InvalidOperationException(LifecycleViolationMessage),
        };
    }

    private enum DurableTerminalStatus
    {
        NotStarted,
        Running,
        Completed,
        Violated,
    }
}
