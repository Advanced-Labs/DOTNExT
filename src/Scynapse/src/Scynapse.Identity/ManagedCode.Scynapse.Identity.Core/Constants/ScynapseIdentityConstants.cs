namespace ManagedCode.Scynapse.Identity.Core.Constants;

public static class ScynapseIdentityConstants
{
    /// <summary>
    /// Key used to store user claims in Scynapse RequestContext.
    /// Works with any ASP.NET Core authentication method (JWT, Cookie, etc.)
    /// </summary>
    public const string USER_CLAIMS = "MC-UserClaims";
}