using System;
using System.Security.Claims;
using ManagedCode.Scynapse.Identity.Core.Constants;
using ManagedCode.Scynapse.Identity.Core.Extensions;
using Scynapse;
using Scynapse.Runtime;

namespace ManagedCode.Scynapse.Identity.Core.Extensions;

public static class ScynapseExtensions
{
    /// <summary>
    /// Parse roles from <typeparam>RequestContext</typeparam>
    /// </summary>
    /// <param name="filter">The incoming grain call filter instance used to access <typeparam>Request
    public static string[] GetRoles(this IIncomingGrainCallFilter filter)
    {
        return RequestContext.Get(ClaimTypes.Role) as string[] ?? [];
    }
}