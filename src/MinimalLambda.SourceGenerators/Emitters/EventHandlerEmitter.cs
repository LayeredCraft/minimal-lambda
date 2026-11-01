using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using MinimalLambda.SourceGenerators.Models;

namespace MinimalLambda.SourceGenerators.Emitters;

internal static class EventHandlerEmitter
{
    private const string TemplateFile = "Templates/MapEventHandler.scriban";

    internal static void Emit(
        SourceProductionContext context,
        ImmutableArray<EventHandlerMethodInfo> infos)
    {
        if (infos.Length == 0)
            return;

        var ordered = infos
            .OrderBy(info => info.MapCallLocation.FilePath, System.StringComparer.Ordinal)
            .ThenBy(info => info.SourceSpanStart)
            .ToImmutableArray();

        var code = TemplateHelper.Render(
            TemplateFile,
            new { TemplateHelper.GeneratedCodeAttribute, EventHandlerCalls = ordered });

        context.AddSource("MinimalLambda.EventHandlers.g.cs", code);
    }
}
