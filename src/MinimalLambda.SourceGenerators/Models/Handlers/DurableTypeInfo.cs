using System;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using Microsoft.CodeAnalysis;
using WellKnownType = MinimalLambda.SourceGenerators.WellKnownTypes.WellKnownTypeData.WellKnownType;

namespace MinimalLambda.SourceGenerators.Models;

[Flags]
internal enum DurableTypeIssue
{
    None = 0,
    Envelope = 1,
    Stream = 2,
    RefLikePointerOrFunctionPointer = 4,
    TypeParameter = 8,
    Inaccessible = 16,
}

internal readonly record struct DurableTypeInfo(string CanonicalName, DurableTypeIssue Issues);

internal static class DurableTypeInfoFactory
{
    internal static DurableTypeInfo Create(
        ITypeSymbol type,
        GeneratorContext context,
        bool recurseTransport)
    {
        var issues = DurableTypeIssue.None;
        Visit(type, context, recurseTransport, true, ref issues);
        return new DurableTypeInfo(Render(type), issues);
    }

    internal static string Render(ITypeSymbol type)
    {
        var builder = new StringBuilder();
        Append(type, builder);
        return builder.ToString();
    }

    private static void Visit(
        ITypeSymbol type,
        GeneratorContext context,
        bool recurseTransport,
        bool isRoot,
        ref DurableTypeIssue issues)
    {
        context.ThrowIfCancellationRequested();

        if (type is ITypeParameterSymbol)
            issues |= DurableTypeIssue.TypeParameter;

        if (type.IsRefLikeType || type is IPointerTypeSymbol or IFunctionPointerTypeSymbol)
            issues |= DurableTypeIssue.RefLikePointerOrFunctionPointer;

        if (type is IErrorTypeSymbol
            || type is INamedTypeSymbol { IsAnonymousType: true, }
            || type is INamedTypeSymbol { IsFileLocal: true }
            || type is INamedTypeSymbol named
            && !IsAccessible(named, context.SemanticModel.Compilation))
            issues |= DurableTypeIssue.Inaccessible;

        if (isRoot || recurseTransport)
        {
            if (context.WellKnownTypes.IsType(
                type,
                WellKnownType.Amazon_Lambda_DurableExecution_DurableExecutionInvocationInput,
                WellKnownType.Amazon_Lambda_DurableExecution_DurableExecutionInvocationOutput))
                issues |= DurableTypeIssue.Envelope;

            if (IsStream(type, context))
                issues |= DurableTypeIssue.Stream;
        }

        switch (type)
        {
            case IArrayTypeSymbol array:
                Visit(array.ElementType, context, recurseTransport, false, ref issues);
                break;
            case IPointerTypeSymbol pointer:
                Visit(pointer.PointedAtType, context, recurseTransport, false, ref issues);
                break;
            case IFunctionPointerTypeSymbol functionPointer:
                Visit(
                    functionPointer.Signature.ReturnType,
                    context,
                    recurseTransport,
                    false,
                    ref issues);
                foreach (var parameter in functionPointer.Signature.Parameters)
                    Visit(parameter.Type, context, recurseTransport, false, ref issues);
                break;
            case INamedTypeSymbol namedType:
                if (namedType.ContainingType is not null)
                    Visit(namedType.ContainingType, context, recurseTransport, false, ref issues);
                foreach (var argument in namedType.TypeArguments)
                    Visit(argument, context, recurseTransport, false, ref issues);
                break;
        }
    }

    private static bool IsAccessible(INamedTypeSymbol type, Compilation compilation)
    {
        for (INamedTypeSymbol? current = type;
            current is not null;
            current = current.ContainingType)
            if (!compilation.IsSymbolAccessibleWithin(current, compilation.Assembly))
                return false;
        return true;
    }

    private static bool IsStream(ITypeSymbol type, GeneratorContext context)
    {
        var stream = context.WellKnownTypes.Get(WellKnownType.System_IO_Stream);
        if (SymbolEqualityComparer.Default.Equals(type, stream))
            return true;

        if (type is INamedTypeSymbol named)
        {
            for (var current = named.BaseType; current is not null; current = current.BaseType)
                if (SymbolEqualityComparer.Default.Equals(current, stream))
                    return true;

            return named.AllInterfaces.Any(@interface =>
                SymbolEqualityComparer.Default.Equals(@interface, stream));
        }

        return false;
    }

