using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace MinimalLambda.OpenTelemetry.UnitTests;

[TestSubject(typeof(OnShutdownOpenTelemetryExtensions))]
public class OnShutdownOpenTelemetryExtensionsTests
{
    #region OnShutdownFlushTracer Tests

    [Fact]
    public void OnShutdownFlushTracer_ThrowsOnNullILambdaApplication()
    {
        // Arrange
        ILambdaOnShutdownBuilder mockApp = null!;

        // Act
        Action act = () => mockApp.OnShutdownFlushTracer();

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [Fact]
    public void OnShutdownFlushTracer_ThrowsOnNoTracerProviderRegistered()
    {
        // Arrange
        var mockApp = Substitute.For<ILambdaOnShutdownBuilder>();
        var mockServiceProvider = Substitute.For<IServiceProvider>();
        mockServiceProvider.GetService(typeof(TracerProvider)).Returns(null);
        mockApp.Services.Returns(mockServiceProvider);

        // Act
        Action act = () => mockApp.OnShutdownFlushTracer();

        // Assert
        act
            .Should()
            .ThrowExactly<InvalidOperationException>()
            .WithMessage(
                "No service for type 'OpenTelemetry.Trace.TracerProvider' has been registered.");
    }

    [Fact]
    public void OnShutdownFlushTracer_ShouldNotThrowOnNoILoggerFactoryRegistered()
    {
        // Arrange
        var mockApp = Substitute.For<ILambdaOnShutdownBuilder>();
        var mockServiceProvider = Substitute.For<IServiceProvider>();
        mockServiceProvider
            .GetService(typeof(TracerProvider))
            .Returns(Substitute.For<TracerProvider>());
        mockServiceProvider.GetService(typeof(ILoggerFactory)).Returns(null);
        mockApp.Services.Returns(mockServiceProvider);

        // Act
        Action act = () => mockApp.OnShutdownFlushTracer();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task OnShutdownFlushTracer_RegistersShutdownHandler_AndSuccessfullyFlushes()
    {
        // Arrange
        LambdaShutdownDelegate? capturedShutdownAction = null;
        var mockApp = CreateMockApp(a => capturedShutdownAction = a);

        var mockContext = Substitute.For<ILambdaLifecycleContext>();

        // Act
        var result = mockApp.OnShutdownFlushTracer();

        capturedShutdownAction.Should().NotBeNull();
        await capturedShutdownAction.Invoke(mockContext);

        // Assert
        result.Should().Be(mockApp);
        mockApp.Received(1).OnShutdown(Arg.Any<LambdaShutdownDelegate>());
        mockApp
            .Services
            .GetFakeLogCollector()
            .GetSnapshot()
            .Should()
            .ContainEquivalentOf(
                new
                {
                    Level = LogLevel.Information,
                    Category = "MinimalLambda.OpenTelemetry",
                    Message = "OpenTelemetry tracer provider force flush succeeded",
                });
    }

    [Fact]
    public async Task OnShutdownFlushTracer_RegistersShutdownHandler_AndFailedFlushes()
    {
        // Arrange
        LambdaShutdownDelegate? capturedShutdownAction = null;
        var mockApp = CreateMockApp(
            a => capturedShutdownAction = a,
            options => options.TracerShouldSucceed = false);

        var mockContext = Substitute.For<ILambdaLifecycleContext>();

        // Act
        var result = mockApp.OnShutdownFlushTracer();

        capturedShutdownAction.Should().NotBeNull();
        await capturedShutdownAction.Invoke(mockContext);

        // Assert
        result.Should().Be(mockApp);
        mockApp.Received(1).OnShutdown(Arg.Any<LambdaShutdownDelegate>());
        mockApp
            .Services
            .GetFakeLogCollector()
            .GetSnapshot()
            .Should()
            .ContainEquivalentOf(
                new
                {
                    Level = LogLevel.Information,
                    Category = "MinimalLambda.OpenTelemetry",
                    Message = "OpenTelemetry tracer provider force flush failed",
                });
    }

    [Fact]
    public async Task OnShutdownFlushTracer_RegistersShutdownHandler_AndCanceledFlushes()
    {
        // Arrange
        LambdaShutdownDelegate? capturedShutdownAction = null;
        var mockApp = CreateMockApp(
            a => capturedShutdownAction = a,
            options => options.TracerDelay = TimeSpan.FromMilliseconds(10));

        var mockContext = Substitute.For<ILambdaLifecycleContext>();
        mockContext.CancellationToken.Returns(new CancellationToken(true));

        // Act
        var result = mockApp.OnShutdownFlushTracer();

        capturedShutdownAction.Should().NotBeNull();
        await capturedShutdownAction.Invoke(mockContext);

        // Assert
        result.Should().Be(mockApp);
        mockApp.Received(1).OnShutdown(Arg.Any<LambdaShutdownDelegate>());
        mockApp
            .Services
            .GetFakeLogCollector()
            .GetSnapshot()
            .Should()
            .ContainEquivalentOf(
                new
                {
                    Level = LogLevel.Warning,
                    Category = "MinimalLambda.OpenTelemetry",
                    Message =
                        "OpenTelemetry tracer provider force flush failed to complete within allocated time",
                });
    }

    #endregion

    #region OnShutdownFlushMeter Tests

    [Fact]
    public void OnShutdownFlushMeter_ThrowsOnNullILambdaApplication()
    {
        // Arrange
        ILambdaOnShutdownBuilder mockApp = null!;

        // Act
        Action act = () => mockApp.OnShutdownFlushMeter();

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [Fact]
    public void OnShutdownFlushMeter_ThrowsOnNoMeterProviderRegistered()
    {
        // Arrange
        var mockApp = Substitute.For<ILambdaOnShutdownBuilder>();
        var mockServiceProvider = Substitute.For<IServiceProvider>();
        mockServiceProvider.GetService(typeof(MeterProvider)).Returns(null);
        mockApp.Services.Returns(mockServiceProvider);

        // Act
        Action act = () => mockApp.OnShutdownFlushMeter();

        // Assert
        act
            .Should()
            .ThrowExactly<InvalidOperationException>()
            .WithMessage(
                "No service for type 'OpenTelemetry.Metrics.MeterProvider' has been registered.");
    }

    [Fact]
    public void OnShutdownFlushMeter_ShouldNotThrowOnNoILoggerFactoryRegistered()
    {
        // Arrange
        var mockApp = Substitute.For<ILambdaOnShutdownBuilder>();
        var mockServiceProvider = Substitute.For<IServiceProvider>();
        mockServiceProvider
            .GetService(typeof(MeterProvider))
            .Returns(Substitute.For<MeterProvider>());
        mockServiceProvider.GetService(typeof(ILoggerFactory)).Returns(null);
        mockApp.Services.Returns(mockServiceProvider);

        // Act
        Action act = () => mockApp.OnShutdownFlushMeter();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public async Task OnShutdownFlushMeter_RegistersShutdownHandler_AndSuccessfullyFlushes()
    {
        // Arrange
        LambdaShutdownDelegate? capturedShutdownAction = null;
        var mockApp = CreateMockApp(a => capturedShutdownAction = a);

        var mockContext = Substitute.For<ILambdaLifecycleContext>();

        // Act
        var result = mockApp.OnShutdownFlushMeter();

        capturedShutdownAction.Should().NotBeNull();
        await capturedShutdownAction.Invoke(mockContext);

        // Assert
        result.Should().Be(mockApp);
        mockApp.Received(1).OnShutdown(Arg.Any<LambdaShutdownDelegate>());
        mockApp
            .Services
            .GetFakeLogCollector()
            .GetSnapshot()
            .Should()
            .ContainEquivalentOf(
                new
                {
                    Level = LogLevel.Information,
                    Category = "MinimalLambda.OpenTelemetry",
                    Message = "OpenTelemetry meter provider force flush succeeded",
                });
    }

    [Fact]
    public async Task OnShutdownFlushMeter_RegistersShutdownHandler_AndFailedFlushes()
    {
        // Arrange
        LambdaShutdownDelegate? capturedShutdownAction = null;
        var mockApp = CreateMockApp(
            a => capturedShutdownAction = a,
            options => options.MeterShouldSucceed = false);

        var mockContext = Substitute.For<ILambdaLifecycleContext>();
        // Act
        var result = mockApp.OnShutdownFlushMeter();

        capturedShutdownAction.Should().NotBeNull();
        await capturedShutdownAction.Invoke(mockContext);

        // Assert
        result.Should().Be(mockApp);
        mockApp.Received(1).OnShutdown(Arg.Any<LambdaShutdownDelegate>());
        mockApp
            .Services
            .GetFakeLogCollector()
            .GetSnapshot()
            .Should()
            .ContainEquivalentOf(
                new
                {
                    Level = LogLevel.Information,
                    Category = "MinimalLambda.OpenTelemetry",
                    Message = "OpenTelemetry meter provider force flush failed",
                });
    }

    [Fact]
    public async Task OnShutdownFlushMeter_RegistersShutdownHandler_AndCanceledFlushes()
    {
        // Arrange
        LambdaShutdownDelegate? capturedShutdownAction = null;
        var mockApp = CreateMockApp(
            a => capturedShutdownAction = a,
            options => options.MeterDelay = TimeSpan.FromMilliseconds(10));

        var mockContext = Substitute.For<ILambdaLifecycleContext>();
        mockContext.CancellationToken.Returns(new CancellationToken(true));

        // Act
        var result = mockApp.OnShutdownFlushMeter();

        capturedShutdownAction.Should().NotBeNull();
        await capturedShutdownAction.Invoke(mockContext);

        // Assert
        result.Should().Be(mockApp);
        mockApp.Received(1).OnShutdown(Arg.Any<LambdaShutdownDelegate>());
        mockApp
            .Services
            .GetFakeLogCollector()
            .GetSnapshot()
            .Should()
            .ContainEquivalentOf(
                new
                {
                    Level = LogLevel.Warning,
                    Category = "MinimalLambda.OpenTelemetry",
                    Message =
                        "OpenTelemetry meter provider force flush failed to complete within allocated time",
                });
    }

    #endregion

    #region OnShutdownFlushOpenTelemetry Tests

    [Fact]
    public void OnShutdownFlushOpenTelemetry_ThrowsOnNullILambdaApplication()
    {
        // Arrange
        ILambdaOnShutdownBuilder mockApp = null!;

        // Act
        Action act = () => mockApp.OnShutdownFlushOpenTelemetry();

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [Fact]
    public void OnShutdownFlushOpenTelemetry_ReturnsApplicationForChaining()
    {
        // Arrange
        var mockApp = CreateMockApp(_ => { });

        // Act
        var result = mockApp.OnShutdownFlushOpenTelemetry();

        // Assert
        result.Should().Be(mockApp);
    }

    [Fact]
    public void OnShutdownFlushOpenTelemetry_RegistersBothFlushHandlers()
    {
        // Arrange
        var mockApp = CreateMockApp(_ => { });

        // Act
        mockApp.OnShutdownFlushOpenTelemetry();

        // Assert
        mockApp.Received(2).OnShutdown(Arg.Any<LambdaShutdownDelegate>());
    }

    [Fact]
    public void OnShutdownFlushOpenTelemetry_PassesTimeoutToHandlers()
    {
        // Arrange
        var mockApp = CreateMockApp(_ => { });
        const int customTimeout = 5000;

        // Act - Should not throw when passing custom timeout
        Action act = () => mockApp.OnShutdownFlushOpenTelemetry(customTimeout);

        // Assert
        act.Should().NotThrow();
        mockApp.Received(2).OnShutdown(Arg.Any<LambdaShutdownDelegate>());
    }

    [Fact]
    public async Task OnShutdownFlushOpenTelemetry_BothFlushesBothSucceed()
    {
        // Arrange
        var shutdownActions = new List<LambdaShutdownDelegate>();
        var mockApp = CreateMockApp(a => shutdownActions.Add(a));

        var mockContext = Substitute.For<ILambdaLifecycleContext>();

        // Act
        var result = mockApp.OnShutdownFlushOpenTelemetry();

        shutdownActions.Should().HaveCount(2);
        await shutdownActions[0].Invoke(mockContext);
        await shutdownActions[1].Invoke(mockContext);

        // Assert
        result.Should().Be(mockApp);
        mockApp
            .Services
            .GetFakeLogCollector()
            .GetSnapshot()
            .Should()
            .ContainEquivalentOf(
                new
                {
                    Level = LogLevel.Information,
                    Category = "MinimalLambda.OpenTelemetry",
                    Message = "OpenTelemetry meter provider force flush succeeded",
                })
            .And
            .ContainEquivalentOf(
                new
                {
                    Level = LogLevel.Information,
                    Category = "MinimalLambda.OpenTelemetry",
                    Message = "OpenTelemetry tracer provider force flush succeeded",
                });
    }

    [Fact]
    public async Task OnShutdownFlushOpenTelemetry_TracerFailsButMeterSucceeds()
    {
        // Arrange
        var shutdownActions = new List<LambdaShutdownDelegate>();
        var mockApp = CreateMockApp(
            a => shutdownActions.Add(a),
            options => options.TracerShouldSucceed = false);

        var mockContext = Substitute.For<ILambdaLifecycleContext>();

        // Act
        var result = mockApp.OnShutdownFlushOpenTelemetry();

        shutdownActions.Should().HaveCount(2);
        await shutdownActions[0].Invoke(mockContext);
        await shutdownActions[1].Invoke(mockContext);

        // Assert
        result.Should().Be(mockApp);
        mockApp
            .Services
            .GetFakeLogCollector()
            .GetSnapshot()
            .Should()
            .ContainEquivalentOf(
                new
                {
                    Level = LogLevel.Information,
                    Category = "MinimalLambda.OpenTelemetry",
                    Message = "OpenTelemetry meter provider force flush succeeded",
                })
            .And
            .ContainEquivalentOf(
                new
                {
                    Level = LogLevel.Information,
                    Category = "MinimalLambda.OpenTelemetry",
                    Message = "OpenTelemetry tracer provider force flush failed",
                });
    }

    [Fact]
    public async Task OnShutdownFlushOpenTelemetry_MeterFailsButTracerSucceeds()
    {
        // Arrange
        var shutdownActions = new List<LambdaShutdownDelegate>();
        var mockApp = CreateMockApp(
            a => shutdownActions.Add(a),
            options => options.MeterShouldSucceed = false);

        var mockContext = Substitute.For<ILambdaLifecycleContext>();

        // Act
        var result = mockApp.OnShutdownFlushOpenTelemetry();

        shutdownActions.Should().HaveCount(2);
        await shutdownActions[0].Invoke(mockContext);
        await shutdownActions[1].Invoke(mockContext);

        // Assert
        result.Should().Be(mockApp);
        mockApp
            .Services
            .GetFakeLogCollector()
            .GetSnapshot()
            .Should()
            .ContainEquivalentOf(
                new
                {
                    Level = LogLevel.Information,
                    Category = "MinimalLambda.OpenTelemetry",
                    Message = "OpenTelemetry meter provider force flush failed",
                })
            .And
            .ContainEquivalentOf(
                new
                {
                    Level = LogLevel.Information,
                    Category = "MinimalLambda.OpenTelemetry",
                    Message = "OpenTelemetry tracer provider force flush succeeded",
                });
    }

    [Fact]
    public async Task OnShutdownFlushOpenTelemetry_BothFlushesTimeout()
    {
        // Arrange
        var shutdownActions = new List<LambdaShutdownDelegate>();
        var mockApp = CreateMockApp(
            a => shutdownActions.Add(a),
            options =>
            {
                options.MeterDelay = TimeSpan.FromMilliseconds(10);
                options.TracerDelay = TimeSpan.FromMilliseconds(10);
            });

        var mockContext = Substitute.For<ILambdaLifecycleContext>();
        mockContext.CancellationToken.Returns(new CancellationToken(true));

        // Act
        var result = mockApp.OnShutdownFlushOpenTelemetry();

        shutdownActions.Should().HaveCount(2);
        await shutdownActions[0].Invoke(mockContext);
        await shutdownActions[1].Invoke(mockContext);

        // Assert
        result.Should().Be(mockApp);
        mockApp
            .Services
            .GetFakeLogCollector()
            .GetSnapshot()
            .Should()
            .ContainEquivalentOf(
                new
                {
                    Level = LogLevel.Warning,
                    Category = "MinimalLambda.OpenTelemetry",
                    Message =
                        "OpenTelemetry meter provider force flush failed to complete within allocated time",
                })
            .And
            .ContainEquivalentOf(
                new
                {
                    Level = LogLevel.Warning,
                    Category = "MinimalLambda.OpenTelemetry",
                    Message =
                        "OpenTelemetry tracer provider force flush failed to complete within allocated time",
                });
    }

    #endregion

    #region Test Helpers

    private static ILambdaOnShutdownBuilder CreateMockApp(
        Action<LambdaShutdownDelegate> onShutdown,
        Action<MockOptions>? configureOptions = null)
    {
        var options = new MockOptions();
        configureOptions?.Invoke(options);

        var mockApp = Substitute.For<ILambdaOnShutdownBuilder>();

        mockApp.OnShutdown(Arg.Do(onShutdown)).Returns(mockApp);

        var serviceCollection = new ServiceCollection();

        serviceCollection
            .AddOpenTelemetry()
            .WithTracing(builder =>
            {
                builder.AddProcessor(
                    new MockProcessor<Activity>(options.TracerDelay, options.TracerShouldSucceed));
            })
            .WithMetrics(builder =>
            {
                builder.AddReader(
                    new MockMetricReader(options.MeterDelay, options.MeterShouldSucceed));
            });

        serviceCollection.AddLogging(builder =>
        {
            builder.AddFakeLogging();
        });

        var serviceProvider = serviceCollection.BuildServiceProvider();

        mockApp.Services.Returns(serviceProvider);

        return mockApp;
    }

    private class MockOptions
    {
        public TimeSpan MeterDelay { get; set; } = TimeSpan.Zero;
        public bool MeterShouldSucceed { get; set; } = true;
        public TimeSpan TracerDelay { get; set; } = TimeSpan.Zero;
        public bool TracerShouldSucceed { get; set; } = true;
    }

    private class MockProcessor<T>(TimeSpan delay, bool shouldSucceed) : BaseProcessor<T>
    {
        protected override bool OnForceFlush(int timeoutMilliseconds)
        {
            Thread.Sleep(delay);
            return shouldSucceed;
        }
    }

    private class MockMetricReader(TimeSpan delay, bool shouldSucceed) : MetricReader
    {
        protected override bool OnCollect(int timeoutMilliseconds)
        {
            Thread.Sleep(delay);
            return shouldSucceed;
        }
    }

    #endregion
}
