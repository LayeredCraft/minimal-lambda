using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LayeredCraft.SourceGeneratorTools.Types;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using MinimalLambda.SourceGenerators.Extensions;
using WellKnownType = MinimalLambda.SourceGenerators.WellKnownTypes.WellKnownTypeData.WellKnownType;

namespace MinimalLambda.SourceGenerators.Models;

internal sealed record DurableHandlerParameterInfo(
    int Ordinal,
    string Name,
    string GloballyQualifiedType,
    string Assignment,
    ParameterSource Source,
    bool IsEvent,
    bool IsFromKeyedService,
    string? KeyedServicesKey,
    LocationInfo? LocationInfo);

internal sealed record DurableMethodInfo(
    string InterceptableLocationAttribute,
    string DelegateCastType,
    string? HandlerDelegateType,
    EquatableArray<DurableHandlerParameterInfo> ParameterAssignments,
    string? InputType,
    bool HasOutput,
    string? OutputType,
    bool HasAnyFromKeyedServices,
    LocationInfo MapCallLocation,
    LocationInfo HandlerArgumentLocation,
    int TreeOrdinal,
    EquatableArray<DiagnosticInfo> DiagnosticInfos,
    MethodType MethodType = MethodType.MapDurableHandler) : IMethodInfo;

internal static class DurableMethodInfoExtensions
{
    extension(DurableMethodInfo)
    {
        internal static DurableMethodInfo Create(
            IMethodSymbol methodSymbol,
            IArgumentOperation handlerArgument,
            GeneratorContext context)
        {
            if (!InterceptableLocationInfo.TryGet(context, out var interceptableLocation))
                throw new InvalidOperationException("Unable to get interceptable location");

            var invocationLocation = LocationInfo.Create(context.Node)
                ?? throw new InvalidOperationException("Durable mapping has no source location");
            var handlerSyntax = GetHandlerSyntax(handlerArgument);
            var handlerArgumentLocation = LocationInfo.Create(handlerSyntax) ?? invocationLocation;
            var declaration =
                methodSymbol
                    .DeclaringSyntaxReferences
                    .Select(reference => reference.GetSyntax(context.CancellationToken))
                    .FirstOrDefault();
            var returnLocation = GetReturnLocation(declaration) ?? handlerArgumentLocation;
            var compilation = context.SemanticModel.Compilation;
            var mapTreeOrdinal = GetTreeOrdinal(context.Node.SyntaxTree, compilation);

            var diagnostics = new List<DiagnosticInfo>();
            var parameters = methodSymbol.Parameters;
            var durable = new bool[parameters.Length];
            var candidates = new List<int>();

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var reserved = context.WellKnownTypes.IsType(
                    parameter.Type,
                    WellKnownType.Amazon_Lambda_DurableExecution_IDurableContext,
                    WellKnownType.Amazon_Lambda_Core_ILambdaContext,
                    WellKnownType.MinimalLambda_ILambdaInvocationContext);
                durable[i] = context.WellKnownTypes.IsType(
                    parameter.Type,
                    WellKnownType.Amazon_Lambda_DurableExecution_IDurableContext);
                if (!reserved && parameter.IsFromEvent(context))
                    candidates.Add(i);
            }

            var inputOrdinal = candidates.Count == 0 ? -1 : candidates[0];
            var assignments = new List<DurableHandlerParameterInfo>(parameters.Length);
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var parameterLocation = GetParameterLocation(parameter, handlerArgumentLocation);
                var source = ParameterSource.Services;
                var assignment = string.Empty;
                var isKeyed = false;
                string? key = null;

                if (i == inputOrdinal)
                {
                    source = ParameterSource.Event;
                    assignment = "input";
                }
                else if (durable[i])
                {
                    source = ParameterSource.DurableContext;
                    assignment = "durableContext";
                }
                else if (context.WellKnownTypes.IsType(
                    parameter.Type,
                    WellKnownType.Amazon_Lambda_Core_ILambdaContext,
                    WellKnownType.MinimalLambda_ILambdaInvocationContext))
                {
                    source = ParameterSource.Context;
                    assignment = "context";
                }
                else
                {
                    var diResult = parameter.GetDiParameterAssignment(context);
                    if (diResult.IsSuccess)
                    {
                        assignment = diResult.Value!.Assignment;
                        key = diResult.Value!.Key;
                        isKeyed = key is not null;
                        source = isKeyed ? ParameterSource.KeyedServices : ParameterSource.Services;
                    }
                    else if (diResult.Error is { } error)
                    {
                        diagnostics.Add(error);
                    }
                }

                assignments.Add(
                    new DurableHandlerParameterInfo(
                        parameter.Ordinal,
                        parameter.Name,
                        parameter.Type.QualifiedNullableName,
                        assignment,
                        source,
                        i == inputOrdinal,
                        isKeyed,
                        key,
                        parameterLocation));
            }

            var hasOutput = false;
            ITypeSymbol? outputType = null;
            var validReturn = false;
            if (SymbolEqualityComparer.Default.Equals(
                methodSymbol.ReturnType,
                context.WellKnownTypes.Get(WellKnownType.System_Threading_Tasks_Task)))
                validReturn = true;
            else if (methodSymbol.ReturnType is INamedTypeSymbol namedReturn
                && namedReturn.Arity == 1
                && SymbolEqualityComparer.Default.Equals(
                    namedReturn.OriginalDefinition,
                    context.WellKnownTypes.Get(WellKnownType.System_Threading_Tasks_Task_T)))
            {
                hasOutput = true;
                outputType = namedReturn.TypeArguments[0];
                validReturn = true;
            }

