using System;
using System.Collections.Generic;
using System.Linq;
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
    string CanonicalType,
    string Assignment,
    ParameterSource Source,
    bool IsEvent,
    bool IsFromKeyedService,
    string? KeyedServicesKey,
    LocationInfo? LocationInfo);

internal sealed record DurableMethodInfo(
    string InterceptableLocationAttribute,
    string DelegateCastType,
    EquatableArray<DurableHandlerParameterInfo> ParameterAssignments,
    string? InputType,
    string? InputCanonicalType,
    bool HasOutput,
    string? OutputType,
    string? OutputCanonicalType,
    bool HasAnyFromKeyedServices,
    LocationInfo MapCallLocation,
    LocationInfo HandlerArgumentLocation,
    int TreeOrdinal,
    EquatableArray<DiagnosticInfo> DiagnosticInfos,
    MethodType MethodType = MethodType.MapDurableHandler) : IMethodInfo;

internal static class DurableMethodInfoExtensions
{
    private const string EnvelopeReason =
        "outer durable envelope types are reserved for low-level MapHandler";

    private const string CancellationReason =
        "CancellationToken is not bound automatically; use ILambdaInvocationContext for explicit access";

    private const string StreamReason =
        "Stream transport types are reserved for low-level MapHandler";

    private const string ReservedReason =
        "reserved context parameters cannot use event or service binding attributes";

    private const string RefKindReason = "ref, in, and out parameters are not supported";

    private const string RefLikeReason =
        "ref-like, pointer, and function-pointer types are not supported";

    private const string OpenReason =
        "signature types must be closed and cannot contain type parameters";

    private const string InaccessibleReason =
        "signature types must be nameable and accessible from generated code";

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
            var handlerAnchor = GetHandlerAnchor(declaration) ?? handlerArgumentLocation;
            var returnLocation = GetReturnLocation(declaration) ?? handlerArgumentLocation;
            var compilation = context.SemanticModel.Compilation;
            var mapTreeOrdinal = GetTreeOrdinal(context.Node.SyntaxTree, compilation);
            var handlerTreeOrdinal = declaration is null
                ? mapTreeOrdinal
                : GetTreeOrdinal(declaration.SyntaxTree, compilation);

            var diagnostics = new List<DiagnosticInfo>();
            var parameters = methodSymbol.Parameters;
            var reserved = new bool[parameters.Length];
            var durable = new bool[parameters.Length];
            var conflicts = new bool[parameters.Length];
            var candidates = new List<int>();

            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                reserved[i] = context.WellKnownTypes.IsType(
                    parameter.Type,
                    WellKnownType.Amazon_Lambda_DurableExecution_IDurableContext,
                    WellKnownType.Amazon_Lambda_Core_ILambdaContext,
                    WellKnownType.MinimalLambda_ILambdaInvocationContext);
                durable[i] = context.WellKnownTypes.IsType(
                    parameter.Type,
                    WellKnownType.Amazon_Lambda_DurableExecution_IDurableContext);
                conflicts[i] = reserved[i] && HasBindingAttribute(parameter, context);

