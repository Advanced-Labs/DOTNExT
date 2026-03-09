using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scynapse.Connections.Security;
using Scynapse.Hosting;
using Scynapse.Security.Assertions;
using Scynapse.Security.Configuration;
using Scynapse.Security.Crypto;
using Scynapse.Security.Transport;
using Scynapse.Security.Verification;

namespace Scynapse.Security.Orleans;

/// <summary>
/// ISiloBuilder extension to wire all Scynapse security infrastructure:
/// TLS, assertion store, verifier, call filters, policy provider, lifecycle participant.
/// </summary>
public static class ScynapseSecuritySiloBuilderExtensions
{
    /// <summary>
    /// Configure the silo for Scynapse security from an IConfigurationSection.
    /// Binds the section to SecurityConfigurationSection and loads keys/assertions from disk.
    /// </summary>
    public static ISiloBuilder UseScynapseSecurity(
        this ISiloBuilder builder,
        IConfigurationSection configSection,
        string? basePath = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var config = new SecurityConfigurationSection();
        configSection.Bind(config);
        var options = SecurityConfigurationLoader.Load(config, basePath);
        return builder.UseScynapseSecurity(options, configureServices);
    }
    /// <summary>
    /// Configure the silo with auto-generated development mode security.
    /// Generates org key, node key, delegation, and wildcard CCap automatically.
    /// WARNING: Not for production use. Logs a warning on every startup.
    /// </summary>
    public static ISiloBuilder UseScynapseSecurityDevelopmentMode(
        this ISiloBuilder builder,
        Action<IServiceCollection>? configureServices = null)
    {
        var options = DevelopmentModeHelper.CreateDevelopmentOptions();

        builder.Services.AddSingleton<ILifecycleParticipant<Scynapse.Runtime.ISiloLifecycle>>(sp =>
        {
            return new DevelopmentModeWarningParticipant();
        });

        return builder.UseScynapseSecurity(options, configureServices);
    }

    /// <summary>
    /// Configure the silo for Scynapse capability-based security.
    /// Sets up mTLS with Ed25519 identities, assertion verification,
    /// grain call filters, and security policy enforcement.
    /// </summary>
    public static ISiloBuilder UseScynapseSecurity(
        this ISiloBuilder builder,
        ScynapseSecurityOptions options,
        Action<IServiceCollection>? configureServices = null)
    {
        // Register options and identity
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(options.NodeKeyPair);

        // Register default implementations (can be overridden by configureServices)
        builder.Services.AddSingleton<IAssertionStore, InMemoryAssertionStore>();
        builder.Services.AddSingleton<INonceStore, InMemoryNonceStore>();
        builder.Services.AddSingleton<IAttenuationChecker, DefaultAttenuationChecker>();
        builder.Services.AddSingleton<ICCapWallet, InMemoryCCapWallet>();

        // Register grain security policy provider with system grain protections
        var policyProvider = new AttributeBasedPolicyProvider();
        RegisterSystemGrainPolicies(policyProvider);
        builder.Services.AddSingleton<IGrainSecurityPolicyProvider>(policyProvider);

        // Build trusted node keys set: this node + all peer assertion subjects
        var trustedNodeKeys = new HashSet<ReadOnlyMemory<byte>>(ByteMemoryEqualityComparer.Instance)
        {
            options.NodeKeyPair.PublicKeyBytes.ToArray()
        };
        foreach (var peer in options.PeerAssertions)
        {
            // Peer delegation assertions have the node key as subject
            if (peer.ClaimType == ClaimType.Delegation)
                trustedNodeKeys.Add(peer.Subject.ToArray());
        }

        // Register grain call filters
        builder.Services.AddSingleton<IIncomingGrainCallFilter>(sp =>
        {
            var store = sp.GetRequiredService<IAssertionStore>();
            var nonceStore = sp.GetRequiredService<INonceStore>();
            var policyProvider = sp.GetRequiredService<IGrainSecurityPolicyProvider>();
            var attenuationChecker = sp.GetRequiredService<IAttenuationChecker>();
            var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<ScynapseIncomingCallFilter>>();
            var traceSink = sp.GetService<ISecurityFlowTraceSink>();
            return new ScynapseIncomingCallFilter(
                store, nonceStore, options.TrustedRoots, policyProvider,
                attenuationChecker, trustedNodeKeys, logger, traceSink);
        });

        builder.Services.AddSingleton<IOutgoingGrainCallFilter>(sp =>
        {
            var wallet = sp.GetRequiredService<ICCapWallet>();
            var traceSink = sp.GetService<ISecurityFlowTraceSink>();
            return new ScynapseOutgoingCallFilter(options.NodeKeyPair, wallet, traceSink);
        });

        // Register lifecycle participant for bootstrap assertion and CCap loading
        builder.Services.AddSingleton<ILifecycleParticipant<Scynapse.Runtime.ISiloLifecycle>>(sp =>
        {
            var store = sp.GetRequiredService<IAssertionStore>();
            var wallet = sp.GetRequiredService<ICCapWallet>();
            return new ScynapseSecurityLifecycleParticipant(options, store, wallet);
        });

        // Allow caller to override default implementations
        configureServices?.Invoke(builder.Services);

        // Configure TLS with Ed25519-derived certificates if enabled.
        // The cert provides transport encryption; identity verification happens
        // at the grain call filter level via CCaps and assertion chains.
        if (options.EnableTls)
        {
            var cert = ScynapseCertificateFactory.CreateSelfSigned(options.NodeKeyPair);
            var remoteCertMode = options.RequireMutualTls
                ? RemoteCertificateMode.RequireCertificate
                : RemoteCertificateMode.AllowCertificate;

            builder.UseTls(cert, tlsOptions =>
            {
                tlsOptions.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;
                tlsOptions.ClientCertificateMode = remoteCertMode;
                tlsOptions.RemoteCertificateMode = RemoteCertificateMode.AllowCertificate;
                tlsOptions.AllowAnyRemoteCertificate();
                tlsOptions.LocalCertificate = cert;
                tlsOptions.OnAuthenticateAsClient = (connection, sslOptions) =>
                {
                    sslOptions.TargetHost = "Scynapse Node";
                };
            });
        }

        return builder;
    }

    /// <summary>
    /// Registers security policies for system grains that cannot carry [SecurityPolicy]
    /// attributes because they live in projects that don't reference Scynapse.Security.Orleans.
    /// </summary>
    private static void RegisterSystemGrainPolicies(AttributeBasedPolicyProvider provider)
    {
        // IGrainTypeDirectoryGrain — exposes deployment topology.
        // Require authentication for read operations; admin CCap for writes.
        var gtdType = Type.GetType("Scynapse.DynamicGrains.IGrainTypeDirectoryGrain, Scynapse.Core.Abstractions");
        if (gtdType is not null)
        {
            provider.RegisterPolicy(gtdType, new GrainSecurityPolicy
            {
                RequiresAuthentication = true,
                RequiresCallerCapability = false, // node trust sufficient for silo-to-silo
            });
        }
    }
}
