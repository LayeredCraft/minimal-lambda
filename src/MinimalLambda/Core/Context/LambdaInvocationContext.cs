using Amazon.Lambda.Core;
using Microsoft.Extensions.DependencyInjection;

namespace MinimalLambda;

internal sealed class LambdaInvocationContext : ILambdaInvocationContext, IAsyncDisposable
{
    private readonly ILambdaContext _lambdaContext;
    private readonly IServiceScopeFactory _serviceScopeFactory;

    private IServiceScope? _instanceServicesScope;

    public LambdaInvocationContext(
        ILambdaContext lambdaContext,
        IServiceScopeFactory serviceScopeFactory,
        IDictionary<string, object?> properties,
        IFeatureCollection featuresCollection,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(lambdaContext);
        ArgumentNullException.ThrowIfNull(serviceScopeFactory);
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentNullException.ThrowIfNull(featuresCollection);

        _lambdaContext = lambdaContext;
        _serviceScopeFactory = serviceScopeFactory;

        CancellationToken = cancellationToken;
        Properties = properties;
        Features = featuresCollection;
    }

    public async ValueTask DisposeAsync()
    {
        if (_instanceServicesScope is IAsyncDisposable instanceServicesScopeAsyncDisposable)
            await instanceServicesScopeAsyncDisposable.DisposeAsync();

        _instanceServicesScope?.Dispose();

        _instanceServicesScope = null;
        ServiceProvider = null!;
        Items.Clear();
    }

    //      ┌──────────────────────────────────────────────────────────┐
    //      │                      ILambdaContext                      │
    //      └──────────────────────────────────────────────────────────┘

    public string AwsRequestId => _lambdaContext.AwsRequestId;

    public IClientContext ClientContext => _lambdaContext.ClientContext;

    public string FunctionName => _lambdaContext.FunctionName;

    public string FunctionVersion => _lambdaContext.FunctionVersion;

    public ICognitoIdentity Identity => _lambdaContext.Identity;

    public string InvokedFunctionArn => _lambdaContext.InvokedFunctionArn;

    public ILambdaLogger Logger => _lambdaContext.Logger;

    public string LogGroupName => _lambdaContext.LogGroupName;

    public string LogStreamName => _lambdaContext.LogStreamName;

    public int MemoryLimitInMB => _lambdaContext.MemoryLimitInMB;

    public TimeSpan RemainingTime => _lambdaContext.RemainingTime;

    public string TenantId => _lambdaContext.TenantId;

    public string TraceId => _lambdaContext.TraceId;

    //      ┌──────────────────────────────────────────────────────────┐
    //      │                    ILambdaInvocationContext                    │
    //      └──────────────────────────────────────────────────────────┘

    public IServiceProvider ServiceProvider
    {
        get
        {
            if (field is null)
            {
                _instanceServicesScope = _serviceScopeFactory.CreateScope();
                field = _instanceServicesScope.ServiceProvider;
            }

            return field;
        }
        private set;
    }

    public IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();

    public IDictionary<string, object?> Properties { get; }

    public CancellationToken CancellationToken { get; }

    public IFeatureCollection Features { get; }
}
