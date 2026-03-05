using Scynapse.Security.Assertions;
using Scynapse.Security.Crypto;
using Scynapse.Security.Verification;
using Xunit;

namespace Scynapse.Security.Tests;

public class InMemoryAssertionStoreTests
{
    [Fact]
    public async Task StoreAndResolve_RoundTrips()
    {
        var store = new InMemoryAssertionStore();
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var assertion = AssertionBuilder.CreateIdentity(root);

        await store.StoreAsync(assertion);
        var resolved = await store.ResolveAsync(assertion.Id);

        Assert.NotNull(resolved);
        Assert.True(resolved.Id.Span.SequenceEqual(assertion.Id.Span));
    }

    [Fact]
    public async Task Resolve_Unknown_ReturnsNull()
    {
        var store = new InMemoryAssertionStore();
        var result = await store.ResolveAsync(new byte[32]);
        Assert.Null(result);
    }

    [Fact]
    public async Task StoreIdempotent_SameAssertion()
    {
        var store = new InMemoryAssertionStore();
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var assertion = AssertionBuilder.CreateIdentity(root);

        await store.StoreAsync(assertion);
        await store.StoreAsync(assertion); // idempotent
        var resolved = await store.ResolveAsync(assertion.Id);
        Assert.NotNull(resolved);
    }

    [Fact]
    public async Task Revoke_MakesIsRevokedTrue()
    {
        var store = new InMemoryAssertionStore();
        var root = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var assertion = AssertionBuilder.CreateIdentity(root);
        await store.StoreAsync(assertion);

        Assert.False(await store.IsRevokedAsync(assertion.Id));
        store.Revoke(assertion.Id);
        Assert.True(await store.IsRevokedAsync(assertion.Id));
    }
}

public class InMemoryNonceStoreTests
{
    [Fact]
    public void HasSeen_BeforeRecord_ReturnsFalse()
    {
        var store = new InMemoryNonceStore();
        Assert.False(store.HasSeen(new byte[32]));
    }

    [Fact]
    public void Record_ThenHasSeen_ReturnsTrue()
    {
        var store = new InMemoryNonceStore();
        var id = new byte[32];
        Random.Shared.NextBytes(id);

        store.Record(id, null);
        Assert.True(store.HasSeen(id));
    }

    [Fact]
    public void DifferentIds_AreIndependent()
    {
        var store = new InMemoryNonceStore();
        var id1 = new byte[32];
        var id2 = new byte[32];
        Random.Shared.NextBytes(id1);
        Random.Shared.NextBytes(id2);

        store.Record(id1, null);
        Assert.True(store.HasSeen(id1));
        Assert.False(store.HasSeen(id2));
    }
}

public class ByteMemoryEqualityComparerTests
{
    [Fact]
    public void SameContent_AreEqual()
    {
        var a = new byte[] { 1, 2, 3 };
        var b = new byte[] { 1, 2, 3 };
        Assert.True(ByteMemoryEqualityComparer.Instance.Equals(a, b));
    }

    [Fact]
    public void DifferentContent_AreNotEqual()
    {
        var a = new byte[] { 1, 2, 3 };
        var b = new byte[] { 1, 2, 4 };
        Assert.False(ByteMemoryEqualityComparer.Instance.Equals(a, b));
    }

    [Fact]
    public void SameContent_SameHashCode()
    {
        var a = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var b = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        Assert.Equal(
            ByteMemoryEqualityComparer.Instance.GetHashCode(a),
            ByteMemoryEqualityComparer.Instance.GetHashCode(b));
    }

    [Fact]
    public void HashSet_WorksWithComparer()
    {
        var set = new HashSet<ReadOnlyMemory<byte>>(ByteMemoryEqualityComparer.Instance);
        var key = new byte[] { 10, 20, 30 };
        var keyCopy = new byte[] { 10, 20, 30 };

        set.Add(key);
        Assert.Contains(new ReadOnlyMemory<byte>(keyCopy), set);
    }
}
