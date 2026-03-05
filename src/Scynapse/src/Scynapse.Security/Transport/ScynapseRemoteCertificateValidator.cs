using System.Security.Cryptography.X509Certificates;
using Scynapse.Security.Assertions;
using Scynapse.Security.Verification;

namespace Scynapse.Security.Transport;

/// <summary>
/// Custom TLS remote certificate validator for Scynapse mTLS.
///
/// Ignores standard CA chain validation (all Scynapse certs are self-signed).
/// Instead, extracts the Ed25519 public key from the peer's certificate extension,
/// finds assertions in the store that the peer's key is the subject of,
/// and verifies at least one valid assertion chain back to a trusted root.
///
/// Workaround: currently extracts Ed25519 key from a custom X.509 extension because
/// SslStream can't use Ed25519 certs directly. When .NET supports Ed25519 TLS,
/// extract the key directly from the certificate's public key instead.
/// </summary>
public sealed class ScynapseRemoteCertificateValidator
{
    private readonly IAssertionStore _store;
    private readonly INonceStore _nonceStore;
    private readonly IReadOnlySet<ReadOnlyMemory<byte>> _trustedRoots;
    private readonly IAttenuationChecker _attenuationChecker;

    public ScynapseRemoteCertificateValidator(
        IAssertionStore store,
        INonceStore nonceStore,
        IReadOnlySet<ReadOnlyMemory<byte>> trustedRoots,
        IAttenuationChecker? attenuationChecker = null)
    {
        _store = store;
        _nonceStore = nonceStore;
        _trustedRoots = trustedRoots;
        _attenuationChecker = attenuationChecker ?? new DefaultAttenuationChecker();
    }

    /// <summary>
    /// Validates a peer's certificate by verifying its Ed25519 identity against the assertion store.
    /// Returns true only if the peer's Ed25519 key has a valid assertion chain to a trusted root.
    /// </summary>
    public bool Validate(X509Certificate2 peerCertificate)
    {
        var identityData = ScynapseCertificateFactory.ExtractEd25519PublicKey(peerCertificate);
        if (identityData is null)
            return false;

        var peerPublicKey = identityData.AsSpan(1); // skip key type byte

        // Find assertions where this key is the subject
        // We need to find at least one verifiable assertion chain for this peer.
        // The assertion store is queried by walking known assertions.
        var verifier = new AssertionVerifier(_store, _nonceStore, _trustedRoots, _attenuationChecker);

        // Look for assertions that have this peer as subject.
        // The store resolves by assertion ID, so we need the peer to have pre-loaded
        // their assertion chain into our store (via bootstrap or configuration).
        // We search for any assertion where subject == peerPublicKey.
        return _store.FindBySubjectAsync(peerPublicKey.ToArray())
            .AsTask().GetAwaiter().GetResult()
            is SignedAssertion assertion
            && verifier.VerifyAsync(assertion).AsTask().GetAwaiter().GetResult().IsValid;
    }
}
