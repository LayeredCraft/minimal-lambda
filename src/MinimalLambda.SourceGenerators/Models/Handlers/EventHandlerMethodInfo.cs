using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LayeredCraft.SourceGeneratorTools.Types;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using MinimalLambda.SourceGenerators.Extensions;
using WellKnownType = MinimalLambda.SourceGenerators.WellKnownTypes.WellKnownTypeData.WellKnownType;

namespace MinimalLambda.SourceGenerators.Models;

internal enum EventHandlerFamily
{
    N1,
    N2,
    N3,
    R1,
    R2,
}

internal record EventHandlerCallbackInfo(string Name, string Type);

internal record EventHandlerMethodInfo(
    string InterceptableLocationAttribute,
    EquatableArray<EventHandlerCallbackInfo> CallbackParameters,
    EventHandlerFamily Family,
    bool HasResponse,
    bool HasOnError,
    bool CollectsValues,
    bool CollectsSourceItems,
    bool CollectsMappedFailures,
    bool HandleReturnsResult,
    string EventType,
    string ItemType,
    string CollectedType,
    string ValueType,
    string? ResponseType,
    EquatableArray<EventHandlerParameterInfo> HandlerParameters,
    string HandlerArguments,
    bool HasAnyFromKeyedServices,
    LocationInfo MapCallLocation,
    LocationInfo HandlerArgumentLocation,
    int TreeOrdinal,
    int SourceSpanStart,
    EquatableArray<DiagnosticInfo> DiagnosticInfos,
    MethodType MethodType = MethodType.MapEventHandler) : IMethodInfo;

