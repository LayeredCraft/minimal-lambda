using Microsoft.CodeAnalysis;

#pragma warning disable RS1032 // Diagnostic text is fixed by ADR-004.

namespace MinimalLambda.SourceGenerators;

internal static class Diagnostics
{
    private const string UsageCategory = "MinimalLambda.Usage";
    private const string ConfigurationCategory = "MinimalLambda.Configuration";

    internal static readonly DiagnosticDescriptor MultipleParametersUseAttribute = new(
        "LH0002",
        "Multiple parameters use attribute",
        "Handler method contains multiple parameters that use the '{0}' attribute. Only one parameter can use this attribute.",
        UsageCategory,
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor InvalidAttributeArgument = new(
        "LH0003",
        "Invalid attribute argument",
        "An argument of type '{0}' is not valid for this attribute. Please use a valid type.",
        UsageCategory,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor CSharpVersionTooLow = new(
        "LH0004",
        "C# language version too low",
        "MinimalLambda requires C# 11 or newer (or LanguageVersion=default with a modern SDK). "
        + "Set <LangVersion>latest</LangVersion> or enable preview features.",
        ConfigurationCategory,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor MultipleConstructorsWithAttribute = new(
        "LH0005",
        "Multiple constructors use attribute",
        "Type contains multiple constructors that use the '{0}' attribute. Only one constructor can use this attribute.",
        ConfigurationCategory,
        DiagnosticSeverity.Error,
        true);

    public static readonly DiagnosticDescriptor MustBeConcreteType = new(
        "LH0006",
        "Type must be a concrete class",
        "The type '{0}' must be a concrete class. Interfaces, abstract classes, and other non-instantiable types cannot be used as middleware.",
        ConfigurationCategory,
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor UnsupportedDurableHandlerSignature = new(
        "LH0007",
        "Unsupported durable handler signature",
        "Durable handler signature component '{0}' is not supported; use Task or Task<TOutput>, value parameters, and types accessible to generated adapter code.",
        UsageCategory,
        DiagnosticSeverity.Error,
        true);
}
