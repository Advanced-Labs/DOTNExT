namespace Scynapse.Security.Assertions;

/// <summary>
/// The type of claim carried by a SignedAssertion.
/// Extensible: 0xFF reserved for custom extension types identified by URI.
/// </summary>
public enum ClaimType : byte
{
    /// <summary>Self-signed: "I exist as this key." Issuer == Subject.</summary>
    Identity = 0x01,

    /// <summary>"Subject may perform action on resource."</summary>
    Capability = 0x02,

    /// <summary>"Subject may issue assertions within scope."</summary>
    Delegation = 0x03,

    /// <summary>"Issuer recognizes subject in context." Directed relationship.</summary>
    Relation = 0x04,

    /// <summary>"Target assertion is revoked."</summary>
    Revocation = 0x05,

    /// <summary>"Subject may act as issuer within scope."</summary>
    Impersonation = 0x06,

    /// <summary>Custom claim type, identified by URI in claim data.</summary>
    Extension = 0xFF,
}