internal static class EventHandlerMethodInfoExtensions
{
    extension(EventHandlerMethodInfo)
    {
        internal static EventHandlerMethodInfo Create(
            IInvocationOperation invocation,
            GeneratorContext context)
        {
            if (!InterceptableLocationInfo.TryGet(context, out var interceptableLocation))
                throw new InvalidOperationException("Unable to get interceptable location");

            var diagnostics = new List<DiagnosticInfo>();
            var method = invocation.TargetMethod;
            var compilation = context.SemanticModel.Compilation;
            var invocationLocation =
                LocationInfo.Create(context.Node)
                ?? throw new InvalidOperationException("Event mapping has no source location");
            var handleArgument = GetArgument(invocation, "handle");
            var handlerSyntax = handleArgument is null
                ? context.Node
                : GetHandlerSyntax(handleArgument);
            var handlerArgumentLocation = LocationInfo.Create(handlerSyntax) ?? invocationLocation;
            var genericTypes = GetGenericTypes(method);

            if (!genericTypes.TryGetValue("TEvent", out var eventType))
                diagnostics.Add(
                    CreateDiagnostic(
                        Diagnostics.UnsupportedEventHandlerSignature,
                        handlerArgumentLocation,
                        "TEvent"));
            if (!genericTypes.TryGetValue("TItem", out var itemType))
                diagnostics.Add(
                    CreateDiagnostic(
                        Diagnostics.UnsupportedEventHandlerSignature,
                        handlerArgumentLocation,
                        "TItem"));

            ValidateAccessibleType(eventType, "TEvent", handlerArgumentLocation, diagnostics);
            ValidateAccessibleType(itemType, "TItem", handlerArgumentLocation, diagnostics);

            if (eventType is not null && IsStream(eventType, context))
                diagnostics.Add(
                    CreateDiagnostic(
                        Diagnostics.UnsupportedEventHandlerSignature,
                        handlerArgumentLocation,
                        "stream TEvent"));

            var handleDelegate = handleArgument?.Parameter?.Type as INamedTypeSymbol;
            var handleReturn = handleDelegate?.DelegateInvokeMethod?.ReturnType;
            var handleReturnsResult = TryUnwrapTask(
                handleReturn,
                context,
                out var handleResultType);

            var onErrorArgument = GetArgument(invocation, "onError");
            var createResponseArgument = GetArgument(invocation, "createResponse");
            var family = DetermineFamily(
                handleReturnsResult,
                onErrorArgument is not null,
                createResponseArgument is not null);
            var hasResponse = family is not EventHandlerFamily.N1;
            var hasOnError = family is EventHandlerFamily.N3 or EventHandlerFamily.R2;
            var collectValues = family is not EventHandlerFamily.N1;
            var collectsSourceItems = family is EventHandlerFamily.N2;
            var collectsMappedFailures = family is EventHandlerFamily.N3;

            string? collectedType = null;
            switch (family)
            {
                case EventHandlerFamily.N2:
                    collectedType = itemType?.QualifiedNullableName;
                    break;
                case EventHandlerFamily.N3:
                {
                    genericTypes.TryGetValue("TFailure", out var failureType);
                    ValidateAccessibleType(
                        failureType,
                        "TFailure",
                        handlerArgumentLocation,
                        diagnostics);
                    collectedType = failureType?.QualifiedNullableName;
                    break;
                }
                case EventHandlerFamily.R1:
                case EventHandlerFamily.R2:
                    ValidateAccessibleType(
                        handleResultType,
                        "TResult",
                        handlerArgumentLocation,
                        diagnostics);
                    collectedType = handleResultType?.QualifiedNullableName;
                    break;
            }

            string? responseType = null;
            if (hasResponse)
            {
                genericTypes.TryGetValue("TResponse", out var declaredResponseType);
                ValidateAccessibleType(
                    declaredResponseType,
                    "TResponse",
                    handlerArgumentLocation,
                    diagnostics);

                if (declaredResponseType is not null)
                {
                    if (IsAwaitable(declaredResponseType))
                        diagnostics.Add(
                            CreateDiagnostic(
                                Diagnostics.UnsupportedEventHandlerSignature,
                                handlerArgumentLocation,
                                "awaitable TResponse"));

                    if (IsStream(declaredResponseType, context))
                        diagnostics.Add(
                            CreateDiagnostic(
                                Diagnostics.UnsupportedEventHandlerSignature,
                                handlerArgumentLocation,
                                "stream TResponse"));
                }

                responseType = declaredResponseType?.QualifiedNullableName;
            }

            var reassigned = false;
            IMethodSymbol? handlerMethod = null;
            if (handleArgument is not null)
                handlerMethod = ResolveHandlerMethod(
                    handleArgument.Value,
                    context.SemanticModel,
                    context.CancellationToken,
                    out reassigned);

            if (handlerMethod is null)
                diagnostics.Add(
                    CreateDiagnostic(
                        Diagnostics.UnresolvedEventHandler,
                        handlerArgumentLocation,
                        reassigned
                            ? $"{handleArgument?.Parameter?.Name ?? "handle"} (reassigned local)"
                            : handleArgument?.Parameter?.Name ?? "handle"));
            else if (family is EventHandlerFamily.N1
                    or EventHandlerFamily.N2
                    or EventHandlerFamily.N3
                && TryUnwrapTask(handlerMethod.ReturnType, context, out _))
                diagnostics.Add(
                    CreateDiagnostic(
                        Diagnostics.DiscardedEventHandlerResult,
                        handlerArgumentLocation,
                        handlerMethod.Name));

            var parameters = handlerMethod is null
                ? []
                : CreateParameters(handlerMethod, itemType, context, diagnostics);

            var valueType = family switch
            {
                EventHandlerFamily.N1 => "global::System.Object",
                EventHandlerFamily.N2 => itemType?.QualifiedNullableName ?? "global::System.Object",
                _ => collectedType ?? "global::System.Object",
            };

            var hasAnyKeyed = parameters.Any(parameter =>
                parameter.Kind is EventHandlerParameterKind.KeyedService);

            var callbackParameters = new List<EventHandlerCallbackInfo>(4);
            AddCallback(invocation, "split", callbackParameters);
            AddCallback(invocation, "handle", callbackParameters);
            if (hasOnError)
                AddCallback(invocation, "onError", callbackParameters);
            if (hasResponse)
                AddCallback(invocation, "createResponse", callbackParameters);

            return new EventHandlerMethodInfo(
                InterceptableLocationAttribute: interceptableLocation.Attribute,
                CallbackParameters: callbackParameters.ToEquatableArray(),
                Family: family,
                HasResponse: hasResponse,
                HasOnError: hasOnError,
                CollectsValues: collectValues,
                CollectsSourceItems: collectsSourceItems,
                CollectsMappedFailures: collectsMappedFailures,
                HandleReturnsResult: handleReturnsResult,
                EventType: eventType?.QualifiedNullableName ?? "global::System.Object",
                ItemType: itemType?.QualifiedNullableName ?? "global::System.Object",
                CollectedType: collectedType ?? "global::System.Object",
                ValueType: valueType,
                ResponseType: responseType,
                HandlerParameters: parameters.ToEquatableArray(),
                HandlerArguments: string.Join(
                    ", ",
                    parameters.Select(parameter => $"arg{parameter.Ordinal}")),
                HasAnyFromKeyedServices: hasAnyKeyed,
                MapCallLocation: invocationLocation,
                HandlerArgumentLocation: handlerArgumentLocation,
                TreeOrdinal: DurableMethodInfoExtensions.GetTreeOrdinal(
                    context.Node.SyntaxTree,
                    compilation),
                SourceSpanStart: context.Node.SpanStart,
                DiagnosticInfos: diagnostics.ToEquatableArray());
        }
    }

