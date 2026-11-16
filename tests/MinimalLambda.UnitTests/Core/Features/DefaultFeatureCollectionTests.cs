using System.Collections;

// ReSharper disable UnusedAutoPropertyAccessor.Local UnusedVariable

namespace MinimalLambda.UnitTests.Core.Features;

[TestSubject(typeof(DefaultFeatureCollection))]
public class DefaultFeatureCollectionTest
{
    #region Helper Methods

    /// <summary>Creates a DefaultFeatureCollection with optional feature providers.</summary>
    private static DefaultFeatureCollection CreateDefaultFeatureCollection(
        IEnumerable<IFeatureProvider>? featureProviders = null)
    {
        featureProviders ??= [];
        return new DefaultFeatureCollection(featureProviders);
    }

    #endregion

    #region Test Fixtures

    private class TestFeature
    {
        public string Value { get; set; } = "";
    }

    #endregion

    #region Constructor Validation Tests

    [Fact]
    public void Constructor_WithNullFeatureProviders_ThrowsArgumentNullException()
    {
        // Act & Assert
        var act = () => new DefaultFeatureCollection(null!);
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [Fact]
    public void Constructor_WithEmptyFeatureProviders_SuccessfullyConstructs()
    {
        // Act
        var collection = CreateDefaultFeatureCollection([]);

        // Assert
        collection.Should().NotBeNull();
    }

    [Theory]
    [AutoNSubstituteData]
    public void Constructor_WithValidFeatureProviders_SuccessfullyConstructs(
        IFeatureProvider provider1,
        IFeatureProvider provider2)
    {
        // Act
        var collection = new DefaultFeatureCollection(new[] { provider1, provider2 });

        // Assert
        collection.Should().NotBeNull();
    }

    #endregion

    #region Set<T> Method Tests

    [Fact]
    public void Set_StoresFeatureByType()
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection();
        var feature = "test-feature";

        // Act
        collection.Set(feature);

        // Assert
        collection.Get<string>().Should().Be(feature);
    }

    [Fact]
    public void Set_OverwritesExistingFeatureOfSameType()
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection();
        var feature1 = "feature1";
        var feature2 = "feature2";

        collection.Set(feature1);

        // Act
        collection.Set(feature2);

