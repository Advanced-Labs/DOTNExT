using Scynapse.Security.Assertions;
using Scynapse.Security.Configuration;
using Scynapse.Security.Crypto;
using Xunit;

namespace Scynapse.Security.Tests;

public class SecurityConfigurationLoaderTests : IDisposable
{
    private readonly string _tempDir;

    public SecurityConfigurationLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "scynapse-test-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public void SaveAndLoadSeed_Roundtrips()
    {
        var kp = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var seedPath = Path.Combine(_tempDir, "keys", "node.seed");

        SecurityConfigurationLoader.SaveSeed(seedPath, kp, ScynapseKeyType.Node);
        Assert.True(File.Exists(seedPath));

        var bytes = File.ReadAllBytes(seedPath);
        Assert.Equal(33, bytes.Length); // 1 byte type + 32 bytes seed
        Assert.Equal((byte)ScynapseKeyType.Node, bytes[0]);

        // Reconstruct and verify same public key
        var restored = ScynapseKeyPair.FromSeed(bytes.AsSpan(1).ToArray(), ScynapseKeyType.Node);
        Assert.True(kp.PublicKeyBytes.SequenceEqual(restored.PublicKeyBytes));
    }

    [Fact]
    public void SaveAndLoadPublicKey_Roundtrips()
    {
        var kp = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var pubPath = Path.Combine(_tempDir, "keys", "org.pub");

        SecurityConfigurationLoader.SavePublicKey(pubPath, kp.PublicKeyBytes.ToArray(), ScynapseKeyType.Organization);
        Assert.True(File.Exists(pubPath));

        var loaded = SecurityConfigurationLoader.LoadPublicKey(pubPath);
        Assert.True(kp.PublicKeyBytes.SequenceEqual(loaded));
    }

    [Fact]
    public void SaveAndLoadAssertion_Roundtrips()
    {
        var kp = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var identity = AssertionBuilder.CreateIdentity(kp);
        var assertionPath = Path.Combine(_tempDir, "assertions", "org-identity.assertion");

        SecurityConfigurationLoader.SaveAssertion(assertionPath, identity);
        Assert.True(File.Exists(assertionPath));

        var loaded = SecurityConfigurationLoader.LoadAssertion(assertionPath);
        Assert.True(identity.Id.Span.SequenceEqual(loaded.Id.Span));
        Assert.Equal(identity.ClaimType, loaded.ClaimType);
    }

    [Fact]
    public void Load_FullConfig_PopulatesOptions()
    {
        // Setup: create org key, node key, delegation, capability
        var org = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);

        var keysDir = Path.Combine(_tempDir, "keys");
        var assertionsDir = Path.Combine(_tempDir, "assertions");
        var peersDir = Path.Combine(_tempDir, "assertions", "peers");
        Directory.CreateDirectory(peersDir);

        // Save seed and public key
        SecurityConfigurationLoader.SaveSeed(Path.Combine(keysDir, "node.seed"), node, ScynapseKeyType.Node);
        SecurityConfigurationLoader.SavePublicKey(Path.Combine(keysDir, "org.pub"), org.PublicKeyBytes.ToArray(), ScynapseKeyType.Organization);

        // Save bootstrap assertion
        var orgIdentity = AssertionBuilder.CreateIdentity(org);
        SecurityConfigurationLoader.SaveAssertion(Path.Combine(assertionsDir, "org-identity.assertion"), orgIdentity);

        // Save peer assertion
        var peerNode = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var peerDelegation = AssertionBuilder.CreateDelegation(
            org, peerNode.PublicKeyBytes,
            new[] { ClaimType.Capability },
            proofs: new[] { orgIdentity.Id.ToArray() });
        SecurityConfigurationLoader.SaveAssertion(Path.Combine(peersDir, "peer1.assertion"), peerDelegation);

        // Build config section
        var config = new SecurityConfigurationSection
        {
            NodeSeedFile = "./keys/node.seed",
            TrustedRoots = new List<string> { "./keys/org.pub" },
            BootstrapAssertionFiles = new List<string> { "./assertions/org-identity.assertion" },
            PeerAssertionDirectory = "./assertions/peers/",
            BootstrapCapabilityFiles = new List<string>(),
            EnableTls = false,
            RequireMutualTls = false,
        };

        var options = SecurityConfigurationLoader.Load(config, _tempDir);

        // Verify node key loaded correctly
        Assert.True(node.PublicKeyBytes.SequenceEqual(options.NodeKeyPair.PublicKeyBytes));

        // Verify trusted roots
        Assert.Single(options.TrustedRoots);

        // Verify bootstrap assertions
        Assert.Single(options.BootstrapAssertions);
        Assert.True(orgIdentity.Id.Span.SequenceEqual(options.BootstrapAssertions[0].Id.Span));

        // Verify peer assertions loaded from directory
        Assert.Single(options.PeerAssertions);
        Assert.True(peerDelegation.Id.Span.SequenceEqual(options.PeerAssertions[0].Id.Span));

