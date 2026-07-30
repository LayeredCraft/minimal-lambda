using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.Lambda.CloudWatchLogsEvents;
using AutoFixture;
using AwesomeAssertions;
using JetBrains.Annotations;
using MinimalLambda.Envelopes.CloudWatchLogs;
using MinimalLambda.Options;
using Xunit;

namespace MinimalLambda.Envelopes.UnitTests;

[TestSubject(typeof(CloudWatchLogsEnvelope<>))]
[TestSubject(typeof(CloudWatchLogsEnvelope))]
public class CloudWatchLogsEnvelopeTests
{
    private readonly Fixture _fixture = new();

    private record TestPayload(string Content, int Priority);

    #region CloudWatchLogsEnvelope<T> Tests

    [Fact]
    public void ExtractPayload_Generic_WithSingleLogEvent_DeserializesMessageContent()
    {
        // Arrange
        var payload = _fixture.Create<TestPayload>();
        var envelope = CreateGenericEnvelope(payload);
        var options = new EnvelopeOptions();

        // Act
        envelope.ExtractPayload(options);

        // Assert
        envelope.AwslogsContent.Should().NotBeNull();
        envelope.AwslogsContent!.LogEvents.Should().HaveCount(1);
        envelope.AwslogsContent.LogEvents[0].MessageContent.Should().NotBeNull();
        envelope.AwslogsContent.LogEvents[0].MessageContent!.Content.Should().Be(payload.Content);
        envelope.AwslogsContent.LogEvents[0].MessageContent!.Priority.Should().Be(payload.Priority);
    }

    [Fact]
    public void ExtractPayload_Generic_WithMultipleLogEvents_DeserializesAllMessages()
    {
        // Arrange
        var payloads = _fixture.CreateMany<TestPayload>(3).ToArray();
        var envelope = CreateGenericEnvelopeWithMultipleEvents(payloads);
        var options = new EnvelopeOptions();

        // Act
        envelope.ExtractPayload(options);

        // Assert
        envelope.AwslogsContent.Should().NotBeNull();
        envelope.AwslogsContent!.LogEvents.Should().HaveCount(3);
        for (var i = 0; i < payloads.Length; i++)
        {
            envelope.AwslogsContent.LogEvents[i].MessageContent.Should().NotBeNull();
            envelope.AwslogsContent.LogEvents[i].MessageContent!
                .Content
                .Should()
                .Be(payloads[i].Content);
            envelope.AwslogsContent.LogEvents[i].MessageContent!
                .Priority
                .Should()
                .Be(payloads[i].Priority);
        }
    }

    [Fact]
    public void
        ExtractPayload_Generic_WithCamelCaseNamingPolicy_DeserializesWithCamelCaseProperties()
    {
        // Arrange
        var payload = _fixture.Create<TestPayload>();
        var envelope = CreateGenericEnvelope(
            payload,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        var options = new EnvelopeOptions
        {
            JsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            },
        };

        // Act
        envelope.ExtractPayload(options);

        // Assert
        envelope.AwslogsContent.Should().NotBeNull();
        envelope.AwslogsContent!.LogEvents[0].MessageContent.Should().NotBeNull();
        envelope.AwslogsContent.LogEvents[0].MessageContent!.Content.Should().Be(payload.Content);
        envelope.AwslogsContent.LogEvents[0].MessageContent!.Priority.Should().Be(payload.Priority);
    }

    [Fact]
    public void ExtractPayload_Generic_WithNullMessage_SetsMessageContentToNull()
    {
        // Arrange
        var envelope = CreateGenericEnvelopeWithMessageContent("null");
        var options = new EnvelopeOptions();

        // Act
        envelope.ExtractPayload(options);

        // Assert
        envelope.AwslogsContent.Should().NotBeNull();
        envelope.AwslogsContent!.LogEvents[0].MessageContent.Should().BeNull();
    }

