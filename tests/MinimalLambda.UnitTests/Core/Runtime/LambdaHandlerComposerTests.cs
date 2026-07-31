using Amazon.Lambda.DurableExecution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MinimalLambda.UnitTests.Core.Runtime;

[TestSubject(typeof(LambdaHandlerComposer))]
public class LambdaHandlerComposerTests
{
    private readonly Fixture _fixture = new();

    #region Error Handling Tests

    [Fact]
    public async Task RequestHandler_PropagatesHandlerException()
    {
        // Arrange
        var expectedException = new InvalidOperationException("Test exception");
        LambdaInvocationDelegate handler = async _ =>
        {
            await Task.CompletedTask;
            throw expectedException;
        };
        _fixture.SetInvocationHandler(handler);

        var composer = _fixture.CreateComposer();
        var requestHandler = composer.CreateHandler(CancellationToken.None);

        // Act & Assert
        var act = () => requestHandler(new MemoryStream(), _fixture.LambdaContext);
        await act.Should().ThrowExactlyAsync<InvalidOperationException>();
    }

    #endregion

    /// <summary>Fixture for setting up mocks and dependencies for LambdaHandlerComposer tests.</summary>
    private class Fixture
    {
        public Fixture()
        {
            LambdaInvocationBuilderFactory = Substitute.For<ILambdaInvocationBuilderFactory>();
            CancellationFactory = Substitute.For<ILambdaCancellationFactory>();
            Options = Microsoft.Extensions.Options.Options.Create(new LambdaHostedServiceOptions());
            LambdaInvocationContextFactory = Substitute.For<ILambdaInvocationContextFactory>();
            InvocationDataFeatureFactory = Substitute.For<IInvocationDataFeatureFactory>();

            InvocationBuilder = Substitute.For<ILambdaInvocationBuilder>();
            CancellationTokenSource = new CancellationTokenSource();
            LambdaContext = Substitute.For<ILambdaContext>();
            ResponseFeature = Substitute.For<IResponseFeature>();
            Features = Substitute.For<IFeatureCollection>();
            InvocationDataFeature = Substitute.For<IInvocationDataFeature>();
            LambdaInvocationContext = Substitute.For<ILambdaInvocationContext, IAsyncDisposable>();

            SetupDefaults();
        }

        public ILambdaCancellationFactory CancellationFactory { get; }
        public CancellationTokenSource CancellationTokenSource { get; }
        public ILambdaInvocationBuilder InvocationBuilder { get; }
        public IInvocationDataFeatureFactory InvocationDataFeatureFactory { get; }
        public IFeatureCollection Features { get; }
        public IInvocationDataFeature InvocationDataFeature { get; }
        public ILambdaContext LambdaContext { get; }
        public ILambdaInvocationContext LambdaInvocationContext { get; private set; }
        public ILambdaInvocationContextFactory LambdaInvocationContextFactory { get; }
        public ILambdaInvocationBuilderFactory LambdaInvocationBuilderFactory { get; }
        public IOptions<LambdaHostedServiceOptions> Options { get; }
        public IResponseFeature ResponseFeature { get; }

        /// <summary>Sets up default mock behaviors.</summary>
        private void SetupDefaults()
        {
            InvocationBuilder.Build().Returns(_ => Task.CompletedTask);
            InvocationBuilder.Properties.Returns(new Dictionary<string, object?>());
            LambdaInvocationBuilderFactory.CreateBuilder().Returns(InvocationBuilder);

            CancellationFactory
                .NewCancellationTokenSource(Arg.Any<ILambdaContext>())
                .Returns(CancellationTokenSource);

            // Create a mock features collection
            Features.Get<IResponseFeature>().Returns(ResponseFeature);

            // Create a mock invocation data feature with response stream
            InvocationDataFeature.ResponseStream.Returns(new MemoryStream());
            InvocationDataFeatureFactory.Create(Arg.Any<Stream>()).Returns(InvocationDataFeature);

            // Set up the context factory to return the current context for any Create call
            LambdaInvocationContext.Features.Returns(Features);
            ((IAsyncDisposable)LambdaInvocationContext)
                .DisposeAsync()
                .Returns(ValueTask.CompletedTask);
            LambdaInvocationContextFactory
                .Create(
                    Arg.Any<ILambdaContext>(),
                    Arg.Any<IDictionary<string, object?>>(),
                    Arg.Any<CancellationToken>())
                .Returns(_ => LambdaInvocationContext);
        }

