using NSec.Cryptography;
using Scynapse.Security.Crypto;

namespace Scynapse.Security.Assertions;

/// <summary>
/// Builder for creating and signing assertions.
/// Computes content hash (Id) and Ed25519 signature on Build().
/// </summary>
public sealed class AssertionBuilder
{
    private ScynapseKeyPair? _issuerKeyPair;
    private byte[]? _issuerPublicKey;
    private byte[]? _subjectPublicKey;
    private ClaimType _claimType;
    private byte[]? _claimData;
    private long? _notBefore;
    private long? _expiresAt;
    private byte[]? _nonce;
    private readonly List<byte[]> _proofs = new();
    private readonly Dictionary<string, byte[]> _extensions = new();

    public AssertionBuilder SetIssuer(ScynapseKeyPair issuer)
    {
        _issuerKeyPair = issuer;
        _issuerPublicKey = issuer.PublicKeyBytes.ToArray();
        return this;
    }

    public AssertionBuilder SetSubject(ReadOnlySpan<byte> subjectPublicKey)
    {
        _subjectPublicKey = subjectPublicKey.ToArray();
        return this;
    }

    public AssertionBuilder SetClaim(ClaimType type, byte[] claimData)
    {
        _claimType = type;
        _claimData = claimData;
        return this;
    }

    public AssertionBuilder SetScope(long? notBefore = null, long? expiresAt = null, byte[]? nonce = null)
    {
        _notBefore = notBefore;
        _expiresAt = expiresAt;
        _nonce = nonce;
        return this;
    }

    public AssertionBuilder AddProof(ReadOnlySpan<byte> parentAssertionId)
    {
        if (parentAssertionId.Length != 32)
            throw new ArgumentException("Assertion ID must be 32 bytes.", nameof(parentAssertionId));
        _proofs.Add(parentAssertionId.ToArray());
        return this;
    }

    public AssertionBuilder AddExtension(string key, ReadOnlySpan<byte> value)
    {
        _extensions[key] = value.ToArray();
        return this;
    }

    /// <summary>
    /// Build and sign the assertion. Computes content hash and Ed25519 signature.
    /// </summary>
    public SignedAssertion Build()
    {
        if (_issuerKeyPair == null || _issuerPublicKey == null)
            throw new InvalidOperationException("Issuer must be set.");
        if (_subjectPublicKey == null)
            throw new InvalidOperationException("Subject must be set.");
        if (_claimData == null)
            throw new InvalidOperationException("Claim must be set.");
        if (!_issuerKeyPair.CanSign)
            throw new InvalidOperationException("Issuer keypair must have signing capability.");

        // Build proofs as ReadOnlyMemory
        var proofs = _proofs.Select(p => (ReadOnlyMemory<byte>)p).ToArray();

        // Build extensions as ReadOnlyMemory
        var extensions = _extensions.ToDictionary(
            kv => kv.Key,
            kv => (ReadOnlyMemory<byte>)kv.Value);

        // Create a temporary assertion (without Id and Signature) for content serialization
        var tempAssertion = new SignedAssertion(
            version: SignedAssertion.CurrentVersion,
            id: new byte[32],         // placeholder
            issuer: _issuerPublicKey,
            subject: _subjectPublicKey,
            claimType: _claimType,
            claimData: _claimData,
            notBefore: _notBefore,
            expiresAt: _expiresAt,
            nonce: _nonce != null ? new ReadOnlyMemory<byte>(_nonce) : null,
            proofs: proofs,
            extensions: extensions,
            signature: new byte[64]    // placeholder
        );

        // Compute content hash → Id
        var contentBytes = AssertionSerializer.SerializeContentFields(tempAssertion);
        var id = SignedAssertion.ComputeContentHash(contentBytes);

        // Sign: content fields || Id
        var signableBytes = AssertionSerializer.BuildSignableBytes(contentBytes, id);
        var signature = _issuerKeyPair.Sign(signableBytes);

        return new SignedAssertion(
            version: SignedAssertion.CurrentVersion,
            id: id,
            issuer: _issuerPublicKey,
            subject: _subjectPublicKey,
            claimType: _claimType,
            claimData: _claimData,
            notBefore: _notBefore,
            expiresAt: _expiresAt,
            nonce: _nonce != null ? new ReadOnlyMemory<byte>(_nonce) : null,
            proofs: proofs,
            extensions: extensions,
            signature: signature
        );
    }

    // --- Convenience factory methods ---

    /// <summary>
    /// Create a self-signed Identity assertion (issuer == subject).
    /// </summary>
    public static SignedAssertion CreateIdentity(ScynapseKeyPair keypair, long? expiresAt = null)
    {
        var pubKey = keypair.PublicKeyBytes.ToArray();
        return new AssertionBuilder()
            .SetIssuer(keypair)
            .SetSubject(pubKey)
            .SetClaim(ClaimType.Identity, Array.Empty<byte>())
            .SetScope(expiresAt: expiresAt)
            .Build();
    }

    /// <summary>
    /// Create a Capability assertion granting subject permission to act on a resource.
    /// </summary>
    public static SignedAssertion CreateCapability(
        ScynapseKeyPair issuer,
        ReadOnlySpan<byte> subject,
        string resource,
        string action,
        IEnumerable<byte[]>? proofs = null,
        long? expiresAt = null)
    {
        var claim = new CapabilityClaim(resource, action);
        var builder = new AssertionBuilder()
            .SetIssuer(issuer)
            .SetSubject(subject)
            .SetClaim(ClaimType.Capability, claim.Serialize())
            .SetScope(expiresAt: expiresAt);

        if (proofs != null)
            foreach (var proof in proofs)
                builder.AddProof(proof);

        return builder.Build();
    }

    /// <summary>
    /// Create a Delegation assertion authorizing subject to issue assertions within scope.
    /// </summary>
    public static SignedAssertion CreateDelegation(
        ScynapseKeyPair issuer,
        ReadOnlySpan<byte> subject,
        ClaimType[] allowedClaimTypes,
        IEnumerable<byte[]>? proofs = null,
        long? expiresAt = null,
        string? resourcePattern = null,
        string? actionPattern = null,
        byte? maxDepth = null)
    {
        var claim = new DelegationClaim(allowedClaimTypes, resourcePattern, actionPattern, maxDepth);
        var builder = new AssertionBuilder()
            .SetIssuer(issuer)
            .SetSubject(subject)
            .SetClaim(ClaimType.Delegation, claim.Serialize())
            .SetScope(expiresAt: expiresAt);

        if (proofs != null)
            foreach (var proof in proofs)
                builder.AddProof(proof);

        return builder.Build();
    }

    /// <summary>
    /// Create a Relation assertion establishing a directed relationship.
    /// </summary>
    public static SignedAssertion CreateRelation(
        ScynapseKeyPair issuer,
        ReadOnlySpan<byte> subject,
        string context,
        IReadOnlyDictionary<string, byte[]>? metadata = null,
        long? expiresAt = null)
    {
        var claim = new RelationClaim(context, metadata);
        return new AssertionBuilder()
            .SetIssuer(issuer)
            .SetSubject(subject)
            .SetClaim(ClaimType.Relation, claim.Serialize())
            .SetScope(expiresAt: expiresAt)
            .Build();
    }
}
