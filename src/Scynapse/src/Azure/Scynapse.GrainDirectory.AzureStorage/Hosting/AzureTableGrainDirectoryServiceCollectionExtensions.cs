using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scynapse.Configuration;
using Scynapse.GrainDirectory;
using Scynapse.GrainDirectory.AzureStorage;
using Scynapse.Runtime;
using Scynapse.Runtime.Hosting;

namespace Scynapse.Hosting
{
    /// <summary>
    /// <see cref="IServiceCollection"/> extensions.
    /// </summary>
    public static class AzureTableGrainDirectoryServiceCollectionExtensions
    {
        internal static IServiceCollection AddAzureTableGrainDirectory(
            this IServiceCollection services,
            string name,
            Action<OptionsBuilder<AzureTableGrainDirectoryOptions>> configureOptions)
        {
            configureOptions.Invoke(services.AddOptions<AzureTableGrainDirectoryOptions>(name));
            services
                .AddTransient<IConfigurationValidator>(sp => new AzureTableGrainDirectoryOptionsValidator(sp.GetRequiredService<IOptionsMonitor<AzureTableGrainDirectoryOptions>>().Get(name), name))
                .ConfigureNamedOptionForLogging<AzureTableGrainDirectoryOptions>(name)
                .AddGrainDirectory(name, (sp, name) => ActivatorUtilities.CreateInstance<AzureTableGrainDirectory>(sp, sp.GetOptionsByName<AzureTableGrainDirectoryOptions>(name)));

            return services;
        }
    }
}
