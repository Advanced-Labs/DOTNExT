using Scynapse.Hosting;

namespace Scynapse.TestingHost;

internal class ConfigureDistributedGrainDirectory : ISiloConfigurator
{
#pragma warning disable SCYNAPSEEXP003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    public void Configure(ISiloBuilder siloBuilder) => siloBuilder.AddDistributedGrainDirectory();
#pragma warning restore SCYNAPSEEXP003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
}