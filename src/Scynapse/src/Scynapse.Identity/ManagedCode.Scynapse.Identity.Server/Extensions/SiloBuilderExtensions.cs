using ManagedCode.Scynapse.Identity.Server.GrainCallFilter;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scynapse;
using Scynapse.Hosting;
using Scynapse.Runtime;

namespace ManagedCode.Scynapse.Identity.Server.Extensions;

public static class SiloBuilderExtensions
{
    /// <summary>
    /// Add incoming grain filter for authorization
    /// </summary>
    /// <param name="siloBuilder"></param>
    /// <returns></returns>
    public static ISiloBuilder AddScynapseIdentity(this ISiloBuilder siloBuilder)
    {
        siloBuilder.AddIncomingGrainCallFilter<GrainAuthorizationIncomingFilter>();
        return siloBuilder;
    }
}