        // Verify TLS settings
        Assert.False(options.EnableTls);
        Assert.False(options.RequireMutualTls);
    }

    [Fact]
    public void Load_AbsolutePaths_Work()
    {
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var seedPath = Path.Combine(_tempDir, "node.seed");
        SecurityConfigurationLoader.SaveSeed(seedPath, node, ScynapseKeyType.Node);

        var config = new SecurityConfigurationSection
        {
            NodeSeedFile = seedPath, // absolute path
            TrustedRoots = new List<string>(),
            BootstrapAssertionFiles = new List<string>(),
            BootstrapCapabilityFiles = new List<string>(),
            EnableTls = false,
        };

        var options = SecurityConfigurationLoader.Load(config, _tempDir);
        Assert.True(node.PublicKeyBytes.SequenceEqual(options.NodeKeyPair.PublicKeyBytes));
    }

    [Fact]
    public void Load_EmptyPeerDirectory_LoadsEmpty()
    {
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        SecurityConfigurationLoader.SaveSeed(Path.Combine(_tempDir, "node.seed"), node, ScynapseKeyType.Node);

        var emptyPeersDir = Path.Combine(_tempDir, "peers");
        Directory.CreateDirectory(emptyPeersDir);

        var config = new SecurityConfigurationSection
        {
            NodeSeedFile = "./node.seed",
            TrustedRoots = new List<string>(),
            BootstrapAssertionFiles = new List<string>(),
            PeerAssertionDirectory = "./peers/",
            BootstrapCapabilityFiles = new List<string>(),
            EnableTls = false,
        };

        var options = SecurityConfigurationLoader.Load(config, _tempDir);
        Assert.Empty(options.PeerAssertions);
    }

    [Fact]
    public void Load_NonexistentPeerDirectory_LoadsEmpty()
    {
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        SecurityConfigurationLoader.SaveSeed(Path.Combine(_tempDir, "node.seed"), node, ScynapseKeyType.Node);

        var config = new SecurityConfigurationSection
        {
            NodeSeedFile = "./node.seed",
            TrustedRoots = new List<string>(),
            BootstrapAssertionFiles = new List<string>(),
            PeerAssertionDirectory = "./nonexistent-peers/",
            BootstrapCapabilityFiles = new List<string>(),
            EnableTls = false,
        };

        var options = SecurityConfigurationLoader.Load(config, _tempDir);
        Assert.Empty(options.PeerAssertions);
    }

    [Fact]
    public void Load_AllKeyTypes_SaveAndRestore()
    {
        foreach (var keyType in new[] { ScynapseKeyType.Organization, ScynapseKeyType.Domain, ScynapseKeyType.Node, ScynapseKeyType.Instance })
        {
            var kp = ScynapseKeyPair.Generate(keyType);
            var seedPath = Path.Combine(_tempDir, $"{keyType}.seed");
            SecurityConfigurationLoader.SaveSeed(seedPath, kp, keyType);

            var bytes = File.ReadAllBytes(seedPath);
            Assert.Equal((byte)keyType, bytes[0]);

            var restored = ScynapseKeyPair.FromSeed(bytes.AsSpan(1).ToArray(), keyType);
            Assert.True(kp.PublicKeyBytes.SequenceEqual(restored.PublicKeyBytes));
        }
    }

    [Fact]
    public void Load_DefaultBasePath_UsesCurrentDirectory()
    {
        // Verify that null basePath defaults to current directory
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var seedPath = Path.Combine(Directory.GetCurrentDirectory(), "test-node-temp.seed");
        try
        {
            SecurityConfigurationLoader.SaveSeed(seedPath, node, ScynapseKeyType.Node);

            var config = new SecurityConfigurationSection
            {
                NodeSeedFile = "./test-node-temp.seed",
                TrustedRoots = new List<string>(),
                BootstrapAssertionFiles = new List<string>(),
                BootstrapCapabilityFiles = new List<string>(),
                EnableTls = false,
            };

            var options = SecurityConfigurationLoader.Load(config);
            Assert.True(node.PublicKeyBytes.SequenceEqual(options.NodeKeyPair.PublicKeyBytes));
        }
        finally
        {
            if (File.Exists(seedPath))
                File.Delete(seedPath);
        }
    }

    [Fact]
    public void Load_MultipleTrustedRoots()
    {
        var node = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        SecurityConfigurationLoader.SaveSeed(Path.Combine(_tempDir, "node.seed"), node, ScynapseKeyType.Node);

        var org1 = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var org2 = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        SecurityConfigurationLoader.SavePublicKey(Path.Combine(_tempDir, "org1.pub"), org1.PublicKeyBytes.ToArray(), ScynapseKeyType.Organization);
        SecurityConfigurationLoader.SavePublicKey(Path.Combine(_tempDir, "org2.pub"), org2.PublicKeyBytes.ToArray(), ScynapseKeyType.Organization);

        var config = new SecurityConfigurationSection
        {
            NodeSeedFile = "./node.seed",
            TrustedRoots = new List<string> { "./org1.pub", "./org2.pub" },
            BootstrapAssertionFiles = new List<string>(),
            BootstrapCapabilityFiles = new List<string>(),
            EnableTls = false,
        };

        var options = SecurityConfigurationLoader.Load(config, _tempDir);
        Assert.Equal(2, options.TrustedRoots.Count);
    }
}
