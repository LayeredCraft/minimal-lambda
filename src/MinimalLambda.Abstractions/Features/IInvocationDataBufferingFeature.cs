namespace MinimalLambda;

/// <summary>
///     Optional capability of <see cref="IInvocationDataFeature" /> that allows buffering the
///     invocation event stream into memory so it can be read outside of event deserialization.
/// </summary>
/// <remarks>
///     Implemented alongside <see cref="IInvocationDataFeature" /> by implementations that support
///     it, rather than registered as its own entry in <see cref="IFeatureCollection" />. Probe the
///     currently active <see cref="IInvocationDataFeature" /> for this capability (for example,
///     <c>context.Features.Get&lt;IInvocationDataFeature&gt;() is IInvocationDataBufferingFeature</c>,
///     or the <c>context.EnableEventBuffering()</c> convenience extension) rather than looking this
///     type up in the feature collection directly or assuming every
///     <see cref="IInvocationDataFeature" /> implementation supports buffering. Looking it up
///     separately in the feature collection risks it becoming out of sync with whichever
///     <see cref="IInvocationDataFeature" /> is currently registered, if middleware replaces that
///     registration.
/// </remarks>
public interface IInvocationDataBufferingFeature
{
    /// <summary>
    ///     Ensures <see cref="IInvocationDataFeature.EventStream" /> is seekable, buffering it into
    ///     memory first if necessary. Enables middleware to read the raw event payload (for example,
    ///     to log it) without consuming the stream that event deserialization depends on.
    /// </summary>
    /// <remarks>
    ///     Call before reading <see cref="IInvocationDataFeature.EventStream" />. After reading, reset
    ///     <c>EventStream.Position</c> to <c>0</c> so downstream event deserialization can still
    ///     consume it. Opt in per-invocation, since it buffers the event payload into memory even
    ///     when it is already seekable.
    /// </remarks>
    void EnableBuffering();
}
