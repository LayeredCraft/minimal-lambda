namespace MinimalLambda;

internal sealed class InvocationDataFeature
    : IInvocationDataFeature, IInvocationDataBufferingFeature
{
    private Stream _eventStream = null!;

    public required Stream EventStream
    {
        get => _eventStream;
        init => _eventStream = value;
    }

    private Stream? _responseStream;

    public Stream ResponseStream
    {
        get
        {
            if (Volatile.Read(ref _responseStream) is { } response)
                return response;

            var created = new MemoryStream();
            response = Interlocked.CompareExchange(ref _responseStream, created, null);
            if (response is null)
                return created;

            created.Dispose();
            return response;
        }
        set => Volatile.Write(ref _responseStream, value);
    }

    public void EnableBuffering()
    {
        if (_eventStream.CanSeek)
            return;

        var buffered = new MemoryStream();
        _eventStream.CopyTo(buffered);
        _eventStream.Dispose();
        buffered.Position = 0L;
        _eventStream = buffered;
    }

    /// <summary>
    ///     Dispose the underlying stream. We only dispose of the event stream, not the response
    ///     stream as the Lambda bootstrap will dispose of it.
    /// </summary>
    public void Dispose() => _eventStream.Dispose();
}
