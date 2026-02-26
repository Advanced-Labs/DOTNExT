using ManagedCode.Scynapse.Identity.Client.Extensions;
using Microsoft.Extensions.Configuration;
using Scynapse.TestingHost;

namespace ManagedCode.Scynapse.Identity.Tests.Cluster;

public class TestClientConfigurations : IClientBuilderConfigurator
{
    public void Configure(IConfiguration configuration, IClientBuilder clientBuilder)
    {
        // Add Scynapse Identity client-side components
    }
}