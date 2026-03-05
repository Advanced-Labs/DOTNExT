namespace Scynapse.Security.Verification;

/// <summary>
/// In-memory nonce store with TTL-based expiry cleanup.
/// Thread-safe. Periodically evicts expired entries on Record().
/// </summary>
public sealed class InMemoryNonceStore : INonceStore
{
    private readonly Dictionary<ReadOnlyMemory<byte>, long> _seen = new(ByteMemoryEqualityComparer.Instance);
    private readonly object _lock = new();
    private long _lastCleanup;
    private readonly long _cleanupIntervalSeconds;

    /// <param name="cleanupIntervalSeconds">
    /// How often (in seconds between Unix timestamps) to evict expired entries. Default: 60.
    /// </param>
    public InMemoryNonceStore(long cleanupIntervalSeconds = 60)
    {
        _cleanupIntervalSeconds = cleanupIntervalSeconds;
        _lastCleanup = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }

    public bool HasSeen(ReadOnlyMemory<byte> assertionId)
    {
        lock (_lock)
        {
            return _seen.ContainsKey(assertionId);
        }
    }

    public void Record(ReadOnlyMemory<byte> assertionId, long? expiresAt)
    {
        lock (_lock)
        {
            // Store with expiry. If no expiry, use long.MaxValue (never auto-evicts).
            _seen[assertionId] = expiresAt ?? long.MaxValue;
            TryCleanup();
        }
    }

    private void TryCleanup()
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        if (now - _lastCleanup < _cleanupIntervalSeconds)
            return;

        _lastCleanup = now;
        var expired = new List<ReadOnlyMemory<byte>>();
        foreach (var (key, expiry) in _seen)
        {
            if (expiry <= now)
                expired.Add(key);
        }

        foreach (var key in expired)
            _seen.Remove(key);
    }
}
