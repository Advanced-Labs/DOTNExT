using System;
using System.Security.Claims;
using Scynapse;
using Scynapse.Runtime;
using ManagedCode.Scynapse.Identity.Core.Constants;

namespace ManagedCode.Scynapse.Identity.Core.Extensions;

public static class GrainExtensions
{
    public static ClaimsPrincipal GetCurrentUser(this Grain grain)
    {
        var requestContext = RequestContext.Get(ScynapseIdentityConstants.USER_CLAIMS);
        return requestContext as ClaimsPrincipal ?? new ClaimsPrincipal(new ClaimsIdentity());
    }
} 