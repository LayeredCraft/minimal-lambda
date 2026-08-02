using MinimalLambda.UnitTests;
using OpenTelemetry.Trace;

namespace MinimalLambda.OpenTelemetry.UnitTests;

[TestSubject(typeof(MiddlewareOpenTelemetryExtensions))]
public class MiddlewareOpenTelemetryExtensionsTest
{
    [Fact]
    public void UseOpenTelemetryTracing_WithNullBuilder_ThrowsArgumentNullException()
    {
        // Arrange
        ILambdaInvocationBuilder builder = null!;

        // Act
        Action act = () => builder.UseOpenTelemetryTracing();

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [Theory]
    [AutoNSubstituteData]
    public void UseOpenTelemetryTracing_WithNoTracerProvider_ThrowsInvalidOperationException(
        [Frozen] IServiceProvider serviceProvider,
        ILambdaInvocationBuilder builder)
    {
        // Arrange
        serviceProvider.GetService(typeof(TracerProvider)).Returns(null!);
        builder.Services.Returns(serviceProvider);

        // Act
        Action act = () => builder.UseOpenTelemetryTracing();

        // Assert
        act
            .Should()
            .ThrowExactly<InvalidOperationException>()
            .WithMessage(
                "No service for type 'OpenTelemetry.Trace.TracerProvider' has been registered.");
    }

    [Theory]
    [AutoNSubstituteData]
    public void UseOpenTelemetryTracing_RegistersMiddleware_AndReturnsBuilder(
        [Frozen] IServiceProvider serviceProvider,
        [Frozen] ILambdaInvocationBuilder builder,
        TracerProvider tracerProvider)
    {
        // Arrange
        serviceProvider.GetService(typeof(TracerProvider)).Returns(tracerProvider);
        builder.Services.Returns(serviceProvider);
        builder
            .Use(Arg.Any<Func<LambdaInvocationDelegate, LambdaInvocationDelegate>>())
            .Returns(builder);

        // Act
        var result = builder.UseOpenTelemetryTracing();

        // Assert
        result.Should().Be(builder);
        builder
            .Received(1)
            .Use(Arg.Any<Func<LambdaInvocationDelegate, LambdaInvocationDelegate>>());
    }

    [Theory]
    [AutoNSubstituteData]
    public async Task UseOpenTelemetryTracing_Middleware_CallsNextDelegate(
        [Frozen] IServiceProvider serviceProvider,
        [Frozen] ILambdaInvocationBuilder builder,
        TracerProvider tracerProvider,
        ILambdaInvocationContext context,
        IFeatureCollection features)
    {
        // Arrange
        Func<LambdaInvocationDelegate, LambdaInvocationDelegate>? capturedMiddleware = null;

        serviceProvider.GetService(typeof(TracerProvider)).Returns(tracerProvider);
        builder.Services.Returns(serviceProvider);
        builder
            .Use(
                Arg.Do<Func<LambdaInvocationDelegate, LambdaInvocationDelegate>>(m
                    => capturedMiddleware = m))
            .Returns(builder);

        context.Features.Returns(features);
        features.Get<IEventFeature>().Returns((IEventFeature?)null);
        features.Get<IResponseFeature>().Returns((IResponseFeature?)null);

        // Act
        builder.UseOpenTelemetryTracing();

        // Assert - middleware was captured
        capturedMiddleware.Should().NotBeNull();

        // Create a mock next delegate to verify it gets called
        var nextWasCalled = false;
        LambdaInvocationDelegate next = _ =>
        {
            nextWasCalled = true;
            return Task.CompletedTask;
        };

        // Execute the captured middleware
        var wrappedDelegate = capturedMiddleware!(next);
        await wrappedDelegate(context);

        // Verify the next delegate was called (wrapped by AWSLambdaWrapper.TraceAsync)
        nextWasCalled.Should().BeTrue();
    }
}
