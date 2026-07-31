using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using MinimalLambda.SourceGenerators.Emitters;
using MinimalLambda.SourceGenerators.Models;

namespace MinimalLambda.SourceGenerators;

[Generator]
public class MinimalLambdaGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Language version gate - only generate source if C# 11 or later is used
        var csharpSufficient = context.CompilationProvider.Select(static (compilation, _) =>
            compilation is CSharpCompilation
            {
                LanguageVersion: LanguageVersion.Default or >= LanguageVersion.CSharp11,
            });

        context.RegisterSourceOutput(
            csharpSufficient,
            static (spc, ok) =>
            {
                if (!ok)
                    spc.ReportDiagnostic(
                        Diagnostic.Create(Diagnostics.CSharpVersionTooLow, Location.None));
            });

        // handler registration calls
        var registrationCalls = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                HandlerSyntaxProvider.Predicate,
                HandlerSyntaxProvider.Transformer)
            .WhereNotNull();

        // find UseMiddleware<T>() calls
        var useMiddlewareTCalls = context
            .SyntaxProvider
            .CreateSyntaxProvider(
                UseMiddlewareTSyntaxProvider.Predicate,
                UseMiddlewareTSyntaxProvider.Transformer)
            .WhereNotNull();

        var invocationHandlerCalls = registrationCalls
            .WhereNoErrors()
            .Where(static c => c is MapHandlerMethodInfo)
            .Select(static (c, _) => (MapHandlerMethodInfo)c)
            .Collect();

        var durableHandlerModels = registrationCalls
            .Where(static c => c is DurableMethodInfo)
            .Select(static (c, _) => (DurableMethodInfo)c);

        var durableHandlerCalls = durableHandlerModels.Collect();
        var validDurableHandlerCalls = durableHandlerModels.WhereNoErrors().Collect();

        var durableDiagnostics = durableHandlerCalls
            .Combine(context.CompilationProvider)
            .Select(static (pair, cancellationToken) =>
                DurableSerializerAnalyzer.Analyze(pair.Right, pair.Left, cancellationToken));

        var onInitHandlerCalls = registrationCalls
            .WhereNoErrors()
            .Where(static c => c is LifecycleMethodInfo { MethodType: MethodType.OnInit })
            .Select(static (c, _) => (LifecycleMethodInfo)c)
            .Collect();

        var onShutdownHandlerCalls = registrationCalls
            .WhereNoErrors()
            .Where(static c => c is LifecycleMethodInfo { MethodType: MethodType.OnShutdown })
            .Select(static (c, _) => (LifecycleMethodInfo)c)
            .Collect();

        var middlewareTCallsCollected = useMiddlewareTCalls.WhereNoErrors().Collect();

        context.RegisterSourceOutput(
            registrationCalls.Where(static call => call is not DurableMethodInfo),
            (ctx, call) => call.DiagnosticInfos.ForEach(d => d.ReportDiagnostic(ctx)));

        context.RegisterSourceOutput(
            durableDiagnostics,
            static (ctx, diagnostics) =>
                diagnostics.ForEach(diagnostic => diagnostic.ReportDiagnostic(ctx)));

        context.RegisterSourceOutput(
            useMiddlewareTCalls,
            (ctx, call) => call.DiagnosticInfos.ForEach(d => d.ReportDiagnostic(ctx)));

        context.RegisterSourceOutput(invocationHandlerCalls, InvocationHandlerEmitter.Emit);
        context.RegisterSourceOutput(validDurableHandlerCalls, DurableHandlerEmitter.Emit);
        context.RegisterSourceOutput(onInitHandlerCalls, LifecycleHandlerEmitter.Emit);
        context.RegisterSourceOutput(onShutdownHandlerCalls, LifecycleHandlerEmitter.Emit);
        context.RegisterSourceOutput(middlewareTCallsCollected, MiddlewareClassEmitter.Emit);
    }
}
