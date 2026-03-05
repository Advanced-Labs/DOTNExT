using System.Security.Cryptography.X509Certificates;
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
        ScynapseSecurityOptions options)
    {
        // Register core security services
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(options.NodeKeyPair);
        builder.Services.AddSingleton<IAssertionStore, InMemoryAssertionStore>();
        builder.Services.AddSingleton<INonceStore, InMemoryNonceStore>();
        builder.Services.AddSingleton<IAttenuationChecker, DefaultAttenuationChecker>();

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

        // Register lifecycle participant for bootstrap assertion loading
        builder.Services.AddSingleton<ILifecycleParticipant<Scynapse.Runtime.ISiloLifecycle>>(sp =>
        {
            var store = sp.GetRequiredService<IAssertionStore>();
            return new ScynapseSecurityLifecycleParticipant(options, store);
        });

        // Configure mTLS with Ed25519-derived certificates
        // Workaround: ECDSA bridge for TLS because SslStream lacks Ed25519 support.
        // Replace with direct Ed25519 cert when .NET supports Ed25519 in TLS handshakes.
        var cert = ScynapseCertificateFactory.CreateSelfSigned(options.NodeKeyPair);

        builder.UseTls(cert, tlsOptions =>
        {
            tlsOptions.ClientCertificateMode = RemoteCertificateMode.AllowCertificate;
            // Custom validation: verify Ed25519 assertion chain, not X.509 CA chain
            tlsOptions.RemoteCertificateValidation = (certificate, chain, errors) =>
            {
                var validator = new ScynapseRemoteCertificateValidator(
                    new InMemoryAssertionStore(), new InMemoryNonceStore(), options.TrustedRoots);
                return validator.Validate(certificate);
            };
        });

        return builder;
    }
}
