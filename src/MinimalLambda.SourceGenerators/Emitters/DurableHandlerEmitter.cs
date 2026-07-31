using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using MinimalLambda.SourceGenerators.Models;

namespace MinimalLambda.SourceGenerators.Emitters;

internal static class DurableHandlerEmitter
{
    private const string DurableHandlerTemplateFile = "Templates/MapDurableHandler.scriban";

    internal static void Emit(
        SourceProductionContext context,
        ImmutableArray<DurableMethodInfo> infos)
    {
        if (infos.Length == 0)
            return;

        var sortedInfos = infos
            .OrderBy(static info => info.TreeOrdinal)
            .ThenBy(static info => info.MapCallLocation.TextSpan.Start)
            .ToImmutableArray();
        var code = TemplateHelper.Render(
            DurableHandlerTemplateFile,
            new { TemplateHelper.GeneratedCodeAttribute, MapDurableHandlerCalls = sortedInfos });

        context.AddSource("MinimalLambda.DurableHandlers.g.cs", code);
    }
}
