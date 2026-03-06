using Scynapse.Security.Assertions;
using Scynapse.Security.Crypto;
using Scynapse.Security.Verification;

namespace Scynapse.Security.Transport;

/// <summary>
/// Configuration for Scynapse transport security.
/// Passed to UseScynapseSecurity() on ISiloBuilder or IClientBuilder.
/// </summary>
public sealed class ScynapseSecurityOptions
{
    /// <summary>
    /// This node's/client's identity keypair. Used for assertion signing and TLS certificate generation.
    /// Must be set before startup.
    /// </summary>
    public required ScynapseKeyPair NodeKeyPair { get; init; }

    /// <summary>
    /// Trusted root public keys. Assertion chains must terminate at one of these roots.
    /// Typically organization-level keys.
    /// </summary>
    public HashSet<ReadOnlyMemory<byte>> TrustedRoots { get; init; } = new(ByteMemoryEqualityComparer.Instance);

    /// <summary>
    /// Pre-loaded assertions for this node's own delegation chain (e.g., from operator to node).
    /// Loaded into the assertion store at startup, before networking begins.
    /// </summary>
    public List<SignedAssertion> BootstrapAssertions { get; init; } = new();

    /// <summary>
    /// Pre-loaded assertions for known peers (other silos, clients).
    /// These are the delegation chains that allow this node to verify remote peers
    /// during mTLS handshakes. Without these, the remote certificate validator
    /// cannot walk the assertion chain for connecting peers.
    /// </summary>
    public List<SignedAssertion> PeerAssertions { get; init; } = new();

    /// <summary>
    /// Pre-loaded CCaps for the outgoing call filter's wallet.
    /// These are capabilities this node/client has been granted.
    /// </summary>
    public List<SignedAssertion> BootstrapCapabilities { get; init; } = new();

    /// <summary>
    /// Whether to require mTLS for silo-to-silo connections (default: true).
    /// When false, only server-side TLS is enforced (peers are not required to present certificates).
    /// </summary>
    public bool RequireMutualTls { get; init; } = true;

    /// <summary>
    /// Whether to enable TLS transport encryption (default: true).
    /// Set to false for TestCluster environments where TLS is not needed or
    /// when TLS is managed separately. Call filter security still applies.
    /// </summary>
    public bool EnableTls { get; init; } = true;
}
