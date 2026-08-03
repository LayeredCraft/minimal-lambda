using System;
using System.Collections.Generic;
using LayeredCraft.SourceGeneratorTools.Utilities;
using Microsoft.CodeAnalysis;

namespace MinimalLambda.SourceGenerators.Models;

internal sealed record DiagnosticInfo(
    DiagnosticDescriptor DiagnosticDescriptor,
    LocationInfo? LocationInfo = null,
    params object?[] MessageArgs)
{
    public bool Equals(DiagnosticInfo? other) =>
        other is not null
        && Equals(DiagnosticDescriptor.Id, other.DiagnosticDescriptor.Id)
        && Equals(LocationInfo, other.LocationInfo);

    public override int GetHashCode() => HashCode.Combine(DiagnosticDescriptor, LocationInfo);
}

internal static class DiagnosticInfoExtensions
{
    extension(DiagnosticInfo diagnosticInfo)
    {
        internal static DiagnosticInfo Create(
            DiagnosticDescriptor diagnosticDescriptor,
            LocationInfo? locationInfo,
            object?[] messageArgs) =>
            new(diagnosticDescriptor, locationInfo, messageArgs);

        internal Diagnostic ToDiagnostic() =>
            Diagnostic.Create(
                diagnosticInfo.DiagnosticDescriptor,
                diagnosticInfo.LocationInfo?.ToLocation(),
                diagnosticInfo.MessageArgs);

        internal void ReportDiagnostic(SourceProductionContext context) =>
            context.ReportDiagnostic(diagnosticInfo.ToDiagnostic());
    }
}
