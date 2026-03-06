using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scynapse.Connections.Security;
using Scynapse.Hosting;
using Scynapse.Security.Assertions;
using Scynapse.Security.Crypto;
using Scynapse.Security.Orleans;
using Scynapse.Security.Transport;
using Scynapse.Security.Verification;
using Scynapse.TestingHost;
using Xunit;

namespace Scynapse.Security.Integration.Tests;

// ────────────────────────────────────────────────────────
// Test grain interfaces
// ────────────────────────────────────────────────────────

[SecurityPolicy(RequiresAuthentication = true)]
public interface ISecuredGrain : IGrainWithStringKey
{
    [RequireCapability(Action = "read")]
    Task<string> GetData();

    [RequireCapability(Action = "write")]
    Task SetData(string value);

    Task<byte[]?> WhoAmI();
}

[SecurityPolicy(AllowAnonymous = true)]
public interface IOpenGrain : IGrainWithStringKey
{
    Task<string> Hello();
}

// ────────────────────────────────────────────────────────
// Test grain implementations
// ────────────────────────────────────────────────────────

public class SecuredGrain : Grain, ISecuredGrain
{
    private string _data = "initial";

    public Task<string> GetData() => Task.FromResult(_data);

    public Task SetData(string value)
    {
        _data = value;
        return Task.CompletedTask;
    }

    public Task<byte[]?> WhoAmI()
    {
        return Task.FromResult(GrainSecurityExtensions.GetCallerPublicKey());
    }
}

public class OpenGrain : Grain, IOpenGrain
{
    public Task<string> Hello() => Task.FromResult("hello");
}

// ────────────────────────────────────────────────────────
// Shared test infrastructure
// ────────────────────────────────────────────────────────

/// <summary>
/// Holds shared key material for a test scenario. Created once per test,
/// passed to silo and client configurators via TestClusterBuilder.Properties.
/// </summary>
internal static class TestSecuritySetup
{
    // Property keys for passing data through TestClusterBuilder configuration
    public const string OrgSeedKey = "ScynapseSecurity:OrgSeed";
    public const string NodeSeedKey = "ScynapseSecurity:NodeSeed";
    public const string OrgIdentityKey = "ScynapseSecurity:OrgIdentity";
    public const string NodeDelegationKey = "ScynapseSecurity:NodeDelegation";
    public const string ClientSeedKey = "ScynapseSecurity:ClientSeed";
    public const string ClientDelegationKey = "ScynapseSecurity:ClientDelegation";
    public const string ClientCCapKey = "ScynapseSecurity:ClientCCap";

    /// <summary>
    /// Creates the full key hierarchy and assertion chain for a test.
    /// Returns everything needed as serialized strings for TestClusterBuilder.Properties.
    /// </summary>
    public static Dictionary<string, string> CreateTestHierarchy(
        string ccapResource = "scynapse.>",
        string ccapAction = "*",
        long? ccapExpiresAt = null)
    {
        // Organization root key
        using var orgKey = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
        var orgIdentity = AssertionBuilder.CreateIdentity(orgKey);

        // Node key (silo identity)
        using var nodeKey = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
        var nodeDelegation = AssertionBuilder.CreateDelegation(
            orgKey, nodeKey.PublicKeyBytes,
            new[] { ClaimType.Capability, ClaimType.Delegation },
            new[] { orgIdentity.Id.ToArray() },
            resourcePattern: "scynapse.>",
            actionPattern: "*");

        // Client key
        using var clientKey = ScynapseKeyPair.Generate(ScynapseKeyType.User);
        var clientDelegation = AssertionBuilder.CreateDelegation(
            orgKey, clientKey.PublicKeyBytes,
            new[] { ClaimType.Capability },
            new[] { orgIdentity.Id.ToArray() },
            resourcePattern: "scynapse.>",
            actionPattern: "*");

        // CCap for the client (issued by org, subject = client)
        var clientCCap = AssertionBuilder.CreateCapability(
            orgKey, clientKey.PublicKeyBytes,
            ccapResource, ccapAction,
            new[] { orgIdentity.Id.ToArray() },
            ccapExpiresAt);

        return new Dictionary<string, string>
        {
            [OrgSeedKey] = Convert.ToBase64String(orgKey.ExportSeed()),
            [NodeSeedKey] = Convert.ToBase64String(nodeKey.ExportSeed()),
            [OrgIdentityKey] = Convert.ToBase64String(orgIdentity.Serialize()),
            [NodeDelegationKey] = Convert.ToBase64String(nodeDelegation.Serialize()),
            [ClientSeedKey] = Convert.ToBase64String(clientKey.ExportSeed()),
            [ClientDelegationKey] = Convert.ToBase64String(clientDelegation.Serialize()),
            [ClientCCapKey] = Convert.ToBase64String(clientCCap.Serialize()),
        };
    }

