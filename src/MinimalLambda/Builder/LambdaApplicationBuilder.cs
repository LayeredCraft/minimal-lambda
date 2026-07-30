// Portions of this file are derived from aspnetcore
// Source:
// https://github.com/dotnet/aspnetcore/blob/v10.0.0/src/DefaultBuilder/src/WebApplicationBuilder.cs
// Copyright (c) .NET Foundation and Contributors
// Licensed under the MIT License
// See THIRD-PARTY-LICENSES.txt file in the project root or visit
// https://github.com/dotnet/aspnetcore/blob/v10.0.0/LICENSE.txt

using System.Diagnostics;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.Metrics;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MinimalLambda.Builder;

/// <summary>A builder for configuring and constructing a Lambda application.</summary>
/// <remarks>
///     <para>
///         The <see cref="LambdaApplicationBuilder" /> is used to configure services, logging,
///         configuration, and other aspects of a Lambda application before building it into a
///         <see cref="LambdaApplication" /> instance.
///     </para>
///     <para>
///         This builder implements the <see cref="IHostApplicationBuilder" /> interface, providing
///         access to the standard .NET host builder APIs for dependency injection, configuration,
///         logging, and metrics.
///     </para>
///     <para>
///         Instances are created using factory methods on <see cref="LambdaApplication" />, such as
///         <see cref="BuilderLambdaApplicationExtensions.CreateBuilder()" />.
///     </para>
/// </remarks>
/// <seealso cref="BuilderLambdaApplicationExtensions.CreateBuilder()" />
/// <seealso cref="IHostApplicationBuilder" />
public sealed class LambdaApplicationBuilder : IHostApplicationBuilder
{
    private const string LambdaHostAppSettingsSectionName = "LambdaHost";
    private readonly HostApplicationBuilder _hostBuilder;

    private LambdaApplication? _builtApplication;

