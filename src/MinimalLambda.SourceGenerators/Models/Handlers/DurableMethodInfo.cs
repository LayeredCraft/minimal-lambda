using System;
using System.Collections.Generic;
using LayeredCraft.SourceGeneratorTools.Types;
using Microsoft.CodeAnalysis;
using MinimalLambda.SourceGenerators.Extensions;

namespace MinimalLambda.SourceGenerators.Models;

internal sealed record DurableMethodInfo(
    string InterceptableLocationAttribute,
    string DelegateCastType,
    EquatableArray<DiagnosticInfo> DiagnosticInfos,
    MethodType MethodType = MethodType.MapDurableHandler) : IMethodInfo;

internal static class DurableMethodInfoExtensions
{
    extension(DurableMethodInfo)
    {
        internal static DurableMethodInfo Create(
            IMethodSymbol methodSymbol,
            GeneratorContext context)
        {
            if (!InterceptableLocationInfo.TryGet(context, out var interceptableLocation))
                throw new InvalidOperationException("Unable to get interceptable location");

            return new DurableMethodInfo(
                InterceptableLocationAttribute: interceptableLocation.Attribute,
                DelegateCastType: methodSymbol.GetCastableSignature(),
                DiagnosticInfos: new List<DiagnosticInfo>().ToEquatableArray());
        }
    }
}
