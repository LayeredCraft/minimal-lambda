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
    internal int TreeOrdinal { get; init; } = int.MaxValue;
    internal int SubKey { get; init; }

    public bool Equals(DiagnosticInfo? other)
    {
        if (other is null
            || !string.Equals(
                DiagnosticDescriptor.Id,
                other.DiagnosticDescriptor.Id,
                StringComparison.Ordinal)
            || !Equals(LocationInfo, other.LocationInfo)
            || MessageArgs.Length != other.MessageArgs.Length)
            return false;

        for (var i = 0; i < MessageArgs.Length; i++)
            if (!Equals(MessageArgs[i], other.MessageArgs[i]))
                return false;

        return true;
    }

    public override int GetHashCode()
    {
        var hash = HashCode.Combine(DiagnosticDescriptor.Id, LocationInfo);
        foreach (var argument in MessageArgs)
            hash = HashCode.Combine(hash, argument);
        return hash;
    }
}

internal sealed class DiagnosticInfoOrderComparer : IComparer<DiagnosticInfo>
{
    internal static readonly DiagnosticInfoOrderComparer Instance = new();

    public int Compare(DiagnosticInfo? x, DiagnosticInfo? y)
    {
        if (ReferenceEquals(x, y))
            return 0;
        if (x is null)
            return -1;
        if (y is null)
            return 1;

        var result = x.TreeOrdinal.CompareTo(y.TreeOrdinal);
        if (result != 0)
            return result;
        result = (x.LocationInfo?.TextSpan.Start ?? int.MaxValue).CompareTo(
            y.LocationInfo?.TextSpan.Start ?? int.MaxValue);
        if (result != 0)
            return result;
        result = string.CompareOrdinal(x.DiagnosticDescriptor.Id, y.DiagnosticDescriptor.Id);
        if (result != 0)
            return result;
        result = x.SubKey.CompareTo(y.SubKey);
        if (result != 0)
            return result;

        var count = Math.Min(x.MessageArgs.Length, y.MessageArgs.Length);
        for (var i = 0; i < count; i++)
        {
            result = CompareArgument(x.MessageArgs[i], y.MessageArgs[i]);
            if (result != 0)
                return result;
        }

        return x.MessageArgs.Length.CompareTo(y.MessageArgs.Length);
    }

    private static int CompareArgument(object? left, object? right)
    {
        if (ReferenceEquals(left, right))
            return 0;
        if (left is null)
            return -1;
        if (right is null)
            return 1;
        if (left is int leftInt && right is int rightInt)
            return leftInt.CompareTo(rightInt);
        return string.CompareOrdinal(left.ToString(), right.ToString());
    }
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
