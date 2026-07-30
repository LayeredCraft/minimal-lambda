using Microsoft.CodeAnalysis;
using MinimalLambda.SourceGenerators.Extensions;
using WellKnownType = MinimalLambda.SourceGenerators.WellKnownTypes.WellKnownTypeData.WellKnownType;

namespace MinimalLambda.SourceGenerators.Models;

internal record MiddlewareParameterInfo(
    string Name,
    string GloballyQualifiedType,
    string GloballyQualifiedNotNullableType,
    bool FromArguments,
    bool FromServices,
    string FromServicesAssignment,
    string InfoComment,
    ParameterSource ServiceSource,
    string? KeyedServicesKey);

internal static class MiddlewareParameterInfoExtensions
{
    extension(MiddlewareParameterInfo)
    {
        internal static DiagnosticResult<MiddlewareParameterInfo> Create(
            IParameterSymbol parameterSymbol,
            GeneratorContext context)
        {
            context.ThrowIfCancellationRequested();

            // parameter name
            var name = parameterSymbol.Name;

            // globally qualified type
            var globallyQualifiedType = parameterSymbol.Type.QualifiedNullableName;

            // globally qualified type - not nullable
            var globallyQualifiedNotNullableType = parameterSymbol.Type.QualifiedName;

            // determine if it has a `[FromArguments]` attribute
            var fromArguments = parameterSymbol.IsDecoratedWithAttribute(
                context,
                WellKnownType.MinimalLambda_Builder_FromArgumentsAttribute);

            // determine if it has a `[FromServices]` attribute
            var fromServices = !fromArguments
                && parameterSymbol.IsDecoratedWithAttribute(
                    context,
                    WellKnownType.MinimalLambda_Builder_FromServicesAttribute,
                    WellKnownType
                        .Microsoft_Extensions_DependencyInjection_FromKeyedServicesAttribute);

            // assignment from services
            return parameterSymbol
                .GetDiParameterAssignment(context)
                .Bind(diInfo => DiagnosticResult<MiddlewareParameterInfo>.Success(
                    new MiddlewareParameterInfo(
                        InfoComment: "",
                        Name: name,
                        GloballyQualifiedType: globallyQualifiedType,
                        GloballyQualifiedNotNullableType: globallyQualifiedNotNullableType,
                        FromArguments: fromArguments,
                        FromServices: fromServices,
                        FromServicesAssignment: diInfo.Assignment,
                        ServiceSource: diInfo.Key is not null
                            ? ParameterSource.KeyedServices
                            : ParameterSource.Services,
                        KeyedServicesKey: diInfo.Key)));
        }
    }
}
