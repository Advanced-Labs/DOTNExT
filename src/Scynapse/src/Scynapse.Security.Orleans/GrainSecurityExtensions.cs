using Scynapse.Runtime;
using Scynapse.Security;
using Scynapse.Security.Assertions;
using Scynapse.Security.Crypto;
using Scynapse.Security.Transport;

namespace Scynapse.Security.Orleans;

/// <summary>
/// Extension methods for grain code to access verified security context
/// and issue capabilities. These read from RequestContext values set by
/// ScynapseIncomingCallFilter after successful verification.
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

    /// <summary>
    /// Issue a CCap to the authenticated caller, granting them a specific action
    /// on a resource. Uses the node key to sign the assertion. The caller's public key
    /// (from the verified request) becomes the CCap's subject.
    /// </summary>
    /// <param name="nodeKeyPair">The node's signing keypair.</param>
    /// <param name="action">The action to grant (e.g., "read", "write").</param>
    /// <param name="resource">The resource URI. Null defaults to this grain's interface type.</param>
    /// <param name="grainInterfaceType">The grain interface type (for resource inference when resource is null).</param>
    /// <param name="delegationProofIds">Proof IDs linking this node's authority to issue capabilities.</param>
    /// <param name="expiresAt">Optional expiration timestamp (Unix seconds).</param>
    /// <returns>A signed capability assertion for the caller.</returns>
    public static SignedAssertion IssueCCapToCaller(
        ScynapseKeyPair nodeKeyPair,
        string action,
        string? resource = null,
        Type? grainInterfaceType = null,
        IEnumerable<byte[]>? delegationProofIds = null,
        long? expiresAt = null)
    {
        var callerKey = GetCallerPublicKey()
            ?? throw new InvalidOperationException("No authenticated caller — cannot issue CCap.");

        resource ??= grainInterfaceType != null
            ? GrainResourceInference.FromGrainInterface(grainInterfaceType)
            : GrainResourceInference.WildcardAll;

        return AssertionBuilder.CreateCapability(
            nodeKeyPair, callerKey, resource, action,
            delegationProofIds, expiresAt);
    }
}
