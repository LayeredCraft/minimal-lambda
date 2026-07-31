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

    internal static readonly DiagnosticDescriptor InvalidDurableInputCardinality = new(
        "LH0007",
        "Invalid durable workflow input cardinality",
        "Durable handler must declare exactly one event input using '[FromEvent]'; found {0}.",
        UsageCategory,
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor InvalidDurableContextCardinality = new(
        "LH0008",
        "Invalid durable context cardinality",
        "Durable handler must declare exactly one 'Amazon.Lambda.DurableExecution.IDurableContext' parameter; found {0}.",
        UsageCategory,
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor UnsupportedDurableParameter = new(
        "LH0009",
        "Unsupported durable handler parameter",
        "Durable handler parameter '{0}' of type '{1}' is not supported: {2}.",
        UsageCategory,
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor UnsupportedDurableReturnType = new(
        "LH0010",
        "Unsupported durable handler return type",
        "Durable handler return type '{0}' is not supported; use 'Task' or 'Task<TOutput>' with a closed, nameable, accessible, non-transport output type.",
        UsageCategory,
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor MissingDurableSerializerRoot = new(
        "LH0011",
        "Durable serializer root is not explicitly declared",
        "Source-generated serializer context '{0}' does not explicitly declare durable serialization root '{1}'. Add [JsonSerializable(typeof({1}))] to that context or a base context declaration.",
        ConfigurationCategory,
        DiagnosticSeverity.Warning,
        true);
}
