using Microsoft.CodeAnalysis;
using WellKnownType = MinimalLambda.SourceGenerators.WellKnownTypes.WellKnownTypeData.WellKnownType;

namespace MinimalLambda.SourceGenerators.Models;

internal record LifecycleHandlerParameterInfo(
    string Assignment,
    string InfoComment,
    bool IsFromKeyedService,
    LocationInfo? LocationInfo,
    ParameterSource Source,
    string? KeyedServicesKey);

internal static class LifecycleHandlerParameterInfoExtensions
{
    extension(LifecycleHandlerParameterInfo)
    {
        internal static DiagnosticResult<LifecycleHandlerParameterInfo> Create(
            IParameterSymbol parameter,
            GeneratorContext context)
        {
            var parameterInfo = new LifecycleHandlerParameterInfo(
                IsFromKeyedService: false,
                LocationInfo: LocationInfo.Create(parameter),
                Assignment: string.Empty,
                InfoComment: string.Empty,
                KeyedServicesKey: string.Empty,
                Source: ParameterSource.Services);

            // context
            if (context.WellKnownTypes.IsType(
                parameter.Type,
                WellKnownType.MinimalLambda_ILambdaLifecycleContext))
                return DiagnosticResult<LifecycleHandlerParameterInfo>.Success(
                    parameterInfo with
                    {
                        Assignment = "context", Source = ParameterSource.Context,
                    });

            // cancellation token
            if (context.WellKnownTypes.IsType(
                parameter.Type,
                WellKnownType.System_Threading_CancellationToken))
                return DiagnosticResult<LifecycleHandlerParameterInfo>.Success(
                    parameterInfo with
                    {
                        Assignment = "context.CancellationToken",
                        Source = ParameterSource.CancellationToken,
                    });

            // default assignment from Di
            return parameter
                .GetDiParameterAssignment(context)
                .Bind(diInfo => DiagnosticResult<LifecycleHandlerParameterInfo>.Success(
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
