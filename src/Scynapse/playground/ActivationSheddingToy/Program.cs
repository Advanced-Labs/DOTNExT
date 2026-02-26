using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scynapse.Configuration;
using Scynapse.Hosting;

var builder = Host.CreateApplicationBuilder(args);
builder.UseScynapse(scynapse =>
{
    scynapse.UseLocalhostClustering();
#pragma warning disable SCYNAPSEEXP003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    scynapse.AddDistributedGrainDirectory();
#pragma warning restore SCYNAPSEEXP003 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
});

builder.Services.Configure<GrainCollectionOptions>(options =>
{
    options.EnableActivationSheddingOnMemoryPressure = true;
    options.MemoryUsageLimitPercentage = 80;
    options.MemoryUsageTargetPercentage = 50;
});

builder.Services.AddHostedService<ActivationSheddingToyHostedService>();
await builder.Build().RunAsync();