    private static void AddCallback(
        IInvocationOperation invocation,
        string name,
        List<EventHandlerCallbackInfo> callbacks)
    {
        var argument = GetArgument(invocation, name);
        if (argument?.Parameter is { } parameter)
            callbacks.Add(
                new EventHandlerCallbackInfo(parameter.Name, parameter.Type.QualifiedNullableName));
    }

    private static EventHandlerFamily DetermineFamily(
        bool handleReturnsResult,
        bool hasOnError,
        bool hasCreateResponse)
    {
        if (handleReturnsResult)
            return hasOnError ? EventHandlerFamily.R2 : EventHandlerFamily.R1;

        if (!hasCreateResponse)
            return EventHandlerFamily.N1;

        return hasOnError ? EventHandlerFamily.N3 : EventHandlerFamily.N2;
    }

    private static List<EventHandlerParameterInfo> CreateParameters(
        IMethodSymbol handlerMethod,
        ITypeSymbol? itemType,
        GeneratorContext context,
        List<DiagnosticInfo> diagnostics)
    {
        var parameters = new List<EventHandlerParameterInfo>(handlerMethod.Parameters.Length);
        var contextCount = 0;
        var processingCount = 0;
        var tokenCount = 0;

        for (var index = 0; index < handlerMethod.Parameters.Length; index++)
        {
            var parameter = handlerMethod.Parameters[index];
            var location = GetParameterLocation(parameter, handlerMethod);
            var parameterDiagnostics = new List<DiagnosticInfo>();

            if (parameter.RefKind != RefKind.None)
                parameterDiagnostics.Add(
                    CreateDiagnostic(
                        Diagnostics.UnsupportedEventHandlerSignature,
                        location,
                        $"{parameter.RefKind.ToString().ToLowerInvariant()} {parameter.Name}"));

            ValidateAccessibleType(parameter.Type, parameter.Name, location, parameterDiagnostics);

            var isEventAttribute = IsEventAttribute(parameter, context);
            var isServiceAttribute = parameter.IsServiceBindingAttribute(context);

            if (isEventAttribute)
                parameterDiagnostics.Add(
                    CreateDiagnostic(
                        Diagnostics.UnsupportedEventHandlerBinding,
                        location,
                        $"[FromEvent] {parameter.Name}"));

            var kind = EventHandlerParameterKind.Service;
            var assignment = string.Empty;
            var isOptional = parameter.IsOptional
                || parameter.NullableAnnotation == NullableAnnotation.Annotated;
            string? keyedKey = null;

            if (index == 0)
            {
                kind = EventHandlerParameterKind.Item;
                assignment = "item";
                if (isServiceAttribute)
                    parameterDiagnostics.Add(
                        CreateDiagnostic(
                            Diagnostics.UnsupportedEventHandlerBinding,
                            location,
                            $"service attribute on item parameter '{parameter.Name}'"));
                if (itemType is not null
                    && !context.SemanticModel.Compilation
                        .ClassifyCommonConversion(itemType, parameter.Type)
                        .IsImplicit)
                    parameterDiagnostics.Add(
                        CreateDiagnostic(
                            Diagnostics.UnsupportedEventHandlerSignature,
                            location,
                            $"item parameter '{parameter.Name}'"));
            }
            else if (context.WellKnownTypes.IsType(
                parameter.Type,
                WellKnownType.Amazon_Lambda_Core_ILambdaContext,
                WellKnownType.MinimalLambda_ILambdaInvocationContext))
            {
                kind = EventHandlerParameterKind.Context;
                assignment = "processingContext.Invocation";
                contextCount++;
                if (contextCount > 1)
                    parameterDiagnostics.Add(
                        CreateDiagnostic(
                            Diagnostics.UnsupportedEventHandlerBinding,
                            location,
                            $"duplicate context parameter '{parameter.Name}'"));
                if (isServiceAttribute)
                    parameterDiagnostics.Add(
                        CreateDiagnostic(
                            Diagnostics.UnsupportedEventHandlerBinding,
                            location,
                            $"service attribute on context parameter '{parameter.Name}'"));
            }
            else if (context.WellKnownTypes.IsType(
                parameter.Type,
                WellKnownType.MinimalLambda_IEventProcessingContext))
            {
                kind = EventHandlerParameterKind.ProcessingContext;
                assignment = "processingContext";
                processingCount++;
                if (processingCount > 1)
                    parameterDiagnostics.Add(
                        CreateDiagnostic(
                            Diagnostics.UnsupportedEventHandlerBinding,
                            location,
                            $"duplicate processing context parameter '{parameter.Name}'"));
                if (isServiceAttribute)
                    parameterDiagnostics.Add(
                        CreateDiagnostic(
                            Diagnostics.UnsupportedEventHandlerBinding,
                            location,
                            $"service attribute on processing context parameter '{parameter.Name}'"));
            }
            else if (context.WellKnownTypes.IsType(
                parameter.Type,
                WellKnownType.System_Threading_CancellationToken))
            {
                kind = EventHandlerParameterKind.CancellationToken;
                assignment = "processingContext.CancellationToken";
                tokenCount++;
                if (tokenCount > 1)
                    parameterDiagnostics.Add(
                        CreateDiagnostic(
                            Diagnostics.UnsupportedEventHandlerBinding,
                            location,
                            $"duplicate cancellation token parameter '{parameter.Name}'"));
                if (isServiceAttribute)
                    parameterDiagnostics.Add(
                        CreateDiagnostic(
                            Diagnostics.UnsupportedEventHandlerBinding,
                            location,
                            $"service attribute on cancellation token parameter '{parameter.Name}'"));
            }
            else
            {
                var diResult = parameter.GetDiParameterAssignment(context, "serviceProvider");
                if (diResult.IsSuccess)
                {
                    if (diResult.Value!.Key is not null)
                    {
                        kind = EventHandlerParameterKind.KeyedService;
                        keyedKey = diResult.Value.Key;
                    }

                    assignment = diResult.Value.Assignment;
                }
                else if (diResult.Error is { } error)
                    parameterDiagnostics.Add(error);
            }

            parameters.Add(
                new EventHandlerParameterInfo(
                    parameter.Ordinal,
                    parameter.Name,
                    parameter.Type.QualifiedNullableName,
                    kind,
                    assignment,
                    isOptional,
                    keyedKey,
                    location,
                    parameterDiagnostics.ToEquatableArray()));

            diagnostics.AddRange(parameterDiagnostics);
        }

        if (handlerMethod.Parameters.Length is < 1 or > 11)
            diagnostics.Add(
                CreateDiagnostic(
                    Diagnostics.UnsupportedEventHandlerSignature,
                    GetParameterLocationOrNull(handlerMethod.Parameters.LastOrDefault(), null)
                    ?? LocationInfo.Create(handlerMethod)
                    ?? new LocationInfo(string.Empty, default, default),
                    $"{handlerMethod.Name} parameter count {handlerMethod.Parameters.Length}"));

        return parameters;
    }

