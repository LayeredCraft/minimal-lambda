using MinimalLambda.Builder;

namespace MinimalLambda.DurableExecution.UnitTests;

public class MapDurableHandlerLambdaApplicationExtensionsTests
{
    [Fact]
    public void MapDurableHandler_WhenNotIntercepted_ThrowsFallbackException()
    {
        // Arrange
        var application = Substitute.For<ILambdaInvocationBuilder>();
        Action handler = () => { };

        // Act
        var act = () => application.MapDurableHandler(handler);

        // Assert
#if DEBUG
        act
            .Should()
            .Throw<Xunit.Sdk.TraceAssertException>()
            .WithMessage("*This method should have been intercepted at compile time!*");
#else
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage("This method is replaced at compile time.");
#endif
    }
}
