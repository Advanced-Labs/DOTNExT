namespace Scynapse.Security.Crypto;

/// <summary>
/// Key type prefixes for Scynapse entities.
/// Each type maps to a single-byte prefix used in encoded key strings.
/// Inspired by NATS NKeys but with Scynapse-specific entity types.
/// </summary>
public enum ScynapseKeyType : byte
{
    /// <summary>Root of an organizational trust domain.</summary>
    Organization = 0,   // 'O' prefix

    /// <summary>Sub-division of an organization.</summary>
    Domain = 1,         // 'D' prefix

    /// <summary>A running Scynapse runtime instance (Node).</summary>
    Node = 2,           // 'N' prefix

    /// <summary>A Component's type-level identity.</summary>
    ComponentType = 3,  // 'T' prefix

    /// <summary>A specific activation of a Component.</summary>
    Instance = 4,       // 'I' prefix

    /// <summary>A human or human-equivalent external identity.</summary>
    User = 5,           // 'U' prefix

    /// <summary>X25519 key for encryption (derived from Ed25519 identity).</summary>
    Encryption = 6,     // 'X' prefix

    /// <summary>Private seed — never transmitted, only stored locally.</summary>
    Seed = 7,           // 'P' prefix
}