    private static void ValidateAccessibleType(
        ITypeSymbol? type,
        string component,
        LocationInfo location,
        List<DiagnosticInfo> diagnostics)
    {
        if (type is not null && !IsAccessibleFromGeneratedAdapter(type))
            diagnostics.Add(
                CreateDiagnostic(
                    Diagnostics.UnsupportedEventHandlerSignature,
                    location,
                    component));
    }

    private static bool IsAccessibleFromGeneratedAdapter(ITypeSymbol type) =>
        !ContainsTypeParameter(type) && IsAccessibleFromGeneratedAdapterCore(type);

    private static bool IsAccessibleFromGeneratedAdapterCore(ITypeSymbol type) =>
        type switch
        {
            IArrayTypeSymbol array => IsAccessibleFromGeneratedAdapterCore(array.ElementType),
            IPointerTypeSymbol => false,
            IFunctionPointerTypeSymbol => false,
            ITypeParameterSymbol => false,
            IDynamicTypeSymbol => false,
            INamedTypeSymbol named => IsNamedTypeAccessibleFromGeneratedAdapter(named),
            _ => !type.IsRefLikeType,
        };

    private static bool IsNamedTypeAccessibleFromGeneratedAdapter(INamedTypeSymbol type)
    {
        if (type.IsAnonymousType || type.IsRefLikeType)
            return false;

        for (INamedTypeSymbol? current = type;
            current is not null;
            current = current.ContainingType)
            if (current.IsFileLocal
                || current.DeclaredAccessibility is not (Accessibility.Public
                    or Accessibility.Internal
                    or Accessibility.ProtectedOrInternal))
                return false;

        return type.TypeArguments.All(IsAccessibleFromGeneratedAdapter);
    }

