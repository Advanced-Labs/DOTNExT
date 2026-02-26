using Microsoft.Extensions.Configuration;
using Scynapse;
using Scynapse.Hosting;
using Scynapse.Providers;
using Scynapse.Runtime.Hosting.ProviderConfiguration;

[assembly: RegisterProvider("Memory", "Reminders", "Silo", typeof(MemoryReminderTableBuilder))]

namespace Scynapse.Runtime.Hosting.ProviderConfiguration;

internal sealed class MemoryReminderTableBuilder : IProviderBuilder<ISiloBuilder>
{
    public void Configure(ISiloBuilder builder, string name, IConfigurationSection configurationSection)
    {
        builder.UseInMemoryReminderService();
    }
}