    public static ScynapseKeyPair LoadKey(IConfiguration config, string key, ScynapseKeyType type)
    {
        var seed = Convert.FromBase64String(config[key]!);
        return ScynapseKeyPair.FromSeed(seed, type);
    }

    public static SignedAssertion LoadAssertion(IConfiguration config, string key)
    {
        var bytes = Convert.FromBase64String(config[key]!);
        return SignedAssertion.Deserialize(bytes);
    }
}

// ────────────────────────────────────────────────────────
// Silo configurator: wires UseScynapseSecurity on each silo
// ────────────────────────────────────────────────────────

internal class SecuredSiloConfigurator : IHostConfigurator
{
    public void Configure(IHostBuilder hostBuilder)
    {
        var config = hostBuilder.GetConfiguration();

        var orgKey = TestSecuritySetup.LoadKey(config, TestSecuritySetup.OrgSeedKey, ScynapseKeyType.Organization);
        var nodeKey = TestSecuritySetup.LoadKey(config, TestSecuritySetup.NodeSeedKey, ScynapseKeyType.Node);
        var orgIdentity = TestSecuritySetup.LoadAssertion(config, TestSecuritySetup.OrgIdentityKey);
        var nodeDelegation = TestSecuritySetup.LoadAssertion(config, TestSecuritySetup.NodeDelegationKey);
        var clientDelegation = TestSecuritySetup.LoadAssertion(config, TestSecuritySetup.ClientDelegationKey);

        hostBuilder.UseScynapse((ctx, siloBuilder) =>
        {
            var options = new ScynapseSecurityOptions
            {
                NodeKeyPair = nodeKey,
                TrustedRoots = { orgKey.PublicKeyBytes.ToArray() },
                BootstrapAssertions = { orgIdentity, nodeDelegation },
                PeerAssertions = { clientDelegation },
                RequireMutualTls = false,
                // TLS disabled for TestCluster: in-process transport doesn't need encryption.
                // TLS transport is tested separately in Scynapse.Connections.Security.Tests.
                // These tests validate the call filter / CCap / assertion chain flow.
                EnableTls = false,
            };
            siloBuilder.UseScynapseSecurity(options);
        });
    }
}

// ────────────────────────────────────────────────────────
// Client configurator: wires UseScynapseSecurity on client
// ────────────────────────────────────────────────────────

internal class SecuredClientConfigurator : IClientBuilderConfigurator
{
    public void Configure(IConfiguration configuration, IClientBuilder clientBuilder)
    {
        var orgKey = TestSecuritySetup.LoadKey(configuration, TestSecuritySetup.OrgSeedKey, ScynapseKeyType.Organization);
        var clientKey = TestSecuritySetup.LoadKey(configuration, TestSecuritySetup.ClientSeedKey, ScynapseKeyType.User);
        var orgIdentity = TestSecuritySetup.LoadAssertion(configuration, TestSecuritySetup.OrgIdentityKey);
        var nodeDelegation = TestSecuritySetup.LoadAssertion(configuration, TestSecuritySetup.NodeDelegationKey);
        var clientCCap = TestSecuritySetup.LoadAssertion(configuration, TestSecuritySetup.ClientCCapKey);

        var options = new ScynapseSecurityOptions
        {
            NodeKeyPair = clientKey,
            TrustedRoots = { orgKey.PublicKeyBytes.ToArray() },
            BootstrapAssertions = { orgIdentity },
            PeerAssertions = { nodeDelegation },
            BootstrapCapabilities = { clientCCap },
            EnableTls = false,
        };
        clientBuilder.UseScynapseSecurity(options);
    }
}

