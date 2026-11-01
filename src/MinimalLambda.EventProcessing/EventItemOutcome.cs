namespace MinimalLambda.EventProcessing;

/// <summary>Describes how processing ended for one source position.</summary>
public enum EventItemStatus
{
    /// <summary>The handler completed successfully.</summary>
    Succeeded,

    /// <summary>The error mapper produced a contribution.</summary>
    MappedError,

    /// <summary>The item was not started.</summary>
    Skipped,

    /// <summary>Processing cancellation interrupted the admitted item.</summary>
    Cancelled,
}

/// <summary>An immutable outcome for one source position, including duplicate items.</summary>
/// <typeparam name="TItem">The source item type.</typeparam>
/// <typeparam name="TValue">The contribution type.</typeparam>
public sealed class EventItemOutcome<TItem, TValue>
{
    internal EventItemOutcome(
        int index,
        TItem item,
        EventItemStatus status,
        bool hasValue = false,
        TValue? value = default,
        Exception? exception = null)
    {
        Index = index;
        Item = item;
        Status = status;
        HasValue = hasValue;
        Value = value;
        Exception = exception;
    }

    /// <summary>Gets the zero-based position in the materialized source.</summary>
    public int Index { get; }

    /// <summary>Gets the original source item.</summary>
    public TItem Item { get; }

    /// <summary>Gets the processing status.</summary>
    public EventItemStatus Status { get; }

    /// <summary>Gets whether a contribution exists, including a null contribution.</summary>
    public bool HasValue { get; }

    /// <summary>Gets the contribution; check HasValue to distinguish absent and null values.</summary>
    public TValue? Value { get; }

    /// <summary>Gets the original error when Status is MappedError.</summary>
    public Exception? Exception { get; }
}
