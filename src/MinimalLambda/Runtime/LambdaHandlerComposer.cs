using Amazon.Lambda.Core;
using Microsoft.Extensions.Options;

namespace MinimalLambda.Runtime;

/// <summary>Builds and composes the Lambda invocation pipeline into a request handler.</summary>
internal sealed class LambdaHandlerComposer : ILambdaHandlerFactory
{
    private readonly ILambdaCancellationFactory _cancellationFactory;
    private readonly ILambdaInvocationContextFactory _contextFactory;
    private readonly IInvocationDataFeatureFactory _invocationDataFeatureFactory;
    private readonly ILambdaInvocationBuilderFactory _lambdaInvocationBuilderFactory;
    private readonly LambdaHostedServiceOptions _options;

    public LambdaHandlerComposer(
        ILambdaInvocationBuilderFactory lambdaInvocationBuilderFactory,
        ILambdaCancellationFactory cancellationFactory,
        IOptions<LambdaHostedServiceOptions> options,
        ILambdaInvocationContextFactory contextFactory,
        IInvocationDataFeatureFactory invocationDataFeatureFactory)
    {
        ArgumentNullException.ThrowIfNull(cancellationFactory);
        ArgumentNullException.ThrowIfNull(lambdaInvocationBuilderFactory);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(contextFactory);
        ArgumentNullException.ThrowIfNull(invocationDataFeatureFactory);

        _cancellationFactory = cancellationFactory;
        _lambdaInvocationBuilderFactory = lambdaInvocationBuilderFactory;
        _options = options.Value;
        _contextFactory = contextFactory;
        _invocationDataFeatureFactory = invocationDataFeatureFactory;
    }

    /// <summary>Creates a wrapper that invokes the middleware pipeline for each Lambda invocation.</summary>
    public Func<Stream, ILambdaContext, Task<Stream>> CreateHandler(CancellationToken stoppingToken)
    {
        var builder = _lambdaInvocationBuilderFactory.CreateBuilder();

        _options.ConfigureHandlerBuilder?.Invoke(builder);

        var handler = builder.Build();

        return CreateRequestHandler;

        async Task<Stream> CreateRequestHandler(Stream inputStream, ILambdaContext lambdaContext)
        {
            // Create a base cancellation token source using the provided token source factory.
            // This allows cancellation when the maximum lifetime of the lambda has been reached.
            using var cancellationTokenSource =
                _cancellationFactory.NewCancellationTokenSource(lambdaContext);

            // Combine the base cancellation token source with the stoppingToken.
            // This allows for cancellation when either:
            // - The maximum lifetime of the lambda has been reached, OR
            // - The lambda runtime has started to shut down
            using var linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                stoppingToken,
                cancellationTokenSource.Token);

            // Create a new lambda host context. This will also create a new service scope
            // the first time that the service container is accessed.
            var lambdaInvocationContext = _contextFactory.Create(
                lambdaContext,
                builder.Properties,
                linkedTokenSource.Token);

            await using (lambdaInvocationContext as IAsyncDisposable)
            {
                using var invocationDataFeature = _invocationDataFeatureFactory.Create(inputStream);
                lambdaInvocationContext.Features.Set(invocationDataFeature);

                if (invocationDataFeature is IInvocationDataBufferingFeature bufferingFeature)
                    lambdaInvocationContext.Features.Set(bufferingFeature);

                // Invoke the handler wrapped in the middleware pipeline.
                await handler.Invoke(lambdaInvocationContext).ConfigureAwait(false);

                if (lambdaInvocationContext.Features.TryGet<IResponseFeature>(
                    out var responseFeature))
                    responseFeature.SerializeToStream(lambdaInvocationContext);

                // If no serializer is provided, return an empty stream.
                return invocationDataFeature.ResponseStream;
            }
        }
    }
}
