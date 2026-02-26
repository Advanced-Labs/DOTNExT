using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scynapse;
using Scynapse.Hosting;
using Scynapse.Providers;

[assembly: RegisterProvider("Default", "BroadcastChannel", "Client", typeof(BroadcastChannelProviderBuilder))]
[assembly: RegisterProvider("Default", "BroadcastChannel", "Silo", typeof(BroadcastChannelProviderBuilder))]

namespace Scynapse.Providers;

internal sealed class BroadcastChannelProviderBuilder : IProviderBuilder<ISiloBuilder>, IProviderBuilder<IClientBuilder>
{
    public void Configure(ISiloBuilder builder, string name, IConfigurationSection configurationSection)
    {
        builder.AddBroadcastChannel(name, options => options.Bind(configurationSection));
    }

    public void Configure(IClientBuilder builder, string name, IConfigurationSection configurationSection)
    {
        builder.AddBroadcastChannel(name, options => options.Bind(configurationSection));
    }
}

