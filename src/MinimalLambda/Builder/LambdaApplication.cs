using System.Collections.Concurrent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MinimalLambda.Builder;

/// <summary>A Lambda application that provides host functionality for running AWS Lambda handlers.</summary>
public sealed class LambdaApplication
    : IHost,
        ILambdaInvocationBuilder,
        ILambdaOnInitBuilder,
        ILambdaOnShutdownBuilder,
        IAsyncDisposable
{
    private readonly IHost _host;
    private readonly ILambdaInvocationBuilder _invocationBuilder;
    private readonly ILambdaOnInitBuilder _onInitBuilder;
    private readonly ILambdaOnShutdownBuilder _onShutdownBuilder;

    internal LambdaApplication(IHost host)
    {
        ArgumentNullException.ThrowIfNull(host);

        _host = host;

        _invocationBuilder = Services
            .GetRequiredService<ILambdaInvocationBuilderFactory>()
            .CreateBuilder();

        _onInitBuilder = Services.GetRequiredService<ILambdaOnInitBuilderFactory>().CreateBuilder();

        _onShutdownBuilder = Services
            .GetRequiredService<ILambdaOnShutdownBuilderFactory>()
            .CreateBuilder();
    }

    /// <summary>Gets the application's configuration.</summary>
    public IConfiguration Configuration =>
        field ??= _host.Services.GetRequiredService<IConfiguration>();

    /// <summary>Gets the application's host environment.</summary>
    public IHostEnvironment Environment =>
        field ??= _host.Services.GetRequiredService<IHostEnvironment>();

    /// <summary>Gets the application's lifetime token source.</summary>
    public IHostApplicationLifetime Lifetime =>
        field ??= _host.Services.GetRequiredService<IHostApplicationLifetime>();

    /// <summary>Gets the application's logger.</summary>
    public ILogger Logger =>
        field ??= _host
                .Services
                .GetService<ILoggerFactory>()
                ?.CreateLogger(Environment.ApplicationName)
            ?? NullLogger.Instance;

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ((IAsyncDisposable)_host).DisposeAsync();

    /// <summary>Gets the application's service provider.</summary>
    public IServiceProvider Services => _host.Services;

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default) =>
        _host.StartAsync(cancellationToken);

    /// <inheritdoc />
    public Task StopAsync(CancellationToken cancellationToken = default) =>
        _host.StopAsync(cancellationToken);

    /// <inheritdoc />
    public void Dispose() => _host.Dispose();

    //      ┌──────────────────────────────────────────────────────────┐
    //      │                 ILambdaInvocationBuilder                 │
    //      └──────────────────────────────────────────────────────────┘

    /// <inheritdoc />
    public IDictionary<string, object?> Properties => _invocationBuilder.Properties;

    /// <inheritdoc />
    public IReadOnlyList<Func<LambdaInvocationDelegate, LambdaInvocationDelegate>> Middlewares =>
        _invocationBuilder.Middlewares;

    /// <inheritdoc />
    public LambdaInvocationDelegate? Handler => _invocationBuilder.Handler;

    /// <inheritdoc />
    public ILambdaInvocationBuilder Handle(LambdaInvocationDelegate handler)
    {
        _invocationBuilder.Handle(handler);
        return this;
    }

    /// <inheritdoc />
    public ILambdaInvocationBuilder Use(
        Func<LambdaInvocationDelegate, LambdaInvocationDelegate> middleware)
    {
        _invocationBuilder.Use(middleware);
        return this;
    }

    /// <inheritdoc />
    public LambdaInvocationDelegate Build() => _invocationBuilder.Build();

    //      ┌──────────────────────────────────────────────────────────┐
    //      │                   ILambdaOnInitBuilder                   │
    //      └──────────────────────────────────────────────────────────┘

    /// <inheritdoc />
    public IReadOnlyList<LambdaInitDelegate> InitHandlers => _onInitBuilder.InitHandlers;

    /// <inheritdoc />
    ConcurrentDictionary<string, object?> ILambdaOnInitBuilder.Properties =>
        _onInitBuilder.Properties;

    /// <inheritdoc />
    public ILambdaOnInitBuilder OnInit(LambdaInitDelegate handler)
    {
        _onInitBuilder.OnInit(handler);
        return this;
    }

    /// <inheritdoc />
    Func<CancellationToken, Task<bool>>? ILambdaOnInitBuilder.Build() => _onInitBuilder.Build();

    //      ┌──────────────────────────────────────────────────────────┐
    //      │                 ILambdaOnShutdownBuilder                 │
    //      └──────────────────────────────────────────────────────────┘

    /// <inheritdoc />
    public IReadOnlyList<LambdaShutdownDelegate> ShutdownHandlers =>
        _onShutdownBuilder.ShutdownHandlers;

    /// <inheritdoc />
    ConcurrentDictionary<string, object?> ILambdaOnShutdownBuilder.Properties =>
        _onInitBuilder.Properties;

    /// <inheritdoc />
    public ILambdaOnShutdownBuilder OnShutdown(LambdaShutdownDelegate handler)
    {
        _onShutdownBuilder.OnShutdown(handler);
        return this;
    }

    /// <inheritdoc />
    Func<CancellationToken, Task> ILambdaOnShutdownBuilder.Build() => _onShutdownBuilder.Build();
}
