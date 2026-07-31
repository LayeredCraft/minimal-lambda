using Microsoft.Extensions.DependencyInjection;

namespace MinimalLambda.UnitTests.Core.Runtime;

[TestSubject(typeof(DurableTerminalInfrastructure))]
public class DurableTerminalInfrastructureTests
{
    [Fact]
    public void Register_WithNullBuilder_ThrowsArgumentNullException()
    {
        // Act
        var act = () => DurableTerminalInfrastructure.Register(null!);

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Register_IsIdempotent(ILambdaInvocationBuilder builder)
    {
        // Arrange
        var properties = new Dictionary<string, object?>();
        builder.Properties.Returns(properties);

        // Act
        DurableTerminalInfrastructure.Register(builder);
        DurableTerminalInfrastructure.Register(builder);

        // Assert
        DurableTerminalInfrastructure.IsRegistered(properties).Should().BeTrue();
        properties.Should().ContainSingle();
    }

    [Fact]
    public void Enter_WithNullContext_ThrowsArgumentNullException()
    {
        // Act
        var act = () => DurableTerminalInfrastructure.Enter(null!);

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [Fact]
    public void Complete_WithNullContext_ThrowsArgumentNullException()
    {
        // Act
        var act = () => DurableTerminalInfrastructure.Complete(null!);

        // Assert
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Enter_WithoutRegisteredState_ThrowsInvalidOperationException(
        ILambdaInvocationContext context)
    {
        // Act
        var act = () => DurableTerminalInfrastructure.Enter(context);

        // Assert
        act.Should().ThrowExactly<InvalidOperationException>().WithMessage("*not registered*");
    }

    [Fact]
    public void EnterAndComplete_ThenValidate_Succeeds()
    {
        // Arrange
        var state = new DurableTerminalState();
        var context = CreateContext(state);

        // Act
        DurableTerminalInfrastructure.Enter(context);
        DurableTerminalInfrastructure.Complete(context);
        var act = () => DurableTerminalInfrastructure.Validate(context);

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_BeforeEnter_ThrowsMissingException()
    {
        // Arrange
        var context = CreateContext(new DurableTerminalState());

        // Act
        var act = () => DurableTerminalInfrastructure.Validate(context);

        // Assert
        act
            .Should()
            .ThrowExactly<InvalidOperationException>()
            .WithMessage(DurableTerminalState.MissingMessage);
    }

    [Fact]
    public void Validate_AfterEnterWithoutComplete_ThrowsIncompleteException()
    {
        // Arrange
        var context = CreateContext(new DurableTerminalState());
        DurableTerminalInfrastructure.Enter(context);

        // Act
        var act = () => DurableTerminalInfrastructure.Validate(context);

        // Assert
        act
            .Should()
            .ThrowExactly<InvalidOperationException>()
            .WithMessage(DurableTerminalState.IncompleteMessage);
    }

    [Fact]
    public void SequentialDuplicateEnter_PreventsSecondBodyAndLeavesStickyViolation()
    {
        // Arrange
        var context = CreateContext(new DurableTerminalState());
        var bodyInvocationCount = 0;

        DurableTerminalInfrastructure.Enter(context);
        bodyInvocationCount++;
        DurableTerminalInfrastructure.Complete(context);

        // Act
        var secondExecution = () =>
        {
            DurableTerminalInfrastructure.Enter(context);
            bodyInvocationCount++;
        };

        // Assert
        secondExecution
            .Should()
            .ThrowExactly<InvalidOperationException>()
            .WithMessage(DurableTerminalState.DuplicateExecutionMessage);
        bodyInvocationCount.Should().Be(1);

        var validation = () => DurableTerminalInfrastructure.Validate(context);
        validation
            .Should()
            .ThrowExactly<InvalidOperationException>()
            .WithMessage(DurableTerminalState.LifecycleViolationMessage);
    }

    [Fact]
    public async Task ConcurrentDuplicateEnter_StartsOneBodyAndLeavesStickyViolation()
    {
        // Arrange
        var context = CreateContext(new DurableTerminalState());
        var firstBodyStarted = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstBody = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var bodyInvocationCount = 0;

        var firstExecution = ExecuteFirstAsync();
        await firstBodyStarted.Task;

        // Act
        Exception? secondException = null;
        try
        {
            DurableTerminalInfrastructure.Enter(context);
            Interlocked.Increment(ref bodyInvocationCount);
        }
        catch (Exception exception)
        {
            secondException = exception;
        }

        releaseFirstBody.SetResult(true);
        var firstException = await CaptureExceptionAsync(firstExecution);

        // Assert
        secondException
            .Should()
            .BeOfType<InvalidOperationException>()
            .Which
            .Message
            .Should()
            .Be(DurableTerminalState.DuplicateExecutionMessage);
        firstException
            .Should()
            .BeOfType<InvalidOperationException>()
            .Which
            .Message
            .Should()
            .Be(DurableTerminalState.LifecycleViolationMessage);
        bodyInvocationCount.Should().Be(1);

        var validation = () => DurableTerminalInfrastructure.Validate(context);
        validation
            .Should()
            .ThrowExactly<InvalidOperationException>()
            .WithMessage(DurableTerminalState.LifecycleViolationMessage);

        async Task ExecuteFirstAsync()
        {
            DurableTerminalInfrastructure.Enter(context);
            Interlocked.Increment(ref bodyInvocationCount);
            firstBodyStarted.SetResult(true);
            await releaseFirstBody.Task;
            DurableTerminalInfrastructure.Complete(context);
        }
    }

    [Fact]
    public void Complete_AfterViolation_DoesNotOverwriteViolation()
    {
        // Arrange
        var context = CreateContext(new DurableTerminalState());
        DurableTerminalInfrastructure.Enter(context);
        var duplicate = () => DurableTerminalInfrastructure.Enter(context);
        duplicate.Should().ThrowExactly<InvalidOperationException>();

        // Act
        var act = () => DurableTerminalInfrastructure.Complete(context);

        // Assert
        act
            .Should()
            .ThrowExactly<InvalidOperationException>()
            .WithMessage(DurableTerminalState.LifecycleViolationMessage);

        var validation = () => DurableTerminalInfrastructure.Validate(context);
        validation
            .Should()
            .ThrowExactly<InvalidOperationException>()
            .WithMessage(DurableTerminalState.LifecycleViolationMessage);
    }

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

    private static LambdaInvocationContext CreateContext(DurableTerminalState state) =>
        new(
            Substitute.For<ILambdaContext>(),
            Substitute.For<IServiceScopeFactory>(),
            Substitute.For<ILambdaSerializer>(),
            new Dictionary<string, object?>(),
            Substitute.For<IFeatureCollection>(),
            CancellationToken.None,
            state);
}
