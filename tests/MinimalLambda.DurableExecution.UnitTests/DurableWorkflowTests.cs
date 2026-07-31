using Amazon.Lambda.DurableExecution.Testing;
using Microsoft.Extensions.Logging;

namespace MinimalLambda.DurableExecution.UnitTests;

/// <summary>
/// Exercises AWS durable workflow semantics through public local-runner seams.
/// Generated MinimalLambda adapter, middleware, serializer, and outer-envelope coverage lives in
/// MinimalLambda.Testing.UnitTests.DurableLambdaTests; these tests do not claim combined host/replay E2E.
/// </summary>
public class DurableWorkflowTests
{
    [Fact]
    public async Task Workflow_Succeeds()
    {
        var stepProbe = new StepExecutionProbe();
        await using var runner = CreateRunner(stepProbe);

        var result = await runner.RunAsync(
            "order-42",
            cancellationToken: TestContext.Current.CancellationToken);

        result.EnsureSucceeded();
        result.Result.Should().Be("completed-order-42");
        result.GetStep("load-order").GetResult<string>().Should().Be("order-42");
    }

    [Fact]
    public async Task Workflow_FailingStep_ReturnsFailure()
    {
        await using var runner = new DurableTestRunner<string, string>(async (_, context) =>
        {
            await context.StepAsync<string>(
                async (_, _) =>
                {
                    await Task.CompletedTask;
                    throw new InvalidOperationException("expected workflow failure");
                },
                name: "failing-step");
            return "unreachable";
        });

        var result = await runner.RunAsync(
            "ignored",
            cancellationToken: TestContext.Current.CancellationToken);

        result.IsFailed.Should().BeTrue();
        result.Error.Should().NotBeNull();
        result.Error!.ErrorType.Should().Be(typeof(InvalidOperationException).FullName);
        result.Error.ErrorMessage.Should().Contain("expected workflow failure");
        result.GetStep("failing-step").Status.Should().Be(OperationStatus.Failed);
    }

    [Fact]
    public async Task Workflow_ReplaySkipsCompletedStepBody()
    {
        var stepProbe = new StepExecutionProbe();
        var workflowInvocationCount = 0;
        await using var runner = CreateRunner(
            stepProbe,
            onWorkflowInvocation: () => workflowInvocationCount++);

        var result = await runner.RunAsync(
            "order-42",
            cancellationToken: TestContext.Current.CancellationToken);

        result.EnsureSucceeded();
        workflowInvocationCount.Should().BeGreaterThan(1);
        stepProbe.Count.Should().Be(1);
    }

    [Fact]
    public async Task Workflow_WaitSuspendsAndResumes_WithSkippedTime()
    {
        await using var runner = CreateRunner(new StepExecutionProbe());

        var result = await runner.RunAsync(
            "order-42",
            cancellationToken: TestContext.Current.CancellationToken);

        result.EnsureSucceeded();
        result.InvocationCount.Should().NotBeNull();
        result.InvocationCount!.Value.Should().BeGreaterThan(1);
        var wait = result.GetStep("approval-window");
        wait.Kind.Should().Be(OperationKind.Wait);
        wait.Status.Should().Be(OperationStatus.Succeeded);
    }

    [Fact]
    public async Task Workflow_ReplaySafeLogger_EmitsEachMessageOnce()
    {
        var logger = new CapturingLogger();
        await using var runner = CreateRunner(new StepExecutionProbe(), logger);

        var result = await runner.RunAsync(
            "order-42",
            cancellationToken: TestContext.Current.CancellationToken);

        result.EnsureSucceeded();
        result.InvocationCount.Should().NotBeNull();
        result.InvocationCount!.Value.Should().BeGreaterThan(1);
        logger.Messages.Should().Equal("workflow-start", "step-body", "after-step", "after-wait");
    }

    private static DurableTestRunner<string, string> CreateRunner(
        StepExecutionProbe stepProbe,
        CapturingLogger? logger = null,
        Action? onWorkflowInvocation = null) =>
        new(
            async (input, context) =>
            {
                onWorkflowInvocation?.Invoke();
                if (logger is not null)
                    context.ConfigureLogger(new LoggerConfig { CustomLogger = logger });

                context.Logger.LogInformation("workflow-start");
                var order = await context.StepAsync(
                    async (stepContext, _) =>
                    {
                        stepProbe.Record();
                        stepContext.Logger.LogInformation("step-body");
                        await Task.CompletedTask;
                        return input;
                    },
                    name: "load-order");
                context.Logger.LogInformation("after-step");

                await context.WaitAsync(TimeSpan.FromDays(1), name: "approval-window");
                context.Logger.LogInformation("after-wait");
                return $"completed-{order}";
            },
            new TestRunnerOptions { SkipTime = true });

    private sealed class StepExecutionProbe
    {
        public int Count { get; private set; }

        public void Record() => Count++;
    }

    private sealed class CapturingLogger : ILogger
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Messages.Add(formatter(state, exception));
    }
}
