# Scynapse Security — Implementation Guide (WIP)

## Meta / Recovery Context

**If you're reading this after context compaction:** This file is a work-in-progress implementation guide for Scynapse's security system. The companion document `scynapse-security-architecture.md` (in outputs) contains the full architectural design. READ THAT FIRST. This file bridges architecture to code.

**What Scynapse is:** A fork/evolution of Microsoft Orleans (distributed actor platform). Currently uses Orleans's Silo/Client/Grain paradigm. Evolving toward a Component Model where "Component is the network." Security is being designed to work on the current Orleans paradigm FIRST, then evolve with the Component Model.

**Key architectural decisions (from the architecture doc):**
- Ed25519 is THE identity primitive (use `NSec.Cryptography`)
- The Signed Assertion is the single universal primitive (identity, capability, relation, delegation, revocation — all one format)
- Trust boundary is the Component (future) / Grain type (current Orleans approximation)
- mTLS default transport, TLS as bootstrap ramp
- No ACLs — capability-based auth only
- CCaps (Crypto-Capabilities) are challengeable and channel-bindable

---

## The Plan

### Phase 1: Security on Current Orleans Paradigm

Implement the security system targeting Orleans's existing architecture: Silo, Gateway, Client, Grain.

**Research needed:**
- [x] Orleans Silo-to-Silo connection lifecycle — where to intercept for mTLS + identity verification
- [x] Orleans Gateway (client connection) lifecycle — where to intercept for TLS→mTLS bootstrap
- [x] Orleans message pipeline — IIncomingGrainCallFilter / IOutgoingGrainCallFilter — how to inject assertion verification per grain call
- [x] Orleans RequestContext — how it flows, how to carry assertion/identity data through call chains
- [x] Orleans serialization pipeline — how to serialize/deserialize SignedAssertions efficiently
- [x] Scynapse-specific: naturalized C# events, C# properties on grains — security implications for these extensions
- [ ] Ed25519 → X.509 bridge: confirm .NET 7+ CertificateRequest supports Ed25519 for self-signed certs
- [ ] Confirm NSec.Cryptography API surface matches our KeyPair abstraction needs
- [ ] Investigate how Orleans.TLS sample integrates custom certificate validation callbacks

**Implementation plan (dependency order):**
1. **Core crypto types** — KeyPair, SignedAssertion, AssertionBuilder, AssertionVerifier (pure, no Orleans dependency)
2. **Assertion store** — IAssertionStore interface + InMemoryAssertionStore
3. **Key encoding** — typed prefix encoding for Scynapse keys
4. **Transport security** — mTLS configuration for Silo-to-Silo, TLS+bootstrap for Gateway
5. **Message pipeline integration** — grain call filters that verify CCaps from RequestContext
6. **Grain-level security policy** — attributes/interfaces for grains to declare security requirements
7. **Client SDK integration** — assertion presentation, challenge-response from client side

**Deliverables:**
- C# interface definitions for all core types
- Integration points mapped to specific Orleans source code locations
- NuGet package dependency list
- Test strategy

### Phase 2: Forward-Looking (Component Model)

Less detailed. Focused on what we know we'll need once the Component Model and CNS exist.

- How Signed Assertions map to Component type definitions
- How Component security policy declarations work
- How the CNS integrates with assertion resolution / key distribution
- How Component-level isolation differs from current Grain-level approximation
- Migration path from Phase 1 (Grain-level) to Phase 2 (Component-level)

---

## Research Findings (accumulated as work proceeds)

### Orleans Silo-to-Silo Connections

**Key classes:** `SiloConnectionListener` (in `Orleans.Runtime.Messaging`), `SocketConnectionListener` (in `Orleans.Networking.Shared`), `ConnectionListener` base class.

**Connection lifecycle:**
- Silo starts listening at `ServiceLifecycleStage.RuntimeInitialize` (stage 2000) — this is VERY early, before grain services (8000) or application services (10000).
- `SiloConnectionListener.OnRuntimeInitializeStart()` binds the socket.
- Orleans already supports TLS via `UseTls()` extension method on the silo builder. The TLS sample demonstrates mTLS with self-signed certs.
- The transport is built on ASP.NET Core's connection abstractions (Kestrel's transport layer), NOT on raw sockets directly.

**Integration point for Scynapse security:**
- **Replace or wrap the connection listener factory** (`SocketConnectionListenerFactory`) to enforce mTLS with Ed25519-derived certificates.
- Hook at `ServiceLifecycleStage.RuntimeInitialize` (or `First`) to set up the Scynapse identity/key material BEFORE networking starts.
- The existing `UseTls()` pipeline is the natural place to inject our certificate validation logic (verify assertion chains instead of traditional X.509 CA validation).

