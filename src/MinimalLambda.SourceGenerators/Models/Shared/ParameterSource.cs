namespace MinimalLambda.SourceGenerators.Models;

internal enum ParameterSource
{
    Event,
    Context,
    DurableContext,
    CancellationToken,
    KeyedServices,
    Services,
}
