using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using Scynapse.Runtime;
using ManagedCode.Scynapse.Identity.Core.Constants;

namespace ManagedCode.Scynapse.Identity.Client.Filters;

public sealed class ScynapseAuthorizationActionFilter : IAsyncActionFilter
{
    public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        RequestContext.Set(ScynapseIdentityConstants.USER_CLAIMS, context.HttpContext.User);
        return next();
    }
}