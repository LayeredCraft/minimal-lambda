using System;
using System.Collections.Generic;
using System.Linq;
using LayeredCraft.SourceGeneratorTools.Types;
using Microsoft.CodeAnalysis;
using MinimalLambda.SourceGenerators.Extensions;
using WellKnownType = MinimalLambda.SourceGenerators.WellKnownTypes.WellKnownTypeData.WellKnownType;

namespace MinimalLambda.SourceGenerators.Models;

internal record MiddlewareClassInfo(
    string GloballyQualifiedName,
    string ShortName,
    EquatableArray<MiddlewareParameterInfo> ParameterInfos,
    bool ImplementsDisposable,
    bool ImplementsAsyncDisposable,
    bool AllParametersFromServices);

internal static class MiddlewareExtensions
{
    private const string MiddlewareConstructor =
        "MinimalLambda.Builder.MiddlewareConstructorAttribute";

    extension(MiddlewareClassInfo)
    {
        internal static (MiddlewareClassInfo? Info, List<DiagnosticInfo> Diagnostics) Create(
            INamedTypeSymbol typeSymbol,
            Location? location,
            GeneratorContext context)
        {
            // validate that middleware class is a concrete -> not interface or abstract class
            if (typeSymbol.TypeKind == TypeKind.Interface || typeSymbol.IsAbstract)
                return (null,
                [
                    DiagnosticInfo.Create(
                        Diagnostics.MustBeConcreteType,
                        location?.ToLocationInfo(),
                        [typeSymbol.QualifiedName]),
                ]);

            List<DiagnosticInfo> diagnostics = [];

            // get the globally qualified name of the class
            var globallyQualifiedName = typeSymbol.QualifiedNullableName;

            // get short name, i.e., not qualified
            var shortName = typeSymbol.Name;

            // get constructor
            var constructor = GetConstructor(typeSymbol, context)
                .Map(result =>
                {
                    diagnostics.AddRange(result.DiagnosticInfos);
                    return result.MethodSymbol;
                });

            // get constructor parameters
            var parameterInfos = constructor is not null
                ? constructor
                    .Parameters
                    .CollectDiagnosticResults(parameter =>
                        MiddlewareParameterInfo.Create(parameter, context))
                    .Map(results =>
                    {
                        diagnostics.AddRange(results.Diagnostics);
                        return results.Data;
                    })
                : [];

            // implements IDisposable
            var implementsIDisposable = typeSymbol.AllInterfaces.Any(i =>
                context.WellKnownTypes.IsType(i, WellKnownType.System_IDisposable));

            // implements IAsyncDisposable
            var implementsIAsyncDisposable = typeSymbol.AllInterfaces.Any(i =>
                context.WellKnownTypes.IsType(i, WellKnownType.System_IAsyncDisposable));

            // are all parameters for the constructor from services
            var allParametersFromServices = parameterInfos.All(p => p.FromServices);

            return (
                new MiddlewareClassInfo(
                    globallyQualifiedName,
                    shortName,
                    parameterInfos.ToEquatableArray(),
                    implementsIDisposable,
                    implementsIAsyncDisposable,
                    allParametersFromServices), diagnostics);
        }
    }

    private static (IMethodSymbol? MethodSymbol, DiagnosticInfo[] DiagnosticInfos) GetConstructor(
        INamedTypeSymbol namedTypeSymbol,
        GeneratorContext context)
    {
        // 1. Get constructors annotated with `[MiddlewareConstructor]`
        var constructors = namedTypeSymbol
            .InstanceConstructors
            .Where(c => c
                .GetAttributes()
                .Any(a => a.AttributeClass is not null
                    && context.WellKnownTypes.IsType(
                        a.AttributeClass,
                        WellKnownType.MinimalLambda_Builder_MiddlewareConstructorAttribute)))
            .ToArray();

        return constructors.Length switch
        {
            // if more than one found, we will return diagnostics
            > 1 => (MethodSymbol: null,
                DiagnosticInfos: constructors
                    .Skip(1)
                    .Select(c => DiagnosticInfo.Create(
                        Diagnostics.MultipleConstructorsWithAttribute,
                        c.Locations.FirstOrDefault()?.ToLocationInfo(),
                        [MiddlewareConstructor]))
                    .ToArray()),

            // return single constructor that has an `[MiddlewareConstructor]` attribute
            1 => (MethodSymbol: constructors.FirstOrDefault(), DiagnosticInfos: []),

            // 2. default to constructor with most parameters
            _ => (
                MethodSymbol: namedTypeSymbol
                    .InstanceConstructors
                    .OrderByDescending(c => c.Parameters.Length)
                    .First(), DiagnosticInfos: []),
        };
    }
}
