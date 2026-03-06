using Scynapse.Security.Assertions;
using Scynapse.Security.Crypto;
using Scynapse.Security.Transport;
using Scynapse.Security.Verification;

namespace Scynapse.Security.Orleans;

/// <summary>
/// Default SecurityGateway implementation.
///
/// Issues CCaps based on the caller's delegation chain scope:
/// - Parses the delegation chain from the request
/// - Finds the narrowest delegation scope (resource/action patterns)
/// - Issues CCaps within that scope
///
/// Application developers can replace this with custom logic by registering their own grain class.
/// </summary>
public class DefaultSecurityGatewayGrain : Grain, ISecurityGatewayGrain
{
    private readonly ScynapseSecurityOptions _options;
    private readonly IAssertionStore _store;
    private readonly INonceStore _nonceStore;

    public DefaultSecurityGatewayGrain(
        ScynapseSecurityOptions options,
        IAssertionStore store,
        INonceStore nonceStore)
    {
        _options = options;
        _store = store;
        _nonceStore = nonceStore;
    }

    public async Task<CCapBundle> AuthenticateAsync(byte[] delegationChainCbor)
    {
        var callerKey = GetVerifiedCallerKey();

        // Deserialize and store the delegation chain assertions
        var assertions = DeserializeChain(delegationChainCbor);
        foreach (var assertion in assertions)
            await _store.StoreAsync(assertion);

        // Find delegations targeting the caller
        var delegations = assertions.Where(a =>
            a.ClaimType == ClaimType.Delegation &&
            a.Subject.Span.SequenceEqual(callerKey)).ToList();

        // Issue CCaps based on delegation scope
        var bundle = new CCapBundle();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();

        foreach (var delegation in delegations)
        {
            var delClaim = DelegationClaim.Deserialize(delegation.ClaimData.Span);
            var resource = delClaim.ResourcePattern ?? "scynapse.>";
            var action = delClaim.ActionPattern ?? "*";

            var ccap = AssertionBuilder.CreateCapability(
                _options.NodeKeyPair,
                callerKey,
                resource, action,
                proofs: new[] { delegation.Id.ToArray() },
                expiresAt: expiresAt);

            bundle.Capabilities.Add(ccap.Serialize());
        }

        // If no delegations found but caller is authenticated, issue a minimal CCap
        if (bundle.Capabilities.Count == 0)
        {
            var ccap = AssertionBuilder.CreateCapability(
                _options.NodeKeyPair,
                callerKey,
                GrainResourceInference.WildcardAllApp, "*",
                expiresAt: expiresAt);
            bundle.Capabilities.Add(ccap.Serialize());
        }

        bundle.EarliestExpiry = expiresAt;
        return bundle;
    }

    public Task<byte[]?> RequestCapabilityAsync(string resource, string action)
    {
        var callerKey = GetVerifiedCallerKey();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();

        var ccap = AssertionBuilder.CreateCapability(
            _options.NodeKeyPair,
            callerKey,
            resource, action,
            expiresAt: expiresAt);

        return Task.FromResult<byte[]?>(ccap.Serialize());
    }

    public Task<CCapBundle> RefreshAsync(byte[] expiringCCapsCbor)
    {
        var callerKey = GetVerifiedCallerKey();
        var expiresAt = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var bundle = new CCapBundle { EarliestExpiry = expiresAt };

        var expiring = DeserializeChain(expiringCCapsCbor);
        foreach (var old in expiring.Where(a => a.ClaimType == ClaimType.Capability))
        {
            // Verify the old CCap's subject matches the caller
            if (!old.Subject.Span.SequenceEqual(callerKey))
                continue;

            var claim = CapabilityClaim.Deserialize(old.ClaimData.Span);
            var renewed = AssertionBuilder.CreateCapability(
                _options.NodeKeyPair,
                callerKey,
                claim.Resource, claim.Action,
                expiresAt: expiresAt);

            bundle.Capabilities.Add(renewed.Serialize());
        }

        return Task.FromResult(bundle);
    }

    private byte[] GetVerifiedCallerKey()
    {
        var callerKey = RequestContext.Get(ScynapseSecurityConstants.VerifiedCallerKeyKey) as byte[];
        if (callerKey is null)
            throw new ScynapseSecurityException("No verified caller identity");
        return callerKey;
    }

    private static List<SignedAssertion> DeserializeChain(byte[] cborData)
    {
        var result = new List<SignedAssertion>();
        // The chain is serialized as concatenated CBOR-encoded assertions.
        // Each assertion is self-delimiting in CBOR, so we can read sequentially.
        var span = cborData.AsSpan();
        var offset = 0;
        while (offset < span.Length)
        {
            // Try to deserialize from current offset
            var remaining = span[offset..].ToArray();
            var assertion = SignedAssertion.Deserialize(remaining);
            result.Add(assertion);
            // Re-serialize to determine byte length consumed
            var reserialized = assertion.Serialize();
            offset += reserialized.Length;
        }
        return result;
    }
}
