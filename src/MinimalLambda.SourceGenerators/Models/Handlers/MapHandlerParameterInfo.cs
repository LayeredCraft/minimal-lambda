using Microsoft.CodeAnalysis;
using MinimalLambda.SourceGenerators.Extensions;
using WellKnownType = MinimalLambda.SourceGenerators.WellKnownTypes.WellKnownTypeData.WellKnownType;

namespace MinimalLambda.SourceGenerators.Models;

internal record MapHandlerParameterInfo(
    string GloballyQualifiedType,
    bool IsStream,
    string Assignment,
    string InfoComment,
    bool IsEvent,
    bool IsFromKeyedService,
    LocationInfo? LocationInfo,
    ParameterSource Source,
    string? KeyedServicesKey);

internal static class MapHandlerParameterInfoExtensions
{
    extension(MapHandlerParameterInfo)
    {
        internal static DiagnosticResult<MapHandlerParameterInfo> Create(
            IParameterSymbol parameter,
            GeneratorContext context)
        {
            var paramType = parameter.Type.QualifiedNullableName;

            var parameterInfo = new MapHandlerParameterInfo(
                parameter.Type.QualifiedNullableName,
                context.WellKnownTypes.IsType(parameter.Type, WellKnownType.System_IO_Stream),
                IsEvent: false,
                IsFromKeyedService: false,
                LocationInfo: LocationInfo.Create(parameter),
                Assignment: string.Empty,
                InfoComment: string.Empty,
                KeyedServicesKey: string.Empty,
                Source: ParameterSource.Services);

            // event
            if (parameter.IsFromEvent(context))
                return DiagnosticResult<MapHandlerParameterInfo>.Success(
                    parameterInfo with
                    {
                        Assignment = parameterInfo.IsStream
                            // stream event
                            ? "context.Features.GetRequired<IInvocationDataFeature>().EventStream"
                            // non stream event
                            : $"context.GetRequiredEvent<{paramType}>()",
                        IsEvent = true,
                        Source = ParameterSource.Event,
                    });

            // context
            if (context.WellKnownTypes.IsType(
                parameter.Type,
                WellKnownType.Amazon_Lambda_Core_ILambdaContext,
                WellKnownType.MinimalLambda_ILambdaInvocationContext))
                return DiagnosticResult<MapHandlerParameterInfo>.Success(
                    parameterInfo with
                    {
                        Assignment = "context", Source = ParameterSource.Context,
                    });

            // cancellation token
            if (context.WellKnownTypes.IsType(
                parameter.Type,
                WellKnownType.System_Threading_CancellationToken))
                return DiagnosticResult<MapHandlerParameterInfo>.Success(
                    parameterInfo with
                    {
                        Assignment = "context.CancellationToken",
                        Source = ParameterSource.CancellationToken,
                    });

            // default assignment from Di
            return parameter
                .GetDiParameterAssignment(context)
                .Bind(diInfo => DiagnosticResult<MapHandlerParameterInfo>.Success(
                    parameterInfo with
                    {
                        Assignment = diInfo.Assignment,
                        IsFromKeyedService = diInfo.Key is not null,
                        Source = diInfo.Key is not null
                            ? ParameterSource.KeyedServices
                            : ParameterSource.Services,
                        KeyedServicesKey = diInfo.Key,
                    }));
        }
    }
}
