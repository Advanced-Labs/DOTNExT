using System.Security.Cryptography;
using System.Text;
using Scynapse.Security.Assertions;
using Scynapse.Security.Crypto;
using Scynapse.Security.Verification;

namespace FabricS1Prototype;

internal sealed class SecurityAdapterSession
{
    private readonly ScynapseKeyPair _rootIssuer;
    private readonly ScynapseKeyPair _proofSubject;
    private readonly SignedAssertion _rootIdentity;
    private readonly InMemoryAssertionStore _store;
    private readonly AssertionVerifier _verifier;

    public SecurityAdapterSession()
    {
        _rootIssuer = ScynapseKeyPair.FromSeed(DeriveSeed("fabric.m1s3.root"), ScynapseKeyType.Node);
        _proofSubject = ScynapseKeyPair.FromSeed(DeriveSeed("fabric.m1s3.subject"), ScynapseKeyType.Node);

        _rootIdentity = AssertionBuilder.CreateIdentity(_rootIssuer);
        _store = new InMemoryAssertionStore();
        _store.StoreAsync(_rootIdentity).GetAwaiter().GetResult();

        var nonceStore = new InMemoryNonceStore();
        var trustedRoots = new HashSet<ReadOnlyMemory<byte>>(ByteMemoryEqualityComparer.Instance)
        {
            _rootIdentity.Subject
        };

        _verifier = new AssertionVerifier(_store, nonceStore, trustedRoots, new DefaultAttenuationChecker());
    }

    public SecurityAdapterOutcome VerifyStrictProof(string proofRef, bool forceBadSignature, bool replayProbe, StrictFailureMode failureMode)
    {
        if (string.IsNullOrWhiteSpace(proofRef))
        {
            return SecurityAdapterOutcome.Invalid(
                "E3070_M1S3_STRICT_PROOF_REF_REQUIRED",
                "strict verification requires non-empty proof_ref.");
        }

        var assertion = BuildProofAssertion(proofRef, failureMode);
        _store.StoreAsync(assertion).GetAwaiter().GetResult();

        if (failureMode == StrictFailureMode.Revoked)
        {
            _store.Revoke(assertion.Id);
        }

        if (forceBadSignature)
        {
            assertion = CorruptSignature(assertion);
        }

        if (replayProbe)
        {
            var first = _verifier.VerifyAsync(assertion).GetAwaiter().GetResult();
            if (!first.IsValid)
            {
                return MapFailure(first.FailureReason);
            }

            var second = _verifier.VerifyAsync(assertion).GetAwaiter().GetResult();
            return second.IsValid ? SecurityAdapterOutcome.Valid() : MapFailure(second.FailureReason);
        }

        var result = _verifier.VerifyAsync(assertion).GetAwaiter().GetResult();
        return result.IsValid ? SecurityAdapterOutcome.Valid() : MapFailure(result.FailureReason);
    }

    private SignedAssertion BuildProofAssertion(string proofRef, StrictFailureMode failureMode)
    {
        var nonce = Encoding.UTF8.GetBytes($"m1s3:{proofRef}");
        var capability = new CapabilityClaim($"scynapse://m1-s3/{proofRef}", "handshake.proof");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        long? notBefore = null;
        long? expiresAt = null;

        if (failureMode == StrictFailureMode.Expired)
        {
            notBefore = now - 120;
            expiresAt = now - 60;
        }
        else if (failureMode == StrictFailureMode.NotYetValid)
        {
            notBefore = now + 300;
            expiresAt = now + 900;
        }

        var builder = new AssertionBuilder()
            .SetIssuer(_rootIssuer)
            .SetSubject(_proofSubject.PublicKeyBytes)
            .SetClaim(ClaimType.Capability, capability.Serialize())
            .SetScope(notBefore: notBefore, expiresAt: expiresAt, nonce: nonce);

        if (failureMode == StrictFailureMode.UnresolvableProof)
        {
            builder.AddProof(DeriveSeed($"m1s3.unresolvable.{proofRef}"));
        }
        else
        {
            builder.AddProof(_rootIdentity.Id.Span);
        }

        return builder.Build();
    }

    private static SignedAssertion CorruptSignature(SignedAssertion assertion)
    {
        var bytes = assertion.Serialize();
        if (bytes.Length > 0)
        {
            bytes[^1] ^= 0x01;
        }

        return SignedAssertion.Deserialize(bytes);
    }

    private static SecurityAdapterOutcome MapFailure(string? reason)
    {
        if (reason is null)
        {
            return SecurityAdapterOutcome.Invalid("E3073_M1S3_STRICT_VERIFICATION_FAILED", "strict verification failed with unknown reason.");
        }

        if (reason.IndexOf("bad signature", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return SecurityAdapterOutcome.Invalid("E3071_M1S3_PROOF_INVALID_SIGNATURE", $"strict verification failed: {reason}.");
        }

        if (reason.IndexOf("replay", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return SecurityAdapterOutcome.Invalid("E3072_M1S3_NONCE_REPLAY_DETECTED", $"strict verification failed: {reason}.");
        }

        if (reason.IndexOf("expired", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return SecurityAdapterOutcome.Invalid("E3081_M1S4_PROOF_EXPIRED", $"strict verification failed: {reason}.");
        }

        if (reason.IndexOf("revoked", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return SecurityAdapterOutcome.Invalid("E3082_M1S4_PROOF_REVOKED", $"strict verification failed: {reason}.");
        }

        if (reason.IndexOf("unresolvable proof", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return SecurityAdapterOutcome.Invalid("E3083_M1S4_PROOF_CHAIN_UNRESOLVABLE", $"strict verification failed: {reason}.");
        }

        if (reason.IndexOf("not yet valid", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            return SecurityAdapterOutcome.Invalid("E3084_M1S4_PROOF_NOT_YET_VALID", $"strict verification failed: {reason}.");
        }

        return SecurityAdapterOutcome.Invalid("E3073_M1S3_STRICT_VERIFICATION_FAILED", $"strict verification failed: {reason}.");
    }

    private static byte[] DeriveSeed(string label)
    {
        return SHA256.HashData(Encoding.UTF8.GetBytes(label));
    }
}

internal sealed record SecurityAdapterOutcome(bool IsValid, string? ErrorId = null, string? Message = null)
{
    public static SecurityAdapterOutcome Valid() => new(true);

    public static SecurityAdapterOutcome Invalid(string errorId, string message)
        => new(false, errorId, message);
}

internal enum StrictFailureMode
{
    None,
    Expired,
    Revoked,
    UnresolvableProof,
    NotYetValid
}
