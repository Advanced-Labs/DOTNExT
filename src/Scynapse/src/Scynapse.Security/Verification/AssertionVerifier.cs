using Scynapse.Security.Assertions;

namespace Scynapse.Security.Verification;

/// <summary>
/// The universal verification engine. Verifies any SignedAssertion
/// by checking signature, temporal scope, replay, and walking the proof chain.
/// </summary>
public sealed class AssertionVerifier
{
    private readonly IAssertionStore _store;
    private readonly INonceStore _nonceStore;
    private readonly HashSet<ReadOnlyMemory<byte>> _trustedRoots;
    private readonly IAttenuationChecker _attenuationChecker;
    private readonly int _maxDepth;

    public AssertionVerifier(
        IAssertionStore store,
        INonceStore nonceStore,
        IReadOnlySet<ReadOnlyMemory<byte>> trustedRoots,
        IAttenuationChecker attenuationChecker,
        int maxDepth = 32)
    {
        _store = store;
        _nonceStore = nonceStore;
        // Copy to our own set with structural equality
        _trustedRoots = new HashSet<ReadOnlyMemory<byte>>(trustedRoots, ByteMemoryEqualityComparer.Instance);
        _attenuationChecker = attenuationChecker;
        _maxDepth = maxDepth;
    }

    /// <summary>
    /// Full verification: signature + scope + replay + chain walk + attenuation.
    /// </summary>
    public ValueTask<VerificationResult> VerifyAsync(SignedAssertion assertion)
        => VerifyAsync(assertion, depth: 0);

    /// <summary>
    /// Quick verification: signature + temporal scope only (no chain walk, no replay check).
    /// For performance-sensitive paths where chain was previously verified.
    /// </summary>
    public VerificationResult VerifyLocal(SignedAssertion assertion)
    {
        // Step 1: Signature (includes content hash check)
        if (!assertion.VerifySignature())
            return VerificationResult.Invalid("bad signature", assertion);

        // Step 2: Temporal scope
        var timeResult = CheckTemporalScope(assertion);
        if (!timeResult.IsValid)
            return timeResult;

        return VerificationResult.Valid();
    }

    private async ValueTask<VerificationResult> VerifyAsync(SignedAssertion assertion, int depth)
    {
        if (depth > _maxDepth)
            return VerificationResult.Invalid($"chain depth exceeds maximum ({_maxDepth})", assertion);

        // Step 1: Signature (includes content hash check)
        if (!assertion.VerifySignature())
            return VerificationResult.Invalid("bad signature", assertion);

        // Step 2: Temporal scope
        var timeResult = CheckTemporalScope(assertion);
        if (!timeResult.IsValid)
            return timeResult;

        // Step 3: Revocation check
        if (await _store.IsRevokedAsync(assertion.Id))
            return VerificationResult.Invalid("revoked", assertion);

        // Step 4: Replay prevention (nonce)
        if (assertion.Nonce.HasValue)
        {
            if (_nonceStore.HasSeen(assertion.Id))
                return VerificationResult.Invalid("replay", assertion);
            _nonceStore.Record(assertion.Id, assertion.ExpiresAt);
        }

        // Step 5: Chain verification
        if (assertion.Proofs.Count == 0)
        {
            // No proofs: must be a self-signed identity from a trusted root
            if (assertion.ClaimType == ClaimType.Identity
                && assertion.Issuer.Span.SequenceEqual(assertion.Subject.Span)
                && _trustedRoots.Contains(assertion.Issuer))
            {
                return VerificationResult.Valid();
            }

            return VerificationResult.Invalid("non-root assertion with no proofs", assertion);
        }

        // Walk the proof chain
        foreach (var proofId in assertion.Proofs)
        {
            var parent = await _store.ResolveAsync(proofId);
            if (parent == null)
                return VerificationResult.Invalid("unresolvable proof", assertion);

            // Recursively verify the parent
            var parentResult = await VerifyAsync(parent, depth + 1);
            if (!parentResult.IsValid)
                return parentResult;

            // Chain continuity: parent.subject must equal child.issuer
            if (!parent.Subject.Span.SequenceEqual(assertion.Issuer.Span))
                return VerificationResult.Invalid("chain break: parent.subject != child.issuer", assertion);

            // Attenuation: child's claims must be within parent's scope
            if (!_attenuationChecker.Check(parent, assertion))
                return VerificationResult.Invalid("insufficient authority (attenuation violation)", assertion);
        }

        return VerificationResult.Valid();
    }

    private static VerificationResult CheckTemporalScope(SignedAssertion assertion)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        if (assertion.NotBefore.HasValue && now < assertion.NotBefore.Value)
            return VerificationResult.Invalid("not yet valid", assertion);

        if (assertion.ExpiresAt.HasValue && now > assertion.ExpiresAt.Value)
            return VerificationResult.Invalid("expired", assertion);

        return VerificationResult.Valid();
    }
}