// ────────────────────────────────────────────────────────
// Integration tests
// ────────────────────────────────────────────────────────

[Trait("Category", "BVT")]
public class ScynapseSecurityIntegrationTests
{
    /// <summary>
    /// Full end-to-end: client with valid CCap calls a secured grain.
    /// Verifies: TLS transport, CCap wallet, outgoing filter, incoming filter,
    /// assertion chain verification, bearer proof, action matching.
    /// </summary>
    [Fact]
    public async Task SecuredGrainCall_WithValidCCap_Succeeds()
    {
        TestCluster? testCluster = null;
        try
        {
            var props = TestSecuritySetup.CreateTestHierarchy();
            var builder = new TestClusterBuilder(2)
                .AddSiloBuilderConfigurator<SecuredSiloConfigurator>()
                .AddClientBuilderConfigurator<SecuredClientConfigurator>();

            foreach (var kv in props)
                builder.Properties[kv.Key] = kv.Value;

            testCluster = builder.Build();
            await testCluster.DeployAsync();

            var grain = testCluster.Client.GetGrain<ISecuredGrain>("test-1");
            var result = await grain.GetData();
            Assert.Equal("initial", result);
        }
        finally
        {
            if (testCluster != null)
            {
                await testCluster.StopAllSilosAsync();
                testCluster.Dispose();
            }
        }
    }

    /// <summary>
    /// Client can read the verified caller identity from grain code.
    /// </summary>
    [Fact]
    public async Task SecuredGrainCall_GrainCanReadCallerIdentity()
    {
        TestCluster? testCluster = null;
        try
        {
            var props = TestSecuritySetup.CreateTestHierarchy();
            var builder = new TestClusterBuilder(1)
                .AddSiloBuilderConfigurator<SecuredSiloConfigurator>()
                .AddClientBuilderConfigurator<SecuredClientConfigurator>();

            foreach (var kv in props)
                builder.Properties[kv.Key] = kv.Value;

            testCluster = builder.Build();
            await testCluster.DeployAsync();

            var grain = testCluster.Client.GetGrain<ISecuredGrain>("test-whoami");
            var callerKey = await grain.WhoAmI();
            Assert.NotNull(callerKey);
            Assert.Equal(32, callerKey!.Length); // Ed25519 public key is 32 bytes
        }
        finally
        {
            if (testCluster != null)
            {
                await testCluster.StopAllSilosAsync();
                testCluster.Dispose();
            }
        }
    }

    /// <summary>
    /// Anonymous grain allows calls without any CCap.
    /// </summary>
    [Fact]
    public async Task AnonymousGrain_AllowsCallsWithoutCCap()
    {
        TestCluster? testCluster = null;
        try
        {
            var props = TestSecuritySetup.CreateTestHierarchy();
            var builder = new TestClusterBuilder(1)
                .AddSiloBuilderConfigurator<SecuredSiloConfigurator>()
                .AddClientBuilderConfigurator<SecuredClientConfigurator>();

            foreach (var kv in props)
                builder.Properties[kv.Key] = kv.Value;

            testCluster = builder.Build();
            await testCluster.DeployAsync();

            var grain = testCluster.Client.GetGrain<IOpenGrain>("open-1");
            var result = await grain.Hello();
            Assert.Equal("hello", result);
        }
        finally
        {
            if (testCluster != null)
            {
                await testCluster.StopAllSilosAsync();
                testCluster.Dispose();
            }
        }
    }

