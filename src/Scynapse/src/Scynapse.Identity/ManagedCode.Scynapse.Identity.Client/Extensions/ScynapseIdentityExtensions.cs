using ManagedCode.Scynapse.Identity.Client.Filters;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Scynapse;
using Scynapse.Hosting;

namespace ManagedCode.Scynapse.Identity.Client.Extensions;

public static class ScynapseIdentityExtensions
{
    public static IServiceCollection AddScynapseIdentity(this IServiceCollection services)
    {
        // Add the action filter globally for all controllers
        services.Configure<MvcOptions>(options =>
        {
            options.Filters.Add<ScynapseAuthorizationActionFilter>();
        });
        
        // Configure SignalR with the authorization filter
        services.Configure<HubOptions>(options =>
        {
            options.AddFilter<SignalRAuthorizationFilter>();
        });
        

        return services;
    }
} 