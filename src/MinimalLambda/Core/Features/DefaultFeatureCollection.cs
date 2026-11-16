// Portions of this file are derived from azure-functions-dotnet-worker
// Source:
// https://github.com/Azure/azure-functions-dotnet-worker/blob/2.51.0/src/DotNetWorker.Core/Context/Features/InvocationFeatures.cs
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License
// See THIRD-PARTY-LICENSES.txt file in the project root or visit
// https://github.com/Azure/azure-functions-dotnet-worker/blob/2.51.0/LICENSE

using System.Collections;

namespace MinimalLambda;

internal class DefaultFeatureCollection : IFeatureCollection
{
    private readonly IEnumerable<IFeatureProvider> _featureProviders;
    private readonly Dictionary<Type, object> _features = new();

    internal DefaultFeatureCollection(IEnumerable<IFeatureProvider> featureProviders)
    {
        ArgumentNullException.ThrowIfNull(featureProviders);

        _featureProviders = featureProviders;
    }

    public T? Get<T>()
    {
        var type = typeof(T);
        if (!_features.TryGetValue(type, out var feature))
            foreach (var provider in _featureProviders)
            {
                if (!provider.TryCreate(type, out feature))
                    continue;

                if (!_features.TryAdd(type, feature!))
                    feature = _features[type];
                break;
            }

        return feature is null ? default : (T)feature;
    }

    public IEnumerator<KeyValuePair<Type, object>> GetEnumerator() => _features.GetEnumerator();

    public void Set<T>(T instance)
    {
        ArgumentNullException.ThrowIfNull(instance);

        _features[typeof(T)] = instance;
    }

    IEnumerator IEnumerable.GetEnumerator() => _features.GetEnumerator();
}
