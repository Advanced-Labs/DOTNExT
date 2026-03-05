namespace Scynapse.Security.Orleans;

/// <summary>
/// RequestContext keys used by Scynapse security call filters.
/// </summary>
public static class ScynapseSecurityConstants
{
    public const string CallerPublicKeyKey = "Scynapse.Caller.PublicKey";
    public const string CCapKey = "Scynapse.CCap";
    public const string BearerProofKey = "Scynapse.CCap.BearerProof";
    public const string VerifiedCallerKeyKey = "Scynapse.Verified.CallerKey";
    public const string VerifiedCCapKey = "Scynapse.Verified.CCap";
}
