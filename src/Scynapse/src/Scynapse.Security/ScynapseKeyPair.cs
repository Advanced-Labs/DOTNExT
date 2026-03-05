using System.Security.Cryptography;
using NSec.Cryptography;

namespace Scynapse.Security.Crypto;

/// <summary>
/// An Ed25519 keypair. The fundamental identity unit in Scynapse.
/// Wraps NSec.Cryptography — we don't leak the underlying library's API.
/// </summary>
public sealed class ScynapseKeyPair : IDisposable
{
    private readonly Key? _signingKey;     // null if verify-only (created from public key alone)
    private readonly PublicKey _publicKey;
    private readonly ScynapseKeyType _keyType;
    private bool _disposed;

    private static readonly SignatureAlgorithm Algorithm = SignatureAlgorithm.Ed25519;

    private ScynapseKeyPair(Key signingKey, ScynapseKeyType keyType)
    {
        _signingKey = signingKey;
        _publicKey = signingKey.PublicKey;
        _keyType = keyType;
    }

    private ScynapseKeyPair(PublicKey publicKey, ScynapseKeyType keyType)
    {
        _signingKey = null;
        _publicKey = publicKey;
        _keyType = keyType;
    }

    /// <summary>
    /// The Ed25519 public key (32 bytes).
    /// </summary>
    public ReadOnlySpan<byte> PublicKeyBytes
    {
        get
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _publicKey.Export(KeyBlobFormat.RawPublicKey);
        }
    }

    /// <summary>
    /// The key type this keypair represents (Node, User, etc.).
    /// </summary>
    public ScynapseKeyType KeyType => _keyType;

    /// <summary>
    /// Whether this keypair can sign (has private key material).
    /// False for verify-only keypairs created from a public key.
    /// </summary>
    public bool CanSign => _signingKey != null;

    /// <summary>
    /// Sign data with the private key.
    /// </summary>
    /// <exception cref="InvalidOperationException">This is a verify-only keypair.</exception>
    public byte[] Sign(ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_signingKey == null)
            throw new InvalidOperationException("Cannot sign with a verify-only keypair (no private key).");

        return Algorithm.Sign(_signingKey, data);
    }

    /// <summary>
    /// Verify a signature against this keypair's public key.
    /// </summary>
    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Algorithm.Verify(_publicKey, data, signature);
    }

    /// <summary>
    /// Generate a new random Ed25519 keypair.
    /// </summary>
    public static ScynapseKeyPair Generate(ScynapseKeyType keyType = ScynapseKeyType.Node)
    {
        var creationParams = new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport };
        var key = Key.Create(Algorithm, creationParams);
        return new ScynapseKeyPair(key, keyType);
    }

    /// <summary>
    /// Create a keypair from a 32-byte seed (deterministic).
    /// </summary>
    public static ScynapseKeyPair FromSeed(ReadOnlySpan<byte> seed, ScynapseKeyType keyType = ScynapseKeyType.Node)
    {
        if (seed.Length != 32)
            throw new ArgumentException("Ed25519 seed must be exactly 32 bytes.", nameof(seed));

        var key = Key.Import(Algorithm, seed, KeyBlobFormat.RawPrivateKey,
            new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
        return new ScynapseKeyPair(key, keyType);
    }

    /// <summary>
    /// Create a verify-only keypair from a 32-byte public key.
    /// </summary>
    public static ScynapseKeyPair FromPublicKey(ReadOnlySpan<byte> publicKey, ScynapseKeyType keyType = ScynapseKeyType.Node)
    {
        if (publicKey.Length != 32)
            throw new ArgumentException("Ed25519 public key must be exactly 32 bytes.", nameof(publicKey));

        var pk = NSec.Cryptography.PublicKey.Import(Algorithm, publicKey, KeyBlobFormat.RawPublicKey);
        return new ScynapseKeyPair(pk, keyType);
    }

    /// <summary>
    /// Export the 32-byte seed (private key material).
    /// </summary>
    /// <exception cref="InvalidOperationException">This is a verify-only keypair.</exception>
    public byte[] ExportSeed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_signingKey == null)
            throw new InvalidOperationException("Cannot export seed from a verify-only keypair.");

        return _signingKey.Export(KeyBlobFormat.RawPrivateKey);
    }

    /// <summary>
    /// Encode the public key as a typed, human-readable string.
    /// </summary>
    public string ToEncodedPublicKey()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return ScynapseKeyEncoding.EncodePublicKey(_keyType, PublicKeyBytes);
    }

    /// <summary>
    /// Encode the seed as a typed, human-readable string.
    /// </summary>
    /// <exception cref="InvalidOperationException">This is a verify-only keypair.</exception>
    public string ToEncodedSeed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_signingKey == null)
            throw new InvalidOperationException("Cannot encode seed from a verify-only keypair.");

        var seed = _signingKey.Export(KeyBlobFormat.RawPrivateKey);
        try
        {
            return ScynapseKeyEncoding.EncodeSeed(_keyType, seed);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(seed);
        }
    }

    /// <summary>
    /// Restore a keypair from an encoded seed string.
    /// </summary>
    public static ScynapseKeyPair FromEncodedSeed(string encoded)
    {
        var (keyType, seed) = ScynapseKeyEncoding.DecodeSeed(encoded);
        try
        {
            return FromSeed(seed, keyType);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(seed);
        }
    }

    /// <summary>
    /// Create a verify-only keypair from an encoded public key string.
    /// </summary>
    public static ScynapseKeyPair FromEncodedPublicKey(string encoded)
    {
        var (keyType, publicKey) = ScynapseKeyEncoding.DecodePublicKey(encoded);
        return FromPublicKey(publicKey, keyType);
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _signingKey?.Dispose();
            _disposed = true;
        }
    }
}
