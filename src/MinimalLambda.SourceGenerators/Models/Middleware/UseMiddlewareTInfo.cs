using System;
using System.Collections.Generic;
using System.Linq;
using LayeredCraft.SourceGeneratorTools.Types;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;

namespace MinimalLambda.SourceGenerators.Models;

internal record UseMiddlewareTInfo(
    string? InterceptableLocationAttribute,
    MiddlewareClassInfo? ClassInfo,
    EquatableArray<DiagnosticInfo> DiagnosticInfos,
    MethodType MethodType) : IMethodInfo;

internal static class UseMiddlewareTInfoExtensions
{
    extension(UseMiddlewareTInfo)
    {
        internal static UseMiddlewareTInfo Create(
            IInvocationOperation invocationOperation,
            GeneratorContext context)
        {
            if (invocationOperation.Syntax is not InvocationExpressionSyntax
                invocationExpressionSyntax)
                throw new InvalidOperationException("Syntax is not InvocationExpressionSyntax");

            List<DiagnosticInfo> diagnosticInfos = [];

            var interceptableLocation =
                (context.SemanticModel.GetInterceptableLocation(
                        invocationExpressionSyntax,
                        context.CancellationToken)
                    ?? throw new InvalidOperationException(
                        "Interceptable location is null (Should not happen)"))
                .ToInterceptableLocationInfo()
                .Attribute;

            var middlewareClassType = invocationOperation
                .TargetMethod
                .TypeArguments
                .FirstOrDefault()
                .Map(typeSymbol => typeSymbol as INamedTypeSymbol
                    ?? throw new InvalidOperationException(
                        "Middleware class type is not INamedTypeSymbol (Should not happen)"));

            TryGetLocationInfo(invocationExpressionSyntax, out var typeArgumentLocation);

            var classInfo = MiddlewareClassInfo
                .Create(middlewareClassType, typeArgumentLocation, context)
                .Map(result =>
                {
                    diagnosticInfos.AddRange(result.Diagnostics);
                    return result.Info;
                });

            return new UseMiddlewareTInfo(
                interceptableLocation,
                classInfo,
                diagnosticInfos.ToEquatableArray(),
                MethodType.UseMiddlewareT);
        }
    }

    private static bool TryGetLocationInfo(
        InvocationExpressionSyntax invocationExpressionSyntax,
        out Location? locationInfo)
    {
        locationInfo = null;
        if (invocationExpressionSyntax is
            {
                Expression: MemberAccessExpressionSyntax { Name: GenericNameSyntax genericName },
            })
        {
            var typeArgument = genericName.TypeArgumentList.Arguments[0];
            locationInfo = typeArgument.GetLocation();
            return true;
        }

        return false;
    }
}
