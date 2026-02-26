using System;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scynapse.Hosting;
using Scynapse.Runtime;

namespace Microsoft.Extensions.Hosting
{
    /// <summary>
    /// Extension methods for <see cref="IHostBuilder"/>.
    /// </summary>
    public static class ScynapseSiloGenericHostExtensions
    {
        private static readonly Type MarkerType = typeof(ScynapseBuilderMarker);

        /// <summary>
        /// Configures the host app builder to host an Scynapse silo.
        /// </summary>
        /// <param name="hostAppBuilder">The host app builder.</param>
        /// <returns>The host builder.</returns>
        /// <remarks>
        /// Calling this method multiple times on the same <see cref="IHostApplicationBuilder"/> instance will result in one silo being configured.
        /// </remarks>
        public static IHostApplicationBuilder UseScynapse(
            this IHostApplicationBuilder hostAppBuilder)
            => hostAppBuilder.UseScynapse(_ => { });

        /// <summary>
        /// Configures the host app builder to host an Scynapse silo.
        /// </summary>
        /// <param name="hostAppBuilder">The host app builder.</param>
        /// <returns>The host builder.</returns>
        /// <remarks>
        /// Calling this method multiple times on the same <see cref="HostApplicationBuilder"/> instance will result in one silo being configured.
        /// </remarks>
        public static HostApplicationBuilder UseScynapse(
            this HostApplicationBuilder hostAppBuilder)
            => hostAppBuilder.UseScynapse(_ => { });

        /// <summary>
        /// Configures the host builder to host an Scynapse silo.
        /// </summary>
        /// <param name="hostAppBuilder">The host app builder.</param>
        /// <param name="configureDelegate">The delegate used to configure the silo.</param>
        /// <returns>The host builder.</returns>
        /// <remarks>
        /// Calling this method multiple times on the same <see cref="IHostApplicationBuilder"/> instance will result in one silo being configured.
        /// However, the effects of <paramref name="configureDelegate"/> will be applied once for each call.
        /// </remarks>
        public static IHostApplicationBuilder UseScynapse(
            this IHostApplicationBuilder hostAppBuilder,
            Action<ISiloBuilder> configureDelegate)
        {
            ArgumentNullException.ThrowIfNull(hostAppBuilder);
            ArgumentNullException.ThrowIfNull(configureDelegate);

            configureDelegate(AddScynapseCore(hostAppBuilder.Services, hostAppBuilder.Configuration));

            return hostAppBuilder;
        }

        /// <summary>
        /// Configures the host builder to host an Scynapse silo.
        /// </summary>
        /// <param name="hostAppBuilder">The host app builder.</param>
        /// <param name="configureDelegate">The delegate used to configure the silo.</param>
        /// <returns>The host builder.</returns>
        /// <remarks>
        /// Calling this method multiple times on the same <see cref="HostApplicationBuilder"/> instance will result in one silo being configured.
        /// However, the effects of <paramref name="configureDelegate"/> will be applied once for each call.
        /// </remarks>
        public static HostApplicationBuilder UseScynapse(
            this HostApplicationBuilder hostAppBuilder,
            Action<ISiloBuilder> configureDelegate)
        {
            ArgumentNullException.ThrowIfNull(hostAppBuilder);
            ArgumentNullException.ThrowIfNull(configureDelegate);

            configureDelegate(AddScynapseCore(hostAppBuilder.Services, hostAppBuilder.Configuration));

            return hostAppBuilder;
        }

        /// <summary>
        /// Configures the host builder to host an Scynapse silo.
        /// </summary>
        /// <param name="hostBuilder">The host builder.</param>
        /// <param name="configureDelegate">The delegate used to configure the silo.</param>
        /// <returns>The host builder.</returns>
        /// <remarks>
        /// Calling this method multiple times on the same <see cref="IHostBuilder"/> instance will result in one silo being configured.
        /// However, the effects of <paramref name="configureDelegate"/> will be applied once for each call.
        /// </remarks>
        public static IHostBuilder UseScynapse(
            this IHostBuilder hostBuilder,
            Action<ISiloBuilder> configureDelegate) => hostBuilder.UseScynapse((_, siloBuilder) => configureDelegate(siloBuilder));

        /// <summary>
        /// Configures the host builder to host an Scynapse silo.
        /// </summary>
        /// <param name="hostBuilder">The host builder.</param>
        /// <param name="configureDelegate">The delegate used to configure the silo.</param>
        /// <returns>The host builder.</returns>
        /// <remarks>
        /// Calling this method multiple times on the same <see cref="IHostBuilder"/> instance will result in one silo being configured.
        /// However, the effects of <paramref name="configureDelegate"/> will be applied once for each call.
        /// </remarks>
        public static IHostBuilder UseScynapse(
            this IHostBuilder hostBuilder,
            Action<HostBuilderContext, ISiloBuilder> configureDelegate)
        {
            ArgumentNullException.ThrowIfNull(hostBuilder);
            ArgumentNullException.ThrowIfNull(configureDelegate);

            if (hostBuilder.Properties.ContainsKey("HasScynapseClientBuilder"))
            {
                throw GetScynapseClientAddedException();
            }

            hostBuilder.Properties["HasScynapseSiloBuilder"] = "true";

            return hostBuilder.ConfigureServices((context, services) => configureDelegate(context, AddScynapseCore(services, context.Configuration)));
        }

        /// <summary>
        /// Configures the service collection to host an Scynapse silo.
        /// </summary>
        /// <param name="services">The service collection.</param>
        /// <param name="configureDelegate">The delegate used to configure the silo.</param>
        /// <returns>The service collection.</returns>
        /// <remarks>
        /// Calling this method multiple times on the same <see cref="IHostBuilder"/> instance will result in one silo being configured.
        /// However, the effects of <paramref name="configureDelegate"/> will be applied once for each call.
        /// </remarks>
        public static IServiceCollection AddScynapse(
            this IServiceCollection services,
            Action<ISiloBuilder> configureDelegate)
        {
            ArgumentNullException.ThrowIfNull(configureDelegate);

            var builder = AddScynapseCore(services, null);

            configureDelegate(builder);

            return services;
        }

        private static ISiloBuilder AddScynapseCore(IServiceCollection services, IConfiguration configuration)
        {
            ISiloBuilder builder = default;
            configuration ??= new ConfigurationBuilder().Build();
            foreach (var descriptor in services.Where(d => d.ServiceType.Equals(MarkerType)))
            {
                var marker = (ScynapseBuilderMarker)descriptor.ImplementationInstance;
                builder = marker.BuilderInstance switch
                {
                    ISiloBuilder existingBuilder => existingBuilder,
                    _ => throw GetScynapseClientAddedException()
                };
            }

            if (builder is null)
            {
                builder = new SiloBuilder(services, configuration);
                services.AddSingleton(new ScynapseBuilderMarker(builder));
            }

            return builder;
        }

        private static ScynapseConfigurationException GetScynapseClientAddedException() =>
            new("Do not call both UseScynapseClient/AddScynapseClient with UseScynapse/AddScynapse. If you want a client and server in the same process, only UseScynapse/AddScynapse is necessary and the UseScynapseClient/AddScynapseClient call can be removed.");
    }
}