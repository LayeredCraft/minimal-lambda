using System;
using System.Collections.Generic;
using System.Linq;
using LayeredCraft.SourceGeneratorTools.Types;
using Microsoft.CodeAnalysis;
using MinimalLambda.SourceGenerators.Extensions;
using WellKnownType = MinimalLambda.SourceGenerators.WellKnownTypes.WellKnownTypeData.WellKnownType;

namespace MinimalLambda.SourceGenerators.Models;

internal record MapHandlerMethodInfo(
    string InterceptableLocationAttribute,
    string DelegateCastType,
    EquatableArray<MapHandlerParameterInfo> ParameterAssignments,
    bool IsAwaitable,
    bool HasResponse,
    bool IsResponseTypeStream,
    bool IsEventTypeStream,
    bool HasEvent,
    string? EventType,
    string? UnwrappedResponseType,
    bool HasAnyFromKeyedServices,
    EquatableArray<DiagnosticInfo> DiagnosticInfos,
    MethodType MethodType = MethodType.MapHandler) : IMethodInfo;

internal static class MapHandlerMethodInfoExtensions
{
    private static IEnumerable<DiagnosticInfo> ReportMultipleEvents(
        IEnumerable<MapHandlerParameterInfo> assignments,
        GeneratorContext context)
    {
        var eventAttribute = new Lazy<string>(() =>
            context.WellKnownTypes.Get(WellKnownType.MinimalLambda_Builder_FromEventAttribute)
                .QualifiedNullableName);

        return assignments
            .Where(a => a.IsEvent)
            .Skip(1)
            .Select(a => DiagnosticInfo.Create(
                Diagnostics.MultipleParametersUseAttribute,
                a.LocationInfo,
                [eventAttribute.Value]));
    }

    extension(MapHandlerMethodInfo)
    {
        internal static MapHandlerMethodInfo Create(
            IMethodSymbol methodSymbol,
            GeneratorContext context)
        {
            var handlerCastType = methodSymbol.GetCastableSignature();

            if (!InterceptableLocationInfo.TryGet(context, out var interceptableLocation))
                throw new InvalidOperationException("Unable to get interceptable location");

            var (assignments, diagnostics) =
                methodSymbol.Parameters.CollectDiagnosticResults(parameter =>
                    MapHandlerParameterInfo.Create(parameter, context));

            // add parameter diagnostics
            diagnostics.AddRange(ReportMultipleEvents(assignments, context));

            var isAwaitable = methodSymbol.IsAwaitable(context);

            var hasResponse = methodSymbol.HasMeaningfulReturnType(
                context,
                out var unwrappedReturnType);

            var isReturnTypeStream = hasResponse
                && context.WellKnownTypes.IsType(
                    methodSymbol.ReturnType,
                    WellKnownType.System_IO_Stream);

            var hasEvent = assignments.Any(a => a.IsEvent);

            var eventType = hasEvent
                ? assignments.Where(a => a.IsEvent).Select(a => a.GloballyQualifiedType).First()
                : null;

            var isEventTypeStream =
                hasEvent && assignments.Any(a => a is { IsEvent: true, IsStream: true });

            var hasAnyKeyedServices = assignments.Any(a => a is { IsFromKeyedService: true });

            return new MapHandlerMethodInfo(
                MethodType: MethodType.MapHandler,
                InterceptableLocationAttribute: interceptableLocation.Attribute,
                DelegateCastType: handlerCastType,
                ParameterAssignments: assignments.ToEquatableArray(),
                IsAwaitable: isAwaitable,
                HasResponse: hasResponse,
                IsResponseTypeStream: isReturnTypeStream,
                IsEventTypeStream: isEventTypeStream,
                HasEvent: hasEvent,
                EventType: eventType,
                UnwrappedResponseType: unwrappedReturnType?.QualifiedNullableName,
                HasAnyFromKeyedServices: hasAnyKeyedServices,
                DiagnosticInfos: diagnostics.ToEquatableArray());
        }
    }
}
