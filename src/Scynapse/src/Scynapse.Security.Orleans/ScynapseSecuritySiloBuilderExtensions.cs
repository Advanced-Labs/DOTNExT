using Microsoft.Extensions.DependencyInjection;
using Scynapse.Connections.Security;
using Scynapse.Hosting;
using Scynapse.Security.Assertions;
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

        // Register grain security policy provider
        builder.Services.AddSingleton<IGrainSecurityPolicyProvider, AttributeBasedPolicyProvider>();

        // Register grain call filters
        builder.Services.AddSingleton<IIncomingGrainCallFilter>(sp =>
        {
            var store = sp.GetRequiredService<IAssertionStore>();
            var nonceStore = sp.GetRequiredService<INonceStore>();
            var policyProvider = sp.GetRequiredService<IGrainSecurityPolicyProvider>();
            var attenuationChecker = sp.GetRequiredService<IAttenuationChecker>();
            return new ScynapseIncomingCallFilter(store, nonceStore, options.TrustedRoots, policyProvider, attenuationChecker);
        });

        builder.Services.AddSingleton<IOutgoingGrainCallFilter>(sp =>
        {
            var wallet = sp.GetRequiredService<ICCapWallet>();
            return new ScynapseOutgoingCallFilter(options.NodeKeyPair, wallet);
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
}