**Orleans TLS configuration pattern (from Microsoft sample):**
```csharp
builder.UseTls(
    storeName, certSubject, allowInvalid, storeLocation,
    options => { /* configure TLS options */ }
);
```
We'd replace this with our own TLS configuration that uses Ed25519-derived certificates and assertion-chain verification.

### Orleans Gateway / Client Connections

**Key insight:** Orleans has TWO listeners per silo:
1. `SiloConnectionListener` — for silo-to-silo traffic (port 11111 default)
2. `GatewayConnectionListener` — for client-to-silo traffic (port 30000 default)

Both use the same `ConnectionListener` base class and `SocketConnectionListenerFactory`. Both can be TLS-enabled independently.

**Client connection model (Orleans 7+):**
- Clients can be **co-hosted** (same process as silo, direct DI access, no network hop) or **external** (separate process, connects via gateway).
- Co-hosted clients bypass the gateway entirely — they use the silo's internal `InsideRuntimeClient`.
- External clients connect to the gateway, which is a TCP listener that accepts connections and routes grain calls.

**For Scynapse:** Since "everything is a Node," the distinction between silo and client is less relevant. But the gateway is still the entry point for any entity connecting to a silo for the first time. The bootstrap ramp (TLS → identity negotiation → mTLS upgrade) maps naturally to the gateway connection lifecycle.

**Integration point:** The `GatewayConnectionListener` is where the TLS→mTLS bootstrap ramp is implemented. On connection accept, start with server-authenticated TLS, then run the bootstrap protocol to verify the peer's identity and upgrade to mTLS.

### Orleans Message Pipeline (Call Filters)

**Two filter types:**
1. `IIncomingGrainCallFilter` — executed on the RECEIVING silo when a grain call arrives.
2. `IOutgoingGrainCallFilter` — executed on the SENDING side (silo or client) when a grain call is made.

**Registration:**
- **Silo-wide:** Register via DI (`services.AddSingleton<IIncomingGrainCallFilter, MyFilter>()` or `builder.AddIncomingGrainCallFilter(...)`)
- **Grain-level:** Grain class implements `IIncomingGrainCallFilter` directly — its `Invoke` is called for ALL calls to that grain, before the grain method.
- Both silo-wide and grain-level filters form a chain. Order: silo-wide filters → grain-level filter → actual grain method.

**Context available in filters:**
```csharp
IIncomingGrainCallContext {
    IGrain Grain { get; }            // the grain being called
    MethodInfo InterfaceMethod;      // interface method being invoked
    MethodInfo ImplementationMethod; // implementation method
    object[] Arguments;              // call arguments
    object Result;                   // can be set by filter
    Task Invoke();                   // call next filter or grain method
}
```

**Critical for security:** The `IIncomingGrainCallFilter` is THE place to verify CCaps for grain calls. The filter can:
- Read CCap/assertion data from `RequestContext`
- Inspect the grain type and method being called
- Check the CCap grants the right to call this method on this grain type
- Reject the call by throwing before calling `context.Invoke()`
- The grain itself can also participate if it implements `IIncomingGrainCallFilter`

**Issue from Orleans #8442:** There's an open discussion about standardizing authorization in Orleans. The community approach proposed is exactly what we're doing: define security attributes on grain interfaces, validate in call filters using RequestContext. This validates our approach.

**Issue from Orleans #6256:** RequestContext may not flow correctly for some internal Orleans operations (e.g., stream handshake calls). We need to be aware that not ALL calls will have security context. Internal/system calls may need exemption.

### Orleans RequestContext Flow

**How it works:**
- `RequestContext` is an `AsyncLocal`-based dictionary that flows with the async call chain.
- Set values with `RequestContext.Set(key, value)`, read with `RequestContext.Get(key)`.
- When a grain call crosses silo boundaries, RequestContext is serialized into the message and deserialized on the other side.
- It flows outgoing → across network → incoming. Both filters and grains can read/write it.

**For Scynapse security:**
- The OUTGOING filter sets security context: `RequestContext.Set("Scynapse.Caller.Identity", callerPublicKey)`, `RequestContext.Set("Scynapse.CCap", serializedCCap)`, etc.
- The INCOMING filter reads and verifies this context.
- The grain can also read it (e.g., to make authorization decisions beyond what the filter checks).

**Important:** RequestContext values must be serializable by Orleans' serialization pipeline. Our assertion types need to be serializable.

**Caveat:** RequestContext is NOT a security boundary by itself — it's just data flowing with the call. The INCOMING filter is the enforcement point. A malicious silo could forge RequestContext values. This is why CCaps need their own cryptographic verification (signature checking) independent of the transport.

### Orleans Serialization

**Orleans 7+ uses code-generated serializers:**
- `[GenerateSerializer]` attribute + `[Id(N)]` on fields/properties.
- Source generators produce efficient binary serializers at compile time.
- Custom types need `[GenerateSerializer]` or a custom `ICodec<T>` / `ISurrogate<T>`.