    private static bool ContainsTypeParameter(ITypeSymbol type) =>
        type switch
        {
            ITypeParameterSymbol => true,
            IArrayTypeSymbol array => ContainsTypeParameter(array.ElementType),
            IPointerTypeSymbol pointer => ContainsTypeParameter(pointer.PointedAtType),
            IFunctionPointerTypeSymbol => true,
            INamedTypeSymbol named => (named.ContainingType is not null
                    && ContainsTypeParameter(named.ContainingType))
                || named.TypeArguments.Any(ContainsTypeParameter),
            _ => false,
        };

    private static bool IsEventAttribute(IParameterSymbol parameter, GeneratorContext context) =>
        parameter
            .GetAttributes()
            .Any(attribute => attribute.AttributeClass is not null
                && context.WellKnownTypes.IsType(
                    attribute.AttributeClass,
                    WellKnownType.MinimalLambda_Builder_EventAttribute,
                    WellKnownType.MinimalLambda_Builder_FromEventAttribute));

    private static bool IsStream(ITypeSymbol type, GeneratorContext context) =>
        context.WellKnownTypes.IsType(type, WellKnownType.System_IO_Stream);

    private static bool IsAwaitable(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
            return false;

        var metadataName = named.OriginalDefinition.ToDisplayString();
        return metadataName is "System.Threading.Tasks.Task"
                or "System.Threading.Tasks.Task<TResult>"
                or "System.Threading.Tasks.ValueTask"
                or "System.Threading.Tasks.ValueTask<TResult>"
            || named
                .GetMembers("GetAwaiter")
                .OfType<IMethodSymbol>()
                .Any(method => method.Parameters.Length == 0 && !method.IsStatic);
    }

    private static bool TryUnwrapTask(
        ITypeSymbol? type,
        GeneratorContext context,
        out ITypeSymbol? resultType)
    {
        resultType = null;
        if (type is not INamedTypeSymbol named)
            return false;

        var taskOfT = context.WellKnownTypes.Get(WellKnownType.System_Threading_Tasks_Task_T);
        if (!SymbolEqualityComparer.Default.Equals(named.OriginalDefinition, taskOfT))
            return false;

        resultType = named.TypeArguments[0];
        return true;
    }

