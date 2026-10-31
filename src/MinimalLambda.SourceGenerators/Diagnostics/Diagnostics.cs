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

    internal static readonly DiagnosticDescriptor UnsupportedEventHandlerSignature = new(
        "LH0008",
        "Unsupported event handler signature",
        "Event handler signature component '{0}' is not supported; use an item-first asynchronous handler returning Task or Task<TResult>, value parameters, and types accessible to generated adapter code.",
        UsageCategory,
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor UnsupportedEventHandlerBinding = new(
        "LH0009",
        "Unsupported event handler binding",
        "Event handler binding '{0}' is not supported; the first parameter is the source item, and framework context or cancellation-token parameters cannot use service or event attributes.",
        UsageCategory,
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor UnresolvedEventHandler = new(
        "LH0010",
        "Unresolved event handler",
        "The event handler '{0}' cannot be resolved to a single analyzable method; use an explicitly typed lambda, a typed delegate local with one initialization, or an explicit method group.",
        UsageCategory,
        DiagnosticSeverity.Error,
        true);

    internal static readonly DiagnosticDescriptor DiscardedEventHandlerResult = new(
        "LH0011",
        "Discarded event handler result",
        "The event handler '{0}' returns a result but is bound to a no-result overload; use a result overload with a response builder or wrap the handler so it intentionally returns Task.",
        UsageCategory,
        DiagnosticSeverity.Error,
        true);
}
