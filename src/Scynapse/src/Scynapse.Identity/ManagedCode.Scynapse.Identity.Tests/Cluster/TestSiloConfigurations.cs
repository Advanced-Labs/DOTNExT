using ManagedCode.Scynapse.Identity.Server.Extensions;
using Scynapse.TestingHost;

namespace ManagedCode.Scynapse.Identity.Tests.Cluster;

public class TestSiloConfigurations : ISiloConfigurator
{
    public void Configure(ISiloBuilder siloBuilder)
    {
        // Add Scynapse Identity server-side components
        siloBuilder.AddScynapseIdentity();

        // For test purpose - in-memory reminder service
        siloBuilder.UseInMemoryReminderService();
    }
}