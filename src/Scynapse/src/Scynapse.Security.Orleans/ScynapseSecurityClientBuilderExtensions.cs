using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scynapse.Connections.Security;
using Scynapse.Hosting;
using Scynapse.Runtime;
using Scynapse.Security.Assertions;
using Scynapse.Security.Configuration;
using Scynapse.Security.Crypto;
using Scynapse.Security.Transport;
using Scynapse.Security.Verification;

namespace Scynapse.Security.Orleans;

/// <summary>
/// IClientBuilder extension to wire Scynapse security for external clients.
/// Configures TLS, outgoing call filter with CCap wallet, and identity.
/// </summary>
public static class ScynapseSecurityClientBuilderExtensions
{
    /// <summary>
    /// Configure the client for Scynapse security from an IConfigurationSection.
    /// Binds the section to SecurityConfigurationSection and loads keys/assertions from disk.
    /// </summary>
    public static IClientBuilder UseScynapseSecurity(
        this IClientBuilder builder,
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
    /// Configure the client with auto-generated development mode security.
    /// WARNING: Not for production use.
    /// </summary>
    public static IClientBuilder UseScynapseSecurityDevelopmentMode(
        this IClientBuilder builder,
        Action<IServiceCollection>? configureServices = null)
    {
        var options = DevelopmentModeHelper.CreateDevelopmentOptions();
        return builder.UseScynapseSecurity(options, configureServices);
    }

    /// <summary>
    /// Configure the client for Scynapse capability-based security.
    /// Sets up TLS to silo gateway, CCap wallet, and outgoing call filter.
    /// </summary>
    public static IClientBuilder UseScynapseSecurity(
        this IClientBuilder builder,
        ScynapseSecurityOptions options,
        Action<IServiceCollection>? configureServices = null)
    {
        // Register core services
        builder.Services.AddSingleton(options);
        builder.Services.AddSingleton(options.NodeKeyPair);
        builder.Services.AddSingleton<IAssertionStore, InMemoryAssertionStore>();
        builder.Services.AddSingleton<INonceStore, InMemoryNonceStore>();
        builder.Services.AddSingleton<IAttenuationChecker, DefaultAttenuationChecker>();
        builder.Services.AddSingleton<ICCapWallet, InMemoryCCapWallet>();

        // Register outgoing call filter (clients only send, they don't receive grain calls)
        builder.Services.AddSingleton<IOutgoingGrainCallFilter>(sp =>
        {
            var wallet = sp.GetRequiredService<ICCapWallet>();
            var traceSink = sp.GetService<ISecurityFlowTraceSink>();
            return new ScynapseOutgoingCallFilter(options.NodeKeyPair, wallet, traceSink);
        });

        // Allow caller to override default implementations
        configureServices?.Invoke(builder.Services);

        // Load bootstrap assertions and capabilities via lifecycle participant
        builder.Services.AddSingleton<ILifecycleParticipant<IClusterClientLifecycle>>(sp =>
        {
            var store = sp.GetRequiredService<IAssertionStore>();
            var wallet = sp.GetRequiredService<ICCapWallet>();
            return new ScynapseClientLifecycleParticipant(options, store, wallet);
        });

        // Configure TLS to gateway if enabled.
        // The cert provides transport encryption; identity verification happens
        // at the grain call filter level via CCaps and assertion chains.
        if (options.EnableTls)
        {
            var cert = ScynapseCertificateFactory.CreateSelfSigned(options.NodeKeyPair);

            builder.UseTls(cert, tlsOptions =>
            {
                tlsOptions.SslProtocols = System.Security.Authentication.SslProtocols.Tls12 | System.Security.Authentication.SslProtocols.Tls13;
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

/// <summary>
/// Loads bootstrap assertions and capabilities when the client lifecycle starts.
/// </summary>
internal sealed class ScynapseClientLifecycleParticipant : ILifecycleParticipant<IClusterClientLifecycle>
{
    private readonly ScynapseSecurityOptions _options;
    private readonly IAssertionStore _store;
    private readonly ICCapWallet _wallet;

    public ScynapseClientLifecycleParticipant(
        ScynapseSecurityOptions options,
        IAssertionStore store,
        ICCapWallet wallet)
    {
        _options = options;
        _store = store;
        _wallet = wallet;
    }

    public void Participate(IClusterClientLifecycle lifecycle)
    {
        lifecycle.Subscribe<ScynapseClientLifecycleParticipant>(
            ServiceLifecycleStage.First,
            OnStart);
    }

    private async Task OnStart(CancellationToken ct)
    {
        foreach (var assertion in _options.BootstrapAssertions)
            await _store.StoreAsync(assertion);
        foreach (var assertion in _options.PeerAssertions)
            await _store.StoreAsync(assertion);
        foreach (var ccap in _options.BootstrapCapabilities)
            _wallet.Store(ccap);
    }
}
