using System.Net;
using Microsoft.Extensions.Configuration;
using Scynapse;
using Scynapse.Hosting;
using Scynapse.Providers;
using Scynapse.Runtime.Hosting.ProviderConfiguration;

[assembly: RegisterProvider("Development", "Clustering", "Silo", typeof(DevelopmentClusteringProvider))]

namespace Scynapse.Runtime.Hosting.ProviderConfiguration;

internal sealed class DevelopmentClusteringProvider : IProviderBuilder<ISiloBuilder>
{
    public void Configure(ISiloBuilder builder, string name, IConfigurationSection configurationSection)
    {
        IPEndPoint primarySiloEndPoint = null;
        if (configurationSection["PrimarySiloEndPoint"] is { Length: > 0 } primarySiloEndPointValue && !IPEndPoint.TryParse(primarySiloEndPointValue, out primarySiloEndPoint))
        {
            throw new ScynapseConfigurationException($"Unable to parse configuration value at path {configurationSection.Path}:PrimarySiloEndPoint as an IPEndPoint. Value: '{primarySiloEndPointValue}'.");
        }

        builder.UseDevelopmentClustering(primarySiloEndPoint);
    }
}
