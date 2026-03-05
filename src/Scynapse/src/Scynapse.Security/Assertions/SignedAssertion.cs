using NSec.Cryptography;
using Scynapse.Security.Crypto;

namespace Scynapse.Security.Assertions;

/// <summary>
/// The universal primitive. Identity, capability, relation, delegation,
/// impersonation, and revocation are all SignedAssertions with different claim types.
///
/// Immutable after creation. Content-addressed by Blake2b-256 hash.
/// </summary>
public sealed class SignedAssertion
{
    public const byte CurrentVersion = 1;

    public byte Version { get; }
    public ReadOnlyMemory<byte> Id { get; }           // Blake2b-256 of content fields (32 bytes)
    public ReadOnlyMemory<byte> Issuer { get; }       // Ed25519 public key (32 bytes)
    public ReadOnlyMemory<byte> Subject { get; }      // Ed25519 public key (32 bytes)

    public ClaimType ClaimType { get; }
    public ReadOnlyMemory<byte> ClaimData { get; }    // Serialized claim payload

    public long? NotBefore { get; }                   // Unix timestamp (seconds)
    public long? ExpiresAt { get; }
    public ReadOnlyMemory<byte>? Nonce { get; }

    public IReadOnlyList<ReadOnlyMemory<byte>> Proofs { get; }  // Parent assertion IDs

    public IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Extensions { get; }

    public ReadOnlyMemory<byte> Signature { get; }    // Ed25519 signature (64 bytes)

    internal SignedAssertion(
        byte version,
        ReadOnlyMemory<byte> id,
        ReadOnlyMemory<byte> issuer,
        ReadOnlyMemory<byte> subject,
        ClaimType claimType,
        ReadOnlyMemory<byte> claimData,
        long? notBefore,
        long? expiresAt,
        ReadOnlyMemory<byte>? nonce,
        IReadOnlyList<ReadOnlyMemory<byte>> proofs,
        IReadOnlyDictionary<string, ReadOnlyMemory<byte>> extensions,
        ReadOnlyMemory<byte> signature)
    {
        Version = version;
        Id = id;
        Issuer = issuer;
        Subject = subject;
        ClaimType = claimType;
        ClaimData = claimData;
        NotBefore = notBefore;
        ExpiresAt = expiresAt;
        Nonce = nonce;
        Proofs = proofs;
        Extensions = extensions;
        Signature = signature;
    }

    /// <summary>
    /// Verify ONLY the Ed25519 signature. Fast, local check.
    /// Does NOT verify chain, temporal scope, or revocation (that's Layer 2).
    /// Also verifies that the content hash (Id) matches the content fields.
    /// </summary>
    public bool VerifySignature()
    {
        // Recompute the content hash and check it matches the stored Id
        var contentBytes = AssertionSerializer.SerializeContentFields(this);
        var expectedId = ComputeContentHash(contentBytes);
        if (!expectedId.AsSpan().SequenceEqual(Id.Span))
            return false;

        // Verify Ed25519 signature over content + Id
        var signableBytes = AssertionSerializer.BuildSignableBytes(contentBytes, Id);
        var publicKey = NSec.Cryptography.PublicKey.Import(
            SignatureAlgorithm.Ed25519,
            Issuer.Span,
            KeyBlobFormat.RawPublicKey);

        return SignatureAlgorithm.Ed25519.Verify(publicKey, signableBytes, Signature.Span);
    }

    /// <summary>
    /// Serialize to binary wire format.
    /// </summary>
    public byte[] Serialize() => AssertionSerializer.Serialize(this);

    /// <summary>
    /// Deserialize from binary wire format.
    /// </summary>
    public static SignedAssertion Deserialize(ReadOnlySpan<byte> data)
        => AssertionSerializer.Deserialize(data);

    private static readonly NSec.Cryptography.Blake2b Blake2b256 = new(32);

    /// <summary>
    /// Compute Blake2b-256 hash of content fields (32 bytes).
    /// </summary>
    internal static byte[] ComputeContentHash(byte[] contentBytes)
        => Blake2b256.Hash(contentBytes);
}
