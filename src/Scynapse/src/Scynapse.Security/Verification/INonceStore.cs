namespace Scynapse.Security.Verification;

/// <summary>
/// Tracks nonces for replay prevention.
/// </summary>
public interface INonceStore
{
    /// <summary>
    /// Check if this assertion ID has already been seen (replay).
    /// </summary>
    bool HasSeen(ReadOnlyMemory<byte> assertionId);

    /// <summary>
    /// Record an assertion ID as seen. ExpiresAt is used for TTL-based cleanup.
    /// </summary>
    void Record(ReadOnlyMemory<byte> assertionId, long? expiresAt);
}
