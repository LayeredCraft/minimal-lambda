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
                Substitute.For<IFeatureCollectionFactory>());
        };
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithNullFeatureCollectionFactory_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        var act = () =>
        {
            _ = new LambdaInvocationContextFactory(Substitute.For<IServiceScopeFactory>(), null!);
        };
        act.Should().Throw<ArgumentNullException>();
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Constructor_WithValidDependencies_SuccessfullyConstructs(
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollectionFactory featureCollectionFactory)
    {
        // Act
        var factory = new LambdaInvocationContextFactory(
            serviceScopeFactory,
            featureCollectionFactory);

        // Assert
        factory.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithNullContextAccessor_SuccessfullyConstructs()
    {
        // Act
        var factory = new LambdaInvocationContextFactory(
            Substitute.For<IServiceScopeFactory>(),
            Substitute.For<IFeatureCollectionFactory>());

        // Assert
        factory.Should().NotBeNull();
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Create_ForwardsRuntimeSerializer(
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollectionFactory featureCollectionFactory,
        ILambdaContext lambdaContext,
        IDictionary<string, object?> properties,
        ILambdaSerializer serializer)
    {
        // Arrange
        lambdaContext.Serializer.Returns(serializer);
        var factory = new LambdaInvocationContextFactory(
            serviceScopeFactory,
            featureCollectionFactory);

        // Act
        var context = factory.Create(lambdaContext, properties, CancellationToken.None);

        // Assert
        context.Serializer.Should().BeSameAs(serializer);
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Create_CallsFeatureCollectionFactoryCreate(
        [Frozen] IFeatureCollectionFactory featureCollectionFactory,
        IServiceScopeFactory serviceScopeFactory,
        ILambdaContext lambdaContext,
        IDictionary<string, object?> properties)
    {
        // Arrange
        var factory = new LambdaInvocationContextFactory(
            serviceScopeFactory,
            featureCollectionFactory);

        // Act
        _ = factory.Create(lambdaContext, properties, CancellationToken.None);

        // Assert
        featureCollectionFactory.Received(1).Create(Arg.Any<IEnumerable<IFeatureProvider>>());
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
            new DefaultFeatureCollectionFactory([]));

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
        ILambdaContext lambdaContext,
        IDictionary<string, object?> properties)
    {
        // Arrange
        var factory = new LambdaInvocationContextFactory(
            serviceScopeFactory,
            featureCollectionFactory,
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
                    providers != null
                    && providers.Count() == 2
                    && providers.Contains(eventFeatureProvider)
                    && providers.Contains(responseFeatureProvider)));
        // ReSharper restore PossibleMultipleEnumeration
    }

    private sealed record SerializerIdentityEvent(string Value);

    private sealed record SerializerIdentityResponse(string Value);
}
