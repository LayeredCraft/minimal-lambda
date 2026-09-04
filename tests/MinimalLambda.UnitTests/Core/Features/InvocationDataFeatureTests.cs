using System.Text;

namespace MinimalLambda.UnitTests.Core.Features;

[TestSubject(typeof(InvocationDataFeature))]
public class InvocationDataFeatureTests
{
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
