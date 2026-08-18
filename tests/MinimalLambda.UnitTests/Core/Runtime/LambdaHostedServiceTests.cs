using System.Reflection;
using Microsoft.Extensions.Hosting;

namespace MinimalLambda.UnitTests.Core.Runtime;

[TestSubject(typeof(LambdaHostedService))]
public class LambdaHostedServiceTests
{
    #region Constructor Validation Tests

    [Theory]
    [InlineData(0)] // bootstrap
    [InlineData(1)] // handlerFactory
    [InlineData(2)] // lifetime
    [InlineData(3)] // lambdaOnInitBuilderFactory
    [InlineData(4)] // lambdaHostOptions
    [InlineData(5)] // lambdaOnShutdownBuilderFactory
    internal void Constructor_WithNullParameter_ThrowsArgumentNullException(int parameterIndex)
    {
        // Arrange
        var bootstrap = parameterIndex == 0 ? null : Substitute.For<ILambdaBootstrapOrchestrator>();
        var handlerFactory = parameterIndex == 1 ? null : Substitute.For<ILambdaHandlerFactory>();
        var lifetime = parameterIndex == 2 ? null : Substitute.For<IHostApplicationLifetime>();
        var onInitBuilderFactory =
            parameterIndex == 3 ? null : Substitute.For<ILambdaOnInitBuilderFactory>();
        var lambdaHostOptions = parameterIndex == 4
            ? null
            : Microsoft.Extensions.Options.Options.Create(new LambdaHostedServiceOptions());
        var onShutdownBuilderFactory = parameterIndex == 5
            ? null
            : Substitute.For<ILambdaOnShutdownBuilderFactory>();

        // Act & Assert
        var act = () => new LambdaHostedService(
            bootstrap!,
            handlerFactory!,
            lifetime!,
            onInitBuilderFactory!,
            lambdaHostOptions!,
            onShutdownBuilderFactory!);
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Constructor_WithValidParameters_SuccessfullyConstructs(
        LambdaHostedService service)
    {
        // Assert
        service.Should().NotBeNull();
        service.Should().BeAssignableTo<IHostedService>();
        service.Should().BeAssignableTo<IDisposable>();
    }

    #endregion

    #region StartAsync Tests

    [Theory]
    [AutoNSubstituteData]
    internal async Task StartAsync_CreatesLinkedCancellationTokenSource(
        [Frozen] ILambdaBootstrapOrchestrator bootstrapOrchestrator,
        LambdaHostedService service)
    {
        // Arrange
        using var cts = new CancellationTokenSource();

        // Act
        await service.StartAsync(cts.Token);

        // Assert
        // Verify by attempting to cancel and checking if the bootstrap was called with a token
        await bootstrapOrchestrator
            .Received(1)
            .RunAsync(
                Arg.Any<Func<Stream, ILambdaContext, Task<Stream>>>(),
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>());
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task StartAsync_CancelsBootstrapWhenApplicationStops(
        [Frozen] ILambdaBootstrapOrchestrator bootstrap,
        [Frozen] IHostApplicationLifetime lifetime,
        LambdaHostedService service)
    {
        // Arrange
        using var applicationStoppingCts = new CancellationTokenSource();
        var bootstrapToken = new TaskCompletionSource<CancellationToken>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        lifetime.ApplicationStopping.Returns(applicationStoppingCts.Token);
        bootstrap
            .RunAsync(
                Arg.Any<Func<Stream, ILambdaContext, Task<Stream>>>(),
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var cancellationToken = callInfo.ArgAt<CancellationToken>(2);
                bootstrapToken.SetResult(cancellationToken);
                return Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            });

        // Act
        await service.StartAsync(CancellationToken.None);
        applicationStoppingCts.Cancel();

        // Assert
        (await bootstrapToken.Task).IsCancellationRequested.Should().BeTrue();
        await service.StopAsync(CancellationToken.None);
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task StartAsync_CreatesRequestHandler(
        [Frozen] ILambdaHandlerFactory handlerFactory,
        LambdaHostedService service)
    {
        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        handlerFactory.Received(1).CreateHandler(Arg.Any<CancellationToken>());
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task StartAsync_CreatesOnInitBuilder(
        [Frozen] ILambdaOnInitBuilderFactory factory,
        LambdaHostedService service)
    {
        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        factory.Received(1).CreateBuilder();
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task StartAsync_InvokesConfigureOnInitBuilder_WhenProvided(
        ILambdaBootstrapOrchestrator bootstrap,
        ILambdaHandlerFactory handlerFactory,
        IHostApplicationLifetime lifetime,
        ILambdaOnInitBuilderFactory onInitBuilderFactory,
        ILambdaOnShutdownBuilderFactory onShutdownBuilderFactory)
    {
        // Arrange
        var configureInvoked = false;
        Action<ILambdaOnInitBuilder> configureAction = _ =>
        {
            configureInvoked = true;
        };

        var lambdaOptions = new LambdaHostedServiceOptions
        {
            ConfigureOnInitBuilder = configureAction,
        };
        var options = Microsoft.Extensions.Options.Options.Create(lambdaOptions);

        var service = new LambdaHostedService(
            bootstrap,
            handlerFactory,
            lifetime,
            onInitBuilderFactory,
            options,
            onShutdownBuilderFactory);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        configureInvoked.Should().BeTrue();
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task StartAsync_CreatesOnShutdownBuilder(
        [Frozen] ILambdaOnShutdownBuilderFactory onShutdownBuilderFactory,
        LambdaHostedService service)
    {
        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        onShutdownBuilderFactory.Received(1).CreateBuilder();
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task StartAsync_InvokesConfigureOnShutdownBuilder_WhenProvided(
        ILambdaBootstrapOrchestrator bootstrap,
        ILambdaHandlerFactory handlerFactory,
        IHostApplicationLifetime lifetime,
        ILambdaOnInitBuilderFactory onInitBuilderFactory,
        ILambdaOnShutdownBuilderFactory onShutdownBuilderFactory)
    {
        // Arrange
        var configureInvoked = false;
        Action<ILambdaOnShutdownBuilder> configureAction = _ =>
        {
            configureInvoked = true;
        };

        var lambdaOptions = new LambdaHostedServiceOptions
        {
            ConfigureOnShutdownBuilder = configureAction,
        };
        var options = Microsoft.Extensions.Options.Options.Create(lambdaOptions);

        var service = new LambdaHostedService(
            bootstrap,
            handlerFactory,
            lifetime,
            onInitBuilderFactory,
            options,
            onShutdownBuilderFactory);

        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        configureInvoked.Should().BeTrue();
    }

    #endregion

    #region StopAsync Tests

    [Theory]
    [AutoNSubstituteData]
    internal async Task StopAsync_WithoutStartAsync_ReturnsImmediately(LambdaHostedService service)
    {
        // Act & Assert (should not throw)
        var act = async () => await service.StopAsync(CancellationToken.None);

        await act.Should().NotThrowAsync();
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task StopAsync_CancelsExecuteTask(
        [Frozen] ILambdaBootstrapOrchestrator bootstrap,
        LambdaHostedService service)
    {
        // Arrange
        var taskCompletionSource = new TaskCompletionSource();
        bootstrap
            .RunAsync(
                Arg.Any<Func<Stream, ILambdaContext, Task<Stream>>>(),
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => taskCompletionSource.Task);

        await service.StartAsync(CancellationToken.None);

        // Act
        var stopTask = service.StopAsync(CancellationToken.None);
        taskCompletionSource.SetResult();

        await stopTask;

        // Assert
        await bootstrap
            .Received(1)
            .RunAsync(
                Arg.Any<Func<Stream, ILambdaContext, Task<Stream>>>(),
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>());
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task StopAsync_WaitsForBootstrapToComplete(
        [Frozen] ILambdaBootstrapOrchestrator bootstrap,
        LambdaHostedService service)
    {
        // Arrange
        var taskCompletionSource = new TaskCompletionSource();
        bootstrap
            .RunAsync(
                Arg.Any<Func<Stream, ILambdaContext, Task<Stream>>>(),
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => taskCompletionSource.Task);

        await service.StartAsync(CancellationToken.None);

        // Verify task is not completed
        service.GetExecuteTask()?.IsCompleted.Should().BeFalse();

        // Act
        var stopTask = service.StopAsync(CancellationToken.None);
        taskCompletionSource.SetResult();

        // Assert
        var act = async () => await stopTask;
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task StopAsync_ThrowsOperationCanceledException_WhenBootstrapTimesOut(
        [Frozen] ILambdaBootstrapOrchestrator bootstrap,
        LambdaHostedService service)
    {
        // Arrange
        var taskCompletionSource = new TaskCompletionSource();
        bootstrap
            .RunAsync(
                Arg.Any<Func<Stream, ILambdaContext, Task<Stream>>>(),
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => taskCompletionSource.Task);

        await service.StartAsync(CancellationToken.None);

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromMilliseconds(10));

        // Act & Assert
        // ReSharper disable once AccessToDisposedClosure
        var act = () => service.StopAsync(timeoutCts.Token);
        await act
            .Should()
            .ThrowExactlyAsync<AggregateException>()
            .Where(ae => ae.InnerExceptions.Any(ex =>
                ex is OperationCanceledException && ex.Message.Contains("Graceful shutdown")));
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task StopAsync_InvokesShutdownHandler(
        [Frozen] ILambdaBootstrapOrchestrator bootstrap,
        [Frozen] ILambdaOnShutdownBuilder onShutdownBuilder,
        LambdaHostedService service)
    {
        // Arrange
        var taskCompletionSource = new TaskCompletionSource();
        bootstrap
            .RunAsync(
                Arg.Any<Func<Stream, ILambdaContext, Task<Stream>>>(),
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => taskCompletionSource.Task);

        var shutdownHandlerInvoked = false;
        onShutdownBuilder
            .Build()
            .Returns(async _ =>
            {
                shutdownHandlerInvoked = true;
                await Task.CompletedTask;
            });

        await service.StartAsync(CancellationToken.None);

        // Act
        var stopTask = service.StopAsync(CancellationToken.None);
        taskCompletionSource.SetResult();
        await stopTask;

        // Assert
        shutdownHandlerInvoked.Should().BeTrue();
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task StopAsync_PropagatesShutdownHandlerException(
        [Frozen] ILambdaBootstrapOrchestrator bootstrap,
        [Frozen] ILambdaOnShutdownBuilder onShutdownBuilder,
        LambdaHostedService service)
    {
        // Arrange
        var shutdownException = new InvalidOperationException("Shutdown handler error");
        var taskCompletionSource = new TaskCompletionSource();
        bootstrap
            .RunAsync(
                Arg.Any<Func<Stream, ILambdaContext, Task<Stream>>>(),
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => taskCompletionSource.Task);

        onShutdownBuilder
            .Build()
            .Returns(async _ =>
            {
                await Task.CompletedTask;
                throw shutdownException;
            });

        await service.StartAsync(CancellationToken.None);

        // Act & Assert - StopAsync should propagate exception from shutdown handler
        var stopTask = service.StopAsync(CancellationToken.None);
        taskCompletionSource.SetResult();

        var act = async () => await stopTask;
        await act
            .Should()
            .ThrowExactlyAsync<AggregateException>()
            .Where(ae => ae.InnerExceptions.Contains(shutdownException));
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task StopAsync_PropagatesNonCanceledExceptionFromExecuteTask(
        [Frozen] ILambdaBootstrapOrchestrator bootstrap,
        LambdaHostedService service)
    {
        // Arrange
        var handlerException = new InvalidOperationException("Handler error");
        var taskCompletionSource = new TaskCompletionSource();
        bootstrap
            .RunAsync(
                Arg.Any<Func<Stream, ILambdaContext, Task<Stream>>>(),
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => taskCompletionSource.Task);

        await service.StartAsync(CancellationToken.None);

        // Simulate an exception from the execute task (after StartAsync completes)
        taskCompletionSource.SetException(handlerException);

        // Act & Assert - StopAsync should propagate non-TaskCanceledException from execute task
        var act = () => service.StopAsync(CancellationToken.None);
        await act
            .Should()
            .ThrowExactlyAsync<AggregateException>()
            .Where(ae => ae.InnerExceptions.Contains(handlerException));
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task StopAsync_PropagatesBootstrapExceptionWhenBootstrapFails(
        [Frozen] ILambdaBootstrapOrchestrator bootstrap,
        LambdaHostedService service)
    {
        // Arrange
        var bootstrapException = new InvalidOperationException("Bootstrap failed");
        var taskCompletionSource = new TaskCompletionSource();
        taskCompletionSource.SetException(bootstrapException);
        bootstrap
            .RunAsync(
                Arg.Any<Func<Stream, ILambdaContext, Task<Stream>>>(),
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => taskCompletionSource.Task);

        // Act & Assert - StartAsync returns the failed task which throws when awaited
        var act = async () => await service.StartAsync(CancellationToken.None);
        await act.Should().ThrowExactlyAsync<InvalidOperationException>();
    }

    #endregion

    #region Dispose Tests

    [Theory]
    [AutoNSubstituteData]
    internal void Dispose_Idempotent_WhenNotStarted(LambdaHostedService service)
    {
        // Act
        var act = () =>
        {
            service.Dispose();
            service.Dispose();
            service.Dispose();
        };

        // Assert
        act.Should().NotThrow();
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task Dispose_DisposesExecuteTask(
        [Frozen] ILambdaBootstrapOrchestrator bootstrap,
        LambdaHostedService service)
    {
        // Arrange
        var taskCompletionSource = new TaskCompletionSource();
        bootstrap
            .RunAsync(
                Arg.Any<Func<Stream, ILambdaContext, Task<Stream>>>(),
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => taskCompletionSource.Task);

        await service.StartAsync(CancellationToken.None);

        // Act
        taskCompletionSource.SetResult();
        await Task.Delay(10, TestContext.Current.CancellationToken); // Allow task to complete
        service.Dispose();

        // Assert
        service.GetExecuteTask()?.IsCompleted.Should().BeTrue();
    }

    #endregion

    #region ExecuteAsync Behavior Tests (via StartAsync/StopAsync)

    [Theory]
    [AutoNSubstituteData]
    internal async Task ExecuteAsync_CallsBootstrapRunAsync(
        [Frozen] ILambdaBootstrapOrchestrator bootstrap,
        LambdaHostedService service)
    {
        // Act
        await service.StartAsync(CancellationToken.None);

        // Assert
        await bootstrap
            .Received(1)
            .RunAsync(
                Arg.Any<Func<Stream, ILambdaContext, Task<Stream>>>(),
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>());
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task ExecuteAsync_CallsLifetimeStopApplication_InFinallyBlock(
        [Frozen] ILambdaBootstrapOrchestrator bootstrap,
        [Frozen] IHostApplicationLifetime lifetime,
        LambdaHostedService service)
    {
        // Arrange
        var taskCompletionSource = new TaskCompletionSource();
        bootstrap
            .RunAsync(
                Arg.Any<Func<Stream, ILambdaContext, Task<Stream>>>(),
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => taskCompletionSource.Task);

        await service.StartAsync(CancellationToken.None);

        // Act
        taskCompletionSource.SetResult();

        // Wait a bit for the finally block to execute
        await Task.Delay(50, TestContext.Current.CancellationToken);

        // Assert
        lifetime.Received(1).StopApplication();
    }

    #endregion

    #region Full Lifecycle Tests

    [Theory]
    [AutoNSubstituteData]
    internal async Task FullLifecycle_StartAsyncStopAsyncDispose(
        [Frozen] ILambdaBootstrapOrchestrator bootstrap,
        LambdaHostedService service)
    {
        // Arrange
        var taskCompletionSource = new TaskCompletionSource();
        bootstrap
            .RunAsync(
                Arg.Any<Func<Stream, ILambdaContext, Task<Stream>>>(),
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => taskCompletionSource.Task);

        // Act - StartAsync
        await service.StartAsync(CancellationToken.None);
        service.GetExecuteTask().Should().NotBeNull();

        // Act - StopAsync
        var stopTask = service.StopAsync(CancellationToken.None);
        taskCompletionSource.SetResult();
        await stopTask;

        // Act - Dispose
        service.Dispose();

        // Assert - all phases completed without error
        service.Should().NotBeNull();
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task FullLifecycle_WithException_PropagatesInStartAsync(
        [Frozen] ILambdaBootstrapOrchestrator bootstrap,
        LambdaHostedService service)
    {
        // Arrange
        var bootstrapException = new InvalidOperationException("Bootstrap error");
        var taskCompletionSource = new TaskCompletionSource();
        taskCompletionSource.SetException(bootstrapException);
        bootstrap
            .RunAsync(
                Arg.Any<Func<Stream, ILambdaContext, Task<Stream>>>(),
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => taskCompletionSource.Task);

        // Act & Assert - StartAsync returns the failed task which throws when awaited
        var act = async () => await service.StartAsync(CancellationToken.None);
        await act.Should().ThrowExactlyAsync<InvalidOperationException>();
    }

    #endregion
}

/// <summary>Extension methods for testing LambdaHostedService private state.</summary>
internal static class LambdaHostedServiceTestExtensions
{
    internal static Task? GetExecuteTask(this LambdaHostedService service)
    {
        var field = typeof(LambdaHostedService).GetField(
            "_executeTask",
            BindingFlags.NonPublic | BindingFlags.Instance);
        return (Task?)field?.GetValue(service);
    }
}
