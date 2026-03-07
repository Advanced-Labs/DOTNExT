using Scynapse.Security.Assertions;
using Scynapse.Security.Crypto;
using Scynapse.Security.Transport;
using Scynapse.Security.Verification;

namespace Scynapse.Security.Orleans;

/// <summary>
/// Generates auto-configured security options for development.
/// NOT FOR PRODUCTION. Creates ephemeral keys and wildcard capabilities.
/// </summary>
public static class DevelopmentModeHelper
{
    /// <summary>
    /// Create ScynapseSecurityOptions with auto-generated development keys.
    /// Generates: org key, node key, identity assertion, delegation, and wildcard CCap.
    /// TLS is disabled in development mode.
    /// </summary>
    public static ScynapseSecurityOptions CreateDevelopmentOptions()
    {
        var org = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);

        // Self-signed org identity
        var orgIdentity = AssertionBuilder.CreateIdentity(org);

        // Delegation: org → node (full scope)
        var delegation = AssertionBuilder.CreateDelegation(
            org, node.PublicKeyBytes,
            new[] { ClaimType.Capability },
            proofs: new[] { orgIdentity.Id.ToArray() },
            resourcePattern: "scynapse.>",
            actionPattern: "*");

        // Wildcard CCap for the node (so it can act on behalf of any resource)
        var wildcardCCap = AssertionBuilder.CreateCapability(
            org, node.PublicKeyBytes,
            "scynapse.>", "*",
            proofs: new[] { orgIdentity.Id.ToArray() });

        var trustedRoots = new HashSet<ReadOnlyMemory<byte>>(ByteMemoryEqualityComparer.Instance)
        {
            org.PublicKeyBytes.ToArray()
        };

        return new ScynapseSecurityOptions
        {
            NodeKeyPair = node,
            TrustedRoots = trustedRoots,
            BootstrapAssertions = new List<SignedAssertion> { orgIdentity, delegation },
            PeerAssertions = new List<SignedAssertion>(),
            BootstrapCapabilities = new List<SignedAssertion> { wildcardCCap },
            EnableTls = false,
            RequireMutualTls = false,
        };
    }
}

/// <summary>
/// Lifecycle participant that logs a warning on every startup in development mode.
/// </summary>
internal sealed class DevelopmentModeWarningParticipant : ILifecycleParticipant<Scynapse.Runtime.ISiloLifecycle>
{
    public void Participate(Scynapse.Runtime.ISiloLifecycle lifecycle)
    {
        lifecycle.Subscribe<DevelopmentModeWarningParticipant>(
            ServiceLifecycleStage.First,
            _ =>
            {
                Console.WriteLine("===========================================================");
                Console.WriteLine("  WARNING: Scynapse Security running in DEVELOPMENT MODE");
                Console.WriteLine("  Auto-generated keys — NOT for production use!");
                Console.WriteLine("  All clients will be trusted. TLS is disabled.");
                Console.WriteLine("===========================================================");
                return Task.CompletedTask;
            });
    }
}
