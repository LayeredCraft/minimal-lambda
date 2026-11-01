namespace MinimalLambda.SourceGenerators.Models;

internal enum MethodType
{
    MapHandler,
    MapEventHandler,
    MapDurableHandler,
    OnInit,
    OnShutdown,
    UseMiddlewareT,
}
