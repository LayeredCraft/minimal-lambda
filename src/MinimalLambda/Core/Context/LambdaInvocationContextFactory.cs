using Amazon.Lambda.Core;
using Microsoft.Extensions.DependencyInjection;

namespace MinimalLambda;

internal class LambdaInvocationContextFactory : ILambdaInvocationContextFactory
{
    private readonly ILambdaInvocationContextAccessor? _contextAccessor;
    private readonly IFeatureCollectionFactory _featureCollectionFactory;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private IFeatureProvider[]? _featureProviders;

    public LambdaInvocationContextFactory(
        IServiceScopeFactory serviceScopeFactory,
        IFeatureCollectionFactory featureCollectionFactory,
        ILambdaInvocationContextAccessor? contextAccessor = null)
    {
        ArgumentNullException.ThrowIfNull(serviceScopeFactory);
        ArgumentNullException.ThrowIfNull(featureCollectionFactory);

        _serviceScopeFactory = serviceScopeFactory;
        _contextAccessor = contextAccessor;
        _featureCollectionFactory = featureCollectionFactory;
    }

    public ILambdaInvocationContext Create(
        ILambdaContext lambdaContext,
        IDictionary<string, object?> properties,
        CancellationToken cancellationToken)
    {
        _featureProviders ??= CreateFeatureProviders(properties);

        var context = new LambdaInvocationContext(
            lambdaContext,
            _serviceScopeFactory,
            properties,
            _featureCollectionFactory.Create(_featureProviders),
            cancellationToken);

        _contextAccessor?.LambdaInvocationContext = context;

        return context;
    }

    private static IFeatureProvider[] CreateFeatureProviders(
        IDictionary<string, object?> properties)
    {
        var list = new List<IFeatureProvider>(2);

        AddIfPresent(properties, LambdaInvocationBuilder.EventFeatureProviderKey, list);
        AddIfPresent(properties, LambdaInvocationBuilder.ResponseFeatureProviderKey, list);

        return list.ToArray();
    }

    private static void AddIfPresent(
        IDictionary<string, object?> properties,
        string key,
        List<IFeatureProvider> target)
    {
        if (properties.TryGetValue(key, out var value) && value is IFeatureProvider provider)
            target.Add(provider);
    }
}