        /// <summary>Creates a LambdaHandlerComposer with the configured mocks.</summary>
        public LambdaHandlerComposer CreateComposer() =>
            new(
                LambdaInvocationBuilderFactory,
                CancellationFactory,
                Options,
                LambdaInvocationContextFactory,
                InvocationDataFeatureFactory);

        /// <summary>Sets the invocation handler that will be built by the builder.</summary>
        public void SetInvocationHandler(LambdaInvocationDelegate handler) =>
            InvocationBuilder.Build().Returns(handler);

        public DurableTerminalState RegisterDurableTerminal()
        {
            DurableTerminalInfrastructure.Register(InvocationBuilder);
            var state = new DurableTerminalState();
            LambdaInvocationContext = new LambdaInvocationContext(
                LambdaContext,
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<ILambdaSerializer>(),
                InvocationBuilder.Properties,
                Features,
                CancellationToken.None,
                state);
            return state;
        }

        /// <summary>Creates a fresh cancellation token source for a test.</summary>
        public CancellationTokenSource CreateNewCancellationTokenSource()
        {
            var newSource = new CancellationTokenSource();
            CancellationFactory
                .NewCancellationTokenSource(Arg.Any<ILambdaContext>())
                .Returns(newSource);
            return newSource;
        }
    }

    #region Constructor Validation Tests

