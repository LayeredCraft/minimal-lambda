using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using LayeredCraft.SourceGeneratorTools.Types;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using MinimalLambda.SourceGenerators.Models;
using WellKnownType = MinimalLambda.SourceGenerators.WellKnownTypes.WellKnownTypeData.WellKnownType;

namespace MinimalLambda.SourceGenerators;

internal static class DurableSerializerAnalyzer
{
    internal static EquatableArray<DiagnosticInfo> Analyze(
        Compilation compilation,
        ImmutableArray<DurableMethodInfo> handlers,
        CancellationToken cancellationToken)
    {
        var diagnostics = handlers.SelectMany(handler => handler.DiagnosticInfos).ToList();
        var validHandlers = handlers
            .Where(handler => handler.DiagnosticInfos.All(diagnostic =>
                diagnostic.DiagnosticDescriptor.DefaultSeverity != DiagnosticSeverity.Error))
            .OrderBy(handler => handler.TreeOrdinal)
            .ThenBy(handler => handler.MapCallLocation.TextSpan.Start)
            .ToList();

        if (validHandlers.Count != 0)
            diagnostics.AddRange(AnalyzeSerializer(compilation, validHandlers, cancellationToken));

        return diagnostics
            .Distinct()
            .OrderBy(diagnostic => diagnostic, DiagnosticInfoOrderComparer.Instance)
            .ToEquatableArray();
    }

    private static IEnumerable<DiagnosticInfo> AnalyzeSerializer(
        Compilation compilation,
        IReadOnlyList<DurableMethodInfo> handlers,
        CancellationToken cancellationToken)
    {
        var wellKnownTypes = WellKnownTypes.WellKnownTypes.GetOrCreate(compilation);
        var mapsByBlock = new Dictionary<IBlockOperation, List<MapOperation>>(
            ReferenceEqualityComparer<IBlockOperation>.Instance);

        foreach (var handler in handlers)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var tree = compilation.SyntaxTrees.ElementAtOrDefault(handler.TreeOrdinal);
            if (tree is null)
                continue;
            var invocationSyntax = tree
                .GetRoot(cancellationToken)
                .FindNode(handler.MapCallLocation.TextSpan, getInnermostNodeForTie: true)
                .FirstAncestorOrSelf<InvocationExpressionSyntax>();
            if (invocationSyntax is null)
                continue;
            var semanticModel = compilation.GetSemanticModel(tree);
            if (semanticModel.GetOperation(invocationSyntax, cancellationToken) is not
                    IInvocationOperation invocation
                || !IsDurableMap(invocation)
                || !TryGetDirectBlock(invocation, out var block, out var statement)
                || !IsDirectMethodBodyBlock(block)
                || !IsDirectInvocationStatement(invocation, statement))
                continue;

            if (!mapsByBlock.TryGetValue(block, out var maps))
                mapsByBlock.Add(block, maps = []);
            maps.Add(new MapOperation(handler, invocation, statement, semanticModel));
        }