**For Signed Assertions:**
- Option A: Make `SignedAssertion` a `[GenerateSerializer]` type. Simple but ties the format to Orleans' internal serialization.
- Option B: Serialize assertions independently (CBOR/custom) and pass as `byte[]` through Orleans. More portable, assertion format independent of Orleans.
- **Recommendation: Option B.** Assertions are verified by cryptographic signature, not by trusting the transport. They should be opaque bytes from Orleans' perspective. This also means they can be stored, forwarded, and verified outside of Orleans contexts.

### Scynapse-Specific Extensions

**From past conversations and memory:**
- Scynapse has naturalized C# events on grains (events are bridged to SMS streams).
- Scynapse has naturalized C# properties on grains (StateTask<T> for remote property access).
- These extensions expand the grain's "surface area" beyond just methods — events and properties also need security coverage.

**Security implications:**
- **Events:** Subscribing to a grain's event is an action that should require a CCap. The outgoing filter for event subscription should verify the subscriber has the right to subscribe.
- **Properties:** Reading/writing a grain property is semantically a method call (getter/setter). If these go through the normal call filter pipeline (they should, since they're just grain calls under the hood), they get security for free.
- **If events bypass the call filter pipeline** (e.g., go through SMS streams directly), we need a separate security check at the stream subscription level. This needs investigation in the Scynapse source.

---

## C# Interface Sketches

### Layer 0: Cryptographic Primitives

```csharp
// Wraps NSec.Cryptography or nkeys.net
// This is our abstraction — we don't leak the underlying library's API

namespace Scynapse.Security.Crypto;

/// <summary>
/// An Ed25519 keypair. The fundamental identity unit in Scynapse.
/// </summary>
public sealed class ScynapseKeyPair : IDisposable
{
    public ReadOnlySpan<byte> PublicKey { get; }
    
    /// <summary>
    /// Sign data with the private key. Only available if this keypair was created
    /// from a seed (not from a public key alone).
    /// </summary>
    public byte[] Sign(ReadOnlySpan<byte> data);
    
    /// <summary>
    /// Verify a signature against this keypair's public key.
    /// Available even if only public key is known.
    /// </summary>
    public bool Verify(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature);
    
    // Factory methods
    public static ScynapseKeyPair Generate();
    public static ScynapseKeyPair FromSeed(ReadOnlySpan<byte> seed);
    public static ScynapseKeyPair FromPublicKey(ReadOnlySpan<byte> publicKey);  // verify-only
    
    // Encoded key strings with typed prefixes
    public string ToEncodedPublicKey();          // e.g., "NABC123..." for a Node
    public string ToEncodedSeed();               // e.g., "SNABC123..." for a Node seed
    public static ScynapseKeyPair FromEncodedSeed(string encoded);
    public static ScynapseKeyPair FromEncodedPublicKey(string encoded);
}

/// <summary>
/// Key type prefixes for Scynapse entities.
/// Inspired by NATS NKeys but with Scynapse-specific types.
/// </summary>
public enum ScynapseKeyType : byte
{
    Node       = 0,   // 'N' prefix
    Component  = 1,   // 'C' prefix  
    Instance   = 2,   // 'I' prefix
    User       = 3,   // 'U' prefix
    Session    = 4,   // 'E' prefix (Ephemeral)
    Operator   = 5,   // 'O' prefix
    // Extensible — add more as needed
}
```

### Layer 1: Signed Assertion Core

```csharp
namespace Scynapse.Security.Assertions;

/// <summary>
/// The universal primitive. Identity, capability, relation, delegation,
/// and revocation are all SignedAssertions with different claim types.
/// 
/// Immutable after creation. Content-addressed by Blake2b-256 hash.
/// </summary>
public sealed class SignedAssertion
{
    public byte Version { get; }
    public ReadOnlyMemory<byte> Id { get; }           // Blake2b-256 of content fields
    public ReadOnlyMemory<byte> Issuer { get; }       // Ed25519 public key
    public ReadOnlyMemory<byte> Subject { get; }      // Ed25519 public key
    
    public ClaimType ClaimType { get; }
    public ReadOnlyMemory<byte> ClaimData { get; }    // serialized claim payload
    
    public long? NotBefore { get; }                   // Unix timestamp (seconds)
    public long? ExpiresAt { get; }
    public ReadOnlyMemory<byte>? Nonce { get; }
    
    public IReadOnlyList<ReadOnlyMemory<byte>> Proofs { get; }  // parent assertion IDs
    
    public IReadOnlyDictionary<string, ReadOnlyMemory<byte>> Extensions { get; }
    
    public ReadOnlyMemory<byte> Signature { get; }    // Ed25519 signature
    
    /// <summary>
    /// Serialize to bytes. Wire format is pluggable (CBOR initially).
    /// </summary>
    public byte[] Serialize();
    public static SignedAssertion Deserialize(ReadOnlySpan<byte> data);
    
    /// <summary>
    /// Verify ONLY the signature (not the chain). Fast, local check.
    /// </summary>
    public bool VerifySignature();
}

public enum ClaimType : byte
{
    Identity   = 0x01,
    Capability = 0x02,
    Delegation = 0x03,
    Relation   = 0x04,
    Revocation = 0x05,
    Extension  = 0xFF,
}

/// <summary>
/// Builder for creating and signing assertions.
/// </summary>
public sealed class AssertionBuilder
{
    public AssertionBuilder SetIssuer(ScynapseKeyPair issuer);
    public AssertionBuilder SetSubject(ReadOnlySpan<byte> subjectPublicKey);
    public AssertionBuilder SetClaim(ClaimType type, object claimData);
    public AssertionBuilder SetScope(long? notBefore = null, long? expiresAt = null, byte[]? nonce = null);
    public AssertionBuilder AddProof(ReadOnlySpan<byte> parentAssertionId);
    public AssertionBuilder AddExtension(string key, ReadOnlySpan<byte> value);
    
    /// <summary>
    /// Build and sign the assertion. Computes content hash and Ed25519 signature.
    /// </summary>
    public SignedAssertion Build();
    
    // Convenience: create common assertion types
    public static SignedAssertion CreateIdentity(ScynapseKeyPair keypair, long? expiresAt = null);
    public static SignedAssertion CreateCapability(
        ScynapseKeyPair issuer,
        ReadOnlySpan<byte> subject,
        string resource,
        string action,
        IEnumerable<byte[]> proofs,
        long? expiresAt = null);
    public static SignedAssertion CreateDelegation(
        ScynapseKeyPair issuer,
        ReadOnlySpan<byte> subject,
        DelegationScope scope,
        IEnumerable<byte[]> proofs,
        long? expiresAt = null);
}

// Claim-specific data structures
public sealed record CapabilityClaim(
    string Resource,      // URI identifying the resource (grain type, etc.)
    string Action,        // what action is authorized
    IReadOnlyDictionary<string, byte[]>? Constraints = null
);

public sealed record DelegationClaim(
    ClaimType[] AllowedClaimTypes,
    string? ResourcePattern = null,
    string? ActionPattern = null,
    byte? MaxDepth = null
);

public sealed record RelationClaim(
    string Context,
    IReadOnlyDictionary<string, byte[]>? Metadata = null
);
```

### Layer 2: Verification

```csharp
namespace Scynapse.Security.Verification;

/// <summary>
/// Resolves assertions by their content-addressed ID.
/// Multiple implementations: in-memory, persistent, distributed (via CNS).
/// </summary>
public interface IAssertionStore
{
    /// <summary>
    /// Resolve an assertion by its content hash ID.
    /// Returns null if not found.
    /// </summary>
    ValueTask<SignedAssertion?> ResolveAsync(ReadOnlyMemory<byte> assertionId);
    
    /// <summary>
    /// Store an assertion. Idempotent (content-addressed).
    /// </summary>
    ValueTask StoreAsync(SignedAssertion assertion);
    
    /// <summary>
    /// Check if a specific assertion has been revoked.
    /// </summary>
    ValueTask<bool> IsRevokedAsync(ReadOnlyMemory<byte> assertionId);
}

/// <summary>
/// Tracks nonces for replay prevention.
/// </summary>
public interface INonceStore
{
    bool HasSeen(ReadOnlyMemory<byte> assertionId);
    void Record(ReadOnlyMemory<byte> assertionId, long? expiresAt);
}

/// <summary>
/// The universal verification engine. Verifies any SignedAssertion
/// by checking signature, temporal scope, and walking the proof chain.
/// </summary>
public sealed class AssertionVerifier
{
    public AssertionVerifier(
        IAssertionStore store,
        INonceStore nonceStore,
        IReadOnlySet<ReadOnlyMemory<byte>> trustedRoots,  // root public keys we trust
        IAttenuationChecker attenuationChecker);
    
    /// <summary>
    /// Full verification: signature + scope + chain walk + attenuation.
    /// </summary>
    public ValueTask<VerificationResult> VerifyAsync(SignedAssertion assertion);
    
    /// <summary>
    /// Quick verification: signature + scope only (no chain walk).
    /// For performance-sensitive paths where chain was previously verified.
    /// </summary>
    public VerificationResult VerifyLocal(SignedAssertion assertion);
}

public sealed record VerificationResult(
    bool IsValid,
    string? FailureReason = null,
    SignedAssertion? FailedAssertion = null  // which assertion in the chain failed
);

/// <summary>
/// Checks that a child assertion's claims are within the scope of its parent.
/// Extensible: different claim types have different attenuation rules.
/// </summary>
public interface IAttenuationChecker
{
    bool Check(SignedAssertion parent, SignedAssertion child);
}
```

### Layer 3-4: Orleans Integration

```csharp
namespace Scynapse.Security.Orleans;

/// <summary>
/// Outgoing grain call filter: attaches security context to every grain call.
/// Registered silo-wide and on clients.
/// </summary>
public sealed class ScynapseOutgoingCallFilter : IOutgoingGrainCallFilter
{
    public async Task Invoke(IOutgoingGrainCallContext context)
    {
        // 1. Get the current node/session's identity
        // 2. Find a valid CCap for the target grain type + method
        // 3. Serialize and attach to RequestContext
        RequestContext.Set("Scynapse.Caller.PublicKey", _currentIdentity.PublicKey);
        RequestContext.Set("Scynapse.CCap", ccapBytes);
        RequestContext.Set("Scynapse.CCap.BearerProof", bearerSignature); // proves we own the CCap
        
        await context.Invoke();
    }
}

/// <summary>
/// Incoming grain call filter: verifies security context on every grain call.
/// Registered silo-wide. THE primary enforcement point.
/// </summary>
public sealed class ScynapseIncomingCallFilter : IIncomingGrainCallFilter
{
    private readonly AssertionVerifier _verifier;
    private readonly IGrainSecurityPolicyProvider _policyProvider;
    
    public async Task Invoke(IIncomingGrainCallContext context)
    {
        // 1. Read security context from RequestContext
        var callerKey = RequestContext.Get("Scynapse.Caller.PublicKey") as byte[];
        var ccapBytes = RequestContext.Get("Scynapse.CCap") as byte[];
        var bearerProof = RequestContext.Get("Scynapse.CCap.BearerProof") as byte[];
        
        // 2. Get the security policy for this grain type
        var policy = _policyProvider.GetPolicy(context.Grain.GetType());
        
        // 3. If policy requires authentication, verify
        if (policy.RequiresAuthentication)
        {
            if (callerKey == null || ccapBytes == null)
                throw new ScynapseSecurityException("Authentication required");
            
            var ccap = SignedAssertion.Deserialize(ccapBytes);
            
            // 4. Verify the CCap (signature + chain + attenuation)
            var result = await _verifier.VerifyAsync(ccap);
            if (!result.IsValid)
                throw new ScynapseSecurityException($"Invalid CCap: {result.FailureReason}");
            
            // 5. Verify the CCap grants the right action on the right resource
            var claim = DeserializeClaim<CapabilityClaim>(ccap.ClaimData);
            if (!policy.IsActionAllowed(claim.Resource, claim.Action, context.InterfaceMethod))
                throw new ScynapseSecurityException("Insufficient capability");
            
            // 6. Verify bearer (caller owns the CCap's subject key)
            if (!VerifyBearerProof(ccap, callerKey, bearerProof))
                throw new ScynapseSecurityException("Bearer verification failed");
            
            // 7. Set verified caller identity for grain to access
            RequestContext.Set("Scynapse.Verified.CallerKey", callerKey);
            RequestContext.Set("Scynapse.Verified.CCap", ccap);
        }
        
        await context.Invoke();
    }
}

/// <summary>
/// Grains can declare their security requirements via attributes.
/// These are read by the security policy provider.
/// </summary>
[AttributeUsage(AttributeTargets.Interface | AttributeTargets.Method)]
public class RequireCapabilityAttribute : Attribute
{
    public string Action { get; set; }
    public string? Resource { get; set; }  // null = infer from grain type
}

[AttributeUsage(AttributeTargets.Interface)]
public class SecurityPolicyAttribute : Attribute
{
    public bool RequiresAuthentication { get; set; } = true;
    public bool RequiresChannelBinding { get; set; } = false;
    public bool AllowAnonymous { get; set; } = false;
    public bool AllowPseudonymous { get; set; } = true;
}

/// <summary>
/// Example grain interface with security attributes.
/// </summary>
[SecurityPolicy(RequiresAuthentication = true)]
public interface ISecureGrain : IGrainWithStringKey
{
    [RequireCapability(Action = "read")]
    Task<string> GetDataAsync();
    
    [RequireCapability(Action = "write")]
    Task SetDataAsync(string value);
    
    [RequireCapability(Action = "admin")]
    Task DeleteAsync();
}

/// <summary>
/// Extension for grain code to access verified security context.
/// </summary>
public static class GrainSecurityExtensions
{
    /// <summary>
    /// Get the verified caller's public key from the current grain call context.
    /// Returns null if the call was unauthenticated (anonymous).
    /// </summary>
    public static byte[]? GetCallerPublicKey(this Grain grain)
        => RequestContext.Get("Scynapse.Verified.CallerKey") as byte[];
    
    /// <summary>
    /// Get the verified CCap that authorized this call.
    /// </summary>
    public static SignedAssertion? GetCallerCapability(this Grain grain)
        => RequestContext.Get("Scynapse.Verified.CCap") as SignedAssertion;
}
```

### Transport Security (Layer 3)

```csharp
namespace Scynapse.Security.Transport;

/// <summary>
/// Configures TLS/mTLS for Scynapse Silos using Ed25519 identities.
/// Replaces Orleans' default X.509-based TLS with assertion-chain-based verification.
/// </summary>
public static class ScynapseTransportExtensions
{
    /// <summary>
    /// Configure the silo for Scynapse security.
    /// Sets up mTLS for silo-to-silo, TLS+bootstrap for gateway.
    /// </summary>
    public static ISiloBuilder UseScynapseSecurity(
        this ISiloBuilder builder,
        ScynapseSecurityOptions options)
    {
        // 1. Register core crypto services
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(options);
            services.AddSingleton<ScynapseKeyPair>(options.NodeKeyPair);
            services.AddSingleton<IAssertionStore, InMemoryAssertionStore>();
            services.AddSingleton<INonceStore, InMemoryNonceStore>();
            services.AddSingleton<AssertionVerifier>();
            
            // 2. Register grain call filters
            services.AddSingleton<IIncomingGrainCallFilter, ScynapseIncomingCallFilter>();
            services.AddSingleton<IOutgoingGrainCallFilter, ScynapseOutgoingCallFilter>();
            
            // 3. Register grain security policy provider
            services.AddSingleton<IGrainSecurityPolicyProvider, AttributeBasedPolicyProvider>();
        });
        
        // 4. Configure silo-to-silo TLS (mTLS)
        // Uses Ed25519 key → self-signed X.509 cert (for TLS compatibility)
        // Custom validation callback verifies assertion chains instead of CA chains
        builder.UseTls(/* Ed25519-derived cert config */);
        
        // 5. Hook into lifecycle for early security initialization
        builder.AddSiloLifecycleParticipant<ScynapseSecurityLifecycleParticipant>();
        
        return builder;
    }
}

public sealed class ScynapseSecurityOptions
{
    /// <summary>
    /// This Node's identity keypair. Must be set before startup.
    /// </summary>
    public ScynapseKeyPair NodeKeyPair { get; set; }
    
    /// <summary>
    /// Trusted root public keys. Assertions chaining to these roots are accepted.
    /// </summary>
    public ISet<byte[]> TrustedRoots { get; set; } = new HashSet<byte[]>(ByteArrayComparer.Instance);
    
    /// <summary>
    /// Pre-loaded assertions (e.g., this Node's delegation chain from operator to node).
    /// </summary>
    public IList<SignedAssertion> BootstrapAssertions { get; set; } = new List<SignedAssertion>();
}
```

---

## Integration Points Map

### Where Security Hooks Into Orleans Source Code

```
LIFECYCLE (when things happen):
├── ServiceLifecycleStage.First (int.MinValue)
│   └── ★ ScynapseSecurityLifecycleParticipant: load node keypair, 
│       bootstrap assertions, initialize assertion store, nonce store
│
├── ServiceLifecycleStage.RuntimeInitialize (2000)
│   ├── SiloConnectionListener starts (silo-to-silo networking)
│   │   └── ★ Must have Ed25519-derived TLS cert ready BEFORE this
│   └── GatewayConnectionListener starts (client/peer connections)
│       └── ★ Must have bootstrap TLS cert ready BEFORE this
│
├── ServiceLifecycleStage.RuntimeServices (4000)
│   └── Various agents start (messaging, membership, etc.)
│       └── ★ Silo-to-silo messages now flowing — mTLS must be active
│
├── ServiceLifecycleStage.BecomeActive (19999)
│   └── Silo joins cluster
│       └── ★ Other silos verify this silo's identity via assertion chain
│
└── ServiceLifecycleStage.Active (20000)
    └── Ready for grain calls
        └── ★ Call filters active, CCap verification operational


MESSAGE PIPELINE (per grain call):
├── Client/Silo makes grain call
│   └── IOutgoingGrainCallFilter chain
│       └── ★ ScynapseOutgoingCallFilter: attach CCap + bearer proof to RequestContext
│
├── RequestContext serialized into Message
│   └── ★ CCap bytes travel with the message (opaque to Orleans serialization)
│
├── Message received on target silo
│   └── IIncomingGrainCallFilter chain
│       ├── ★ ScynapseIncomingCallFilter: verify CCap, check policy, set verified context
│       └── Grain-level IIncomingGrainCallFilter (if grain implements it)
│           └── ★ Grain can do additional fine-grained authorization
│
└── Grain method executes
    └── ★ Grain reads verified caller identity via GrainSecurityExtensions


TRANSPORT LAYER:
├── Silo-to-Silo (SiloConnectionListener, port 11111 default)
│   └── ★ mTLS with Ed25519-derived certs
│       └── Custom validation: verify assertion chain, not X.509 CA chain
│
├── Gateway (GatewayConnectionListener, port 30000 default)
│   └── ★ TLS initially, bootstrap ramp to mTLS
│       ├── Phase 1: Server-authenticated TLS (server presents Ed25519 cert)
│       ├── Phase 2: Client presents identity + proofs, challenge-response
│       └── Phase 3: Upgrade to mTLS (or maintain TLS with application-layer auth)
│
└── Co-hosted Client (InsideRuntimeClient, no network)
    └── ★ No transport security needed (same process)
        └── But call filters still apply (grain-level authorization still enforced)


KEY ORLEANS SOURCE FILES TO MODIFY/EXTEND:
├── Orleans.Runtime/Silo/Silo.cs — lifecycle participation
├── Orleans.Runtime/Messaging/SiloConnectionListener.cs — silo-to-silo TLS
├── Orleans.Runtime/Messaging/GatewayConnectionListener.cs — gateway TLS/bootstrap
├── Orleans.Runtime/Messaging/ConnectionListener.cs — base connection handling
├── Orleans.Networking.Shared/SocketConnectionListenerFactory.cs — connection factory
├── Orleans.Runtime/Messaging/MessageCenter.cs — message routing
├── [Scynapse additions, not Orleans modifications]:
│   ├── Scynapse.Security.Crypto/ — KeyPair, encoding
│   ├── Scynapse.Security.Assertions/ — SignedAssertion, builder, claims
│   ├── Scynapse.Security.Verification/ — verifier, stores, attenuation
│   ├── Scynapse.Security.Orleans/ — call filters, policy provider, attributes
│   └── Scynapse.Security.Transport/ — TLS/mTLS configuration, bootstrap
```

### Ed25519 → X.509 Bridge (Critical Implementation Detail)

TLS requires X.509 certificates. Ed25519 keys aren't directly X.509 certs. The bridge:

1. Generate a self-signed X.509 certificate where the key is the Ed25519 keypair.
2. .NET's `X509Certificate2` supports Ed25519 keys (via `CertificateRequest` API in .NET 7+).
3. The certificate's Subject contains the Scynapse key type and encoded public key.
4. The custom TLS validation callback ignores CA chain validation and instead:
   a. Extracts the peer's Ed25519 public key from their certificate.
   b. Looks up assertion chain for that public key.
   c. Verifies the chain against trusted roots.
5. This gives us mTLS with Ed25519 identities while remaining TLS-compatible.

```csharp
// Sketch: creating an Ed25519 self-signed cert for TLS
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

// NSec.Cryptography provides the Ed25519 key
// .NET's CertificateRequest can create a self-signed cert from it
// The custom SslServerAuthenticationOptions.RemoteCertificateValidationCallback
// does assertion-chain verification instead of CA verification
```

---

## NuGet Dependencies

**Core (required):**
- `NSec.Cryptography` (v25.4.0) — Ed25519, X25519, Blake2b-256, ChaCha20-Poly1305. Based on libsodium. Strongly typed key model. THE primary crypto library.
- `PeterO.Cbor` — CBOR serialization for assertion wire format (initial choice, plastic). Pure C#, no native dependencies.

**Key encoding:**
- `SimpleBase` — Base32/Base58 encoding. For typed key strings (NATS-style).
- CRC16 implementation — either custom (trivial) or small NuGet package.

**Already in Orleans (no additional dependency):**
- `System.Net.Security` (SslStream) — TLS/mTLS transport
- `System.Security.Cryptography` — X.509 cert generation for Ed25519→TLS bridge (.NET 7+)
- `System.IO.Pipelines` — binary protocol handling
- Orleans serialization pipeline — for RequestContext transport
- ASP.NET Core Kestrel transport — connection lifecycle

**Reference (not runtime dependency, but code patterns to study):**
- `nkeys.net` (nats-io/nkeys.net) — NATS NKeys encoding/signing patterns. Study their Base32+CRC16 implementation.
- `jwt.net` (nats-io/jwt.net) — NATS JWT claim encoding patterns.

**Future (Phase 2+):**
- Shamir's Secret Sharing library — for threshold de-anonymization. Evaluate when needed.
- Distributed assertion store backing — depends on CNS implementation choice.

## Test Strategy

### Layer 0: Crypto Primitives
- Unit tests: key generation, signing, verification, encoding/decoding roundtrips.
- Cross-verify: sign with NSec, verify with nkeys.net (and vice versa) to ensure Ed25519 interop.
- Property tests: random data signing always verifies; tampered data never verifies.

### Layer 1: Signed Assertion Core
- Unit tests: assertion creation, serialization/deserialization roundtrip, content hash computation.
- Verify that content hash changes when any field changes.
- Verify that deserialized assertion signature still verifies.

### Layer 2: Chain Verification
- Unit tests with hand-crafted assertion chains:
  - Valid 1-deep chain (root identity → capability)
  - Valid 3-deep chain (root → delegation → delegation → capability)
  - Broken chain (chain gap — parent.subject != child.issuer)
  - Expired assertion (time bounds)
  - Overly-broad delegation (attenuation violation)
  - Replay detection (same nonce twice)
  - Unknown root (chain doesn't reach a trusted root)
- Integration test: full chain from "operator creates node identity" → "node creates session" → "session presents CCap to grain."

### Layer 3: Transport
- Integration test: two silos establish mTLS connection using Ed25519-derived certs.
- Test: silo rejects connection from peer with invalid assertion chain.
- Test: gateway accepts TLS connection, runs bootstrap protocol, upgrades to mTLS.
- Test: gateway rejects bootstrap from peer with forged identity.

### Layer 4: Orleans Integration
- Integration test: grain call with valid CCap succeeds.
- Integration test: grain call without CCap is rejected (when policy requires it).
- Integration test: grain call with expired CCap is rejected.
- Integration test: grain call with CCap for wrong grain type/method is rejected.
- Integration test: RequestContext flows CCap across silo boundaries.
- Integration test: grain reads verified caller identity via extension methods.
- Test: anonymous call to grain with `AllowAnonymous = true` succeeds.
- Test: CCap bearer verification (stolen CCap with wrong bearer key is rejected).

### Test Infrastructure
- Use Orleans TestCluster for integration tests (multiple silos in-process).
- Custom `IAssertionStore` for testing that pre-loads known assertion chains.
- Helper methods to generate test keypairs and assertion chains.

---

### What Changes When the Component Model Arrives

Phase 1 uses Grain types as the security boundary (approximation). Phase 2 uses Components.

**Key differences:**

1. **Trust boundary upgrade:** Phase 1: each grain type can declare a security policy. Phase 2: each Component declares a security policy, and ALL grain types within that Component inherit/refine it. The Component IS the security domain; grains within it are sub-units.

2. **Capability vocabulary becomes Component-scoped:** In Phase 1, capability resource URIs reference grain types and methods. In Phase 2, they reference Component types, and the Component's type definition includes its capability vocabulary. This is a straightforward URI namespace change, not a structural change.

3. **Assertion resolution integrates with CNS:** In Phase 1, assertion resolution is local (in-memory store, pre-loaded bootstrap assertions). In Phase 2, the CNS (Scynapse Name System) becomes the distributed assertion store. When you look up a Component, you also get its identity assertions and public keys. Discovery and key distribution merge.

4. **Component isolation on same Node:** In Phase 1, grains on the same silo share the same process and trust boundary. In Phase 2, Components on the same Node are distinct security domains with independent verification. This may require changes to the message pipeline — intra-silo calls between different Components would need to go through security verification even though they don't cross a network boundary.

5. **Component security policy is richer:** In Phase 1, grain security is via attributes (`[SecurityPolicy]`, `[RequireCapability]`). In Phase 2, security policy is part of the Component's type definition metadata — a structured declaration that includes anonymity policy, transport requirements, channel binding, and capability vocabulary. This may be a configuration object rather than attributes.

### What To Preserve From Phase 1 Into Phase 2

- **All of Layer 0 (crypto primitives):** KeyPair, encoding — unchanged.
- **All of Layer 1 (assertion core):** SignedAssertion format, builder, serialization — unchanged.
- **All of Layer 2 (verification):** Verifier, chain walking, attenuation — unchanged. The IAssertionStore interface gains a distributed implementation (CNS-backed) but the interface stays the same.
- **The grain call filter pattern:** Still the enforcement point. The filter logic generalizes from "check grain type" to "check Component + grain type" but the filter infrastructure is the same.
- **The mTLS transport:** Unchanged. TLS doesn't know or care about Components.

### What Changes

- **Policy provider:** From `AttributeBasedPolicyProvider` to `ComponentModelPolicyProvider`. Reads policy from Component type definitions instead of .NET attributes.
- **Capability URIs:** From `grain://MyApp.MyGrain/MethodName` to `component://ComponentType/GrainType/Method` (or whatever the Component Model's addressing scheme becomes).
- **Assertion store:** From `InMemoryAssertionStore` to a distributed implementation backed by the CNS. The `IAssertionStore` interface is designed for this — just swap the implementation.
- **Intra-Node isolation:** New mechanism needed. May involve process isolation (separate AppDomains/processes per Component) or lightweight in-process isolation with mandatory call filter verification for cross-Component calls within the same process.

### Migration Path

Phase 1 code is explicitly designed so that Phase 2 is an extension, not a rewrite:

- `IAssertionStore` is an interface — swap implementation.
- `IGrainSecurityPolicyProvider` is an interface — swap implementation.
- Call filters are registered via DI — swap registration.
- Assertion format is unchanged — Phase 1 assertions are valid Phase 2 assertions.
- Transport is unchanged — mTLS doesn't care about Components.

The only structural change is adding Component-awareness to the policy lookup and capability URI namespace. Everything else is implementation swaps behind stable interfaces.
