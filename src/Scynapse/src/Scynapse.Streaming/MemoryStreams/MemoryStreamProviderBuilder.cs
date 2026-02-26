using Microsoft.Extensions.Configuration;
using Scynapse;
using Scynapse.Hosting;
using Scynapse.Providers;

[assembly: RegisterProvider("Memory", "Streaming", "Client", typeof(MemoryStreamProviderBuilder))]
[assembly: RegisterProvider("Memory", "Streaming", "Silo", typeof(MemoryStreamProviderBuilder))]
namespace Scynapse.Providers;

internal sealed class MemoryStreamProviderBuilder : IProviderBuilder<ISiloBuilder>, IProviderBuilder<IClientBuilder>
{
    public void Configure(ISiloBuilder builder, string name, IConfigurationSection configurationSection)
    {
        builder.AddMemoryStreams(name);
    }

    public void Configure(IClientBuilder builder, string name, IConfigurationSection configurationSection)
    {
        builder.AddMemoryStreams(name);
    }
}
