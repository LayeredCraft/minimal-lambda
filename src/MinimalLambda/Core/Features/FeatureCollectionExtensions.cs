// Portions of this file are derived from azure-functions-dotnet-worker
// Source:
// https://github.com/Azure/azure-functions-dotnet-worker/blob/2.51.0/src/DotNetWorker.Core/Context/Features/InvocationFeaturesExtensions.cs
// Copyright (c) Microsoft Corporation.
// Licensed under the MIT License
// See THIRD-PARTY-LICENSES.txt file in the project root or visit
// https://github.com/Azure/azure-functions-dotnet-worker/blob/2.51.0/LICENSE

using System.Diagnostics.CodeAnalysis;

namespace MinimalLambda;

/// <summary>Extension methods for feature collections.</summary>
public static class FeatureCollectionExtensions
{
    extension(IFeatureCollection featureCollection)
    {
        //      ┌──────────────────────────────────────────────────────────┐
        //      │          General IFeatureCollection Exceptions           │
        //      └──────────────────────────────────────────────────────────┘

        /// <summary>Attempts to get a feature from the collection.</summary>
        /// <typeparam name="T">The type of feature to retrieve.</typeparam>
        /// <param name="result">The retrieved feature, or null if not found.</param>
        /// <returns>True if the feature was found; otherwise false.</returns>
        public bool TryGet<T>([NotNullWhen(true)] out T? result)
        {
            ArgumentNullException.ThrowIfNull(featureCollection);

            result = featureCollection.Get<T>();
            return result is not null;
        }

        /// <summary>Gets a feature from the collection, throwing if not found.</summary>
        /// <typeparam name="T">The type of feature to retrieve.</typeparam>
        /// <returns>The feature instance.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the feature is not found in the collection.</exception>
        public T GetRequired<T>()
        {
            ArgumentNullException.ThrowIfNull(featureCollection);

            return featureCollection.Get<T>()
                ?? throw new InvalidOperationException(
                    $"Feature of type '{typeof(T).FullName}' is not available in the collection.");
        }
    }
}
