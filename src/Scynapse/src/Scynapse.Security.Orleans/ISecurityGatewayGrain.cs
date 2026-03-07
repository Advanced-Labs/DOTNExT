namespace Scynapse.Security.Orleans;

/// <summary>
/// Client authentication entry point. Clients present their identity delegation chain;
/// the gateway verifies and issues CCaps (Communicated Capabilities).
///
/// This grain requires authentication (caller must have a verified identity chain via delegation)
/// but does NOT require a CCap — breaking the bootstrap paradox where you need a CCap to get a CCap.
///
/// Application developers can override the default implementation with custom policy logic
/// (role-based, database-backed, etc.) by registering their own implementation.
/// </summary>
[SecurityPolicy(RequiresAuthentication = true)]
public interface ISecurityGatewayGrain : IGrainWithStringKey
{
    /// <summary>
    /// Authenticate a client and return initial CCaps based on their identity and delegation chain.
    /// The caller must already have a verified identity (delegation from a trusted root).
    /// </summary>
    /// <param name="delegationChainCbor">CBOR-serialized delegation chain assertions.</param>
    /// <returns>A bundle of CCaps the client is authorized to use.</returns>
    Task<CCapBundle> AuthenticateAsync(byte[] delegationChainCbor);

    /// <summary>
    /// Request a specific capability for a resource/action pair.
    /// Returns null if the caller's identity doesn't authorize the requested capability.
    /// Returns CBOR-serialized SignedAssertion bytes.
    /// </summary>
    Task<byte[]?> RequestCapabilityAsync(string resource, string action);

    /// <summary>
    /// Refresh expiring CCaps. Returns new CCaps with extended validity.
    /// </summary>
    /// <param name="expiringCCapsCbor">CBOR-serialized CCaps that are about to expire.</param>
    Task<CCapBundle> RefreshAsync(byte[] expiringCCapsCbor);
}

/// <summary>
/// Bundle of CCaps returned by the security gateway after authentication.
/// </summary>
[GenerateSerializer]
public sealed class CCapBundle
{
    /// <summary>
    /// Serialized CCap assertions (each is a CBOR-encoded SignedAssertion).
    /// </summary>
    [Id(0)]
    public List<byte[]> Capabilities { get; set; } = new();

    /// <summary>
    /// When the earliest CCap in this bundle expires (Unix seconds), or null if none expire.
    /// Clients should call RefreshAsync before this time.
    /// </summary>
    [Id(1)]
    public long? EarliestExpiry { get; set; }
}
