using LayeredCraft.SourceGeneratorTools.Types;

namespace MinimalLambda.SourceGenerators.Models;

internal enum EventHandlerParameterKind
{
    Item,
    Context,
    ProcessingContext,
    CancellationToken,
    Service,
    KeyedService,
}

internal record EventHandlerParameterInfo(
    int Ordinal,
    string Name,
    string Type,
    EventHandlerParameterKind Kind,
    string Assignment,
    bool IsOptional,
    string? KeyedServiceKey,
    LocationInfo Location,
    EquatableArray<DiagnosticInfo> DiagnosticInfos);