    [Theory]
    [InlineData(0)] // LambdaInvocationBuilderFactory
    [InlineData(1)] // CancellationFactory
    [InlineData(2)] // Options
    [InlineData(3)] // LambdaInvocationContextFactory
    [InlineData(4)] // InvocationDataFeatureFactory
    public void Constructor_WithNullParameter_ThrowsArgumentNullException(int parameterIndex)
    {
        // Arrange
        var invocationBuilderFactory =
            parameterIndex == 0 ? null : _fixture.LambdaInvocationBuilderFactory;
        var cancellationFactory = parameterIndex == 1 ? null : _fixture.CancellationFactory;
        var options = parameterIndex == 2 ? null : _fixture.Options;
        var contextFactory = parameterIndex == 3 ? null : _fixture.LambdaInvocationContextFactory;
        var invocationDataFeatureFactory =
            parameterIndex == 4 ? null : _fixture.InvocationDataFeatureFactory;

        // Act & Assert
        var act = () => new LambdaHandlerComposer(
            invocationBuilderFactory!,
            cancellationFactory!,
            options!,
            contextFactory!,
            invocationDataFeatureFactory!);
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithValidParameters_SuccessfullyConstructs()
    {
        // Act
        var composer = _fixture.CreateComposer();

        // Assert
        composer.Should().NotBeNull();
        composer.Should().BeAssignableTo<ILambdaHandlerFactory>();
    }

    #endregion

    #region CreateHandler Method Tests

    [Fact]
    public void CreateHandler_ReturnsValidHandlerFunction()
    {
        // Arrange
        var composer = _fixture.CreateComposer();

        // Act
        var handler = composer.CreateHandler(CancellationToken.None);

        // Assert
        handler.Should().NotBeNull();
    }

    [Fact]
    public void CreateHandler_CreatesInvocationBuilder()
    {
        // Arrange
        var composer = _fixture.CreateComposer();

        // Act
        composer.CreateHandler(CancellationToken.None);

        // Assert
        _fixture.LambdaInvocationBuilderFactory.Received(1).CreateBuilder();
    }

    [Fact]
    public void CreateHandler_BuildsInvocationBuilder()
    {
        // Arrange
        var composer = _fixture.CreateComposer();

        // Act
        composer.CreateHandler(CancellationToken.None);

        // Assert
        _fixture.InvocationBuilder.Received(1).Build();
    }

    [Fact]
    public void CreateHandler_InvokesConfigureHandlerBuilder_WhenProvided()
    {
        // Arrange
        var configureHandlerBuilderInvoked = false;
        Action<ILambdaInvocationBuilder> configureAction = _ =>
        {
            configureHandlerBuilderInvoked = true;
        };

        var lambdaOptions = new LambdaHostedServiceOptions
        {
            ConfigureHandlerBuilder = configureAction,
        };
        var options = Microsoft.Extensions.Options.Options.Create(lambdaOptions);

        var composer = new LambdaHandlerComposer(
            _fixture.LambdaInvocationBuilderFactory,
            _fixture.CancellationFactory,
            options,
            _fixture.LambdaInvocationContextFactory,
            _fixture.InvocationDataFeatureFactory);

        // Act
        composer.CreateHandler(CancellationToken.None);

        // Assert
        configureHandlerBuilderInvoked.Should().BeTrue();
    }

    [Fact]
    public void CreateHandler_DoesNotInvokeConfigureHandlerBuilder_WhenNotProvided()
    {
        // Arrange
        var composer = _fixture.CreateComposer();

        // Act & Assert (should not throw)
        var handler = composer.CreateHandler(CancellationToken.None);
        handler.Should().NotBeNull();
    }

    #endregion

    #region Request Handler Behavior Tests

    [Fact]
    public async Task RequestHandler_CreatesCancellationTokenSource()
    {
        // Arrange
        var composer = _fixture.CreateComposer();
        var handler = composer.CreateHandler(CancellationToken.None);

        // Act
        await handler(new MemoryStream(), _fixture.LambdaContext);

        // Assert
        _fixture.CancellationFactory.Received(1).NewCancellationTokenSource(_fixture.LambdaContext);
    }

    [Fact]
    public async Task RequestHandler_InvokesMiddlewarePipeline()
    {
        // Arrange
        var handlerInvoked = false;
        LambdaInvocationDelegate handler = async _ =>
        {
            handlerInvoked = true;
            await Task.CompletedTask;
        };
        _fixture.SetInvocationHandler(handler);

        var composer = _fixture.CreateComposer();
        var requestHandler = composer.CreateHandler(CancellationToken.None);

        // Act
        await requestHandler(new MemoryStream(), _fixture.LambdaContext);

        // Assert
        handlerInvoked.Should().BeTrue();
    }

    [Fact]
    public async Task RequestHandler_ReturnsResponseStream()
    {
        // Arrange
        var composer = _fixture.CreateComposer();
        var handler = composer.CreateHandler(CancellationToken.None);

        // Act
        var responseStream = await handler(new MemoryStream(), _fixture.LambdaContext);

        // Assert
        responseStream.Should().NotBeNull();
        responseStream.Should().BeOfType<MemoryStream>();
    }

    [Fact]
    public async Task RequestHandler_DisposesResources_AfterInvocation()
    {
        // Arrange
        var cancellationTokenSource = _fixture.CreateNewCancellationTokenSource();
        var composer = _fixture.CreateComposer();
        var handler = composer.CreateHandler(CancellationToken.None);

        // Act
        await handler(new MemoryStream(), _fixture.LambdaContext);

        // Assert
        // After invocation, the cancellation token source should have been disposed
        var act = () => cancellationTokenSource.Token;
        act.Should().ThrowExactly<ObjectDisposedException>();
    }

    [Fact]
    public async Task RequestHandler_OrdinaryHandler_DoesNotValidateDurableTerminal()
    {
        // Arrange
        var composer = _fixture.CreateComposer();
        var handler = composer.CreateHandler(CancellationToken.None);

        // Act
        var act = () => handler(new MemoryStream(), _fixture.LambdaContext);

        // Assert
        await act.Should().NotThrowAsync();
        _fixture.ResponseFeature.Received(1).SerializeToStream(_fixture.LambdaInvocationContext);
    }

    [Fact]
    public async Task RequestHandler_DurableHandlerWithoutTerminal_FailsBeforeSerialization()
    {
        // Arrange
        _fixture.RegisterDurableTerminal();
        var composer = _fixture.CreateComposer();
        var handler = composer.CreateHandler(CancellationToken.None);

        // Act
        var act = () => handler(new MemoryStream(), _fixture.LambdaContext);

        // Assert
        await act
            .Should()
            .ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage(DurableTerminalState.MissingMessage);
        _fixture
            .ResponseFeature
            .DidNotReceive()
            .SerializeToStream(Arg.Any<ILambdaInvocationContext>());
    }

    [Fact]
    public async Task RequestHandler_DurableHandlerStillRunning_FailsBeforeSerialization()
    {
        // Arrange
        _fixture.RegisterDurableTerminal();
        _fixture.SetInvocationHandler(context =>
        {
            DurableTerminalInfrastructure.Enter(context);
            return Task.CompletedTask;
        });
        var composer = _fixture.CreateComposer();
        var handler = composer.CreateHandler(CancellationToken.None);

        // Act
        var act = () => handler(new MemoryStream(), _fixture.LambdaContext);

        // Assert
        await act
            .Should()
            .ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage(DurableTerminalState.IncompleteMessage);
        _fixture
            .ResponseFeature
            .DidNotReceive()
            .SerializeToStream(Arg.Any<ILambdaInvocationContext>());
    }

    [Fact]
    public async Task RequestHandler_CompletedDurableHandler_SerializesResponse()
    {
        // Arrange
        _fixture.RegisterDurableTerminal();
        _fixture.SetInvocationHandler(context =>
        {
            DurableTerminalInfrastructure.Enter(context);
            DurableTerminalInfrastructure.Complete(context);
            return Task.CompletedTask;
        });
        var composer = _fixture.CreateComposer();
        var handler = composer.CreateHandler(CancellationToken.None);

        // Act
        await handler(new MemoryStream(), _fixture.LambdaContext);

        // Assert
        _fixture.ResponseFeature.Received(1).SerializeToStream(_fixture.LambdaInvocationContext);
    }

    [Fact]
    public async Task RequestHandler_SwallowedDuplicateTerminal_FailsBeforeSerialization()
    {
        // Arrange
        _fixture.RegisterDurableTerminal();
        _fixture.SetInvocationHandler(context =>
        {
            DurableTerminalInfrastructure.Enter(context);
            DurableTerminalInfrastructure.Complete(context);

            var duplicate = () => DurableTerminalInfrastructure.Enter(context);
            duplicate.Should().ThrowExactly<InvalidOperationException>();
            return Task.CompletedTask;
        });
        var composer = _fixture.CreateComposer();
        var handler = composer.CreateHandler(CancellationToken.None);

        // Act
        var act = () => handler(new MemoryStream(), _fixture.LambdaContext);

        // Assert
        await act
            .Should()
            .ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage(DurableTerminalState.LifecycleViolationMessage);
        _fixture
            .ResponseFeature
            .DidNotReceive()
            .SerializeToStream(Arg.Any<ILambdaInvocationContext>());
    }

    [Fact]
    public async Task
        RequestHandler_ConcurrentDoubleTerminal_InvokesBodyOnceAndFailsBeforeSerialization()
    {
        // Arrange
        _fixture.RegisterDurableTerminal();
        var bodyEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseBody = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var bodyInvocationCount = 0;
        Exception? duplicateException = null;

        LambdaInvocationDelegate terminalNext = async context =>
        {
            DurableTerminalInfrastructure.Enter(context);
            Interlocked.Increment(ref bodyInvocationCount);
            bodyEntered.TrySetResult(true);
            await releaseBody.Task;
            DurableTerminalInfrastructure.Complete(context);
        };
        _fixture.SetInvocationHandler(async context =>
        {
            var firstExecution = terminalNext(context);
            await bodyEntered.Task;

            var duplicateExecution = CaptureExceptionAsync(terminalNext(context));
            try
            {
                duplicateException = await duplicateExecution.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                releaseBody.TrySetResult(true);
            }

            await firstExecution;
        });
        var composer = _fixture.CreateComposer();
        var handler = composer.CreateHandler(CancellationToken.None);

        // Act
        var act = () => handler(new MemoryStream(), _fixture.LambdaContext);

        // Assert
        await act
            .Should()
            .ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage(DurableTerminalState.LifecycleViolationMessage);
        duplicateException
            .Should()
            .BeOfType<InvalidOperationException>()
            .Which
            .Message
            .Should()
            .Be(DurableTerminalState.DuplicateExecutionMessage);
        bodyInvocationCount.Should().Be(1);
        _fixture
            .ResponseFeature
            .DidNotReceive()
            .SerializeToStream(Arg.Any<ILambdaInvocationContext>());
    }

    [Theory]
    [InlineData(InvocationStatus.Succeeded)]
    [InlineData(InvocationStatus.Failed)]
    [InlineData(InvocationStatus.Pending)]
    public async Task RequestHandler_DurableEnvelope_PreservesTypedResponseAndSerializes(
        InvocationStatus status)
    {
        // Arrange
        _fixture.RegisterDurableTerminal();
        var serializer = Substitute.For<ILambdaSerializer>();
        var responseFeature =
            new DefaultResponseFeature<DurableExecutionInvocationOutput>(serializer);
        _fixture.Features.Get<IResponseFeature>().Returns(responseFeature);
        _fixture.Features.Get<IInvocationDataFeature>().Returns(_fixture.InvocationDataFeature);
        var expected = new DurableExecutionInvocationOutput { Status = status };
        _fixture.SetInvocationHandler(context =>
        {
            DurableTerminalInfrastructure.Enter(context);
            responseFeature.SetResponse(expected);
            DurableTerminalInfrastructure.Complete(context);
            return Task.CompletedTask;
        });
        var composer = _fixture.CreateComposer();
        var handler = composer.CreateHandler(CancellationToken.None);

        // Act
        await handler(new MemoryStream(), _fixture.LambdaContext);

        // Assert
        responseFeature.GetResponse().Should().BeSameAs(expected);
        serializer
            .Received(1)
            .Serialize(
                Arg.Is<DurableExecutionInvocationOutput>(output =>
                    ReferenceEquals(output, expected)),
                _fixture.InvocationDataFeature.ResponseStream);
    }

    [Fact]
    public async Task RequestHandler_SwallowedTerminalBodyFailure_FailsBeforeSerialization()
    {
        // Arrange
        var terminalException = new InvalidOperationException("terminal failed");
        _fixture.RegisterDurableTerminal();
        _fixture.SetInvocationHandler(async context =>
        {
            DurableTerminalInfrastructure.Enter(context);
            try
            {
                await Task.FromException(terminalException);
            }
            catch (InvalidOperationException exception) when (ReferenceEquals(
                exception,
                terminalException))
            {
                // Simulate middleware swallowing the durable terminal body failure.
            }
        });
        var composer = _fixture.CreateComposer();
        var handler = composer.CreateHandler(CancellationToken.None);

        // Act
        var act = () => handler(new MemoryStream(), _fixture.LambdaContext);

        // Assert
        var assertion = await act
            .Should()
            .ThrowExactlyAsync<InvalidOperationException>()
            .WithMessage(DurableTerminalState.IncompleteMessage);
        assertion.Which.Should().NotBeSameAs(terminalException);
        _fixture
            .ResponseFeature
            .DidNotReceive()
            .SerializeToStream(Arg.Any<ILambdaInvocationContext>());
    }

    [Fact]
    public async Task RequestHandler_EscapingDurableTerminalException_PreservesOriginalException()
    {
        // Arrange
        var expected = new InvalidOperationException("terminal failed");
        _fixture.RegisterDurableTerminal();
        _fixture.SetInvocationHandler(context =>
        {
            DurableTerminalInfrastructure.Enter(context);
            return Task.FromException(expected);
        });
        var composer = _fixture.CreateComposer();
        var handler = composer.CreateHandler(CancellationToken.None);

        // Act
        var act = () => handler(new MemoryStream(), _fixture.LambdaContext);

        // Assert
        var assertion = await act.Should().ThrowExactlyAsync<InvalidOperationException>();
        assertion.Which.Should().BeSameAs(expected);
        _fixture
            .ResponseFeature
            .DidNotReceive()
            .SerializeToStream(Arg.Any<ILambdaInvocationContext>());
    }

    #endregion

    private static async Task<Exception?> CaptureExceptionAsync(Task task)
    {
        try
        {
            await task;
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }
}
