using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using Amazon.Lambda.Core;
using Amazon.Lambda.Serialization.SystemTextJson;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Extension methods for registering Lambda Host services.</summary>
public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        /// <summary>Registers core Lambda Host services into the dependency injection container.</summary>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddLambdaHostCoreServices()
        {
            ArgumentNullException.ThrowIfNull(services);

            // register core factories
            services.AddSingleton<ILambdaInvocationBuilderFactory,
                DefaultLambdaInvocationBuilderFactory>();
            services.AddSingleton<ILambdaOnInitBuilderFactory, DefaultLambdaOnInitBuilderFactory>();
            services.AddSingleton<ILambdaOnShutdownBuilderFactory,
                DefaultLambdaOnShutdownBuilderFactory>();
            services.AddSingleton<IFeatureCollectionFactory, DefaultFeatureCollectionFactory>();
            services.AddSingleton<ILambdaInvocationContextFactory,
                LambdaInvocationContextFactory>();
            services.AddSingleton<ILambdaLifecycleContextFactory, LambdaLifecycleContextFactory>();
            services.AddSingleton<IInvocationDataFeatureFactory, InvocationDataFeatureFactory>();

            // Register internal Lambda execution components
            services.AddSingleton<ILambdaHandlerFactory, LambdaHandlerComposer>();
            services.AddSingleton<ILambdaBootstrapOrchestrator, LambdaBootstrapAdapter>();

            // Register LambdaHostedService as IHostedService
            services.AddHostedService<LambdaHostedService>();

            // Register options related services
            services.AddOptions<EventHandlerOptions>().ValidateOnStart();
            services.TryAddEnumerable(
                ServiceDescriptor
                    .Singleton<IValidateOptions<EventHandlerOptions>,
                        EventHandlerOptionsValidator>());
            services.AddSingleton<IPostConfigureOptions<HostOptions>,
                HostOptionsPostConfiguration>();
            services.AddSingleton<IPostConfigureOptions<EnvelopeOptions>,
                EnvelopeOptionsPostConfiguration>();

            // Register IFeatureProvider factories
            services.AddSingleton<IResponseFeatureProviderFactory,
                ResponseFeatureProviderFactory>();
            services.AddSingleton<IEventFeatureProviderFactory, EventFeatureProviderFactory>();

            return services;
        }

        /// <summary>
        ///     Conditionally registers default Lambda Host services if they haven't been registered
        ///     already.
        /// </summary>
        /// <returns>The service collection for chaining.</returns>
        [UnconditionalSuppressMessage(
            "Aot",
            "IL2026:RequiresUnreferencedCode",
            Justification =
                "DefaultLambdaJsonSerializer is a fallback. Users can AddLambdaSerializerWithContext for AOT scenarios.")]
        public IServiceCollection TryAddLambdaHostDefaultServices()
        {
            ArgumentNullException.ThrowIfNull(services);

            // We will only attempt to add ILambdaSerializer if we are not using AOT.
            // This is needed for code AOT analysis.
            if (RuntimeFeature.IsDynamicCodeSupported)
#pragma warning disable IL2026
                services.TryAddSingleton<ILambdaSerializer, DefaultLambdaJsonSerializer>();
#pragma warning restore IL2026

            services.TryAddSingleton<ILambdaCancellationFactory,
                DefaultLambdaCancellationFactory>();

            return services;
        }

        /// <summary>
        ///     Registers the <see cref="ILambdaInvocationContextAccessor" /> service into the dependency
        ///     injection container.
        /// </summary>
        /// <remarks>
        ///     This service allows components throughout the application to access the current
        ///     <see cref="ILambdaInvocationContext" /> via dependency injection without requiring it to be passed as
        ///     a parameter. The accessor is registered as a singleton and stores the context per invocation
        ///     for convenient access throughout the dependency injection container.
        /// </remarks>
        /// <returns>The service collection for chaining.</returns>
        public IServiceCollection AddLambdaInvocationContextAccessor()
        {
            ArgumentNullException.ThrowIfNull(services);

            services.AddSingleton<ILambdaInvocationContextAccessor,
                LambdaInvocationContextAccessor>();

            return services;
        }
    }
}
