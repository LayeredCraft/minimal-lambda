using Microsoft.Extensions.DependencyInjection;

namespace MinimalLambda.UnitTests.Core.Context;

[TestSubject(typeof(LambdaInvocationContext))]
public class LambdaInvocationContextTests
{
    [Theory]
    [AutoNSubstituteData]
    internal void Constructor_WithNullLambdaContext_ThrowsArgumentNullException(
        Dictionary<string, object?> properties,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection,
        CancellationToken cancellationToken)
    {
        // Act & Assert
        var act = () => new LambdaInvocationContext(
            null!,
            serviceScopeFactory,
            properties,
            featuresCollection,
            cancellationToken);
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Constructor_WithNullServiceScopeFactory_ThrowsArgumentNullException(
        Dictionary<string, object?> properties,
        ILambdaContext lambdaContext,
        IFeatureCollection featuresCollection,
        CancellationToken cancellationToken)
    {
        // Act & Assert
        var act = () => new LambdaInvocationContext(
            lambdaContext,
            null!,
            properties,
            featuresCollection,
            cancellationToken);
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Constructor_WithNullProperties_ThrowsArgumentNullException(
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection,
        CancellationToken cancellationToken)
    {
        // Act & Assert
        var act = () => new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            null!,
            featuresCollection,
            cancellationToken);
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Constructor_WithNullFeaturesCollection_ThrowsArgumentNullException(
        Dictionary<string, object?> properties,
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        CancellationToken cancellationToken)
    {
        // Act & Assert
        var act = () => new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            properties,
            null!,
            cancellationToken);
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Constructor_WithValidParameters_SuccessfullyConstructs(
        Dictionary<string, object?> properties,
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection,
        CancellationToken cancellationToken)
    {
        // Act
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            properties,
            featuresCollection,
            cancellationToken);

        // Assert
        context.Should().NotBeNull();
    }

    [Theory]
    [AutoNSubstituteData]
    internal void AwsRequestId_ReturnsValueFromDelegatedContext(
        string expectedValue,
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        lambdaContext.AwsRequestId.Returns(expectedValue);
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);

        // Act
        var result = context.AwsRequestId;

        // Assert
        result.Should().Be(expectedValue);
    }

    [Theory]
    [AutoNSubstituteData]
    internal void ClientContext_ReturnsValueFromDelegatedContext(
        IClientContext expectedValue,
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        lambdaContext.ClientContext.Returns(expectedValue);
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);

        // Act
        var result = context.ClientContext;

