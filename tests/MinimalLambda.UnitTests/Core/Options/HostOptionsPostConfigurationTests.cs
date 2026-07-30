using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace MinimalLambda.UnitTests.Core.Options;

[TestSubject(typeof(HostOptionsPostConfiguration))]
public class HostOptionsPostConfigurationTests
{
    [Fact]
    public void Constructor_WithNullLambdaHostOptions_ThrowsArgumentNullException()
    {
        // Act
        var act = () => new HostOptionsPostConfiguration(null!);

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>().WithParameterName("lambdaHostOptions");
    }

    [Theory]
    [AutoNSubstituteData]
    public void Constructor_WithValidOptions_Succeeds(IOptions<LambdaHostOptions> options)
    {
        // Act
        var postConfig = new HostOptionsPostConfiguration(options);

        // Assert
        postConfig.Should().NotBeNull();
    }

    [Fact]
    public void PostConfigure_WithValidHostOptions_SetsShutdownTimeout()
    {
        // Arrange
        var lambdaHostOptions =
            Microsoft.Extensions.Options.Options.Create(new LambdaHostOptions());
        var postConfig = new HostOptionsPostConfiguration(lambdaHostOptions);
        var hostOptions = new HostOptions();
        var expectedTimeout = lambdaHostOptions.Value.ShutdownDuration
            - lambdaHostOptions.Value.ShutdownDurationBuffer;

        // Act
        postConfig.PostConfigure(null, hostOptions);

        // Assert
        hostOptions.ShutdownTimeout.Should().Be(expectedTimeout);
    }

    [Fact]
    public void PostConfigure_WithDefaultLambdaHostOptions_CalculatesCorrectTimeout()
    {
        // Arrange
        var lambdaHostOptions =
            Microsoft.Extensions.Options.Options.Create(new LambdaHostOptions());
        var postConfig = new HostOptionsPostConfiguration(lambdaHostOptions);
        var hostOptions = new HostOptions();

        var expectedShutdownDuration = TimeSpan.FromMilliseconds(500);
        var expectedBuffer = TimeSpan.FromMilliseconds(50);
        var expectedTimeout = expectedShutdownDuration - expectedBuffer;

        // Act
        postConfig.PostConfigure(null, hostOptions);

        // Assert
        hostOptions.ShutdownTimeout.Should().Be(expectedTimeout);
    }

    [Fact]
    public void PostConfigure_WhenDurationMinusBufferIsNegative_SetsTimeoutToZero()
    {
        // Arrange
        var lambdaHostOptions = Microsoft.Extensions.Options.Options.Create(
            new LambdaHostOptions
            {
                ShutdownDuration = TimeSpan.FromMilliseconds(10),
                ShutdownDurationBuffer = TimeSpan.FromMilliseconds(100),
            });
        var postConfig = new HostOptionsPostConfiguration(lambdaHostOptions);
        var hostOptions = new HostOptions();

        // Act
        postConfig.PostConfigure(null, hostOptions);

        // Assert
        hostOptions.ShutdownTimeout.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void PostConfigure_WhenDurationEqualsBuffer_SetsTimeoutToZero()
    {
        // Arrange
        var duration = TimeSpan.FromMilliseconds(100);
        var lambdaHostOptions = Microsoft.Extensions.Options.Options.Create(
            new LambdaHostOptions
            {
                ShutdownDuration = duration, ShutdownDurationBuffer = duration,
            });
        var postConfig = new HostOptionsPostConfiguration(lambdaHostOptions);
        var hostOptions = new HostOptions();

        // Act
        postConfig.PostConfigure(null, hostOptions);

        // Assert
        hostOptions.ShutdownTimeout.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void PostConfigure_WithLargeShutdownDuration_CalculatesCorrectTimeout()
    {
        // Arrange
        var lambdaHostOptions = Microsoft.Extensions.Options.Options.Create(
            new LambdaHostOptions
            {
                ShutdownDuration = TimeSpan.FromSeconds(30),
                ShutdownDurationBuffer = TimeSpan.FromSeconds(2),
            });
        var postConfig = new HostOptionsPostConfiguration(lambdaHostOptions);
        var hostOptions = new HostOptions();
        var expectedTimeout = TimeSpan.FromSeconds(28);

        // Act
        postConfig.PostConfigure(null, hostOptions);

        // Assert
        hostOptions.ShutdownTimeout.Should().Be(expectedTimeout);
    }

    [Fact]
    public void PostConfigure_CanBeCalledMultipleTimes()
    {
        // Arrange
        var lambdaHostOptions =
            Microsoft.Extensions.Options.Options.Create(new LambdaHostOptions());
        var postConfig = new HostOptionsPostConfiguration(lambdaHostOptions);
        var hostOptions1 = new HostOptions();
        var hostOptions2 = new HostOptions();
        var expectedTimeout = lambdaHostOptions.Value.ShutdownDuration
            - lambdaHostOptions.Value.ShutdownDurationBuffer;

        // Act
        postConfig.PostConfigure(null, hostOptions1);
        postConfig.PostConfigure("custom", hostOptions2);

        // Assert
        hostOptions1.ShutdownTimeout.Should().Be(expectedTimeout);
        hostOptions2.ShutdownTimeout.Should().Be(expectedTimeout);
    }

    [Fact]
    public void PostConfigure_IgnoresNameParameter()
    {
        // Arrange
        var lambdaHostOptions =
            Microsoft.Extensions.Options.Options.Create(new LambdaHostOptions());
        var postConfig = new HostOptionsPostConfiguration(lambdaHostOptions);
        var hostOptions = new HostOptions();
        var expectedTimeout = lambdaHostOptions.Value.ShutdownDuration
            - lambdaHostOptions.Value.ShutdownDurationBuffer;

        // Act
        postConfig.PostConfigure("anyName", hostOptions);

        // Assert
        hostOptions.ShutdownTimeout.Should().Be(expectedTimeout);
    }
}
