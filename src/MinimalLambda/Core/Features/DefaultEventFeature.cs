using Amazon.Lambda.Core;

namespace MinimalLambda;

/// <summary>
///     Default implementation of <see cref="IEventFeature{T}" /> that is used by source generated
///     code.
/// </summary>
/// <typeparam name="T"></typeparam>
internal class DefaultEventFeature<T>(ILambdaSerializer? lambdaSerializer = null) : IEventFeature<T>
{
    private readonly ILambdaSerializer _lambdaSerializer = lambdaSerializer
        ?? throw new ArgumentNullException(
            nameof(lambdaSerializer),
            "ILambdaSerializer has not been registered. In AOT scenarios you must provide an "
            + "serializer by registering an ILambdaSerializer in the DI container. "
            + "Use AddLambdaSerializerWithContext (registers the context for you) or "
            + "manually register your serializer implementation.");

    private T _data = default!;
    private bool _isDeserialized;

    public T GetEvent(ILambdaInvocationContext context)
    {
        if (!_isDeserialized)
        {
            var invocationDataFeature = context.Features.GetRequired<IInvocationDataFeature>();
            _data = _lambdaSerializer.Deserialize<T>(invocationDataFeature.EventStream);
            _isDeserialized = true;
        }

        return _data;
    }

    object? IEventFeature.GetEvent(ILambdaInvocationContext context) => GetEvent(context);
}
