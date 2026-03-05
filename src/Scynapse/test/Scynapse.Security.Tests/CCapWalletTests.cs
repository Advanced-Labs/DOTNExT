using Scynapse.Security.Assertions;
using Scynapse.Security.Crypto;
using Scynapse.Security.Verification;
using Xunit;

namespace Scynapse.Security.Tests;

public class CCapWalletTests
{
    private static (ScynapseKeyPair issuer, ScynapseKeyPair subject) CreateTestKeys()
    {
        var issuer = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var subject = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        return (issuer, subject);
    }

    [Fact]
    public void Store_And_FindCapability_Roundtrips()
    {
        var (issuer, subject) = CreateTestKeys();
        var wallet = new InMemoryCCapWallet();

        var ccap = AssertionBuilder.CreateCapability(
            issuer, subject.PublicKeyBytes,
            "scynapse:grain/IMyGrain", "read");

        wallet.Store(ccap);
        var found = wallet.FindCapability("scynapse:grain/IMyGrain", "read");
        Assert.NotNull(found);
        Assert.True(found!.Id.Span.SequenceEqual(ccap.Id.Span));
    }

    [Fact]
    public void FindCapability_NoMatch_ReturnsNull()
    {
        var (issuer, subject) = CreateTestKeys();
        var wallet = new InMemoryCCapWallet();

        var ccap = AssertionBuilder.CreateCapability(
            issuer, subject.PublicKeyBytes,
            "scynapse:grain/IMyGrain", "read");

        wallet.Store(ccap);
        var found = wallet.FindCapability("scynapse:grain/IOtherGrain", "write");
        Assert.Null(found);
    }

    [Fact]
    public void FindCapability_WildcardAction_MatchesAny()
    {
        var (issuer, subject) = CreateTestKeys();
        var wallet = new InMemoryCCapWallet();

        var ccap = AssertionBuilder.CreateCapability(
            issuer, subject.PublicKeyBytes,
            "scynapse:grain/IMyGrain", "*");

        wallet.Store(ccap);
        Assert.NotNull(wallet.FindCapability("scynapse:grain/IMyGrain", "read"));
        Assert.NotNull(wallet.FindCapability("scynapse:grain/IMyGrain", "write"));
        Assert.NotNull(wallet.FindCapability("scynapse:grain/IMyGrain", "admin"));
    }

    [Fact]
    public void FindCapability_WildcardResource_MatchesPrefix()
    {
        var (issuer, subject) = CreateTestKeys();
        var wallet = new InMemoryCCapWallet();

        var ccap = AssertionBuilder.CreateCapability(
            issuer, subject.PublicKeyBytes,
            "scynapse:grain/*", "read");

        wallet.Store(ccap);
        Assert.NotNull(wallet.FindCapability("scynapse:grain/IMyGrain", "read"));
        Assert.NotNull(wallet.FindCapability("scynapse:grain/IOtherGrain", "read"));
        Assert.Null(wallet.FindCapability("scynapse:grain/IMyGrain", "write")); // wrong action
    }

    [Fact]
    public void FindCapability_ExpiredCCap_ReturnsNull()
    {
        var (issuer, subject) = CreateTestKeys();
        var wallet = new InMemoryCCapWallet();

        var expired = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
        var ccap = AssertionBuilder.CreateCapability(
            issuer, subject.PublicKeyBytes,
            "scynapse:grain/IMyGrain", "read",
            expiresAt: expired);

        wallet.Store(ccap);
        Assert.Null(wallet.FindCapability("scynapse:grain/IMyGrain", "read"));
    }

    [Fact]
    public void Store_Duplicate_IsIdempotent()
    {
        var (issuer, subject) = CreateTestKeys();
        var wallet = new InMemoryCCapWallet();

        var ccap = AssertionBuilder.CreateCapability(
            issuer, subject.PublicKeyBytes,
            "scynapse:grain/IMyGrain", "read");

        wallet.Store(ccap);
        wallet.Store(ccap);
        // Should not throw or double-store
        Assert.NotNull(wallet.FindCapability("scynapse:grain/IMyGrain", "read"));
    }

    [Fact]
    public void Cleanup_RemovesExpired()
    {
        var (issuer, subject) = CreateTestKeys();
        var wallet = new InMemoryCCapWallet();

        var expired = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
        var valid = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();

        var expiredCCap = AssertionBuilder.CreateCapability(
            issuer, subject.PublicKeyBytes,
            "scynapse:grain/IExpired", "read",
            expiresAt: expired);

        var validCCap = AssertionBuilder.CreateCapability(
            issuer, subject.PublicKeyBytes,
            "scynapse:grain/IValid", "read",
            expiresAt: valid);

        wallet.Store(expiredCCap);
        wallet.Store(validCCap);
        wallet.Cleanup();

        Assert.Null(wallet.FindCapability("scynapse:grain/IExpired", "read"));
        Assert.NotNull(wallet.FindCapability("scynapse:grain/IValid", "read"));
    }

    [Fact]
    public void FindCapability_MultipleStored_ReturnsBestMatch()
    {
        var (issuer, subject) = CreateTestKeys();
        var wallet = new InMemoryCCapWallet();

        var readCCap = AssertionBuilder.CreateCapability(
            issuer, subject.PublicKeyBytes,
            "scynapse:grain/IMyGrain", "read");

        var writeCCap = AssertionBuilder.CreateCapability(
            issuer, subject.PublicKeyBytes,
            "scynapse:grain/IMyGrain", "write");

        wallet.Store(readCCap);
        wallet.Store(writeCCap);

        var readResult = wallet.FindCapability("scynapse:grain/IMyGrain", "read");
        var writeResult = wallet.FindCapability("scynapse:grain/IMyGrain", "write");
        Assert.NotNull(readResult);
        Assert.NotNull(writeResult);
        Assert.True(readResult!.Id.Span.SequenceEqual(readCCap.Id.Span));
        Assert.True(writeResult!.Id.Span.SequenceEqual(writeCCap.Id.Span));
    }
}
