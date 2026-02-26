using System;
using Scynapse.Providers;
using Microsoft.Extensions.Configuration;
using Scynapse;
using Scynapse.Hosting;
using Microsoft.Extensions.DependencyInjection;

[assembly: RegisterProvider("Consul", "Clustering", "Client", typeof(ConsulClusteringProviderBuilder))]
[assembly: RegisterProvider("Consul", "Clustering", "Silo", typeof(ConsulClusteringProviderBuilder))]

namespace Scynapse.Hosting;

internal sealed class ConsulClusteringProviderBuilder : IProviderBuilder<ISiloBuilder>, IProviderBuilder<IClientBuilder>
{
    public void Configure(ISiloBuilder builder, string name, IConfigurationSection configurationSection)
    {
        builder.UseConsulSiloClustering(options => options.Bind(configurationSection));
    }

    public void Configure(IClientBuilder builder, string name, IConfigurationSection configurationSection)
    {
        builder.UseConsulClientClustering(options => options.Bind(configurationSection));
    }
}