                if (!reserved[i] && parameter.IsFromEvent(context))
                    candidates.Add(i);
            }

            var durableCount = durable.Count(value => value);
            if (durableCount == 0)
                diagnostics.Add(
                    CreateDiagnostic(
                        Diagnostics.InvalidDurableContextCardinality,
                        handlerAnchor,
                        handlerTreeOrdinal,
                        0,
                        durableCount));
            else if (durableCount > 1)
            {
                var seen = 0;
                for (var i = 0; i < durable.Length; i++)
                    if (durable[i] && seen++ > 0)
                        diagnostics.Add(
                            CreateDiagnostic(
                                Diagnostics.InvalidDurableContextCardinality,
                                GetParameterLocation(parameters[i], handlerArgumentLocation),
                                GetParameterTreeOrdinal(parameters[i], compilation, mapTreeOrdinal),
                                i + 1,
                                durableCount));
            }

            if (candidates.Count == 0)
                diagnostics.Add(
                    CreateDiagnostic(
                        Diagnostics.InvalidDurableInputCardinality,
                        handlerAnchor,
                        handlerTreeOrdinal,
                        0,
                        0));
            else if (candidates.Count > 1)
                foreach (var ordinal in candidates.Skip(1))
                    diagnostics.Add(
                        CreateDiagnostic(
                            Diagnostics.InvalidDurableInputCardinality,
                            GetParameterLocation(parameters[ordinal], handlerArgumentLocation),
                            GetParameterTreeOrdinal(
                                parameters[ordinal],
                                compilation,
                                mapTreeOrdinal),
                            ordinal + 1,
                            candidates.Count));

            var inputOrdinal = candidates.Count == 0 ? -1 : candidates[0];
            var assignments = new List<DurableHandlerParameterInfo>(parameters.Length);
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];
                var recursiveTransport = i == inputOrdinal;
                var typeInfo = DurableTypeInfoFactory.Create(
                    parameter.Type,
                    context,
                    recursiveTransport);
                var reason = GetParameterReason(parameter, typeInfo.Issues, conflicts[i], context);
                var parameterLocation = GetParameterLocation(parameter, handlerArgumentLocation);
                var parameterTreeOrdinal = GetParameterTreeOrdinal(
                    parameter,
                    compilation,
                    mapTreeOrdinal);

                if (reason is not null)
                    diagnostics.Add(
                        CreateDiagnostic(
                            Diagnostics.UnsupportedDurableParameter,
                            parameterLocation,
                            parameterTreeOrdinal,
                            i + 1,
                            parameter.Name,
                            typeInfo.CanonicalName,
                            reason));

                var source = ParameterSource.Services;
                var assignment = string.Empty;
                var isKeyed = false;
                string? key = null;

                if (i == inputOrdinal)
                {
                    source = ParameterSource.Event;
                    assignment = "input";
                }
                else if (durable[i] && !conflicts[i])
                {
                    source = ParameterSource.DurableContext;
                    assignment = "durableContext";
                }
                else if (reserved[i] && !conflicts[i])
                {
                    source = ParameterSource.Context;
                    assignment = "context";
                }
                else if (!conflicts[i])
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
                        diagnostics.Add(
                            error with { TreeOrdinal = parameterTreeOrdinal, SubKey = i + 1, });
                    }
                }

                assignments.Add(
                    new DurableHandlerParameterInfo(
                        parameter.Ordinal,
                        parameter.Name,
                        parameter.Type.QualifiedNullableName,
                        typeInfo.CanonicalName,
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
                var outputInfo = DurableTypeInfoFactory.Create(outputType, context, true);
                validReturn = outputInfo.Issues == DurableTypeIssue.None;
            }

            if (!validReturn)
                diagnostics.Add(
                    CreateDiagnostic(
                        Diagnostics.UnsupportedDurableReturnType,
                        returnLocation,
                        handlerTreeOrdinal,
                        0,
                        DurableTypeInfoFactory.Render(methodSymbol.ReturnType)));

            return new DurableMethodInfo(
                InterceptableLocationAttribute: interceptableLocation.Attribute,
                DelegateCastType: methodSymbol.GetCastableSignature(),
                ParameterAssignments: assignments.ToEquatableArray(),
                InputType:
                inputOrdinal < 0 ? null : parameters[inputOrdinal].Type.QualifiedNullableName,
                InputCanonicalType:
                inputOrdinal < 0
                    ? null
                    : DurableTypeInfoFactory.Render(parameters[inputOrdinal].Type),
                HasOutput: hasOutput,
                OutputType: outputType?.QualifiedNullableName,
                OutputCanonicalType:
                outputType is null ? null : DurableTypeInfoFactory.Render(outputType),
                HasAnyFromKeyedServices: assignments.Any(parameter => parameter.IsFromKeyedService),
                MapCallLocation: invocationLocation,
                HandlerArgumentLocation: handlerArgumentLocation,
                TreeOrdinal: mapTreeOrdinal,
                DiagnosticInfos: diagnostics.ToEquatableArray());
        }
    }

    private static string? GetParameterReason(
        IParameterSymbol parameter,
        DurableTypeIssue issues,
        bool reservedConflict,
        GeneratorContext context)
    {
        if ((issues & DurableTypeIssue.Envelope) != 0)
            return EnvelopeReason;
        if (context.WellKnownTypes.IsType(
            parameter.Type,
            WellKnownType.System_Threading_CancellationToken))
            return CancellationReason;
        if ((issues & DurableTypeIssue.Stream) != 0)
            return StreamReason;
        if (reservedConflict)
            return ReservedReason;
        if (parameter.RefKind != RefKind.None)
            return RefKindReason;
        if ((issues & DurableTypeIssue.RefLikePointerOrFunctionPointer) != 0)
            return RefLikeReason;
        if ((issues & DurableTypeIssue.TypeParameter) != 0)
            return OpenReason;
        if ((issues & DurableTypeIssue.Inaccessible) != 0)
            return InaccessibleReason;
        return null;
    }

    private static bool HasBindingAttribute(IParameterSymbol parameter, GeneratorContext context) =>
        parameter.IsDecoratedWithAttribute(
            context,
            WellKnownType.MinimalLambda_Builder_EventAttribute,
            WellKnownType.MinimalLambda_Builder_FromEventAttribute,
            WellKnownType.MinimalLambda_Builder_FromServicesAttribute,
            WellKnownType.Microsoft_Extensions_DependencyInjection_FromKeyedServicesAttribute);

    private static DiagnosticInfo CreateDiagnostic(
        DiagnosticDescriptor descriptor,
        LocationInfo location,
        int treeOrdinal,
        int subKey,
        params object?[] arguments) =>
        new(descriptor, location, arguments) { TreeOrdinal = treeOrdinal, SubKey = subKey, };

    private static SyntaxNode GetHandlerSyntax(IArgumentOperation argument) =>
        argument.Syntax is ArgumentSyntax argumentSyntax
            ? argumentSyntax.Expression
            : argument.Syntax;

    private static LocationInfo? GetHandlerAnchor(SyntaxNode? declaration) =>
        declaration switch
        {
            MethodDeclarationSyntax method => LocationInfo.Create(method.Identifier.GetLocation()),
            LocalFunctionStatementSyntax local => LocationInfo.Create(
                local.Identifier.GetLocation()),
            ParenthesizedLambdaExpressionSyntax lambda => LocationInfo.Create(
                lambda.ParameterList.OpenParenToken.GetLocation()),
            SimpleLambdaExpressionSyntax lambda => LocationInfo.Create(
                lambda.Parameter.GetLocation()),
            AnonymousMethodExpressionSyntax anonymous => LocationInfo.Create(
                anonymous.DelegateKeyword.GetLocation()),
            _ => null,
        };

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

    private static int GetParameterTreeOrdinal(
        IParameterSymbol parameter,
        Compilation compilation,
        int fallback) =>
        parameter.DeclaringSyntaxReferences.FirstOrDefault() is { } reference
            ? GetTreeOrdinal(reference.SyntaxTree, compilation)
            : fallback;

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
