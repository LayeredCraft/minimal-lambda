using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MinimalLambda.SourceGenerators;
using MinimalLambda.SourceGenerators.Extensions;
using MinimalLambda.SourceGenerators.Models;
using WellKnownType = MinimalLambda.SourceGenerators.WellKnownTypes.WellKnownTypeData.WellKnownType;

namespace Microsoft.CodeAnalysis;

internal static class ParameterSymbolExtensions
{
    internal static bool IsDecoratedWithAttribute(
        this IParameterSymbol parameterSymbol,
        GeneratorContext context,
        params WellKnownType[] attributeType) =>
        parameterSymbol
            .GetAttributes()
            .Any(a => a.AttributeClass is not null
                && context.WellKnownTypes.IsType(a.AttributeClass, attributeType));

    extension(IParameterSymbol parameterSymbol)
    {
        internal bool IsFromEvent(GeneratorContext context) =>
            parameterSymbol
                .GetAttributes()
                .Any(attribute => attribute.AttributeClass is not null
                    && context.WellKnownTypes.IsType(
                        attribute.AttributeClass,
                        WellKnownType.MinimalLambda_Builder_EventAttribute,
                        WellKnownType.MinimalLambda_Builder_FromEventAttribute));

        internal DiagnosticResult<(string Assignment, string? Key)> GetDiParameterAssignment(
            GeneratorContext context)
        {
            var paramType = parameterSymbol.Type.QualifiedNullableName;

            var isRequired = parameterSymbol.IsOptional
                || parameterSymbol.NullableAnnotation == NullableAnnotation.Annotated;

            return parameterSymbol
                .IsFromKeyedService(context)
                .Bind<(string, string?)>(result =>
                    result.IsKeyed
                        ? (
                            isRequired
                                ? $"context.ServiceProvider.GetKeyedService<{paramType}>({result.Key})"
                                : $"context.ServiceProvider.GetRequiredKeyedService<{paramType}>({result.Key})",
                            result.Key)
                        : (
                            isRequired
                                ? $"context.ServiceProvider.GetService<{paramType}>()"
                                : $"context.ServiceProvider.GetRequiredService<{paramType}>()",
                            null));
        }

        private DiagnosticResult<(bool IsKeyed, string? Key)>
            IsFromKeyedService(GeneratorContext context) =>
            parameterSymbol
                .GetAttributes()
                .FirstOrDefault(attribute => attribute is { AttributeClass: not null }
                    && context.WellKnownTypes.IsType(
                        attribute.AttributeClass,
                        WellKnownType
                            .Microsoft_Extensions_DependencyInjection_FromKeyedServicesAttribute))
                ?.ExtractKeyedServiceKey()
                .Bind<(bool, string?)>(key => (true, key))
            ?? (false, null);
    }

    extension(AttributeData attributeData)
    {
        private DiagnosticResult<string> ExtractKeyedServiceKey()
        {
            var argument = attributeData.ConstructorArguments[0];

            if (argument.IsNull)
                return "null";

            object? value = null;
            try
            {
                value = argument.Value;
            }
            catch
            {
                // ignore
            }

            if (value is null)
                return DiagnosticResult<string>.Failure(
                    MinimalLambda.SourceGenerators.Diagnostics.InvalidAttributeArgument,
                    attributeData.GetAttributeArgumentLocation(0),
                    argument.Type?.QualifiedNullableName);

            return argument.Kind switch
            {
                TypedConstantKind.Primitive when value is string strValue => CSharp.SymbolDisplay
                    .FormatLiteral(strValue, true),

                TypedConstantKind.Primitive when value is char charValue => $"'{charValue}'",

                TypedConstantKind.Primitive when value is bool boolValue => boolValue
                    ? "true"
                    : "false",

                TypedConstantKind.Primitive or TypedConstantKind.Enum =>
                    $"({argument.Type?.QualifiedNullableName}){value}",

                TypedConstantKind.Type when value is ITypeSymbol typeValue =>
                    $"typeof({typeValue.QualifiedNullableName})",

                _ => value.ToString(),
            };
        }

        private LocationInfo? GetAttributeArgumentLocation(int index) =>
            attributeData.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax
            {
                ArgumentList: { } argumentList,
            }
                ? argumentList
                    .Arguments
                    .ElementAtOrDefault(index)
                    ?.Expression
                    .GetLocation()
                    .ToLocationInfo()
                : null;
    }
}
