using Amazon.Lambda.Core;

namespace MinimalLambda.DurableExecution.UnitTests;

public class DurableContextExtensionsTests
{
    [Fact]
    public void GetInvocationContext_WithNullContext_ThrowsArgumentNullException()
    {
        // Arrange
        IDurableContext context = null!;

        // Act
        var act = context.GetInvocationContext;

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName(nameof(context));
    }

    [Fact]
    public void GetInvocationContext_WithForeignLambdaContext_ThrowsInvalidOperationException()
    {
        // Arrange
        var context = Substitute.For<IDurableContext>();
        context.LambdaContext.Returns(Substitute.For<ILambdaContext>());

        // Act
        var act = context.GetInvocationContext;

        // Assert
        act
            .Should()
            .Throw<InvalidOperationException>()
            .WithMessage(
                "MinimalLambda invocation context is not available on this durable context.");
    }

    [Fact]
    public void GetInvocationContext_WithMinimalLambdaContext_ReturnsExactInstance()
    {
        // Arrange
        var invocationContext = Substitute.For<ILambdaInvocationContext>();
        var context = Substitute.For<IDurableContext>();
        context.LambdaContext.Returns(invocationContext);

        // Act
        var result = context.GetInvocationContext();

        // Assert
        result.Should().BeSameAs(invocationContext);
    }
}
