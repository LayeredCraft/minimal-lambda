using System.Threading;
using Microsoft.CodeAnalysis;

namespace MinimalLambda.SourceGenerators;

internal class GeneratorContext
{
    internal WellKnownTypes.WellKnownTypes WellKnownTypes { get; }
    internal CancellationToken CancellationToken { get; }
    internal SemanticModel SemanticModel { get; }
    internal SyntaxNode Node { get; }

    internal GeneratorContext(GeneratorSyntaxContext context, CancellationToken cancellationToken) :
        this(context.Node, context.SemanticModel, cancellationToken) { }

    internal GeneratorContext(
        SyntaxNode node,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        Node = node;
        SemanticModel = semanticModel;
        CancellationToken = cancellationToken;
        WellKnownTypes =
            SourceGenerators.WellKnownTypes.WellKnownTypes.GetOrCreate(semanticModel.Compilation);
    }
}

internal static class GeneratorContextExtensions
{
    extension(GeneratorContext context)
    {
        public void ThrowIfCancellationRequested() =>
            context.CancellationToken.ThrowIfCancellationRequested();
    }
}
