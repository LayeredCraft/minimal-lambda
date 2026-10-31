using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace MinimalLambda.EventProcessing.UnitTests;

public sealed class EventHandlerFactoryTests
{
    [Fact]
    public async Task DefaultsNeedNoRegistrationAndCallbacksAreLazy()
    {
        await using var app = LambdaApplication.CreateBuilder().Build();
        var called = false;
        var terminal = EventHandlerFactory.Create<int[], int>(
            app.Services,
            (batch, _) =>
            {
                called = true;
                return batch;
            },
            (_, _, _) => Task.CompletedTask);
        terminal.Should().NotBeNull();
        called.Should().BeFalse();
    }

    [Fact]
    public void RejectsInvalidOptionsBeforeCallbacks()
    {
        using var services =
            new ServiceCollection()
                .AddOptions()
                .Configure<EventHandlerOptions>(options => options.MaxConcurrentExecutions = 0)
                .BuildServiceProvider();
        var create = () => EventHandlerFactory.Create<int[], int>(
            services,
            (batch, _) => throw new Exception("split must not run"),
            (_, _, _) => Task.CompletedTask);
        create.Should().Throw<OptionsValidationException>();
    }

    [Fact]
    public void PreservesConfigurationException()
    {
        var original = new InvalidOperationException("configuration");
        using var services =
            new ServiceCollection()
                .AddOptions()
                .Configure<EventHandlerOptions>(_ => throw original)
                .BuildServiceProvider();
        var create = () => EventHandlerFactory.Create<int[], int>(
            services,
            (batch, _) => batch,
            (_, _, _) => Task.CompletedTask);
        create.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(original);
    }

    [Fact]
    public void RejectsNullCallbacks()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var create = () =>
            EventHandlerFactory.Create<int[], int>(
                services,
                null!,
                (_, _, _) => Task.CompletedTask);
        create.Should().Throw<ArgumentNullException>().WithParameterName("split");
    }

    [Fact]
    public async Task NullOriginalHandlerFailsBeforeInstallingTerminal()
    {
        await using var app = LambdaApplication.CreateBuilder().Build();
        var map = () => app.MapEventHandler(
            split: (string[] batch, ILambdaInvocationContext _) => batch,
            handle: NullHandler.Value);
        map.Should().Throw<ArgumentNullException>().WithParameterName("handle");
        var normal = () => app.MapHandler(() => Task.CompletedTask);
        normal.Should().NotThrow();
    }

    [Fact]
    public async Task MappingPreservesCustomFeatureProviders()
    {
        await using var app = LambdaApplication.CreateBuilder().Build();
        var invocationBuilder = (ILambdaInvocationBuilder)app;
        var eventProvider = Substitute.For<IFeatureProvider>();
        var responseProvider = Substitute.For<IFeatureProvider>();
        invocationBuilder.Properties["__EventFeatureProvider"] = eventProvider;
        invocationBuilder.Properties["__ResponseFeatureProvider"] = responseProvider;
        app.MapEventHandler(
            split: (string[] batch, ILambdaInvocationContext _) => batch,
            handle: (string _) => Task.CompletedTask,
            createResponse: (outcomes, _) => "response");
        invocationBuilder.Properties["__EventFeatureProvider"].Should().BeSameAs(eventProvider);
        invocationBuilder
            .Properties["__ResponseFeatureProvider"]
            .Should()
            .BeSameAs(responseProvider);
    }

    internal static class NullHandler
    {
        public static readonly Func<string, Task> Value = Handle;
        static NullHandler() => Value = null!;
        private static Task Handle(string _) => Task.CompletedTask;
    }
}
