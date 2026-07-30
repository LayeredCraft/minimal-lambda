using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using MinimalLambda.SourceGenerators.Models;

namespace MinimalLambda.SourceGenerators;

internal static class DiagnosticGenerator
{
    internal static List<Diagnostic> GenerateDiagnostics(CompilationInfo compilationInfo)
    {
        var diagnostics = new List<Diagnostic>();

        diagnostics.AddRange(
            compilationInfo
                .MapHandlerInvocationInfos
                .SelectMany(m => m.DiagnosticInfos)
                .Select(d => d.ToDiagnostic()));

        diagnostics.AddRange(
            compilationInfo
                .OnInitInvocationInfos
                .SelectMany(m => m.DiagnosticInfos)
                .Select(d => d.ToDiagnostic()));

        diagnostics.AddRange(
            compilationInfo
                .OnShutdownInvocationInfos
                .SelectMany(m => m.DiagnosticInfos)
                .Select(d => d.ToDiagnostic()));

        diagnostics.AddRange(
            compilationInfo
                .UseMiddlewareTInfos
                .SelectMany(m => m.DiagnosticInfos)
                .Select(d => d.ToDiagnostic()));

        return diagnostics;
    }
}
