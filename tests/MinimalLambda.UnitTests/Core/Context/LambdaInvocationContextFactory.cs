using Microsoft.Extensions.DependencyInjection;

namespace MinimalLambda.UnitTests.Core.Context;

[TestSubject(typeof(LambdaInvocationContextFactory))]
public class LambdaInvocationContextFactoryTests
{
    [Fact]
    public void Constructor_WithNullServiceScopeFactory_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var act = () =>
        {
            _ = new LambdaInvocationContextFactory(
                null!,
                Substitute.For<IFeatureCollectionFactory>(),
                Substitute.For<ILambdaSerializer>());
        };
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullFeatureCollectionFactory_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var act = () =>
        {
            _ = new LambdaInvocationContextFactory(
                Substitute.For<IServiceScopeFactory>(),
                null!,
                Substitute.For<ILambdaSerializer>());
        };
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullLambdaSerializer_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var act = () =>
        {
            _ = new LambdaInvocationContextFactory(
                Substitute.For<IServiceScopeFactory>(),
                Substitute.For<IFeatureCollectionFactory>(),
                null!);
        };
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Constructor_WithValidDependencies_SuccessfullyConstructs(
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollectionFactory featureCollectionFactory,
        ILambdaSerializer lambdaSerializer)
    {
        // Act
        var factory = new LambdaInvocationContextFactory(
            serviceScopeFactory,
            featureCollectionFactory,
            lambdaSerializer);

        // Assert
        factory.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullContextAccessor_SuccessfullyConstructs()
    {
        // Act
        var factory = new LambdaInvocationContextFactory(
            Substitute.For<IServiceScopeFactory>(),
            Substitute.For<IFeatureCollectionFactory>(),
            Substitute.For<ILambdaSerializer>());

        // Assert
        factory.Should().NotBeNull();
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Create_CallsFeatureCollectionFactoryCreate(
        [Frozen] IFeatureCollectionFactory featureCollectionFactory,
        IServiceScopeFactory serviceScopeFactory,
        ILambdaSerializer lambdaSerializer,
        ILambdaContext lambdaContext,
        IDictionary<string, object?> properties)
    {
        // Arrange
        var factory = new LambdaInvocationContextFactory(
            serviceScopeFactory,
            featureCollectionFactory,
            lambdaSerializer);

        // Act
        _ = factory.Create(lambdaContext, properties, CancellationToken.None);

        // Assert
        featureCollectionFactory.Received(1).Create(Arg.Any<IEnumerable<IFeatureProvider>>());
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Create_UsesExactLambdaSerializer(
        [Frozen] IFeatureCollectionFactory featureCollectionFactory,
        [Frozen] ILambdaSerializer lambdaSerializer,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection,
        ILambdaContext lambdaContext)
    {
        // Arrange
        featureCollectionFactory
            .Create(Arg.Any<IEnumerable<IFeatureProvider>>())
            .Returns(featuresCollection);
        var factory = new LambdaInvocationContextFactory(
            serviceScopeFactory,
            featureCollectionFactory,
            lambdaSerializer);

        // Act
        var context = factory.Create(
            lambdaContext,
            new Dictionary<string, object?>(),
            CancellationToken.None);

        // Assert
        context.Serializer.Should().BeSameAs(lambdaSerializer);
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task Create_ForRegisteredDurableHandler_UsesIsolatedTerminalStatePerInvocation(
        [Frozen] IFeatureCollectionFactory featureCollectionFactory,
        [Frozen] ILambdaSerializer lambdaSerializer,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection,
        ILambdaContext lambdaContext,
        ILambdaInvocationBuilder builder)
    {
        // Arrange
        featureCollectionFactory
            .Create(Arg.Any<IEnumerable<IFeatureProvider>>())
            .Returns(featuresCollection);
        var properties = new Dictionary<string, object?>();
        builder.Properties.Returns(properties);
        DurableTerminalInfrastructure.Register(builder);
        var factory = new LambdaInvocationContextFactory(
            serviceScopeFactory,
            featureCollectionFactory,
            lambdaSerializer);

        var first = factory.Create(lambdaContext, properties, CancellationToken.None);
        var second = factory.Create(lambdaContext, properties, CancellationToken.None);
        var firstEntered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var firstExecution = ExecuteFirstAsync();
        await firstEntered.Task;

        // Act
        Exception? secondException = null;
        try
        {
            DurableTerminalInfrastructure.Enter(second);
            DurableTerminalInfrastructure.Complete(second);
        }
        catch (Exception exception)
        {
            secondException = exception;
        }

        releaseFirst.SetResult(true);
        Exception? firstException = null;
        try
        {
            await firstExecution;
        }
        catch (Exception exception)
        {
            firstException = exception;
        }

        // Assert
        secondException.Should().BeNull();
        firstException.Should().BeNull();
        var validation = () =>
        {
            DurableTerminalInfrastructure.Validate(first);
            DurableTerminalInfrastructure.Validate(second);
        };
        validation.Should().NotThrow();

        async Task ExecuteFirstAsync()
        {
            DurableTerminalInfrastructure.Enter(first);
            firstEntered.SetResult(true);
            await releaseFirst.Task;
            DurableTerminalInfrastructure.Complete(first);
        }
    }

    [Fact]
    public void Create_WithRealEventAndResponseProviders_UsesOneSerializerInstanceEndToEnd()
    {
        // Arrange
        var serializer = Substitute.For<ILambdaSerializer>();
        var expectedEvent = new SerializerIdentityEvent("event");
        var expectedResponse = new SerializerIdentityResponse("response");
        using var eventStream = new MemoryStream([1, 2, 3]);
        var responseStream = new MemoryStream();
        serializer.Deserialize<SerializerIdentityEvent>(eventStream).Returns(expectedEvent);
        var properties = new Dictionary<string, object?>
        {
            [LambdaInvocationBuilder.EventFeatureProviderKey] =
                new DefaultEventFeatureProvider<SerializerIdentityEvent>(serializer),
            [LambdaInvocationBuilder.ResponseFeatureProviderKey] =
                new DefaultResponseFeatureProvider<SerializerIdentityResponse>(serializer),
        };
        var factory = new LambdaInvocationContextFactory(
            Substitute.For<IServiceScopeFactory>(),
            new DefaultFeatureCollectionFactory([]),
            serializer);

        // Act
        var context = factory.Create(
            Substitute.For<ILambdaContext>(),
            properties,
            CancellationToken.None);
        context.Features.Set<IInvocationDataFeature>(
            new InvocationDataFeature
            {
                EventStream = eventStream, ResponseStream = responseStream,
            });
        var eventFeature = context.Features.GetRequired<IEventFeature>();
        var responseFeature = context.Features.GetRequired<IResponseFeature>();
        var actualEvent = ((IEventFeature<SerializerIdentityEvent>)eventFeature).GetEvent(context);
        ((IResponseFeature<SerializerIdentityResponse>)responseFeature).SetResponse(
            expectedResponse);
        responseFeature.SerializeToStream(context);

        // Assert
        context.Serializer.Should().BeSameAs(serializer);
        actualEvent.Should().BeSameAs(expectedEvent);
        serializer.Received(1).Deserialize<SerializerIdentityEvent>(eventStream);
        serializer
            .Received(1)
            .Serialize(
                Arg.Is<SerializerIdentityResponse>(response =>
                    ReferenceEquals(response, expectedResponse)),
                responseStream);
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Create_WithContextAccessor_SetsContextOnAccessor(
        [Frozen] ILambdaInvocationContextAccessor? contextAccessor,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollectionFactory featureCollectionFactory,
        ILambdaSerializer lambdaSerializer,
        ILambdaContext lambdaContext,
        IDictionary<string, object?> properties)
    {
        // Arrange
        var factory = new LambdaInvocationContextFactory(
            serviceScopeFactory,
            featureCollectionFactory,
            lambdaSerializer,
            contextAccessor);

        // Act
        _ = factory.Create(lambdaContext, properties, CancellationToken.None);

        // Assert
        contextAccessor!.LambdaInvocationContext.Should().NotBeNull();
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Create_GetsFeaturesFromProperties(
        [Frozen] IFeatureCollectionFactory featureCollectionFactory,
        IFeatureProvider eventFeatureProvider,
        IFeatureProvider responseFeatureProvider,
        ILambdaInvocationContext lambdaContext,
        LambdaInvocationContextFactory factory)
    {
        // Arrange
        var properties = new Dictionary<string, object?>
        {
            [LambdaInvocationBuilder.EventFeatureProviderKey] = eventFeatureProvider,
            [LambdaInvocationBuilder.ResponseFeatureProviderKey] = responseFeatureProvider,
        };

        // Act
        _ = factory.Create(lambdaContext, properties, CancellationToken.None);

        // Assert

        // ReSharper disable PossibleMultipleEnumeration
        featureCollectionFactory
            .Received(1)
            .Create(
                Arg.Is<IEnumerable<IFeatureProvider>>(providers =>
                    providers.Count() == 2
                    && providers.Contains(eventFeatureProvider)
                    && providers.Contains(responseFeatureProvider)));
        // ReSharper restore PossibleMultipleEnumeration
    }

    private sealed record SerializerIdentityEvent(string Value);

    private sealed record SerializerIdentityResponse(string Value);
}