    private static Dictionary<string, ITypeSymbol> GetGenericTypes(IMethodSymbol method)
    {
        var types = new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal);
        var original = method.OriginalDefinition;
        for (var index = 0; index < original.TypeParameters.Length; index++)
            types[original.TypeParameters[index].Name] = method.TypeArguments[index];
        return types;
    }

    private static IArgumentOperation? GetArgument(IInvocationOperation invocation, string name)
    {
        foreach (var argument in invocation.Arguments)
            if (argument.Parameter?.Name == name)
                return argument;

        return null;
    }

    private static IMethodSymbol? ResolveHandlerMethod(
        IOperation operation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken,
        out bool reassigned)
    {
        reassigned = false;
        var wasReassigned = false;
        var result = Resolve(operation);
        reassigned = wasReassigned;
        return result;

        IMethodSymbol? Resolve(IOperation current)
        {
            switch (current)
            {
                case IArgumentOperation argument:
                    return Resolve(argument.Value);
                case IConversionOperation conversion:
                    return Resolve(conversion.Operand);
                case IDelegateCreationOperation delegateCreation:
                    return Resolve(delegateCreation.Target);
                case IFieldReferenceOperation { Field.IsReadOnly: true } field
                    when GetFieldInitializerOperation(
                        field.Field,
                        semanticModel.Compilation,
                        cancellationToken) is { } initializer:
                    return Resolve(initializer);
                case ILocalReferenceOperation local:
                    if (HasAdditionalWrite(
                        local.Local,
                        semanticModel.Compilation,
                        cancellationToken))
                    {
                        wasReassigned = true;
                        return null;
                    }

                    return GetLocalInitializerOperation(
                        local.Local,
                        semanticModel.Compilation,
                        cancellationToken) is { } localInitializer
                        ? Resolve(localInitializer)
                        : null;
                case IAnonymousFunctionOperation anonymous:
                    return anonymous.Symbol;
                case ILocalFunctionOperation localFunction:
                    return localFunction.Symbol;
                case IMethodReferenceOperation methodReference:
                    return methodReference.Method;
                case IParenthesizedOperation parenthesized:
                    return Resolve(parenthesized.Operand);
                default:
                    return null;
            }
        }
    }

    private static IOperation? GetLocalInitializerOperation(
        ILocalSymbol local,
        Compilation compilation,
        CancellationToken cancellationToken) =>
        local
            .DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(cancellationToken))
            .OfType<VariableDeclaratorSyntax>()
            .Where(declarator => declarator.Initializer is not null)
            .Select(declarator =>
                compilation
                    .GetSemanticModel(declarator.SyntaxTree)
                    .GetOperation(declarator.Initializer!.Value, cancellationToken))
            .FirstOrDefault(operation => operation is not null);

    private static bool HasAdditionalWrite(
        ILocalSymbol local,
        Compilation compilation,
        CancellationToken cancellationToken)
    {
        foreach (var reference in local.DeclaringSyntaxReferences)
        {
            var declaration = reference.GetSyntax(cancellationToken);
            var tree = declaration.SyntaxTree;
            var model = compilation.GetSemanticModel(tree);
            foreach (var node in tree.GetRoot(cancellationToken).DescendantNodes())
            {
                if (node is not IdentifierNameSyntax identifier
                    || identifier.Identifier.ValueText != local.Name)
                    continue;

                if (!SymbolEqualityComparer.Default.Equals(
                    model.GetSymbolInfo(identifier, cancellationToken).Symbol,
                    local))
                    continue;

                SyntaxNode target = identifier;
                while (true)
                    if (target.Parent is ArgumentSyntax { Parent: TupleExpressionSyntax tuple })
                        target = tuple;
                    else if (target.Parent is ParenthesizedExpressionSyntax parenthesized)
                        target = parenthesized;
                    else
                        break;

                var isWrite = target.Parent switch
                {
                    AssignmentExpressionSyntax assignment when assignment.Left == target => true,
                    PrefixUnaryExpressionSyntax prefix => prefix.IsKind(
                            SyntaxKind.PreIncrementExpression)
                        || prefix.IsKind(SyntaxKind.PreDecrementExpression),
                    PostfixUnaryExpressionSyntax postfix => postfix.IsKind(
                            SyntaxKind.PostIncrementExpression)
                        || postfix.IsKind(SyntaxKind.PostDecrementExpression),
                    ArgumentSyntax argument => !argument.RefKindKeyword.IsKind(SyntaxKind.None),
                    RefExpressionSyntax => true,
                    _ => false,
                };

                if (isWrite)
                    return true;
            }
        }

        return false;
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

    private static LocationInfo GetParameterLocation(
        IParameterSymbol parameter,
        IMethodSymbol method)
    {
        var location = parameter
            .DeclaringSyntaxReferences
            .Select(reference => LocationInfo.Create(reference.GetSyntax()))
            .FirstOrDefault(candidate => candidate is not null);

        if (location is not null)
            return location.Value;

        var methodLocation = method
            .DeclaringSyntaxReferences
            .Select(reference => LocationInfo.Create(reference.GetSyntax()))
            .FirstOrDefault(candidate => candidate is not null);

        return methodLocation ?? new LocationInfo(string.Empty, default, default);
    }

    private static LocationInfo? GetParameterLocationOrNull(
        IParameterSymbol? parameter,
        LocationInfo? fallback) =>
        parameter
            ?.DeclaringSyntaxReferences
            .Select(reference => LocationInfo.Create(reference.GetSyntax()))
            .FirstOrDefault(candidate => candidate is not null)
        ?? fallback;

    private static SyntaxNode GetHandlerSyntax(IArgumentOperation argument) =>
        argument.Syntax is ArgumentSyntax argumentSyntax
            ? argumentSyntax.Expression
            : argument.Syntax;

    private static DiagnosticInfo CreateDiagnostic(
        DiagnosticDescriptor descriptor,
        LocationInfo location,
        params object?[] arguments) =>
        new(descriptor, location, arguments);
}
