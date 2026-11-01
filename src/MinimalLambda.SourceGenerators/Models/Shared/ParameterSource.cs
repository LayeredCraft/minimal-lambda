namespace MinimalLambda.SourceGenerators.Models;

internal enum ParameterSource
{
    Event,
    Context,
    DurableContext,
    CancellationToken,
    ProcessingContext,
    KeyedServices,
    Services,
}