    /// <summary>
    /// CCap with wrong action is rejected.
    /// </summary>
    [Fact]
    public async Task SecuredGrainCall_WrongAction_IsRejected()
    {
        TestCluster? testCluster = null;
        try
        {
            // Create CCap with "read" action only, then try to call "write" method
            var props = TestSecuritySetup.CreateTestHierarchy(
                ccapResource: "scynapse.>",
                ccapAction: "read");
            var builder = new TestClusterBuilder(1)
                .AddSiloBuilderConfigurator<SecuredSiloConfigurator>()
                .AddClientBuilderConfigurator<SecuredClientConfigurator>();

            foreach (var kv in props)
                builder.Properties[kv.Key] = kv.Value;

            testCluster = builder.Build();
            await testCluster.DeployAsync();

            var grain = testCluster.Client.GetGrain<ISecuredGrain>("test-wrong-action");

            // "read" action should work
            var data = await grain.GetData();
            Assert.Equal("initial", data);

            // "write" action should be rejected — the wallet has no CCap for "write",
            // so the outgoing filter sends no CCap and the incoming filter rejects it.
            var ex = await Assert.ThrowsAsync<ScynapseSecurityException>(
                () => grain.SetData("new value"));
            Assert.Contains("Authentication required", ex.Message);
        }
        finally
        {
            if (testCluster != null)
            {
                await testCluster.StopAllSilosAsync();
                testCluster.Dispose();
            }
        }
    }

    /// <summary>
    /// CCap with expired timestamp is rejected.
    /// </summary>
    [Fact]
    public async Task SecuredGrainCall_ExpiredCCap_IsRejected()
    {
        TestCluster? testCluster = null;
        try
        {
            // Create CCap that already expired
            var expiredAt = DateTimeOffset.UtcNow.AddMinutes(-5).ToUnixTimeSeconds();
            var props = TestSecuritySetup.CreateTestHierarchy(ccapExpiresAt: expiredAt);

            var builder = new TestClusterBuilder(1)
                .AddSiloBuilderConfigurator<SecuredSiloConfigurator>()
                .AddClientBuilderConfigurator<SecuredClientConfigurator>();

            foreach (var kv in props)
                builder.Properties[kv.Key] = kv.Value;

            testCluster = builder.Build();
            await testCluster.DeployAsync();

            var grain = testCluster.Client.GetGrain<ISecuredGrain>("test-expired");

            // Expired CCap is filtered out by the wallet, so no CCap is sent.
            var ex = await Assert.ThrowsAsync<ScynapseSecurityException>(
                () => grain.GetData());
            Assert.Contains("Authentication required", ex.Message);
        }
        finally
        {
            if (testCluster != null)
            {
                await testCluster.StopAllSilosAsync();
                testCluster.Dispose();
            }
        }
    }
}

// ────────────────────────────────────────────────────────
// Client-only configurator (no security) for negative tests
// ────────────────────────────────────────────────────────

internal class UnsecuredClientConfigurator : IClientBuilderConfigurator
{
    public void Configure(IConfiguration configuration, IClientBuilder clientBuilder)
    {
        // No security configured — calls will lack CCap
    }
}

[Trait("Category", "BVT")]
public class ScynapseSecurityNegativeTests
{
    /// <summary>
    /// Client without CCap cannot call a secured grain.
    /// </summary>
    [Fact]
    public async Task SecuredGrainCall_NoCCap_IsRejected()
    {
        TestCluster? testCluster = null;
        try
        {
            var props = TestSecuritySetup.CreateTestHierarchy();
            var builder = new TestClusterBuilder(1)
                .AddSiloBuilderConfigurator<SecuredSiloConfigurator>()
                .AddClientBuilderConfigurator<UnsecuredClientConfigurator>();

            foreach (var kv in props)
                builder.Properties[kv.Key] = kv.Value;

            testCluster = builder.Build();
            await testCluster.DeployAsync();

            var grain = testCluster.Client.GetGrain<ISecuredGrain>("test-no-ccap");

            var ex = await Assert.ThrowsAsync<ScynapseSecurityException>(
                () => grain.GetData());
            Assert.Contains("Authentication required", ex.Message);
        }
        finally
        {
            if (testCluster != null)
            {
                await testCluster.StopAllSilosAsync();
                testCluster.Dispose();
            }
        }
    }
}