        // Assert
        collection.Get<string>().Should().Be(feature2);
    }

    [Fact]
    public void Set_WithNullInstance_ThrowsArgumentNullException()
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection();

        // Act & Assert
        var act = () => collection.Set<string>(null!);
        act.Should().ThrowExactly<ArgumentNullException>();
    }

    [Theory]
    [AutoData]
    public void Set_CanStoreStringFeature(string feature)
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection();

        // Act
        collection.Set(feature);

        // Assert
        collection.Get<string>().Should().Be(feature);
    }

    [Theory]
    [AutoData]
    public void Set_CanStoreIntFeature(int feature)
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection();

        // Act
        collection.Set(feature);

        // Assert
        collection.Get<int>().Should().Be(feature);
    }

    [Fact]
    public void Set_CanStoreInterfaceFeature()
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection();
        var mockInterface = Substitute.For<IFeatureProvider>();

        // Act
        collection.Set(mockInterface);

        // Assert
        collection.Get<IFeatureProvider>().Should().BeSameAs(mockInterface);
    }

    [Fact]
    public void Set_CanStoreCustomObjectFeature()
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection();
        var customObject = new TestFeature { Value = "test" };

        // Act
        collection.Set(customObject);

        // Assert
        collection.Get<TestFeature>().Should().BeSameAs(customObject);
    }

    #endregion

    #region Get<T> Method Tests

    [Fact]
    public void Get_ReturnsFeatureThatWasSet()
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection();
        var feature = "test-feature";
        collection.Set(feature);

        // Act
        var result = collection.Get<string>();

        // Assert
        result.Should().Be(feature);
    }

    [Fact]
    public void Get_ReturnsNullWhenFeatureNotFound()
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection();

        // Act
        var result = collection.Get<string>();

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [AutoNSubstituteData]
    public void Get_ReturnsFeatureFromProviderWhenNotCached(IFeatureProvider provider)
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection([provider]);
        var testFeature = new TestFeature { Value = "from-provider" };

        provider
            .TryCreate(typeof(TestFeature), out _)
            .Returns(x =>
            {
                x[1] = testFeature;
                return true;
            });

        // Act
        var result = collection.Get<TestFeature>();

        // Assert
        result.Should().BeSameAs(testFeature);
    }

    [Theory]
    [AutoNSubstituteData]
    public void Get_CachesFeatureFromProvider(IFeatureProvider provider)
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection([provider]);
        var testFeature = new TestFeature { Value = "from-provider" };

        provider
            .TryCreate(typeof(TestFeature), out var feature)
            .Returns(x =>
            {
                x[1] = testFeature;
                return true;
            });

        // Act
        var result1 = collection.Get<TestFeature>();
        var result2 = collection.Get<TestFeature>();

        // Assert
        result1.Should().BeSameAs(result2);
        provider.Received(1).TryCreate(typeof(TestFeature), out _);
    }

    [Theory]
    [AutoNSubstituteData]
    public void Get_DoesNotCallProvidersForCachedFeatures(IFeatureProvider provider)
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection([provider]);
        var feature = "cached-feature";
        collection.Set(feature);

        // Act
        _ = collection.Get<string>();
        _ = collection.Get<string>();

        // Assert
        provider.DidNotReceive().TryCreate(typeof(string), out _);
    }

    [Theory]
    [AutoNSubstituteData]
    public void Get_TriesMultipleProvidersUntilOneSucceeds(
        IFeatureProvider provider1,
        IFeatureProvider provider2)
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection([provider1, provider2]);
        var testFeature = new TestFeature { Value = "from-second-provider" };

        provider1.TryCreate(Arg.Any<Type>(), out _).Returns(false);
        provider2
            .TryCreate(typeof(TestFeature), out var feature)
            .Returns(x =>
            {
                x[1] = testFeature;
                return true;
            });

        // Act
        var result = collection.Get<TestFeature>();

        // Assert
        result.Should().BeSameAs(testFeature);
        provider1.Received(1).TryCreate(typeof(TestFeature), out _);
        provider2.Received(1).TryCreate(typeof(TestFeature), out _);
    }

    [Theory]
    [AutoNSubstituteData]
    public void Get_StopsAfterFirstSuccessfulProvider(
        IFeatureProvider provider1,
        IFeatureProvider provider2)
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection([provider1, provider2]);
        var testFeature = new TestFeature { Value = "from-first-provider" };

        provider1
            .TryCreate(typeof(TestFeature), out var feature)
            .Returns(x =>
            {
                x[1] = testFeature;
                return true;
            });

        // Act
        _ = collection.Get<TestFeature>();

        // Assert
        provider1.Received(1).TryCreate(typeof(TestFeature), out _);
        provider2.DidNotReceive().TryCreate(Arg.Any<Type>(), out _);
    }

    [Fact]
    public void Get_ReturnsDefaultWhenFeatureNotProvidedAndNotCached()
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection();

        // Act
        var result = collection.Get<TestFeature>();

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void Get_WithDifferentTypeReturnsCorrectInstance()
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection();
        var stringFeature = "string-feature";
        var intFeature = 42;

        collection.Set(stringFeature);
        collection.Set(intFeature);

        // Act
        var stringResult = collection.Get<string>();
        var intResult = collection.Get<int>();

        // Assert
        stringResult.Should().Be(stringFeature);
        intResult.Should().Be(intFeature);
    }

    #endregion

    [Fact]
    public void Get_WhenProviderSetsFeature_PreservesAlreadyStoredFeature()
    {
        var provider = Substitute.For<IFeatureProvider>();
        var collection = new DefaultFeatureCollection([provider]);
        provider
            .TryCreate(typeof(string), out _)
            .Returns(call =>
            {
                collection.Set("stored");
                call[1] = "created";
                return true;
            });

        collection.Get<string>().Should().Be("stored");
        collection.Get<string>().Should().Be("stored");
        provider.Received(1).TryCreate(typeof(string), out _);
    }

    #region Enumeration Tests

    [Fact]
    public void GetEnumerator_ReturnsKeyValuePairCollection()
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection();
        var feature = "test-feature";
        collection.Set(feature);

        // Act
        var items = new List<KeyValuePair<Type, object>>();
        using var enumerator = collection.GetEnumerator();
        while (enumerator.MoveNext())
            items.Add(enumerator.Current);

        // Assert
        items.Should().HaveCount(1);
        items[0].Key.Should().Be<string>();
        items[0].Value.Should().Be(feature);
    }

    [Fact]
    public void Enumeration_IncludesAllSetFeatures()
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection();
        var stringFeature = "string-feature";
        var intFeature = 42;

        collection.Set(stringFeature);
        collection.Set(intFeature);

        // Act
        var items = collection.ToList();

        // Assert
        items.Should().HaveCount(2);
        items.Should().Contain(x => x.Key == typeof(string) && x.Value.Equals(stringFeature));
        items.Should().Contain(x => x.Key == typeof(int) && x.Value.Equals(intFeature));
    }

    [Fact]
    public void Enumeration_WorksWithForeach()
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection();
        collection.Set("feature1");
        collection.Set(42);

        var count = 0;

        // Act
        foreach (var item in collection)
        {
            count++;
            item.Key.Should().NotBeNull();
            item.Value.Should().NotBeNull();
        }

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public void IEnumerable_GetEnumerator_WorksCorrectly()
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection();
        collection.Set("feature1");
        collection.Set(42);

        // Act
        var enumerator = ((IEnumerable)collection).GetEnumerator();
        using var _ = enumerator as IDisposable;
        var items = new List<object>();
        while (enumerator.MoveNext())
            items.Add(enumerator.Current!);

        // Assert
        items.Should().HaveCount(2);
    }

    [Theory]
    [AutoNSubstituteData]
    public void Enumeration_IncludesProviderCreatedFeaturesAfterAccess(IFeatureProvider provider)
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection([provider]);
        var testFeature = new TestFeature { Value = "from-provider" };

        provider
            .TryCreate(typeof(TestFeature), out _)
            .Returns(x =>
            {
                x[1] = testFeature;
                return true;
            });

        // Also set a direct feature
        var directFeature = "direct-feature";
        collection.Set(directFeature);

        // Act
        _ = collection.Get<TestFeature>(); // Trigger provider
        var items = collection.ToList();

        // Assert
        items.Should().HaveCount(2);
        items.Should().Contain(x => x.Key == typeof(string) && x.Value.Equals(directFeature));
        items.Should().Contain(x => x.Key == typeof(TestFeature) && x.Value.Equals(testFeature));
    }

    #endregion

    #region Edge Cases & Integration Tests

    [Fact]
    public void MultipleDifferentFeatureTypes_CanCoexist()
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection();
        var stringFeature = "string";
        var intFeature = 42;
        var interfaceFeature = Substitute.For<IFeatureProvider>();
        var customFeature = new TestFeature { Value = "custom" };

        // Act
        collection.Set(stringFeature);
        collection.Set(intFeature);
        collection.Set(interfaceFeature);
        collection.Set(customFeature);

        // Assert
        collection.Get<string>().Should().Be(stringFeature);
        collection.Get<int>().Should().Be(intFeature);
        collection.Get<IFeatureProvider>().Should().BeSameAs(interfaceFeature);
        collection.Get<TestFeature>().Should().BeSameAs(customFeature);
    }

    [Fact]
    public void SettingDifferentInstancesOfSameType_ReplacesFeature()
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection();
        var feature1 = new TestFeature { Value = "feature1" };
        var feature2 = new TestFeature { Value = "feature2" };

        collection.Set(feature1);

        // Act
        collection.Set(feature2);

        // Assert
        collection.Get<TestFeature>().Should().BeSameAs(feature2);
        collection.Get<TestFeature>().Should().NotBeSameAs(feature1);
    }

    [Fact]
    public void EmptyCollection_EnumerationReturnsEmpty()
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection();

        // Act
        var items = collection.ToList();

        // Assert
        items.Should().BeEmpty();
    }

    [Theory]
    [AutoNSubstituteData]
    public void ProviderReturningFalse_CausesGetToReturnNull(IFeatureProvider provider)
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection([provider]);
        provider.TryCreate(Arg.Any<Type>(), out _).Returns(false);

        // Act
        var result = collection.Get<TestFeature>();

        // Assert
        result.Should().BeNull();
        provider.Received(1).TryCreate(typeof(TestFeature), out _);
    }

    [Theory]
    [AutoNSubstituteData]
    public void Get_WithSetFeature_IgnoresProviders(IFeatureProvider provider)
    {
        // Arrange
        var collection = CreateDefaultFeatureCollection([provider]);
        var directFeature = new TestFeature { Value = "direct" };
        collection.Set(directFeature);

        // Act
        var result = collection.Get<TestFeature>();

        // Assert
        result.Should().BeSameAs(directFeature);
        provider.DidNotReceive().TryCreate(typeof(TestFeature), out _);
    }

    #endregion
}
