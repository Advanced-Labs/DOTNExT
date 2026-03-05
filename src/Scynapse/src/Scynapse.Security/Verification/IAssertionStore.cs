using Scynapse.Security.Assertions;

namespace Scynapse.Security.Verification;

/// <summary>
/// Resolves assertions by their content-addressed ID.
/// Multiple implementations: in-memory, persistent, distributed (via CNS).
/// </summary>
public interface IAssertionStore
{
    /// <summary>
    /// Resolve an assertion by its content hash ID.
    /// Returns null if not found.
    /// </summary>
    ValueTask<SignedAssertion?> ResolveAsync(ReadOnlyMemory<byte> assertionId);

    /// <summary>
    /// Store an assertion. Idempotent (content-addressed).
    /// </summary>
    ValueTask StoreAsync(SignedAssertion assertion);

    /// <summary>
    /// Check if a specific assertion has been revoked.
    /// </summary>
    ValueTask<bool> IsRevokedAsync(ReadOnlyMemory<byte> assertionId);
}
