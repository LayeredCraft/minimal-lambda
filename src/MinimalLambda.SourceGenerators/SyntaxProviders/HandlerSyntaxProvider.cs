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
    private static readonly string[] TargetMethodNames =
    [
        "MapHandler", "MapDurableHandler", "OnInit", "OnShutdown"
    ];

    internal static bool Predicate(SyntaxNode node, CancellationToken _) =>
        !node.IsGeneratedFile()
        && node.TryGetMethodName(out var name)
        && TargetMethodNames.Contains(name);

    internal static IMethodInfo? Transformer(
        GeneratorSyntaxContext syntaxContext,
        CancellationToken cancellationToken) =>
        Transform(syntaxContext.Node, syntaxContext.SemanticModel, cancellationToken);

    internal static IMethodInfo? Transform(
        SyntaxNode node,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        var context = new GeneratorContext(node, semanticModel, cancellationToken);

        if (!TryGetInvocationOperation(context, out var targetOperation))
            return null;

        if (!targetOperation.TryGetHandlerMethod(
            context.SemanticModel,
            out var method,
            out var handlerArgument))
            return null;

        return targetOperation.TargetMethod.Name switch
        {
            "MapHandler" => MapHandlerMethodInfo.Create(method, context),
            "MapDurableHandler" => DurableMethodInfo.Create(method, handlerArgument, context),
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

        if (operation is IInvocationOperation targetOperation
            && targetOperation.TargetMethod.GetDeclaredMethod() is { } declaredMethod
            && IsKnownTarget(declaredMethod)
            && targetOperation.TryGetRouteHandlerArgument(
                declaredMethod,
                context.WellKnownTypes.Get(WellKnownType.System_Delegate),
                out _))
        {
            invocationOperation = targetOperation;
            return true;
        }

        return false;
    }

    private static bool TryGetHandlerMethod(
        this IInvocationOperation invocation,
        SemanticModel semanticModel,
        [NotNullWhen(true)] out IMethodSymbol? method,
        [NotNullWhen(true)] out IArgumentOperation? handlerArgument)
    {
        method = null;
        handlerArgument = null;
        var declaredMethod = invocation.TargetMethod.GetDeclaredMethod();
        var delegateType = semanticModel.Compilation.GetTypeByMetadataName("System.Delegate");

        if (delegateType is not null
            && invocation.TryGetRouteHandlerArgument(
                declaredMethod,
                delegateType,
                out var argument))
        {
            method = ResolveMethodFromOperation(argument, semanticModel);
            handlerArgument = argument;
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
        IMethodSymbol declaredMethod,
        ITypeSymbol delegateType,
        [NotNullWhen(true)] out IArgumentOperation? argumentOperation)
    {
        argumentOperation = null;
        var handlerParameter = declaredMethod.Parameters.FirstOrDefault(parameter =>
            SymbolEqualityComparer.Default.Equals(parameter.Type, delegateType));

        if (handlerParameter is null)
            return false;

        var targetOrdinal = invocation.TargetMethod.ReducedFrom is null
            ? handlerParameter.Ordinal
            : handlerParameter.Ordinal - 1;

        foreach (var argument in invocation.Arguments)
            if (argument.Parameter?.Ordinal == targetOrdinal)
            {
                argumentOperation = argument;
                return true;
            }

        return false;
    }

    private static IMethodSymbol GetDeclaredMethod(this IMethodSymbol method) =>
        method.ReducedFrom ?? method;

    private static bool IsKnownTarget(IMethodSymbol method) =>
        method.ContainingNamespace is
        {
            Name: "Builder",
            ContainingNamespace:
            {
                Name: "MinimalLambda", ContainingNamespace.IsGlobalNamespace: true,
            },
        }
        && (method.Name switch
        {
            "MapDurableHandler" => method is
            {
                ContainingType.ContainingType.Name:
                "MapDurableHandlerLambdaApplicationExtensions",
                ContainingAssembly.Name: "MinimalLambda.DurableExecution",
            },
            "MapHandler" or "OnInit" or "OnShutdown" => method.ContainingAssembly.Name
                == "MinimalLambda",
            _ => false,
        });

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
