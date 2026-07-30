// Portions of this file are derived from aspnetcore
// Source:
// https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/Http/Http.Extensions/gen/Microsoft.AspNetCore.Http.RequestDelegateGenerator/StaticRouteHandlerModel/InvocationOperationExtensions.cs
// Copyright (c) .NET Foundation and Contributors
// Licensed under the MIT License
// See THIRD-PARTY-LICENSES.txt file in the project root or visit
// https://github.com/dotnet/aspnetcore/blob/v10.0.0/LICENSE.txt

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using MinimalLambda.SourceGenerators.Models;
using WellKnownType = MinimalLambda.SourceGenerators.WellKnownTypes.WellKnownTypeData.WellKnownType;

namespace MinimalLambda.SourceGenerators;

internal static class HandlerSyntaxProvider
{
    private static readonly string[] TargetMethodNames = ["MapHandler", "OnInit", "OnShutdown"];

    internal static bool Predicate(SyntaxNode node, CancellationToken _) =>
        !node.IsGeneratedFile()
        && node.TryGetMethodName(out var name)
        && TargetMethodNames.Contains(name);

    internal static IMethodInfo? Transformer(
        GeneratorSyntaxContext syntaxContext,
        CancellationToken cancellationToken)
    {
        var context = new GeneratorContext(syntaxContext, cancellationToken);

        if (!TryGetInvocationOperation(context, out var targetOperation))
            return null;

        if (!targetOperation.TryGetHandlerMethod(context.SemanticModel, out var method))
            return null;

        return targetOperation.TargetMethod.Name switch
        {
            "MapHandler" => MapHandlerMethodInfo.Create(method, context),
            "OnInit" => LifecycleMethodInfo.CreateForInit(method, context),
            "OnShutdown" => LifecycleMethodInfo.CreateForShutdown(method, context),
            var methodName => throw new InvalidOperationException($"Unknown method '{methodName}"),
        };
    }

    private static bool TryGetInvocationOperation(
        GeneratorContext context,
        [NotNullWhen(true)] out IInvocationOperation? invocationOperation)
    {
        invocationOperation = null;

        var operation = context.SemanticModel.GetOperation(context.Node, context.CancellationToken);

        if (operation is IInvocationOperation
            {
                TargetMethod.ContainingNamespace:
                {
                    Name: "Builder",
                    ContainingNamespace
                    : { Name: "MinimalLambda", ContainingNamespace.IsGlobalNamespace: true, },
                },
            } targetOperation
            && targetOperation.TargetMethod.ContainingAssembly.Name == "MinimalLambda"
            && targetOperation.TryGetRouteHandlerArgument(out var routeHandlerParameter)
            && routeHandlerParameter is { Parameter.Type: { } delegateType }
            && SymbolEqualityComparer.Default.Equals(
                delegateType,
                context.WellKnownTypes.Get(WellKnownType.System_Delegate)))
        {
            invocationOperation = targetOperation;
            return true;
        }

        return false;
    }

    private static bool TryGetHandlerMethod(
        this IInvocationOperation invocation,
        SemanticModel semanticModel,
        [NotNullWhen(true)] out IMethodSymbol? method)
    {
        method = null;
        if (invocation.TryGetRouteHandlerArgument(out var argument))
        {
            method = ResolveMethodFromOperation(argument, semanticModel);
            return method is not null;
        }

        return false;
    }

    private static IMethodSymbol? ResolveMethodFromOperation(
        IOperation operation,
        SemanticModel semanticModel) =>
        operation switch
        {
            IArgumentOperation argument => ResolveMethodFromOperation(
                argument.Value,
                semanticModel),
            IConversionOperation conv => ResolveMethodFromOperation(conv.Operand, semanticModel),
            IDelegateCreationOperation del => ResolveMethodFromOperation(del.Target, semanticModel),
            IFieldReferenceOperation { Field.IsReadOnly: true } f when ResolveDeclarationOperation(
                f.Field,
                semanticModel) is { } op => ResolveMethodFromOperation(op, semanticModel),
            IAnonymousFunctionOperation anon => anon.Symbol,
            ILocalFunctionOperation local => local.Symbol,
            IMethodReferenceOperation method => method.Method,
            IParenthesizedOperation parenthesized => ResolveMethodFromOperation(
                parenthesized.Operand,
                semanticModel),
            _ => null,
        };

    private static bool TryGetRouteHandlerArgument(
        this IInvocationOperation invocation,
        [NotNullWhen(true)] out IArgumentOperation? argumentOperation)
    {
        argumentOperation = null;
        var routeHandlerArgumentOrdinal = invocation.Arguments.Length - 1;

        foreach (var argument in invocation.Arguments)
            if (argument.Parameter?.Ordinal == routeHandlerArgumentOrdinal)
            {
                argumentOperation = argument;
                return true;
            }

        return false;
    }

    private static IOperation? ResolveDeclarationOperation(
        ISymbol symbol,
        SemanticModel? semanticModel) =>
        symbol
            .DeclaringSyntaxReferences
            .Select(syntaxReference => syntaxReference.GetSyntax())
            .OfType<VariableDeclaratorSyntax>()
            .Where(syn => syn.Initializer?.Value is not null)
            .Select(syn =>
            {
                var expr = syn.Initializer!.Value;
                var targetSemanticModel =
                    semanticModel?.Compilation.GetSemanticModel(expr.SyntaxTree);
                return targetSemanticModel?.GetOperation(expr);
            })
            .FirstOrDefault(operation => operation is not null);
}
