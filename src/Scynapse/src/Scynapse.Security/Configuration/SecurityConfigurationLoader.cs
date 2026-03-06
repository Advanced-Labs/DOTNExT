using Scynapse.Security.Assertions;
using Scynapse.Security.Crypto;
using Scynapse.Security.Transport;
using Scynapse.Security.Verification;

namespace Scynapse.Security.Configuration;

/// <summary>
/// Loads security configuration from file paths specified in a configuration section.
/// Reads .seed files, .pub files, and .assertion files to populate ScynapseSecurityOptions.
///
/// Expected configuration shape:
/// {
///   "NodeSeedFile": "./keys/node.seed",
///   "TrustedRoots": ["./keys/org.pub"],
///   "BootstrapAssertionFiles": ["./assertions/org-identity.assertion"],
///   "PeerAssertionDirectory": "./assertions/peers/",
///   "BootstrapCapabilityFiles": [],
///   "EnableTls": true,
///   "RequireMutualTls": true,
///   "DevelopmentMode": false
/// }
/// </summary>
public static class SecurityConfigurationLoader
{
    /// <summary>
    /// Load a ScynapseSecurityOptions from configuration values containing file paths.
    /// </summary>
    public static ScynapseSecurityOptions Load(SecurityConfigurationSection config, string? basePath = null)
    {
        basePath ??= Directory.GetCurrentDirectory();

        // Load node key from .seed file
        var seedPath = ResolvePath(config.NodeSeedFile, basePath);
        var seedBytes = File.ReadAllBytes(seedPath);
        var keyType = seedBytes.Length > 0 ? (ScynapseKeyType)seedBytes[0] : ScynapseKeyType.Node;
        var nodeKeyPair = ScynapseKeyPair.FromSeed(seedBytes.AsSpan(1).ToArray(), keyType);

        // Load trusted root public keys from .pub files
        var trustedRoots = new HashSet<ReadOnlyMemory<byte>>(ByteMemoryEqualityComparer.Instance);
        foreach (var pubFile in config.TrustedRoots)
        {
            var pubPath = ResolvePath(pubFile, basePath);
            var pubKey = LoadPublicKey(pubPath);
            trustedRoots.Add(pubKey);
        }

        // Load bootstrap assertions from .assertion files
        var bootstrapAssertions = new List<SignedAssertion>();
        foreach (var assertionFile in config.BootstrapAssertionFiles)
        {
            var path = ResolvePath(assertionFile, basePath);
            bootstrapAssertions.Add(LoadAssertion(path));
        }

        // Load peer assertions from directory (all .assertion files)
        var peerAssertions = new List<SignedAssertion>();
        if (!string.IsNullOrEmpty(config.PeerAssertionDirectory))
        {
            var peerDir = ResolvePath(config.PeerAssertionDirectory, basePath);
            if (Directory.Exists(peerDir))
            {
                foreach (var file in Directory.GetFiles(peerDir, "*.assertion"))
                    peerAssertions.Add(LoadAssertion(file));
            }
        }

        // Load bootstrap capabilities from .ccap or .assertion files
        var capabilities = new List<SignedAssertion>();
        foreach (var capFile in config.BootstrapCapabilityFiles)
        {
            var path = ResolvePath(capFile, basePath);
            capabilities.Add(LoadAssertion(path));
        }

        return new ScynapseSecurityOptions
        {
            NodeKeyPair = nodeKeyPair,
            TrustedRoots = trustedRoots,
            BootstrapAssertions = bootstrapAssertions,
            PeerAssertions = peerAssertions,
            BootstrapCapabilities = capabilities,
            EnableTls = config.EnableTls,
            RequireMutualTls = config.RequireMutualTls,
        };
    }

    /// <summary>
    /// Load a .pub file. Single line: NATS-style encoded public key.
    /// </summary>
    public static byte[] LoadPublicKey(string path)
    {
        var text = File.ReadAllText(path).Trim();
        var (_, publicKey) = ScynapseKeyEncoding.DecodePublicKey(text);
        return publicKey;
    }

    /// <summary>
    /// Load a .assertion or .ccap file (CBOR binary).
    /// </summary>
    public static SignedAssertion LoadAssertion(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return SignedAssertion.Deserialize(bytes);
    }

    /// <summary>
    /// Save a key pair's seed to a .seed file (1 byte type prefix + 32 bytes seed).
    /// </summary>
    public static void SaveSeed(string path, ScynapseKeyPair keyPair, ScynapseKeyType type)
    {
        var seed = keyPair.ExportSeed();
        var data = new byte[1 + seed.Length];
        data[0] = (byte)type;
        seed.CopyTo(data, 1);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, data);
    }

    /// <summary>
    /// Save a public key to a .pub file (encoded text).
    /// </summary>
    public static void SavePublicKey(string path, byte[] publicKey, ScynapseKeyType type)
    {
        var encoded = ScynapseKeyEncoding.EncodePublicKey(type, publicKey);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, encoded);
    }

    /// <summary>
    /// Save a signed assertion to a .assertion or .ccap file.
    /// </summary>
    public static void SaveAssertion(string path, SignedAssertion assertion)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, assertion.Serialize());
    }

    private static string ResolvePath(string path, string basePath)
    {
        if (Path.IsPathRooted(path))
            return path;
        return Path.GetFullPath(Path.Combine(basePath, path));
    }
}

/// <summary>
/// Typed representation of the ScynapseSecurity configuration section.
/// </summary>
public sealed class SecurityConfigurationSection
{
    public string NodeSeedFile { get; set; } = "";
    public List<string> TrustedRoots { get; set; } = new();
    public List<string> BootstrapAssertionFiles { get; set; } = new();
    public string? PeerAssertionDirectory { get; set; }
    public List<string> BootstrapCapabilityFiles { get; set; } = new();
    public bool EnableTls { get; set; } = true;
    public bool RequireMutualTls { get; set; } = true;
    public bool DevelopmentMode { get; set; }
}