    [Fact]
    public void ExtractPayload_Generic_WithInvalidJson_ThrowsJsonException()
    {
        // Arrange
        var invalidJson = _fixture.Create<string>();
        var envelope = CreateGenericEnvelopeWithMessageContent(invalidJson);
        var options = new EnvelopeOptions();

        // Act & Assert
        var act = () => envelope.ExtractPayload(options);
        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void ExtractPayload_Generic_WithNullEncodedData_ThrowsArgumentNullException()
    {
        // Arrange
        var envelope = new CloudWatchLogsEnvelope<TestPayload>
        {
            Awslogs = new CloudWatchLogsEvent.Log { EncodedData = null! },
        };
        var options = new EnvelopeOptions();

        // Act & Assert
        var act = () => envelope.ExtractPayload(options);
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [Fact]
    public void ExtractPayload_Generic_PopulatesAwslogsContentProperties()
    {
        // Arrange
        var payload = _fixture.Create<TestPayload>();
        var logGroup = _fixture.Create<string>();
        var logStream = _fixture.Create<string>();
        var messageType = _fixture.Create<string>();
        var owner = _fixture.Create<string>();
        var subscriptionFilters = _fixture.CreateMany<string>(2).ToArray();

        var envelope = CreateGenericEnvelopeWithMetadata(
            payload,
            logGroup,
            logStream,
            messageType,
            owner,
            subscriptionFilters);
        var options = new EnvelopeOptions();

        // Act
        envelope.ExtractPayload(options);

        // Assert
        envelope.AwslogsContent.Should().NotBeNull();
        envelope.AwslogsContent!.LogGroup.Should().Be(logGroup);
        envelope.AwslogsContent.LogStream.Should().Be(logStream);
        envelope.AwslogsContent.MessageType.Should().Be(messageType);
        envelope.AwslogsContent.Owner.Should().Be(owner);
        envelope.AwslogsContent.SubscriptionFilters.Should().BeEquivalentTo(subscriptionFilters);
    }

    #endregion

    #region CloudWatchLogsEnvelope Tests

    [Fact]
    public void ExtractPayload_NonGeneric_WithSingleLogEvent_SetsMessageContentToRawMessage()
    {
        // Arrange
        var message = _fixture.Create<string>();
        var envelope = CreateNonGenericEnvelope(message);
        var options = new EnvelopeOptions();

        // Act
        envelope.ExtractPayload(options);

        // Assert
        envelope.AwslogsContent.Should().NotBeNull();
        envelope.AwslogsContent!.LogEvents.Should().HaveCount(1);
        envelope.AwslogsContent.LogEvents[0].MessageContent.Should().Be(message);
        envelope.AwslogsContent.LogEvents[0].Message.Should().Be(message);
    }

    [Fact]
    public void
        ExtractPayload_NonGeneric_WithMultipleLogEvents_SetsAllMessageContentsToRawMessages()
    {
        // Arrange
        var messages = _fixture.CreateMany<string>(3).ToArray();
        var envelope = CreateNonGenericEnvelopeWithMultipleEvents(messages);
        var options = new EnvelopeOptions();

        // Act
        envelope.ExtractPayload(options);

        // Assert
        envelope.AwslogsContent.Should().NotBeNull();
        envelope.AwslogsContent!.LogEvents.Should().HaveCount(3);
        for (var i = 0; i < messages.Length; i++)
        {
            envelope.AwslogsContent.LogEvents[i].MessageContent.Should().Be(messages[i]);
            envelope.AwslogsContent.LogEvents[i].Message.Should().Be(messages[i]);
        }
    }

    [Fact]
    public void ExtractPayload_NonGeneric_WithJsonMessage_DoesNotDeserialize()
    {
        // Arrange
        var jsonMessage = JsonSerializer.Serialize(_fixture.Create<TestPayload>());
        var envelope = CreateNonGenericEnvelope(jsonMessage);
        var options = new EnvelopeOptions();

        // Act
        envelope.ExtractPayload(options);

        // Assert
        envelope.AwslogsContent.Should().NotBeNull();
        envelope.AwslogsContent!.LogEvents[0].MessageContent.Should().Be(jsonMessage);
        envelope.AwslogsContent.LogEvents[0].MessageContent.Should().BeOfType<string>();
    }

    [Fact]
    public void ExtractPayload_NonGeneric_PopulatesAwslogsContentProperties()
    {
        // Arrange
        var message = _fixture.Create<string>();
        var logGroup = _fixture.Create<string>();
        var logStream = _fixture.Create<string>();
        var messageType = _fixture.Create<string>();
        var owner = _fixture.Create<string>();
        var subscriptionFilters = _fixture.CreateMany<string>(2).ToArray();

        var envelope = CreateNonGenericEnvelopeWithMetadata(
            message,
            logGroup,
            logStream,
            messageType,
            owner,
            subscriptionFilters);
        var options = new EnvelopeOptions();

        // Act
        envelope.ExtractPayload(options);

        // Assert
        envelope.AwslogsContent.Should().NotBeNull();
        envelope.AwslogsContent!.LogGroup.Should().Be(logGroup);
        envelope.AwslogsContent.LogStream.Should().Be(logStream);
        envelope.AwslogsContent.MessageType.Should().Be(messageType);
        envelope.AwslogsContent.Owner.Should().Be(owner);
        envelope.AwslogsContent.SubscriptionFilters.Should().BeEquivalentTo(subscriptionFilters);
    }

    #endregion

    #region Shared Tests

    [Fact]
    public void CloudWatchLogsEnvelope_Generic_InheritsFromCloudWatchLogsEvent()
    {
        // Arrange & Act
        var envelope = new CloudWatchLogsEnvelope<TestPayload>
        {
            Awslogs = new CloudWatchLogsEvent.Log(),
        };

        // Assert
        envelope.Should().BeAssignableTo<CloudWatchLogsEvent>();
    }

    [Fact]
    public void CloudWatchLogsEnvelope_NonGeneric_InheritsFromCloudWatchLogsEvent()
    {
        // Arrange & Act
        var envelope = new CloudWatchLogsEnvelope { Awslogs = new CloudWatchLogsEvent.Log() };

        // Assert
        envelope.Should().BeAssignableTo<CloudWatchLogsEvent>();
    }

    [Fact]
    public void AwslogsContent_HasJsonIgnoreAttribute()
    {
        // Arrange
        var property = typeof(CloudWatchLogsEnvelopeBase<TestPayload>).GetProperty(
            nameof(CloudWatchLogsEnvelopeBase<TestPayload>.AwslogsContent));

        // Act
        var hasJsonIgnoreAttribute =
            property?.GetCustomAttributes(typeof(JsonIgnoreAttribute), false).Length > 0;

        // Assert
        hasJsonIgnoreAttribute.Should().BeTrue();
    }

    [Fact]
    public void LogEventEnvelope_MessageContent_HasJsonIgnoreAttribute()
    {
        // Arrange
        var property = typeof(CloudWatchLogsEnvelopeBase<TestPayload>.LogEventEnvelope).GetProperty(
            nameof(CloudWatchLogsEnvelopeBase<TestPayload>.LogEventEnvelope.MessageContent));

        // Act
        var hasJsonIgnoreAttribute =
            property?.GetCustomAttributes(typeof(JsonIgnoreAttribute), false).Length > 0;

        // Assert
        hasJsonIgnoreAttribute.Should().BeTrue();
    }

    #endregion

    #region Helper Methods

    private CloudWatchLogsEnvelope<TestPayload> CreateGenericEnvelope(
        TestPayload payload,
        JsonSerializerOptions? messageSerializerOptions = null)
    {
        var messageJson = JsonSerializer.Serialize(payload, messageSerializerOptions);
        return CreateGenericEnvelopeWithMessageContent(messageJson);
    }

    private CloudWatchLogsEnvelope<TestPayload> CreateGenericEnvelopeWithMultipleEvents(
        TestPayload[] payloads)
    {
        var logEvents = payloads
            .Select((p, i) => new
            {
                id = i.ToString(),
                timestamp =
                    new DateTimeOffset(DateTime.UtcNow.AddMinutes(i)).ToUnixTimeMilliseconds(),
                message = JsonSerializer.Serialize(p),
            })
            .ToArray();

        var awslogsData = new
        {
            logEvents,
            logGroup = _fixture.Create<string>(),
            logStream = _fixture.Create<string>(),
            messageType = "DATA_MESSAGE",
            owner = _fixture.Create<string>(),
            subscriptionFilters = _fixture.CreateMany<string>(1).ToArray(),
        };

        return CreateGenericEnvelopeWithAwslogsData(awslogsData);
    }

    private CloudWatchLogsEnvelope<TestPayload> CreateGenericEnvelopeWithMessageContent(
        string messageContent)
    {
        var awslogsData = new
        {
            logEvents =
                new[]
                {
                    new
                    {
                        id = _fixture.Create<string>(),
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        message = messageContent,
                    },
                },
            logGroup = _fixture.Create<string>(),
            logStream = _fixture.Create<string>(),
            messageType = "DATA_MESSAGE",
            owner = _fixture.Create<string>(),
            subscriptionFilters = _fixture.CreateMany<string>(1).ToArray(),
        };

        return CreateGenericEnvelopeWithAwslogsData(awslogsData);
    }

    private CloudWatchLogsEnvelope<TestPayload> CreateGenericEnvelopeWithMetadata(
        TestPayload payload,
        string logGroup,
        string logStream,
        string messageType,
        string owner,
        string[] subscriptionFilters)
    {
        var awslogsData = new
        {
            logEvents = new[]
            {
                new
                {
                    id = _fixture.Create<string>(),
                    timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                    message = JsonSerializer.Serialize(payload),
                },
            },
            logGroup,
            logStream,
            messageType,
            owner,
            subscriptionFilters,
        };

        return CreateGenericEnvelopeWithAwslogsData(awslogsData);
    }

    private CloudWatchLogsEnvelope<TestPayload> CreateGenericEnvelopeWithAwslogsData(
        object awslogsData)
    {
        var envelopeOptions = new EnvelopeOptions();

        var json = JsonSerializer.Serialize(awslogsData, envelopeOptions.LambdaDefaultJsonOptions);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
            gzipStream.Write(jsonBytes, 0, jsonBytes.Length);

        var compressedData = outputStream.ToArray();
        var base64String = Convert.ToBase64String(compressedData);

        return new CloudWatchLogsEnvelope<TestPayload>
        {
            Awslogs = new CloudWatchLogsEvent.Log { EncodedData = base64String },
        };
    }

    private CloudWatchLogsEnvelope CreateNonGenericEnvelope(string message)
    {
        var awslogsData = new
        {
            logEvents =
                new[]
                {
                    new
                    {
                        id = _fixture.Create<string>(),
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        message,
                    },
                },
            logGroup = _fixture.Create<string>(),
            logStream = _fixture.Create<string>(),
            messageType = "DATA_MESSAGE",
            owner = _fixture.Create<string>(),
            subscriptionFilters = _fixture.CreateMany<string>(1).ToArray(),
        };

        return CreateNonGenericEnvelopeWithAwslogsData(awslogsData);
    }

    private CloudWatchLogsEnvelope CreateNonGenericEnvelopeWithMultipleEvents(string[] messages)
    {
        var logEvents = messages
            .Select((m, i) => new
            {
                id = i.ToString(),
                timestamp =
                    new DateTimeOffset(DateTime.UtcNow.AddMinutes(i)).ToUnixTimeMilliseconds(),
                message = m,
            })
            .ToArray();

        var awslogsData = new
        {
            logEvents,
            logGroup = _fixture.Create<string>(),
            logStream = _fixture.Create<string>(),
            messageType = "DATA_MESSAGE",
            owner = _fixture.Create<string>(),
            subscriptionFilters = _fixture.CreateMany<string>(1).ToArray(),
        };

        return CreateNonGenericEnvelopeWithAwslogsData(awslogsData);
    }

    private CloudWatchLogsEnvelope CreateNonGenericEnvelopeWithMetadata(
        string message,
        string logGroup,
        string logStream,
        string messageType,
        string owner,
        string[] subscriptionFilters)
    {
        var awslogsData = new
        {
            logEvents =
                new[]
                {
                    new
                    {
                        id = _fixture.Create<string>(),
                        timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        message,
                    },
                },
            logGroup,
            logStream,
            messageType,
            owner,
            subscriptionFilters,
        };

        return CreateNonGenericEnvelopeWithAwslogsData(awslogsData);
    }

    private CloudWatchLogsEnvelope CreateNonGenericEnvelopeWithAwslogsData(object awslogsData)
    {
        var envelopeOptions = new EnvelopeOptions();

        var json = JsonSerializer.Serialize(awslogsData, envelopeOptions.LambdaDefaultJsonOptions);
        var jsonBytes = Encoding.UTF8.GetBytes(json);

        using var outputStream = new MemoryStream();
        using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
            gzipStream.Write(jsonBytes, 0, jsonBytes.Length);

        var compressedData = outputStream.ToArray();
        var base64String = Convert.ToBase64String(compressedData);

        return new CloudWatchLogsEnvelope
        {
            Awslogs = new CloudWatchLogsEvent.Log { EncodedData = base64String },
        };
    }

    #endregion
}
