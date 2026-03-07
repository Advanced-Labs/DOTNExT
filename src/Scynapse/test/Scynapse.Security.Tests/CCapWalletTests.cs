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
            "scynapse.app.IMyGrain", "read");

        wallet.Store(ccap);
        var found = wallet.FindCapability("scynapse.app.IMyGrain", "read");
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
            "scynapse.app.IMyGrain", "read");

        wallet.Store(ccap);
        var found = wallet.FindCapability("scynapse.app.IOtherGrain", "write");
        Assert.Null(found);
    }

    [Fact]
    public void FindCapability_WildcardAction_MatchesAny()
    {
        var (issuer, subject) = CreateTestKeys();
        var wallet = new InMemoryCCapWallet();

        var ccap = AssertionBuilder.CreateCapability(
            issuer, subject.PublicKeyBytes,
            "scynapse.app.IMyGrain", "*");

        wallet.Store(ccap);
        Assert.NotNull(wallet.FindCapability("scynapse.app.IMyGrain", "read"));
        Assert.NotNull(wallet.FindCapability("scynapse.app.IMyGrain", "write"));
        Assert.NotNull(wallet.FindCapability("scynapse.app.IMyGrain", "admin"));
    }

    [Fact]
    public void FindCapability_WildcardResource_MatchesPrefix()
    {
        var (issuer, subject) = CreateTestKeys();
        var wallet = new InMemoryCCapWallet();

        var ccap = AssertionBuilder.CreateCapability(
            issuer, subject.PublicKeyBytes,
            "scynapse.app.>", "read");

        wallet.Store(ccap);
        Assert.NotNull(wallet.FindCapability("scynapse.app.IMyGrain", "read"));
        Assert.NotNull(wallet.FindCapability("scynapse.app.IOtherGrain", "read"));
        Assert.Null(wallet.FindCapability("scynapse.app.IMyGrain", "write")); // wrong action
    }

    [Fact]
    public void FindCapability_ExpiredCCap_ReturnsNull()
    {
        var (issuer, subject) = CreateTestKeys();
        var wallet = new InMemoryCCapWallet();

        var expired = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
        var ccap = AssertionBuilder.CreateCapability(
            issuer, subject.PublicKeyBytes,
            "scynapse.app.IMyGrain", "read",
            expiresAt: expired);

        wallet.Store(ccap);
        Assert.Null(wallet.FindCapability("scynapse.app.IMyGrain", "read"));
    }

    [Fact]
    public void Store_Duplicate_IsIdempotent()
    {
        var (issuer, subject) = CreateTestKeys();
        var wallet = new InMemoryCCapWallet();

        var ccap = AssertionBuilder.CreateCapability(
            issuer, subject.PublicKeyBytes,
            "scynapse.app.IMyGrain", "read");

        wallet.Store(ccap);
        wallet.Store(ccap);
        // Should not throw or double-store
        Assert.NotNull(wallet.FindCapability("scynapse.app.IMyGrain", "read"));
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
            "scynapse.app.IExpired", "read",
            expiresAt: expired);

        var validCCap = AssertionBuilder.CreateCapability(
            issuer, subject.PublicKeyBytes,
            "scynapse.app.IValid", "read",
            expiresAt: valid);

        wallet.Store(expiredCCap);
        wallet.Store(validCCap);
        wallet.Cleanup();

        Assert.Null(wallet.FindCapability("scynapse.app.IExpired", "read"));
        Assert.NotNull(wallet.FindCapability("scynapse.app.IValid", "read"));
    }

    [Fact]
    public void FindCapability_MultipleStored_ReturnsBestMatch()
    {
        var (issuer, subject) = CreateTestKeys();
        var wallet = new InMemoryCCapWallet();

        var readCCap = AssertionBuilder.CreateCapability(
            issuer, subject.PublicKeyBytes,
            "scynapse.app.IMyGrain", "read");

        var writeCCap = AssertionBuilder.CreateCapability(
            issuer, subject.PublicKeyBytes,
            "scynapse.app.IMyGrain", "write");

        wallet.Store(readCCap);
        wallet.Store(writeCCap);

        var readResult = wallet.FindCapability("scynapse.app.IMyGrain", "read");
        var writeResult = wallet.FindCapability("scynapse.app.IMyGrain", "write");
        Assert.NotNull(readResult);
        Assert.NotNull(writeResult);
        Assert.True(readResult!.Id.Span.SequenceEqual(readCCap.Id.Span));
        Assert.True(writeResult!.Id.Span.SequenceEqual(writeCCap.Id.Span));
    }
}
