using System.Text;

namespace MinimalLambda.UnitTests.Core.Features;

[TestSubject(typeof(InvocationDataFeature))]
public class InvocationDataFeatureTests
{
    [Fact]
    public void ResponseStream_WhenReadRepeatedly_ReturnsSameWritableStream()
    {
        using var feature = new InvocationDataFeature { EventStream = Stream.Null };
        using var stream = feature.ResponseStream;

        stream.WriteByte(42);

        feature.ResponseStream.Should().BeSameAs(stream);
        stream.Position = 0;
        stream.ReadByte().Should().Be(42);
    }

    [Fact]
    public async Task ResponseStream_WhenFirstReadConcurrently_ReturnsSameStreamToAllReaders()
    {
        const int readerCount = 8;
        const int roundCount = 128;
        var features =
            Enumerable
                .Range(0, roundCount)
                .Select(_ => new InvocationDataFeature { EventStream = Stream.Null })
                .ToArray();
        var streams = new Stream[roundCount, readerCount];
        using var barrier = new Barrier(readerCount);

        var readers = Enumerable
            .Range(0, readerCount)
            .Select(reader => Task.Factory.StartNew(
                () =>
                {
                    for (var round = 0; round < roundCount; round++)
                    {
                        barrier.SignalAndWait();
                        streams[round, reader] = features[round].ResponseStream;
                    }
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default));

        await Task.WhenAll(readers);

        for (var round = 0; round < roundCount; round++)
        {
            using var feature = features[round];
            using var stream = feature.ResponseStream;
            for (var reader = 0; reader < readerCount; reader++)
                streams[round, reader].Should().BeSameAs(stream);
        }
    }

    [Fact]
    public void ResponseStream_WhenReplacedBeforeFirstRead_PreservesReplacementAfterDisposal()
    {
        using var response = new MemoryStream();
        var feature =
            new InvocationDataFeature { EventStream = Stream.Null, ResponseStream = response };

        feature.Dispose();

        feature.ResponseStream.Should().BeSameAs(response);
        response.CanWrite.Should().BeTrue();
    }

    [Fact]
    public void EnableBuffering_WhenEventStreamAlreadySeekable_DoesNotReplaceStream()
    {
        // Arrange
        var eventStream = new MemoryStream("payload"u8.ToArray());
        var feature = new InvocationDataFeature { EventStream = eventStream };

        // Act
        feature.EnableBuffering();

        // Assert
        feature.EventStream.Should().BeSameAs(eventStream);
    }

    [Fact]
    public void EnableBuffering_WhenEventStreamNotSeekable_ReplacesWithSeekableCopy()
    {
        // Arrange
        var payload = "payload"u8.ToArray();
        var eventStream = new NonSeekableStream(payload);
        var feature = new InvocationDataFeature { EventStream = eventStream };

        // Act
        feature.EnableBuffering();

        // Assert
        feature.EventStream.CanSeek.Should().BeTrue();
    }

    [Fact]
    public void EnableBuffering_WhenEventStreamNotSeekable_PreservesContentAndResetsPosition()
    {
        // Arrange
        var payload = "payload"u8.ToArray();
        var eventStream = new NonSeekableStream(payload);
        var feature = new InvocationDataFeature { EventStream = eventStream };

        // Act
        feature.EnableBuffering();

        // Assert
        feature.EventStream.Position.Should().Be(0L);
        using var reader = new StreamReader(feature.EventStream, Encoding.UTF8);
        reader.ReadToEnd().Should().Be("payload");
    }

    [Fact]
    public void
        EnableBuffering_AfterReadingBufferedStream_AllowsResettingPositionForRedeserialization()
    {
        // Arrange
        var payload = "payload"u8.ToArray();
        var eventStream = new NonSeekableStream(payload);
        var feature = new InvocationDataFeature { EventStream = eventStream };
        feature.EnableBuffering();

        using (var reader = new StreamReader(feature.EventStream, Encoding.UTF8, leaveOpen: true))
            reader.ReadToEnd();

        // Act
        feature.EventStream.Position = 0L;

        // Assert
        using var reader2 = new StreamReader(feature.EventStream, Encoding.UTF8);
        reader2.ReadToEnd().Should().Be("payload");
    }

    private sealed class NonSeekableStream(byte[] data) : Stream
    {
        private readonly MemoryStream _inner = new(data);

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush() => _inner.Flush();

        public override int Read(byte[] buffer, int offset, int count) =>
            _inner.Read(buffer, offset, count);

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