        foreach (var pair in mapsByBlock)
            foreach (var diagnostic in AnalyzeBlock(
                pair.Key,
                pair.Value,
                compilation,
                wellKnownTypes,
                cancellationToken))
                yield return diagnostic;
    }

    private static IEnumerable<DiagnosticInfo> AnalyzeBlock(
        IBlockOperation block,
        IReadOnlyList<MapOperation> validMaps,
        Compilation compilation,
        WellKnownTypes.WellKnownTypes wellKnownTypes,
        CancellationToken cancellationToken)
    {
        var builders = new List<(ILocalSymbol Local, int Index)>();
        var applications = new List<(ILocalSymbol Local, ILocalSymbol Builder, int Index)>();

        for (var i = 0; i < block.Operations.Length; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!TryGetDirectLocalInitializer(
                    block.Operations[i],
                    out var local,
                    out var initializer)
                || Unwrap(initializer) is not IInvocationOperation invocation)
                continue;

            if (IsCreateBuilder(invocation, wellKnownTypes))
                builders.Add((local, i));
            else if (IsBuild(invocation, wellKnownTypes)
                && GetReceiverLocal(invocation) is { } builder)
                applications.Add((local, builder, i));
        }

        foreach (var application in applications)
        {
            var builderMatches =
                builders
                    .Where(builder =>
                        SymbolEqualityComparer.Default.Equals(builder.Local, application.Builder))
                    .ToList();
            if (builderMatches.Count != 1)
                continue;
            var builder = builderMatches[0];
            if (builder.Index >= application.Index)
                continue;

            var maps = validMaps
                .Where(map => SymbolEqualityComparer.Default.Equals(
                    GetReceiverLocal(map.Invocation),
                    application.Local))
                .OrderBy(map => map.Handler.TreeOrdinal)
                .ThenBy(map => map.Handler.MapCallLocation.TextSpan.Start)
                .ToList();
            if (maps.Count == 0)
                continue;

            var sourceRegistrations = new List<IInvocationOperation>();
            var traceable = true;
            for (var i = 0; i < block.Operations.Length && traceable; i++)
            {
                foreach (var operation in block.Operations[i].DescendantsAndSelf())
                {
                    if (operation is ILocalReferenceOperation localReference)
                    {
                        if (SymbolEqualityComparer.Default.Equals(
                                localReference.Local,
                                builder.Local)
                            && !IsAllowedBuilderReference(
                                localReference,
                                block.Operations[i],
                                builder.Local,
                                application.Local,
                                builder.Index,
                                application.Index,
                                i,
                                sourceRegistrations,
                                wellKnownTypes))
                        {
                            traceable = false;
                            break;
                        }

                        if (SymbolEqualityComparer.Default.Equals(
                                localReference.Local,
                                application.Local)
                            && !IsAllowedApplicationReference(
                                localReference,
                                block.Operations[i],
                                application.Local))
                        {
                            traceable = false;
                            break;
                        }
                    }
                }
            }

            if (!traceable || sourceRegistrations.Count != 1)
                continue;

            var registration = sourceRegistrations[0];
            if (registration.TargetMethod.TypeArguments.Length != 1)
                continue;
            var contextType = registration.TargetMethod.TypeArguments[0] as INamedTypeSymbol;
            if (contextType is null
                || !InheritsFrom(
                    contextType,
                    wellKnownTypes.Get(
                        WellKnownType.System_Text_Json_Serialization_JsonSerializerContext)))
                continue;

            if (!TryGetExplicitRoots(contextType, wellKnownTypes, out var explicitRoots))
                continue;

            var roots = new Dictionary<string, int>(StringComparer.Ordinal)
            {
                [DurableTypeInfoFactory.Render(
                        wellKnownTypes.Get(
                            WellKnownType
                                .Amazon_Lambda_DurableExecution_DurableExecutionInvocationInput))] =
                    0,
                [DurableTypeInfoFactory.Render(
                        wellKnownTypes.Get(
                            WellKnownType
                                .Amazon_Lambda_DurableExecution_DurableExecutionInvocationOutput))] =
                    1,
            };
            foreach (var map in maps)
            {
                if (map.Handler.InputCanonicalType is { } input)
                    AddRoot(roots, input, 2);
                if (map.Handler.OutputCanonicalType is { } output)
                    AddRoot(roots, output, 3);
            }

            var contextName = DurableTypeInfoFactory.Render(contextType);
            var registrationLocation = GetContextArgumentLocation(registration)
                ?? maps[0].Handler.HandlerArgumentLocation;
            var treeOrdinal = registration.Syntax.SyntaxTree is { } syntaxTree
                ? DurableMethodInfoExtensions.GetTreeOrdinal(syntaxTree, compilation)
                : maps[0].Handler.TreeOrdinal;

            foreach (var root in roots
                .Where(root => !explicitRoots.Contains(root.Key))
                .OrderBy(root => root.Value)
                .ThenBy(root => root.Key, StringComparer.Ordinal))
                yield return new DiagnosticInfo(
                    Diagnostics.MissingDurableSerializerRoot,
                    registrationLocation,
                    contextName,
                    root.Key) { TreeOrdinal = treeOrdinal, SubKey = root.Value, };
        }
    }

    private static bool IsAllowedBuilderReference(
        ILocalReferenceOperation reference,
        IOperation directStatement,
        ILocalSymbol builder,
        ILocalSymbol application,
        int builderIndex,
        int buildIndex,
        int statementIndex,
        List<IInvocationOperation> sourceRegistrations,
        WellKnownTypes.WellKnownTypes wellKnownTypes)
    {
        if (statementIndex == buildIndex
            && FindAncestorInvocation(reference) is { } build
            && IsBuild(build, wellKnownTypes)
            && SymbolEqualityComparer.Default.Equals(GetReceiverLocal(build), builder))
            return true;

        if (statementIndex == builderIndex)
            return false;

        if (FindAncestorInvocation(reference) is not { } invocation
            || !IsDirectInvocationStatement(invocation, directStatement)
            || !ReferencesBuilderServices(invocation, builder)
            || !IsBuilderServicesReceiverReference(reference, invocation, builder))
            return false;

        if (IsSourceRegistration(invocation, wellKnownTypes))
        {
            if (statementIndex <= builderIndex || statementIndex >= buildIndex)
                return false;
            if (!sourceRegistrations.Contains(invocation))
                sourceRegistrations.Add(invocation);
            return true;
        }

        return statementIndex > builderIndex
            && statementIndex < buildIndex
            && IsSafeServiceRegistration(invocation, wellKnownTypes);
    }

    private static bool IsAllowedApplicationReference(
        ILocalReferenceOperation reference,
        IOperation directStatement,
        ILocalSymbol application)
    {
        if (FindAncestorInvocation(reference) is not { } invocation
            || !IsDirectInvocationStatement(invocation, directStatement)
            || !IsDurableMap(invocation))
            return false;
        return SymbolEqualityComparer.Default.Equals(GetReceiverLocal(invocation), application);
    }

    private static bool TryGetDirectLocalInitializer(
        IOperation statement,
        out ILocalSymbol local,
        out IOperation initializer)
    {
        var declarators = statement
            .DescendantsAndSelf()
            .OfType<IVariableDeclaratorOperation>()
            .Where(declarator => ReferenceEquals(GetDirectStatement(declarator), statement))
            .ToList();
        if (declarators.Count == 1 && declarators[0].Initializer?.Value is { } value)
        {
            local = declarators[0].Symbol;
            initializer = value;
            return true;
        }

        local = null!;
        initializer = null!;
        return false;
    }

    private static bool TryGetDirectBlock(
        IOperation operation,
        out IBlockOperation block,
        out IOperation statement)
    {
        statement = operation;
        while (statement.Parent is not null and not IBlockOperation)
            statement = statement.Parent;
        if (statement.Parent is IBlockOperation parent)
        {
            block = parent;
            return true;
        }

        block = null!;
        return false;
    }

    private static IOperation? GetDirectStatement(IOperation operation)
    {
        var current = operation;
        while (current.Parent is not null and not IBlockOperation)
            current = current.Parent;
        return current.Parent is IBlockOperation ? current : null;
    }

    private static bool IsDirectMethodBodyBlock(IBlockOperation block) =>
        block.Syntax switch
        {
            CompilationUnitSyntax => true,
            BlockSyntax { Parent: BaseMethodDeclarationSyntax, } => true,
            _ => false,
        };

    private static bool IsDirectInvocationStatement(
        IInvocationOperation invocation,
        IOperation directStatement) =>
        directStatement is IExpressionStatementOperation expression
        && ReferenceEquals(Unwrap(expression.Operation), invocation);

    private static IInvocationOperation? FindAncestorInvocation(IOperation operation)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
            if (current is IInvocationOperation invocation)
                return invocation;
        return null;
    }

    private static ILocalSymbol? GetReceiverLocal(IInvocationOperation invocation)
    {
        var receiver = invocation.Instance;
        if (receiver is null
            && invocation.TargetMethod.IsExtensionMethod
            && invocation.Arguments.Length != 0)
            receiver = invocation.Arguments[0].Value;
        receiver = Unwrap(receiver);
        return receiver is ILocalReferenceOperation local ? local.Local : null;
    }

    private static IOperation? Unwrap(IOperation? operation)
    {
        while (operation is IConversionOperation conversion)
            operation = conversion.Operand;
        while (operation is IArgumentOperation argument)
            operation = argument.Value;
        return operation;
    }

    private static bool ReferencesBuilderServices(
        IInvocationOperation invocation,
        ILocalSymbol builder) =>
        invocation
            .DescendantsAndSelf()
            .OfType<IPropertyReferenceOperation>()
            .Any(property => IsBuilderServicesProperty(property, builder));

    private static bool IsBuilderServicesReceiverReference(
        ILocalReferenceOperation reference,
        IInvocationOperation invocation,
        ILocalSymbol builder)
    {
        if (reference.Parent is not IPropertyReferenceOperation property
            || !IsBuilderServicesProperty(property, builder))
            return false;

        var receiver = invocation.Instance;
        if (receiver is null
            && invocation.TargetMethod.IsExtensionMethod
            && invocation.Arguments.Length != 0)
            receiver = invocation.Arguments[0].Value;
        return ReferenceEquals(Unwrap(receiver), property);
    }

    private static bool IsBuilderServicesProperty(
        IPropertyReferenceOperation property,
        ILocalSymbol builder) =>
        property.Property.Name == "Services"
        && Unwrap(property.Instance) is ILocalReferenceOperation local
        && SymbolEqualityComparer.Default.Equals(local.Local, builder);

    private static bool IsCreateBuilder(
        IInvocationOperation invocation,
        WellKnownTypes.WellKnownTypes types) =>
        invocation.TargetMethod.Name == "CreateBuilder"
        && invocation.TargetMethod.ContainingAssembly.Name == "MinimalLambda"
        && SymbolEqualityComparer.Default.Equals(
            invocation.TargetMethod.ReturnType,
            types.Get(WellKnownType.MinimalLambda_Builder_LambdaApplicationBuilder));

    private static bool IsBuild(
        IInvocationOperation invocation,
        WellKnownTypes.WellKnownTypes types) =>
        invocation.TargetMethod.Name == "Build"
        && SymbolEqualityComparer.Default.Equals(
            invocation.TargetMethod.ContainingType,
            types.Get(WellKnownType.MinimalLambda_Builder_LambdaApplicationBuilder));

    private static bool IsDurableMap(IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod.ReducedFrom ?? invocation.TargetMethod;
        return method.Name == "MapDurableHandler"
            && method.ContainingAssembly.Name == "MinimalLambda.DurableExecution";
    }

    private static bool IsSourceRegistration(
        IInvocationOperation invocation,
        WellKnownTypes.WellKnownTypes types)
    {
        var method = invocation.TargetMethod.ReducedFrom ?? invocation.TargetMethod;
        return method.Name == "AddLambdaSerializerWithContext"
            && method.ContainingAssembly.Name == "MinimalLambda";
    }

    private static bool IsSafeServiceRegistration(
        IInvocationOperation invocation,
        WellKnownTypes.WellKnownTypes types)
    {
        var method = invocation.TargetMethod.ReducedFrom ?? invocation.TargetMethod;
        if (method.ContainingAssembly.Name
            != "Microsoft.Extensions.DependencyInjection.Abstractions"
            && method.ContainingAssembly.Name != "Microsoft.Extensions.DependencyInjection")
            return false;
        if (method.Name is not ("AddSingleton" or "AddScoped" or "AddTransient")
            || invocation.TargetMethod.TypeArguments.Length == 0
            || invocation.Arguments.Any(argument =>
                argument.Parameter?.Type.SpecialType != SpecialType.None))
            return false;

        var serviceType = invocation.TargetMethod.TypeArguments[0];
        if (!IsClosedResolvedType(serviceType))
            return false;

        var serializer = types.Get(WellKnownType.Amazon_Lambda_Core_ILambdaSerializer);
        return !SymbolEqualityComparer.Default.Equals(serviceType, serializer)
            && !serviceType.AllInterfaces.Contains(serializer, SymbolEqualityComparer.Default);
    }

    private static bool IsClosedResolvedType(ITypeSymbol type)
    {
        if (type is IErrorTypeSymbol or ITypeParameterSymbol)
            return false;

        return type switch
        {
            IArrayTypeSymbol array => IsClosedResolvedType(array.ElementType),
            IPointerTypeSymbol pointer => IsClosedResolvedType(pointer.PointedAtType),
            IFunctionPointerTypeSymbol functionPointer =>
                IsClosedResolvedType(functionPointer.Signature.ReturnType)
                && functionPointer.Signature.Parameters.All(parameter =>
                    IsClosedResolvedType(parameter.Type)),
            INamedTypeSymbol named => !named.IsUnboundGenericType
                && (named.ContainingType is null || IsClosedResolvedType(named.ContainingType))
                && named.TypeArguments.All(IsClosedResolvedType),
            _ => true,
        };
    }

    private static bool InheritsFrom(INamedTypeSymbol type, INamedTypeSymbol baseType)
    {
        for (var current = type; current is not null; current = current.BaseType)
            if (SymbolEqualityComparer.Default.Equals(current, baseType))
                return true;
        return false;
    }

    private static bool TryGetExplicitRoots(
        INamedTypeSymbol context,
        WellKnownTypes.WellKnownTypes types,
        out HashSet<string> roots)
    {
        roots = new HashSet<string>(StringComparer.Ordinal);
        var attributeType = types.Get(
            WellKnownType.System_Text_Json_Serialization_JsonSerializableAttribute);
        for (var current = context; current is not null; current = current.BaseType)
            foreach (var attribute in current.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeType))
                    continue;
                if (attribute.ConstructorArguments.Length == 0
                    || attribute.ConstructorArguments[0] is not
                    {
                        Kind: TypedConstantKind.Type, Value: ITypeSymbol root,
                    }
                    || root.TypeKind == TypeKind.Error)
                    return false;
                roots.Add(DurableTypeInfoFactory.Render(root));
            }

        return true;
    }

    private static LocationInfo? GetContextArgumentLocation(IInvocationOperation registration) =>
        registration
            .Syntax
            .DescendantNodesAndSelf()
            .OfType<GenericNameSyntax>()
            .FirstOrDefault(name => name.Identifier.ValueText == "AddLambdaSerializerWithContext")
            ?.TypeArgumentList
            .Arguments
            .FirstOrDefault()
            ?.GetLocation()
            .ToLocationInfo();

    private static void AddRoot(Dictionary<string, int> roots, string root, int rank)
    {
        if (!roots.TryGetValue(root, out var existing) || rank < existing)
            roots[root] = rank;
    }

    private sealed record MapOperation(
        DurableMethodInfo Handler,
        IInvocationOperation Invocation,
        IOperation Statement,
        SemanticModel SemanticModel);

    private sealed class ReferenceEqualityComparer<T> : IEqualityComparer<T> where T : class
    {
        internal static readonly ReferenceEqualityComparer<T> Instance = new();
        public bool Equals(T? x, T? y) => ReferenceEquals(x, y);

        public int GetHashCode(T obj) =>
            System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
    }
}