            if (!validReturn)
                diagnostics.Add(
                    CreateDiagnostic(
                        Diagnostics.UnsupportedDurableReturnType,
                        returnLocation,
                        methodSymbol.ReturnType.QualifiedNullableName));

            return new DurableMethodInfo(
                InterceptableLocationAttribute: interceptableLocation.Attribute,
                DelegateCastType: methodSymbol.GetCastableSignature(),
                HandlerDelegateType:
                GetHandlerDelegateType(handlerArgument, compilation, context.CancellationToken),
                ParameterAssignments: assignments.ToEquatableArray(),
                InputType:
                inputOrdinal < 0
                    ? "global::System.Object"
                    : parameters[inputOrdinal].Type.QualifiedNullableName,
                HasOutput: hasOutput,
                OutputType: outputType?.QualifiedNullableName,
                HasAnyFromKeyedServices: assignments.Any(parameter => parameter.IsFromKeyedService),
                MapCallLocation: invocationLocation,
                HandlerArgumentLocation: handlerArgumentLocation,
                TreeOrdinal: mapTreeOrdinal,
                DiagnosticInfos: diagnostics.ToEquatableArray());
        }
    }

    private static string? GetHandlerDelegateType(
        IArgumentOperation handlerArgument,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        var operation = UnwrapHandlerOperation(handlerArgument.Value);

        if (operation is IFieldReferenceOperation field
            && operation.Type is not INamedTypeSymbol { TypeKind: TypeKind.Delegate, })
            operation =
                GetFieldInitializerOperation(field.Field, compilation, cancellationToken) is
                    { } initializer
                    ? UnwrapHandlerOperation(initializer)
                    : operation;

        var hasExplicitDelegateType =
            operation is IConversionOperation { IsImplicit: false }
                or IDelegateCreationOperation { IsImplicit: false }
                or IFieldReferenceOperation;

        return hasExplicitDelegateType
            && operation.Type is INamedTypeSymbol { TypeKind: TypeKind.Delegate, } delegateType
            && IsAccessibleDelegateType(delegateType, compilation)
                ? delegateType.QualifiedNullableName
                : null;
    }

    private static IOperation UnwrapHandlerOperation(IOperation operation)
    {
        while (operation is IParenthesizedOperation parenthesized)
            operation = parenthesized.Operand;

        if (operation is IConversionOperation { IsImplicit: true } conversion)
            operation = conversion.Operand;

        while (operation is IParenthesizedOperation parenthesized)
            operation = parenthesized.Operand;

        return operation;
    }

    private static IOperation? GetFieldInitializerOperation(
        IFieldSymbol field,
        Compilation compilation,
        CancellationToken cancellationToken) =>
        field
            .DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(cancellationToken))
            .OfType<VariableDeclaratorSyntax>()
            .Where(declarator => declarator.Initializer is not null)
            .Select(declarator =>
                compilation
                    .GetSemanticModel(declarator.SyntaxTree)
                    .GetOperation(declarator.Initializer!.Value, cancellationToken))
            .FirstOrDefault(operation => operation is not null);

    private static bool IsAccessibleDelegateType(
        INamedTypeSymbol delegateType,
        Compilation compilation)
    {
        if (delegateType.IsAnonymousType
            || delegateType.IsFileLocal
            || ContainsTypeParameter(delegateType))
            return false;

        for (INamedTypeSymbol? current = delegateType;
            current is not null;
            current = current.ContainingType)
            if (!compilation.IsSymbolAccessibleWithin(current, compilation.Assembly))
                return false;

        return true;
    }

    private static bool ContainsTypeParameter(INamedTypeSymbol type)
    {
        if (type.ContainingType is { } containingType && ContainsTypeParameter(containingType))
            return true;

        return type.TypeArguments.Any(argument =>
            argument is ITypeParameterSymbol
            || argument is INamedTypeSymbol namedArgument && ContainsTypeParameter(namedArgument));
    }

    private static DiagnosticInfo CreateDiagnostic(
        DiagnosticDescriptor descriptor,
        LocationInfo location,
        params object?[] arguments) =>
        new(descriptor, location, arguments);

    private static SyntaxNode GetHandlerSyntax(IArgumentOperation argument) =>
        argument.Syntax is ArgumentSyntax argumentSyntax
            ? argumentSyntax.Expression
            : argument.Syntax;

    private static LocationInfo? GetReturnLocation(SyntaxNode? declaration) =>
        declaration switch
        {
            MethodDeclarationSyntax method => LocationInfo.Create(method.ReturnType),
            LocalFunctionStatementSyntax local => LocationInfo.Create(local.ReturnType),
            LambdaExpressionSyntax lambda => LocationInfo.Create(lambda.ArrowToken.GetLocation()),
            AnonymousMethodExpressionSyntax anonymous => LocationInfo.Create(
                anonymous.DelegateKeyword.GetLocation()),
            _ => null,
        };

    private static LocationInfo GetParameterLocation(
        IParameterSymbol parameter,
        LocationInfo fallback) =>
        parameter
            .DeclaringSyntaxReferences
            .Select(reference => LocationInfo.Create(reference.GetSyntax()))
            .FirstOrDefault(location => location is not null)
        ?? fallback;

    internal static int GetTreeOrdinal(SyntaxTree tree, Compilation compilation)
    {
        var ordinal = 0;
        foreach (var candidate in compilation.SyntaxTrees)
        {
            if (ReferenceEquals(candidate, tree))
                return ordinal;
            ordinal++;
        }

        return int.MaxValue;
    }
}
