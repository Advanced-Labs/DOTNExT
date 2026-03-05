using Scynapse.Runtime;
using Scynapse.Security.Assertions;

namespace Scynapse.Security.Orleans;

/// <summary>
/// Extension methods for grain code to access verified security context.
/// These read from RequestContext values set by ScynapseIncomingCallFilter
/// after successful verification.
/// </summary>
public static class GrainSecurityExtensions
{
    /// <summary>
    /// Get the verified caller's public key from the current grain call context.
    /// Returns null if the call was unauthenticated (anonymous).
    /// </summary>
    public static byte[]? GetCallerPublicKey()
        => RequestContext.Get(ScynapseSecurityConstants.VerifiedCallerKeyKey) as byte[];

    /// <summary>
    /// Get the verified CCap that authorized this call.
    /// Returns null if the call was unauthenticated.
    /// </summary>
    public static SignedAssertion? GetCallerCapability()
        => RequestContext.Get(ScynapseSecurityConstants.VerifiedCCapKey) as SignedAssertion;
}