    private static void Append(ITypeSymbol type, StringBuilder builder)
    {
        switch (type)
        {
            case IDynamicTypeSymbol:
                builder.Append("global::System.Object");
                return;
            case ITypeParameterSymbol parameter:
                builder.Append(parameter.Name);
                return;
            case IArrayTypeSymbol array:
                Append(array.ElementType, builder);
                builder.Append('[');
                builder.Append(',', array.Rank - 1);
                builder.Append(']');
                return;
            case IPointerTypeSymbol pointer:
                Append(pointer.PointedAtType, builder);
                builder.Append('*');
                return;
            case IFunctionPointerTypeSymbol functionPointer:
                builder.Append("delegate*");
                AppendCallingConvention(functionPointer.Signature, builder);
                builder.Append('<');
                for (var i = 0; i < functionPointer.Signature.Parameters.Length; i++)
                {
                    if (i != 0)
                        builder.Append(", ");
                    AppendParameterRefKind(
                        functionPointer.Signature.Parameters[i].RefKind,
                        builder);
                    Append(functionPointer.Signature.Parameters[i].Type, builder);
                }

                if (functionPointer.Signature.Parameters.Length != 0)
                    builder.Append(", ");
                AppendReturnRefKind(functionPointer.Signature, builder);
                Append(functionPointer.Signature.ReturnType, builder);
                builder.Append('>');
                return;
            case INamedTypeSymbol { IsTupleType: true } tuple:
                AppendNamed(tuple.TupleUnderlyingType ?? tuple, builder);
                return;
            case INamedTypeSymbol named:
                AppendNamed(named, builder);
                return;
            default:
                builder.Append(type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                return;
        }
    }

    private static void AppendCallingConvention(IMethodSymbol signature, StringBuilder builder)
    {
        switch (signature.CallingConvention)
        {
            case SignatureCallingConvention.Default:
                return;
            case SignatureCallingConvention.CDecl:
                builder.Append(" unmanaged[Cdecl]");
                return;
            case SignatureCallingConvention.StdCall:
                builder.Append(" unmanaged[Stdcall]");
                return;
            case SignatureCallingConvention.ThisCall:
                builder.Append(" unmanaged[Thiscall]");
                return;
            case SignatureCallingConvention.FastCall:
                builder.Append(" unmanaged[Fastcall]");
                return;
            case SignatureCallingConvention.Unmanaged:
                builder.Append(" unmanaged");
                if (signature.UnmanagedCallingConventionTypes.Length == 0)
                    return;
                builder.Append('[');
                for (var i = 0; i < signature.UnmanagedCallingConventionTypes.Length; i++)
                {
                    if (i != 0)
                        builder.Append(", ");
                    const string prefix = "CallConv";
                    var name = signature.UnmanagedCallingConventionTypes[i].Name;
                    builder.Append(
                        name.StartsWith(prefix, StringComparison.Ordinal)
                            ? name.Substring(prefix.Length)
                            : name);
                }

                builder.Append(']');
                return;
            default:
                builder.Append(' ');
                builder.Append(signature.CallingConvention.ToString().ToLowerInvariant());
                return;
        }
    }

    private static void AppendParameterRefKind(RefKind refKind, StringBuilder builder)
    {
        switch (refKind)
        {
            case RefKind.Ref:
                builder.Append("ref ");
                break;
            case RefKind.Out:
                builder.Append("out ");
                break;
            case RefKind.In:
                builder.Append("in ");
                break;
            case RefKind.RefReadOnlyParameter:
                builder.Append("ref readonly ");
                break;
        }
    }

    private static void AppendReturnRefKind(IMethodSymbol signature, StringBuilder builder)
    {
        if (signature.ReturnsByRefReadonly)
            builder.Append("ref readonly ");
        else if (signature.ReturnsByRef)
            builder.Append("ref ");
    }

    private static void AppendNamed(INamedTypeSymbol type, StringBuilder builder)
    {
        if (type.ContainingType is not null)
        {
            AppendNamed(type.ContainingType, builder);
            builder.Append('.');
        }
        else
        {
            builder.Append("global::");
            if (!type.ContainingNamespace.IsGlobalNamespace)
            {
                builder.Append(type.ContainingNamespace.ToDisplayString());
                builder.Append('.');
            }
        }

        var metadataName = type.MetadataName;
        var tick = metadataName.IndexOf('`');
        builder.Append(tick < 0 ? metadataName : metadataName.Substring(0, tick));

        if (type.Arity == 0)
            return;

        builder.Append('<');
        var start = Math.Max(0, type.TypeArguments.Length - type.Arity);
        for (var i = start; i < type.TypeArguments.Length; i++)
        {
            if (i != start)
                builder.Append(", ");
            Append(type.TypeArguments[i], builder);
        }

        builder.Append('>');
    }
}
