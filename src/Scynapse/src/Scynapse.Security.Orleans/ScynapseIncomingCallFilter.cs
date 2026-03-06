using Scynapse.Runtime;
using Scynapse.Security;
using Scynapse.Security.Assertions;
using Scynapse.Security.Crypto;
using Scynapse.Security.Verification;

namespace Scynapse.Security.Orleans;

/// <summary>
/// THE primary enforcement point. Verifies security context on every incoming grain call.
///
/// Steps:
/// 1. Read security context from RequestContext
/// 2. Get the security policy for the target grain interface
/// 3. If policy requires authentication, verify CCap chain, action/resource match, bearer proof
/// 4. Set verified caller identity for grain code to read
/// </summary>
public sealed class ScynapseIncomingCallFilter : IIncomingGrainCallFilter
{
    private readonly IAssertionStore _store;
    private readonly INonceStore _nonceStore;
    private readonly IReadOnlySet<ReadOnlyMemory<byte>> _trustedRoots;
    private readonly IGrainSecurityPolicyProvider _policyProvider;
    private readonly IAttenuationChecker _attenuationChecker;
    private readonly IReadOnlySet<ReadOnlyMemory<byte>> _trustedNodeKeys;

    public ScynapseIncomingCallFilter(
        IAssertionStore store,
        INonceStore nonceStore,
        IReadOnlySet<ReadOnlyMemory<byte>> trustedRoots,
        IGrainSecurityPolicyProvider policyProvider,
        IAttenuationChecker? attenuationChecker = null,
        IReadOnlySet<ReadOnlyMemory<byte>>? trustedNodeKeys = null)
    {
        _store = store;
        _nonceStore = nonceStore;
        _trustedRoots = trustedRoots;
        _policyProvider = policyProvider;
        _attenuationChecker = attenuationChecker ?? new DefaultAttenuationChecker();
        _trustedNodeKeys = trustedNodeKeys
            ?? new HashSet<ReadOnlyMemory<byte>>(ByteMemoryEqualityComparer.Instance);
    }

    public async Task Invoke(IIncomingGrainCallContext context)
    {
        // Determine the grain interface type from the method's declaring type
        var grainInterfaceType = context.InterfaceMethod.DeclaringType ?? typeof(object);
        var policy = _policyProvider.GetPolicy(grainInterfaceType);

        if (policy.AllowAnonymous)
        {
            await context.Invoke();
            return;
        }

        if (!policy.RequiresAuthentication)
        {
            await context.Invoke();
            return;
        }

        // Read security context
        var callerKey = RequestContext.Get(ScynapseSecurityConstants.CallerPublicKeyKey) as byte[];
        var ccapBytes = RequestContext.Get(ScynapseSecurityConstants.CCapKey) as byte[];
        var bearerProof = RequestContext.Get(ScynapseSecurityConstants.BearerProofKey) as byte[];

        if (callerKey is null)
            throw new ScynapseSecurityException("Authentication required");

        // HYBRID MODEL: Check if caller is a trusted node (valid delegation chain).
        // If yes, and grain doesn't require caller capability (strict mode), allow.
        if (!policy.RequiresCallerCapability && IsTrustedNode(callerKey))
        {
            // Node-trusted call — allowed without CCap verification.
            // Set verified identity for grain code.
            RequestContext.Set(ScynapseSecurityConstants.VerifiedCallerKeyKey, callerKey);
            await context.Invoke();
            return;
        }

        // Not a trusted node, or grain requires caller capability — full CCap verification.
        if (ccapBytes is null)
            throw new ScynapseSecurityException("Authentication required");

        // Deserialize and verify the CCap
        var ccap = SignedAssertion.Deserialize(ccapBytes);

        var verifier = new AssertionVerifier(_store, _nonceStore, _trustedRoots, _attenuationChecker);
        var result = await verifier.VerifyAsync(ccap);
        if (!result.IsValid)
            throw new ScynapseSecurityException($"Invalid CCap: {result.FailureReason}");

        // Verify bearer proof: caller must prove they own the CCap's subject key
        if (bearerProof is null || !VerifyBearerProof(ccap, callerKey, bearerProof))
            throw new ScynapseSecurityException("Bearer verification failed");

        // Check action/resource match if the method requires a specific capability
        var requiredAction = _policyProvider.GetRequiredAction(context.InterfaceMethod);
        if (requiredAction is not null)
        {
            var claim = CapabilityClaim.Deserialize(ccap.ClaimData.Span);
            var requiredResource = _policyProvider.GetRequiredResource(context.InterfaceMethod)
                ?? GrainResourceInference.FromGrainInterface(grainInterfaceType);

            if (!ActionMatches(claim.Action, requiredAction) ||
                !ResourceMatches(claim.Resource, requiredResource))
            {
                throw new ScynapseSecurityException("Insufficient capability");
            }
        }

        // Set verified caller identity for grain code
        RequestContext.Set(ScynapseSecurityConstants.VerifiedCallerKeyKey, callerKey);
        RequestContext.Set(ScynapseSecurityConstants.VerifiedCCapKey, ccap);

        await context.Invoke();
    }

    private bool IsTrustedNode(byte[] callerKey)
    {
        return _trustedNodeKeys.Contains(callerKey);
    }

    private static bool VerifyBearerProof(SignedAssertion ccap, byte[] callerKey, byte[] proof)
    {
        // Bearer proof: the caller signs the CCap's ID with their private key.
        // We verify using the caller's public key (which must match the CCap's subject).
        if (!ccap.Subject.Span.SequenceEqual(callerKey))
            return false;

        var verifyOnly = ScynapseKeyPair.FromPublicKey(callerKey);
        return verifyOnly.Verify(ccap.Id.Span, proof);
    }

    private static bool ActionMatches(string granted, string required)
    {
        if (granted == "*") return true;
        return string.Equals(granted, required, StringComparison.Ordinal);
    }

    private static bool ResourceMatches(string granted, string required)
    {
        return SubjectNameMatcher.Matches(granted, required);
    }
}