    /// <summary>Main internal constructor.</summary>
    internal LambdaApplicationBuilder(LambdaApplicationOptions? settings)
    {
        var lifetimeStopwatch = new LifetimeStopwatch();

        settings ??= new LambdaApplicationOptions();

        if (!settings.DisableDefaults)
        {
            settings.Configuration ??= new ConfigurationManager();
            settings.Configuration.AddEnvironmentVariables("AWS_");
            settings.Configuration.AddEnvironmentVariables("DOTNET_");

            settings.ApplicationName ??= settings.Configuration["LAMBDA_FUNCTION_NAME"];

            ResolveContentRoot(settings);
        }

        _hostBuilder = Host.CreateEmptyApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                DisableDefaults = settings.DisableDefaults,
                Args = settings.Args,
                Configuration = settings.Configuration,
                EnvironmentName = settings.EnvironmentName,
                ApplicationName = settings.ApplicationName,
                ContentRootPath = settings.ContentRootPath,
            });

        if (!settings.DisableDefaults)
        {
            ApplyDefaultConfiguration();
            AddDefaultServices();

            // Configure the default service provider factory. This will also handle validation of
            // scope on build.
            var serviceProviderFactory = GetServiceProviderFactory();
            ConfigureContainer(serviceProviderFactory);

            // Configure LambdaHostSettings from appsettings.json
            Services
                .AddOptions<LambdaHostOptions>()
                .BindConfiguration(LambdaHostAppSettingsSectionName);
        }

        // Configure LambdaHostedServiceOptions with callbacks
        Services.Configure<LambdaHostedServiceOptions>(options =>
        {
            options.ConfigureHandlerBuilder = ConfigureHandlerBuilder;
            options.ConfigureOnInitBuilder = ConfigureOnInitBuilder;
            options.ConfigureOnShutdownBuilder = ConfigureOnShutdownBuilder;
        });

        // Register core services that are required for Lambda Host to run
        Services.AddLambdaHostCoreServices();
        Services.AddSingleton<ILifetimeStopwatch>(lifetimeStopwatch);
    }

    /// <inheritdoc />
    public IDictionary<object, object> Properties =>
        ((IHostApplicationBuilder)_hostBuilder).Properties;

    /// <inheritdoc />
    public IConfigurationManager Configuration => _hostBuilder.Configuration;

    /// <inheritdoc />
    public IHostEnvironment Environment => _hostBuilder.Environment;

    /// <inheritdoc />
    public ILoggingBuilder Logging => _hostBuilder.Logging;

    /// <inheritdoc />
    public IMetricsBuilder Metrics => _hostBuilder.Metrics;

    /// <inheritdoc />
    public IServiceCollection Services => _hostBuilder.Services;

    /// <inheritdoc />
    public void ConfigureContainer<TContainerBuilder>(
        IServiceProviderFactory<TContainerBuilder> factory,
        Action<TContainerBuilder>? configure = null) where TContainerBuilder : notnull =>
        _hostBuilder.ConfigureContainer(factory, configure);

    private void AddDefaultServices() =>
        Services.AddLogging(logging =>
        {
            logging.AddConfiguration(Configuration.GetSection("Logging"));
            logging.AddSimpleConsole();

            logging.Configure(options =>
            {
                options.ActivityTrackingOptions = ActivityTrackingOptions.SpanId
                    | ActivityTrackingOptions.TraceId
                    | ActivityTrackingOptions.ParentId;
            });
        });

    private DefaultServiceProviderFactory GetServiceProviderFactory() =>
        Environment.IsDevelopment()
            ? new DefaultServiceProviderFactory(
                new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true })
            : new DefaultServiceProviderFactory();

    /// <summary>Applies default configuration sources to the application's configuration manager.</summary>
    /// <remarks>
    ///     This method configures the standard configuration sources for a Lambda application:
    ///     <list type="bullet">
    ///         <item>Loads <c>appsettings.json</c> from the content root</item>
    ///         <item>Loads environment-specific settings from <c>appsettings.{EnvironmentName}.json</c></item>
    ///         <item>Loads user secrets in development environments from the entry assembly</item>
    ///         <item>Loads all remaining environment variables</item>
    ///     </list>
    /// </remarks>
    private void ApplyDefaultConfiguration()
    {
        // Add appsettings.json and appsettings.{EnvironmentName}.json
        Configuration
            .AddJsonFile("appsettings.json", true, false)
            .AddJsonFile($"appsettings.{Environment.EnvironmentName}.json", true, false);

        // add user secrets if in development environment
        if (Environment.IsDevelopment())
            try
            {
                var assembly = Assembly.GetEntryAssembly();
                if (assembly is not null)
                    Configuration.AddUserSecrets(assembly, true, false);
            }
            catch
            {
                // ignored
            }

        // add the rest of the environment variables
        Configuration.AddEnvironmentVariables();
    }

    private static void ResolveContentRoot(LambdaApplicationOptions settings)
    {
        // If the user has set the ContentRootPath explicitly, we don't need to do anything. This
        // will also capture if the user set DOTNET_CONTENTROOT.
        if (settings.ContentRootPath is not null
            || settings.Configuration?[HostDefaults.ContentRootKey] is not null)
            return;

        // If the user does not set DOTNET_CONTENTROOT or sets the content root explicitly, we will
        // try to use AWS_LAMBDA_TASK_ROOT as AWS will always set this when deployed.
        if (settings.Configuration?["LAMBDA_TASK_ROOT"] is { Length: > 0 } lambdaRoot)
        {
            settings.ContentRootPath = lambdaRoot;
            return;
        }

        // If we're not deployed as a Lambda, we default to the current directory. This may be
        // overridden later by
        // Borrowed from:
        // https://github.com/dotnet/dotnet/blob/main/src/runtime/src/libraries/Microsoft.Extensions.Hosting/src/HostingHostBuilderExtensions.cs
        // If we're running anywhere other than C:\Windows\system32, we default to using the CWD for
        // the ContentRoot.
        // However, since many things like Windows services and MSIX installers have
        // C:\Windows\system32 as there CWD which is not likely
        // to really be the home for things like appsettings.json, we skip changing the ContentRoot
        // in that case. The non-"default" initial
        // value for ContentRoot is AppContext.BaseDirectory (e.g. the executable path) which
        // probably makes more sense than the system32.

        // In my testing, both Environment.CurrentDirectory and Environment.SystemDirectory return
        // the path without
        // any trailing directory separator characters. I'm not even sure the casing can ever be
        // different from these APIs, but I think it makes sense to
        // ignore case for Windows path comparisons given the file system is usually (always?) going
        // to be case insensitive for the system path.
        var cwd = System.Environment.CurrentDirectory;
        if (!OperatingSystem.IsWindows()
            || !string.Equals(
                cwd,
                System.Environment.SystemDirectory,
                StringComparison.OrdinalIgnoreCase))
            settings.ContentRootPath = cwd;
    }

    /// <summary>Builds the Lambda application with the configured services and settings.</summary>
    /// <remarks>
    ///     <para>
    ///         The <c>Build</c> method finalizes the configuration and creates a
    ///         <see cref="LambdaApplication" /> instance that is ready to run. It performs the following
    ///         operations:
    ///     </para>
    ///     <list type="bullet">
    ///         <item>
    ///             <description>
    ///                 Registers a default <see cref="ILambdaCancellationFactory" /> if one is
    ///                 not already registered, which manages cancellation tokens for Lambda invocations.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Applies shutdown timeout settings from <see cref="LambdaHostOptions" /> to
    ///                 ensure proper graceful shutdown before the Lambda runtime timeout.
    ///             </description>
    ///         </item>
    ///         <item>
    ///             <description>
    ///                 Constructs the underlying host and wraps it in a
    ///                 <see cref="LambdaApplication" /> instance.
    ///             </description>
    ///         </item>
    ///     </list>
    /// </remarks>
    /// <returns>A configured <see cref="LambdaApplication" /> ready to be started and run.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown when the build process fails due to service
    ///     configuration issues.
    /// </exception>
    public LambdaApplication Build()
    {
        Services.TryAddLambdaHostDefaultServices();

        _builtApplication = new LambdaApplication(_hostBuilder.Build());

        return _builtApplication;
    }

    private void ConfigureHandlerBuilder(ILambdaInvocationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        Debug.Assert(_builtApplication is not null);

        if (_builtApplication.Handler is null)
            throw new InvalidOperationException("Lambda Handler is not set.");

        foreach (var middleware in _builtApplication.Middlewares)
            builder.Use(middleware);

        // add default middleware to the end of the middleware pipeline
        builder.UseExtractAndPackEnvelope();

        builder.Handle(_builtApplication.Handler);

        foreach (var property in _builtApplication.Properties)
            builder.Properties[property.Key] = property.Value;
    }

    private void ConfigureOnInitBuilder(ILambdaOnInitBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        Debug.Assert(_builtApplication is not null);

        var settings = _builtApplication.Services.GetRequiredService<IOptions<LambdaHostOptions>>()
            .Value;

        // Add default OnInit handlers if asked for
        if (settings.ClearLambdaOutputFormatting)
            builder.OnInitClearLambdaOutputFormatting();

        foreach (var handlers in _builtApplication.InitHandlers)
            builder.OnInit(handlers);

        foreach (var property in ((ILambdaOnInitBuilder)_builtApplication).Properties)
            builder.Properties[property.Key] = property.Value;
    }

    private void ConfigureOnShutdownBuilder(ILambdaOnShutdownBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        Debug.Assert(_builtApplication is not null);

        foreach (var handlers in _builtApplication.ShutdownHandlers)
            builder.OnShutdown(handlers);

        foreach (var property in ((ILambdaOnShutdownBuilder)_builtApplication).Properties)
            builder.Properties[property.Key] = property.Value;
    }
}
