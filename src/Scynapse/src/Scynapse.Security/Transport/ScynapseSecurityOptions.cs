using Scynapse.Security.Assertions;
using Scynapse.Security.Crypto;
using Scynapse.Security.Verification;

namespace Scynapse.Security.Transport;

/// <summary>
/// Configuration for Scynapse transport security.
/// Passed to UseScynapseSecurity() on ISiloBuilder (Layer 4 integration).
/// </summary>
public sealed class ScynapseSecurityOptions
{
    /// <summary>
    /// This node's identity keypair. Used for assertion signing and TLS certificate generation.
    /// Must be set before silo startup.
    /// </summary>
    public required ScynapseKeyPair NodeKeyPair { get; init; }

    /// <summary>
    /// Trusted root public keys. Assertion chains must terminate at one of these roots.
    /// Typically organization-level keys.
    /// </summary>
    public HashSet<ReadOnlyMemory<byte>> TrustedRoots { get; init; } = new(ByteMemoryEqualityComparer.Instance);

    /// <summary>
    /// Pre-loaded assertions (e.g., this node's delegation chain from operator to node,
    /// and known peer identity assertions). Loaded into the assertion store at startup,
    /// before networking begins.
    /// </summary>
    public List<SignedAssertion> BootstrapAssertions { get; init; } = new();
}
