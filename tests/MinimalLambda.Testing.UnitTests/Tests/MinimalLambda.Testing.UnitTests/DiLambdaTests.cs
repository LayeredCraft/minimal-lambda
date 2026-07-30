using Autofac;
using Autofac.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MinimalLambda.UnitTests;
using NSubstitute.ExceptionExtensions;

namespace MinimalLambda.Testing.UnitTests;

public class DiLambdaTests
{
    [Fact]
    public async Task DiLambda_ReturnsExpectedValue()
    {
        await using var factory =
            new LambdaApplicationFactory<DiLambda>().WithCancellationToken(
                TestContext.Current.CancellationToken);

        var response = await factory.TestServer.InvokeAsync<DiLambdaRequest, DiLambdaResponse>(
            new DiLambdaRequest("World"),
            TestContext.Current.CancellationToken);

        response.Should().NotBeNull();
        response.WasSuccess.Should().BeTrue();
        response.Response.Should().NotBeNull();
        response.Response.Message.Should().Be("Hello World!");
    }

    [Fact]
    internal async Task DiLambda_InitStopped()
    {
        var lifecycleService = Substitute.For<ILifecycleService>();
        await using var factory = new LambdaApplicationFactory<DiLambda>()
            .WithCancellationToken(TestContext.Current.CancellationToken)
            .WithHostBuilder(builder => builder.ConfigureServices((_, services) =>
            {
                services.RemoveAll<ILifecycleService>();
                services.AddSingleton<ILifecycleService>(_ => lifecycleService);
            }));

        // due to how the dependencies are resolved within the test server, if we dont add a delay
        // here, there is a small chance that the DI container will be disposed of before the test
        // server can resolve the IHostedApplicationLifetime. This is only the issue if OnInit
        // signals that the init phase should exit.
        lifecycleService
            .OnStart()
            .Returns(false)
            .AndDoes(_ =>
                Task
                    .Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken)
                    .Wait(TestContext.Current.CancellationToken));

        var initResult = await factory.TestServer.StartAsync(TestContext.Current.CancellationToken);
        initResult.InitStatus.Should().Be(InitStatus.HostExited);
    }

    [Fact]
    internal async Task DiLambda_InitThrowsException()
    {
        var lifecycleService = Substitute.For<ILifecycleService>();

        await using var factory = new LambdaApplicationFactory<DiLambda>()
            .WithCancellationToken(TestContext.Current.CancellationToken)
            .WithHostBuilder(builder => builder.ConfigureServices((_, services) =>
            {
                services.RemoveAll<ILifecycleService>();
                services.AddSingleton<ILifecycleService>(_ => lifecycleService);
            }));

        // See DiLambda_InitStopped for why the delay is needed here
        lifecycleService
            .OnStart()
            .Throws(new Exception("Test init error"))
            .AndDoes(_ =>
                Task
                    .Delay(TimeSpan.FromMilliseconds(100), TestContext.Current.CancellationToken)
                    .Wait(TestContext.Current.CancellationToken));

        var initResult = await factory.TestServer.StartAsync(TestContext.Current.CancellationToken);
        initResult.InitStatus.Should().Be(InitStatus.InitError);
        initResult.Error.Should().NotBeNull();
        initResult
            .Error
            .ErrorMessage
            .Should()
            .Be("Encountered errors while running OnInit handlers: (Test init error)");
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task DiLambda_ShutdownThrowsException(ILifecycleService lifecycleService)
    {
        await using var factory = new LambdaApplicationFactory<DiLambda>()
            .WithCancellationToken(TestContext.Current.CancellationToken)
            .WithHostBuilder(builder => builder.ConfigureServices((_, services) =>
            {
                services.RemoveAll<ILifecycleService>();
                services.AddSingleton<ILifecycleService>(_ => lifecycleService);
            }));

        lifecycleService.OnStart().Returns(true);
        lifecycleService.When(x => x.OnStop()).Do(_ => throw new Exception("Test init error"));

        var initResult = await factory.TestServer.StartAsync(TestContext.Current.CancellationToken);
        initResult.InitStatus.Should().Be(InitStatus.InitCompleted);

        var act = async () =>
            // ReSharper disable once AccessToDisposedClosure
            await factory.TestServer.StopAsync(TestContext.Current.CancellationToken);

        (await act.Should().ThrowAsync<AggregateException>())
            .WithInnerException<AggregateException>()
            .WithInnerException<AggregateException>()
            .WithInnerException<Exception>()
            .WithMessage("Test init error");
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task DiLambda_DiContainerCanBeReplaced(
        ILifecycleService lifecycleService,
        IService service)
    {
        await using var factory = new LambdaApplicationFactory<DiLambda>()
            .WithCancellationToken(TestContext.Current.CancellationToken)
            .WithHostBuilder(builder => builder
                .ConfigureContainer<ContainerBuilder>((_, containerBuilder) =>
                {
                    containerBuilder.RegisterInstance(service).As<IService>();
                    containerBuilder.RegisterInstance(lifecycleService).As<ILifecycleService>();
                })
                .UseServiceProviderFactory(new AutofacServiceProviderFactory()));

        service.GetMessage(Arg.Any<string>()).Returns("Hello Bob!");

        var response = await factory.TestServer.InvokeAsync<DiLambdaRequest, DiLambdaResponse>(
            new DiLambdaRequest("World"),
            TestContext.Current.CancellationToken);

        response.Should().NotBeNull();
        response.WasSuccess.Should().BeTrue();
        response.Response.Should().NotBeNull();
        response.Response.Message.Should().Be("Hello Bob!");
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task DiLambda_DiContainerCanBeReplacedWithFactory(
        ILifecycleService lifecycleService,
        IService service)
    {
        await using var factory = new LambdaApplicationFactory<DiLambda>()
            .WithCancellationToken(TestContext.Current.CancellationToken)
            .WithHostBuilder(builder => builder
                .ConfigureContainer<ContainerBuilder>((_, containerBuilder) =>
                {
                    containerBuilder.RegisterInstance(service).As<IService>();
                    containerBuilder.RegisterInstance(lifecycleService).As<ILifecycleService>();
                })
                .UseServiceProviderFactory(_ => new AutofacServiceProviderFactory()));

        service.GetMessage(Arg.Any<string>()).Returns("Hello Joe!");

        var response = await factory.TestServer.InvokeAsync<DiLambdaRequest, DiLambdaResponse>(
            new DiLambdaRequest("World"),
            TestContext.Current.CancellationToken);

        response.Should().NotBeNull();
        response.WasSuccess.Should().BeTrue();
        response.Response.Should().NotBeNull();
        response.Response.Message.Should().Be("Hello Joe!");
    }
}
