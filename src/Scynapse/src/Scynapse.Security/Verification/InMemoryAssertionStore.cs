using Scynapse.Security.Assertions;

namespace Scynapse.Security.Verification;

/// <summary>
/// In-memory assertion store keyed by content-addressed ID.
/// Thread-safe. Suitable for testing and single-node scenarios.
/// </summary>
public sealed class InMemoryAssertionStore : IAssertionStore
{
    private readonly Dictionary<ReadOnlyMemory<byte>, SignedAssertion> _assertions = new(ByteMemoryEqualityComparer.Instance);
    private readonly HashSet<ReadOnlyMemory<byte>> _revoked = new(ByteMemoryEqualityComparer.Instance);
    private readonly object _lock = new();

    public ValueTask<SignedAssertion?> ResolveAsync(ReadOnlyMemory<byte> assertionId)
    {
        lock (_lock)
        {
            _assertions.TryGetValue(assertionId, out var assertion);
            return new ValueTask<SignedAssertion?>(assertion);
        }
    }

    public ValueTask StoreAsync(SignedAssertion assertion)
    {
        lock (_lock)
        {
            // Content-addressed: if it already exists, it's the same assertion
            _assertions[assertion.Id] = assertion;
        }
        return default;
    }

    public ValueTask<bool> IsRevokedAsync(ReadOnlyMemory<byte> assertionId)
    {
        lock (_lock)
        {
            return new ValueTask<bool>(_revoked.Contains(assertionId));
        }
    }

    /// <summary>
    /// Mark an assertion as revoked.
    /// </summary>
    public void Revoke(ReadOnlyMemory<byte> assertionId)
    {
        lock (_lock)
        {
            _revoked.Add(assertionId);
        }
    }
}
