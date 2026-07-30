using System;
using System.Collections.Generic;
using System.Linq;
using LayeredCraft.SourceGeneratorTools.Types;
using Microsoft.CodeAnalysis;
using MinimalLambda.SourceGenerators.WellKnownTypes;

namespace MinimalLambda.SourceGenerators.Models;

internal record LifecycleMethodInfo(
    string InterceptableLocationAttribute,
    string DelegateCastType,
    EquatableArray<DiagnosticInfo> DiagnosticInfos,
    EquatableArray<LifecycleHandlerParameterInfo> ParameterAssignments,
    bool ShouldAwait,
    MethodType MethodType,
    string HandleResponseAssignment,
    string HandleReturningFromMethod,
    string ReturnType,
    bool HasAnyFromKeyedServices,
    string BuilderName) : IMethodInfo;

internal static class LifecycleMethodInfoExtensions
{
    extension(LifecycleMethodInfo)
    {
        internal static LifecycleMethodInfo CreateForInit(
            IMethodSymbol methodSymbol,
            GeneratorContext context)
        {
            var handlerCastType = methodSymbol.GetCastableSignature();

            if (!InterceptableLocationInfo.TryGet(context, out var interceptableLocation))
                throw new InvalidOperationException("Unable to get interceptable location");

            var (assignments, diagnostics) =
                methodSymbol.Parameters.CollectDiagnosticResults(parameter =>
                    LifecycleHandlerParameterInfo.Create(parameter, context));

            var isAwaitable = methodSymbol.IsAwaitable(context);

            var hasResponse = methodSymbol.HasMeaningfulReturnType(
                context,
                out var unwrappedReturnType);

            var unwrappedReturnIsBool = hasResponse
                && context.WellKnownTypes.IsType(
                    unwrappedReturnType!,
                    WellKnownTypeData.WellKnownType.System_Boolean);

            /*
             * Return rules:
             * If handler returns `Task<bool>`, no need to await, can be returned on its own
             * If handler returns `ValueTask<bool>`, must be awaited and then returned
             * If handler returns `bool`, it doesn't need to be awaited and can be returned as
             *      result default + async, return true default, return Task.FromResult(true);
             */

            var returnIsTaskBool = methodSymbol.ReturnType is INamedTypeSymbol namedTypeSymbol
                && context.WellKnownTypes.IsType(
                    namedTypeSymbol.ConstructedFrom,
                    WellKnownTypeData.WellKnownType.System_Threading_Tasks_Task_T)
                && unwrappedReturnIsBool;

            var shouldAwait = isAwaitable && !returnIsTaskBool;

            var handleResponseAssignment = hasResponse && unwrappedReturnIsBool
                ? "var response = "
                : string.Empty;

            var handleReturningFromMethod = hasResponse switch
            {
                true when returnIsTaskBool || (unwrappedReturnIsBool && isAwaitable) =>
                    "return response;",
                true when unwrappedReturnIsBool => "return Task.FromResult(response);",
                _ when isAwaitable => "return true;",
                _ => "return Task.FromResult(true);",
            };

            var hasAnyKeyedServices = assignments.Any(a => a is { IsFromKeyedService: true });

            return new LifecycleMethodInfo(
                BuilderName: "ILambdaOnInitBuilder",
                MethodType: MethodType.OnInit,
                InterceptableLocationAttribute: interceptableLocation.Attribute,
                DelegateCastType: handlerCastType,
                DiagnosticInfos: diagnostics.ToEquatableArray(),
                ParameterAssignments: assignments.ToEquatableArray(),
                ShouldAwait: shouldAwait,
                HandleResponseAssignment: handleResponseAssignment,
                HandleReturningFromMethod: handleReturningFromMethod,
                ReturnType: "Task<bool>",
                HasAnyFromKeyedServices: hasAnyKeyedServices);
        }

        internal static LifecycleMethodInfo CreateForShutdown(
            IMethodSymbol methodSymbol,
            GeneratorContext context)
        {
            var handlerCastType = methodSymbol.GetCastableSignature();

            if (!InterceptableLocationInfo.TryGet(context, out var interceptableLocation))
                throw new InvalidOperationException("Unable to get interceptable location");

            var (assignments, diagnostics) =
                methodSymbol.Parameters.CollectDiagnosticResults(parameter =>
                    LifecycleHandlerParameterInfo.Create(parameter, context));

            var isAwaitable = methodSymbol.IsAwaitable(context);

            var returnIsTask = context.WellKnownTypes.IsType(
                methodSymbol.ReturnType,
                WellKnownTypeData.WellKnownType.System_Threading_Tasks_Task);

            var shouldAwait = isAwaitable && !returnIsTask;

            var handleResponseAssignment = returnIsTask ? "var response = " : string.Empty;

            var handleReturningFromMethod = shouldAwait switch
            {
                _ when returnIsTask => "return response;",
                true => string.Empty,
                _ => "return Task.CompletedTask;",
            };

            var hasAnyKeyedServices = assignments.Any(a => a is { IsFromKeyedService: true });

            return new LifecycleMethodInfo(
                BuilderName: "ILambdaOnShutdownBuilder",
                MethodType: MethodType.OnShutdown,
                InterceptableLocationAttribute: interceptableLocation.Attribute,
                DelegateCastType: handlerCastType,
                DiagnosticInfos: diagnostics.ToEquatableArray(),
                ParameterAssignments: assignments.ToEquatableArray(),
                ShouldAwait: shouldAwait,
                HandleResponseAssignment: handleResponseAssignment,
                HandleReturningFromMethod: handleReturningFromMethod,
                ReturnType: "Task",
                HasAnyFromKeyedServices: hasAnyKeyedServices);
        }
    }
}