        // Assert
        result.Should().Be(expectedValue);
    }

    [Theory]
    [AutoNSubstituteData]
    internal void FunctionName_ReturnsValueFromDelegatedContext(
        string expectedValue,
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        lambdaContext.FunctionName.Returns(expectedValue);
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);

        // Act
        var result = context.FunctionName;

        // Assert
        result.Should().Be(expectedValue);
    }

    [Theory]
    [AutoNSubstituteData]
    internal void FunctionVersion_ReturnsValueFromDelegatedContext(
        string expectedValue,
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        lambdaContext.FunctionVersion.Returns(expectedValue);
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);

        // Act
        var result = context.FunctionVersion;

        // Assert
        result.Should().Be(expectedValue);
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Identity_ReturnsValueFromDelegatedContext(
        ICognitoIdentity expectedValue,
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        lambdaContext.Identity.Returns(expectedValue);
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);

        // Act
        var result = context.Identity;

        // Assert
        result.Should().Be(expectedValue);
    }

    [Theory]
    [AutoNSubstituteData]
    internal void InvokedFunctionArn_ReturnsValueFromDelegatedContext(
        string expectedValue,
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        lambdaContext.InvokedFunctionArn.Returns(expectedValue);
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);

        // Act
        var result = context.InvokedFunctionArn;

        // Assert
        result.Should().Be(expectedValue);
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Logger_ReturnsValueFromDelegatedContext(
        ILambdaLogger expectedValue,
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        lambdaContext.Logger.Returns(expectedValue);
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);

        // Act
        var result = context.Logger;

        // Assert
        result.Should().Be(expectedValue);
    }

    [Theory]
    [AutoNSubstituteData]
    internal void LogGroupName_ReturnsValueFromDelegatedContext(
        string expectedValue,
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        lambdaContext.LogGroupName.Returns(expectedValue);
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);

        // Act
        var result = context.LogGroupName;

        // Assert
        result.Should().Be(expectedValue);
    }

    [Theory]
    [AutoNSubstituteData]
    internal void LogStreamName_ReturnsValueFromDelegatedContext(
        string expectedValue,
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        lambdaContext.LogStreamName.Returns(expectedValue);
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);

        // Act
        var result = context.LogStreamName;

        // Assert
        result.Should().Be(expectedValue);
    }

    [Theory]
    [AutoNSubstituteData]
    internal void MemoryLimitInMB_ReturnsValueFromDelegatedContext(
        int expectedValue,
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        lambdaContext.MemoryLimitInMB.Returns(expectedValue);
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);

        // Act
        var result = context.MemoryLimitInMB;

        // Assert
        result.Should().Be(expectedValue);
    }

    [Theory]
    [AutoNSubstituteData]
    internal void RemainingTime_ReturnsValueFromDelegatedContext(
        TimeSpan expectedValue,
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        lambdaContext.RemainingTime.Returns(expectedValue);
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);

        // Act
        var result = context.RemainingTime;

        // Assert
        result.Should().Be(expectedValue);
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Properties_ReturnsPropertiesDictionaryPassedToConstructor(
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        var propertiesDict =
            new Dictionary<string, object?> { { "key1", "value1" }, { "key2", 42 } };
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            propertiesDict,
            featuresCollection,
            CancellationToken.None);

        // Act
        var result = context.Properties;

        // Assert
        result.Should().BeEquivalentTo(propertiesDict);
        result.Should().BeSameAs(propertiesDict);
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Items_ReturnsEmptyDictionaryInitially(
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange & Act
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);

        // Assert
        context.Items.Should().NotBeNull();
        context.Items.Should().BeEmpty();
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Items_AllowsAddingAndRetrievingValues(
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);
        var key = new object();
        var value = "test-value";

        // Act
        context.Items[key] = value;
        var result = context.Items[key];

        // Assert
        result.Should().Be(value);
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Items_AllowsClearingValues(
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);
        context.Items["key1"] = "value1";
        context.Items["key2"] = "value2";

        // Act
        context.Items.Clear();

        // Assert
        context.Items.Should().BeEmpty();
    }

    [Theory]
    [AutoNSubstituteData]
    internal void CancellationToken_ReturnsCancellationTokenPassedToConstructor(
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        var expectedToken = new CancellationToken();
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            expectedToken);

        // Act
        var result = context.CancellationToken;

        // Assert
        result.Should().Be(expectedToken);
    }

    [Theory]
    [AutoNSubstituteData]
    internal void Features_ReturnsFeaturesCollectionPassedToConstructor(
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);

        // Act
        var result = context.Features;

        // Assert
        result.Should().BeSameAs(featuresCollection);
    }

    [Theory]
    [AutoNSubstituteData]
    internal void ServiceProvider_IsCreatedOnFirstAccess(
        ILambdaContext lambdaContext,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        var mockScope = Substitute.For<IServiceScope>();
        var mockServiceProvider = Substitute.For<IServiceProvider>();
        mockScope.ServiceProvider.Returns(mockServiceProvider);

        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        serviceScopeFactory.CreateScope().Returns(mockScope);

        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);

        // Act
        var result = context.ServiceProvider;

        // Assert
        result.Should().NotBeNull();
        result.Should().BeSameAs(mockServiceProvider);
        serviceScopeFactory.Received(1).CreateScope();
    }

    [Theory]
    [AutoNSubstituteData]
    internal void ServiceProvider_ReturnsSameScopeOnSubsequentAccess(
        ILambdaContext lambdaContext,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        var mockScope = Substitute.For<IServiceScope>();
        var mockServiceProvider = Substitute.For<IServiceProvider>();
        mockScope.ServiceProvider.Returns(mockServiceProvider);

        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        serviceScopeFactory.CreateScope().Returns(mockScope);

        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);

        // Act
        var result1 = context.ServiceProvider;
        var result2 = context.ServiceProvider;
        var result3 = context.ServiceProvider;

        // Assert
        result1.Should().BeSameAs(result2);
        result2.Should().BeSameAs(result3);
        serviceScopeFactory.Received(1).CreateScope();
    }

    [Theory]
    [AutoNSubstituteData]
    internal void ServiceProvider_UsesServiceScopeFactory(
        ILambdaContext lambdaContext,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        var mockScope = Substitute.For<IServiceScope>();
        serviceScopeFactory.CreateScope().Returns(mockScope);

        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);

        // Act
        _ = context.ServiceProvider;

        // Assert
        serviceScopeFactory.Received(1).CreateScope();
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task DisposeAsync_DisposesServiceScopeWhenAsyncDisposable(
        ILambdaContext lambdaContext,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        var asyncDisposableScope = Substitute.For<IServiceScope, IAsyncDisposable>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        serviceScopeFactory.CreateScope().Returns(asyncDisposableScope);

        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);
        _ = context.ServiceProvider; // Trigger lazy initialization

        // Act
        await context.DisposeAsync();

        // Assert
        await ((IAsyncDisposable)asyncDisposableScope).Received(1).DisposeAsync();
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task DisposeAsync_DisposesServiceScopeSynchronouslyWhenNotAsyncDisposable(
        ILambdaContext lambdaContext,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        var mockScope = Substitute.For<IServiceScope>();
        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        serviceScopeFactory.CreateScope().Returns(mockScope);

        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);
        _ = context.ServiceProvider; // Trigger lazy initialization

        // Act
        await context.DisposeAsync();

        // Assert
        mockScope.Received(1).Dispose();
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task DisposeAsync_ClearsItemsCollection(
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);
        context.Items["key1"] = "value1";
        context.Items["key2"] = "value2";

        // Act
        await context.DisposeAsync();

        // Assert
        context.Items.Should().BeEmpty();
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task DisposeAsync_DisposesServiceScopeBeforeNullingServiceProvider(
        ILambdaContext lambdaContext,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        var mockScope = Substitute.For<IServiceScope>();
        var mockServiceProvider = Substitute.For<IServiceProvider>();
        mockScope.ServiceProvider.Returns(mockServiceProvider);

        var serviceScopeFactory = Substitute.For<IServiceScopeFactory>();
        serviceScopeFactory.CreateScope().Returns(mockScope);

        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);
        _ = context.ServiceProvider; // Trigger initialization

        // Act
        await context.DisposeAsync();

        // Assert
        // Verify the scope was disposed
        mockScope.Received(1).Dispose();
    }

    [Theory]
    [AutoNSubstituteData]
    internal async Task DisposeAsync_CanBeCalledMultipleTimes(
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);

        // Act
        var act = async () =>
        {
            await context.DisposeAsync();
            await context.DisposeAsync(); // Should not throw
        };

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Theory]
    [AutoNSubstituteData]
    internal void ContextImplementsIAsyncDisposable(
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange & Act
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);

        // Assert
        context.Should().BeAssignableTo<IAsyncDisposable>();
    }

    [Theory]
    [AutoNSubstituteData]
    internal void ContextImplementsILambdaInvocationContext(
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange & Act
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);

        // Assert
        context.Should().BeAssignableTo<ILambdaInvocationContext>();
    }

    [Theory]
    [AutoNSubstituteData]
    internal void ContextImplementsILambdaContext(
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange & Act
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);

        // Assert
        context.Should().BeAssignableTo<ILambdaContext>();
    }

    [Theory]
    [AutoNSubstituteData]
    internal void MultipleContextInstances_AreIndependent(
        ILambdaContext lambdaContext1,
        ILambdaContext lambdaContext2,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        var context1 = new LambdaInvocationContext(
            lambdaContext1,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);
        var context2 = new LambdaInvocationContext(
            lambdaContext2,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);

        context1.Items["key"] = "value1";
        context2.Items["key"] = "value2";

        // Act & Assert
        context1.Items["key"].Should().Be("value1");
        context2.Items["key"].Should().Be("value2");
    }

    [Theory]
    [AutoNSubstituteData]
    internal void FeaturesCollection_CanBeUsedToStoreAndRetrieveFeaturesViaItems(
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            new Dictionary<string, object?>(),
            featuresCollection,
            CancellationToken.None);
        var featureType = typeof(string);
        var featureValue = "test-feature";

        // Act
        context.Items[featureType] = featureValue;
        var retrievedValue = context.Items[featureType];

        // Assert
        retrievedValue.Should().Be(featureValue);
    }

    [Theory]
    [AutoNSubstituteData]
    internal void PropertiesDictionary_PersistsAcrossMultipleInvocations(
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollection featuresCollection)
    {
        // Arrange
        var properties = new Dictionary<string, object?> { { "persistent", "value" } };
        var context = new LambdaInvocationContext(
            lambdaContext,
            serviceScopeFactory,
            properties,
            featuresCollection,
            CancellationToken.None);

        // Act
        properties["persistent"] = "updated-value";
        var result = context.Properties["persistent"];

        // Assert
        result.Should().Be("updated-value");
    }
}
