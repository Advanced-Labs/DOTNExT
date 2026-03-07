# Scynapse v1

## What This Document Is

This is the developer documentation for Scynapse v1 — the first versioned release of the Scynapse platform. It covers everything Scynapse adds over Orleans, how to use it, how it works internally, and where it's heading.

**Audience:** Developers joining the Scynapse project. You're assumed to know Microsoft Orleans well (silos, grains, grain interfaces, RequestContext, call filters, clustering, persistence, streams). This document only covers what Scynapse changes or adds.

**What Scynapse is, in one paragraph:** Scynapse is a fork of Microsoft Orleans evolving toward a distributed platform where *types form networks*. A Component type running on Nodes A, B, C forms a single virtual network — the Component IS the network. v1 is the first milestone: Orleans with a cryptographic security system that has no equivalent in stock Orleans. Scynapse v1 runs on .NET 9 and is source-compatible with Orleans grain code, meaning existing Orleans grains work unchanged on Scynapse but can opt into the security system via attributes. Future versions introduce the Component Model, CNS (Scynapse Name System), and the full "Component is the network" architecture.

**Naming:** The name "Scynapse" is the project's permanent name. Earlier iterations were called "NewOrleans." References to NewOrleans in the codebase are legacy.

**Scope:** Everything described in this document is additive over Orleans. If something isn't mentioned here, it works exactly as it does in Orleans.

---

## Part 1: What Scynapse v1 Adds Over Orleans

### 1.1 The Security System (Major — the focus of v1)

Orleans has no built-in security model. You can enable TLS, and you can build your own auth with call filters, but there's nothing out of the box. Scynapse v1 adds a complete cryptographic security system:

- **Ed25519 cryptographic identity** for every entity (silo, client, user, organization)
- **Signed Assertions** — a unified token format for identity, capabilities, delegation, relations, and revocation
- **Crypto-Capabilities (CCaps)** — bearer tokens that grant specific permissions on specific grains, verifiable without a central authority
- **Grain call filter enforcement** — every grain call is verified against the caller's CCap
- **mTLS transport** between silos with Ed25519-derived certificates
- **Subject namespace** — dot-separated hierarchical grain addressing for security (and future name resolution)
- **Client authentication** via SecurityGatewayGrain
- **CLI tooling** (`scy`) for key management, assertion creation, and deployment configuration
- **Configuration file loading** — security configurable from `appsettings.json`
- **DevelopmentMode** — zero-friction security setup for development

### 1.2 Scynapse-Specific Orleans Extensions (Pre-existing, from NewOrleans era)

These existed before v1 and are part of the Scynapse fork but are not the focus of this document:

- **Naturalized C# events** on grains (bridged to SMS streams)
- **StateTask\<T\> properties** on grains (remote property access via code-generated Get/Set methods)
- **Dynamic grain access** (DynamicGrainReference with DLR dispatch)
- **Plugin grain loading** (runtime assembly loading for grain types)
- **GrainTypeDirectory** (singleton grain cataloging all grain types in the cluster)

v1 adds security coverage for these features — StateTask properties go through call filters automatically, IPluginGrainLoader and IGrainTypeDirectoryGrain are now access-controlled.

**Protected system grains:**
- `IPluginGrainLoader` — requires admin CCap (prevents arbitrary assembly loading)
- `IGrainTypeDirectoryGrain` — requires authentication (prevents topology enumeration)

---

## Part 2: Security Concepts

### 2.1 The Core Idea: Unified Signed Assertions

In most systems, identity ("who are you") and authorization ("what can you do") are separate subsystems with separate tokens, separate verification, separate libraries. Scynapse unifies them.

A **Signed Assertion** is the single primitive for everything:

| Claim Type | What It Means | Example |
|-----------|---------------|---------|
| Identity (0x01) | "I exist as this key" | Self-signed: org proves it exists |
| Capability (0x02) | "Subject may do action on resource" | Alice can `read` on `scynapse.app.IOrderGrain.*` |
| Delegation (0x03) | "Subject may issue further assertions" | Org delegates to Node: "you may issue CCaps" |
| Relation (0x04) | "Issuer recognizes subject in context" | Org recognizes Alice as "member" |
| Revocation (0x05) | "Target assertion is revoked" | Org revokes Node's delegation |
| Impersonation (0x06) | "Subject may act as issuer within scope" | Service may act as Alice for read operations |
| Extension (0xFF) | Custom claim type | Future use |

They all share the same structure, the same verification algorithm, the same serialization format. One library, one code path, one set of tests. Identity is the degenerate/root case of capability — a self-signed assertion where issuer equals subject, meaning "I exist."

Every assertion has:
- An **issuer** (Ed25519 public key of who made this claim)
- A **subject** (Ed25519 public key of who this claim is about)
- A **claim** (what's being asserted — identity, capability, delegation, etc.)
- A **scope** (time bounds, nonce for replay prevention)
- **Proofs** (content hashes of parent assertions forming the delegation chain)
- **Extensions** (arbitrary key-value data for future use)
- A **signature** (Ed25519 signature by the issuer over all fields)
- An **ID** (Blake2b-256 hash of the content, making it content-addressed)

Assertions are serialized to CBOR (RFC 7049) using CTAP2 canonical form — deterministic serialization where same content always produces the same bytes, which is essential because the ID is a hash of those bytes.

**Why this matters:** An identity assertion IS a capability — the most fundamental one (the capability to exist in the system). A delegation IS a capability (the capability to issue further capabilities). This isn't a theoretical nicety — it halves the implementation and eliminates an entire class of bugs where "identity verification" and "capability verification" disagree.

### 2.2 Ed25519 Identity

Every entity in Scynapse has an Ed25519 keypair. The public key IS the identity. There is no separate identity registry.

```
Organization key:  OABC123DEF456...  (prefix 'O' = Organization)
Node key:          NXYZ789QRS012...  (prefix 'N' = Node)
User key:          UALICE001BCD...   (prefix 'U' = User)
```

Key prefixes are human-readable and tell you what kind of entity the key belongs to. The encoding uses Base32 + CRC16 checksums, inspired by NATS NKeys.

**Key types available:**

| Prefix | Type | Description |
|--------|------|-------------|
| O | Organization | Root of trust. Signs delegations to nodes and users. |
| D | Domain | Sub-division of an organization (optional hierarchy level) |
| N | Node | A running Scynapse silo instance |
| T | ComponentType | (Phase 2) A Component's type-level identity |
| I | Instance | A specific grain/component activation |
| U | User | A human or external client identity |
| X | Encryption | X25519 key for encrypted channels |
| P | Seed | Private seed encoding prefix (never transmitted) |

**Key Operations:**

```csharp
using var nodeKey = ScynapseKeyPair.Generate(ScynapseKeyType.Node);
byte[] signature = nodeKey.Sign(data);
bool valid = nodeKey.Verify(data, signature);
byte[] seed = nodeKey.ExportSeed();
using var restored = ScynapseKeyPair.FromSeed(seed, ScynapseKeyType.Node);
using var verifyOnly = ScynapseKeyPair.FromPublicKey(pubKeyBytes, ScynapseKeyType.Node);
```

**The trust hierarchy flows through delegation assertions:**

```
Organization (self-signed identity — the root of trust)
  ├── delegates to → Node 1 (silo)
  ├── delegates to → Node 2 (silo)
  ├── delegates to → Alice (user)
  │     └── CCap: can read/write scynapse.app.IOrderGrain.*
  └── delegates to → Bob (user)
        └── CCap: can read scynapse.app.>
```

### 2.3 Crypto-Capabilities (CCaps)

A CCap is a Signed Assertion with claim type Capability. It says: "Subject S may perform action A on resource R, as attested by Issuer I, with proof chain back to the resource owner."

```
CCap {
    issuer:   OABC123...              // Organization (has authority over the resource)
    subject:  UALICE001...            // Alice (gets the capability)
    claim: {
        type:     Capability
        resource: "scynapse.app.IOrderGrain.*"   // All methods on IOrderGrain
        action:   "read"                          // Read action
    }
    expires_at: 1741910400            // Unix timestamp
    proofs:     [blake2b:a1b2c3...]   // References to parent assertions in the chain
    signature:  <Ed25519 signature by issuer>
}
```

**CCaps are:**
- **Self-contained** — carry their own proof chain. Verifiable without calling a central server.
- **Delegatable** — Alice can delegate a narrower CCap to Carol (e.g., only `read` on one specific grain instance).
- **Attenuatable** — each delegation can only narrow, never widen. Alice can't delegate `write` if she only has `read`.
- **Bearer-verified** — presenting a CCap requires proving you own the subject's private key (sign a challenge nonce).
- **Content-addressed** — each assertion has a Blake2b-256 hash ID. Immutable. References are by hash, not by location.

**Creating CCaps programmatically:**

```csharp
var ccap = AssertionBuilder.CreateCapability(
    orgKey, userKey.PublicKeyBytes,
    "scynapse.app.IOrderGrain.>", "read",
    proofs: new[] { orgIdentity.Id.ToArray() },
    expiresAt: DateTimeOffset.UtcNow.AddDays(30).ToUnixTimeSeconds());
```

### 2.4 Subject Namespace

Resources in CCaps use a dot-separated hierarchical namespace with NATS-style wildcards:

```
scynapse.system.membership          # Orleans MembershipTable
scynapse.system.security.gateway    # SecurityGatewayGrain
scynapse.system.plugins             # IPluginGrainLoader
scynapse.app.IOrderGrain.PlaceOrder # Specific method
scynapse.app.IOrderGrain.*          # All methods on IOrderGrain
scynapse.app.>                      # All application grains
```

**Wildcard rules:**
- `.` separates segments
- `*` matches exactly one segment: `scynapse.app.*.GetItem` matches `scynapse.app.IOrderGrain.GetItem` and `scynapse.app.IInventoryGrain.GetItem`
- `>` matches one or more trailing segments: `scynapse.app.>` matches everything under `scynapse.app`

The namespace is inferred automatically from grain interfaces — you don't declare it. `IOrderGrain` → `scynapse.app.IOrderGrain`. Method `PlaceOrder` → `scynapse.app.IOrderGrain.PlaceOrder`. This inference happens in `GrainResourceInference`.

### 2.5 Trust Model

Scynapse has a hybrid trust model with two levels:

**Node-Level Trust (silo-to-silo):** If a silo's node key has a valid delegation chain from a trusted root, grain calls from that silo are allowed by default. This matches Orleans's inherent model where silos in a cluster trust each other. The silo proved its identity when joining the cluster (via mTLS + assertion chain). Subsequent grain calls from that silo don't need per-call CCaps.

**Caller-Level Trust (client-to-silo):** External clients must present a CCap matching the target grain's `[RequireCapability]` attribute. The CCap is verified: signature checked, chain walked back to trusted root, action/resource match confirmed, bearer proof validated.

**Original Caller Propagation:** When Client → GrainA → GrainB, the original client's identity flows through the entire call chain via RequestContext. GrainB sees the node's identity as the direct caller (trusted) and the original client's identity for audit/authorization.

**Strict Mode (opt-in):** Grains annotated with `[SecurityPolicy(RequiresCallerCapability = true)]` require per-call CCap verification even from trusted silos. Use for high-security grains where you need to verify the end-user, not just the silo.

### 2.6 The CCap Wallet

Each silo and client has an `InMemoryCCapWallet` — a thread-safe store of CCaps. When an outgoing grain call is made, the wallet automatically selects the best matching CCap:

1. Outgoing filter infers the target resource URI from the grain interface and method
2. Wallet searches for a CCap whose resource pattern matches and whose action matches
3. Best match is attached to the RequestContext along with the caller's public key and a bearer proof (signature of the CCap ID)
4. On the receiving silo, the incoming filter deserializes and verifies everything

The wallet filters out expired CCaps automatically (fail-fast on the client side). If no matching CCap exists, the call proceeds without one and the receiving silo rejects it.

### 2.7 Verification

One algorithm verifies all assertion types:

1. **Content hash** — assertion ID matches Blake2b-256 of content fields
2. **Signature** — Ed25519 signature verifies against issuer's public key
3. **Time bounds** — `not_before` and `expires_at` checked against current time
4. **Replay** — nonce checked against replay prevention store
5. **Chain walk** — for each proof reference:
   - Resolve parent assertion by content hash
   - Verify parent recursively (same algorithm)
   - Chain continuity: parent's subject == this assertion's issuer
   - Attenuation: this assertion's scope is within parent's scope
6. **Root termination** — chain must end at a self-signed identity in the trusted roots set

Max chain depth is configurable (default 32) to prevent malicious deep chains.

### 2.8 Transport Security

Silo-to-silo connections use mTLS. Each silo has a TLS certificate containing:
- An ECDSA P-256 key for the TLS handshake (required because .NET's SslStream doesn't support Ed25519 for TLS yet)
- The silo's Ed25519 public key embedded in a custom X.509 extension (OID `1.3.6.1.4.1.99999.1.1`)

The ECDSA key is transport plumbing with no security meaning. The real identity is the Ed25519 key in the extension. This is documented in the code as a platform workaround to be removed when .NET supports Ed25519 in TLS handshakes.

The custom TLS validation callback extracts the Ed25519 key from the peer's certificate extension and checks it against the pre-validated peer cache (a set of known-trusted node public keys built from assertion chain verification at startup).

---

## Part 3: Developer Guide

### 3.1 Setting Up Security for Development (Quick Start)

**Option A: DevelopmentMode (zero config)**

```csharp
// Silo
siloBuilder.UseScynapseSecurityDevelopmentMode();

// Client
clientBuilder.UseScynapseSecurityDevelopmentMode();
```

This auto-generates all keys, assertions, and CCaps. Logs a warning on startup. No TLS. Everything is auto-trusted. Use for local development only.

**Option B: CLI + Config (closer to production)**

```bash
# Install the CLI
dotnet tool install -g scy

# Generate development setup
scy dev quickstart --dir ./dev-security

# This generates:
# ./dev-security/org.seed, org.pub, org-identity.assertion
# ./dev-security/silo-1/node.seed, node.pub, node-delegation.assertion, silo-security.json
# ./dev-security/user/user.seed, user.pub, user-delegation.assertion, wildcard.ccap, client-security.json
```

Then in your silo:
```csharp
siloBuilder.UseScynapseSecurity(
    builder.Configuration.GetSection("ScynapseSecurity"));
```

With `appsettings.json`:
```json
{
  "ScynapseSecurity": {
    "NodeSeedFile": "./dev-security/silo-1/node.seed",
    "TrustedRoots": ["./dev-security/org.pub"],
    "BootstrapAssertionFiles": [
      "./dev-security/org-identity.assertion",
      "./dev-security/silo-1/node-delegation.assertion"
    ],
    "EnableTls": false,
    "DevelopmentMode": false
  }
}
```

### 3.2 Writing Secured Grains

```csharp
// 1. Annotate the interface
[SecurityPolicy(RequiresAuthentication = true)]
public interface IOrderGrain : IGrainWithStringKey
{
    [RequireCapability(Action = "read")]
    Task<OrderDetails> GetDetailsAsync();

    [RequireCapability(Action = "write")]
    Task PlaceOrderAsync(OrderRequest request);

    [RequireCapability(Action = "admin")]
    Task CancelOrderAsync(string reason);
}

// 2. Implement normally — security is transparent
public class OrderGrain : Grain, IOrderGrain
{
    public Task<OrderDetails> GetDetailsAsync()
    {
        // Read who's calling (direct caller — node or client)
        var caller = this.GetCallerPublicKey();
        
        // Read the original end-user (if this is a grain-to-grain call,
        // this is the user who initiated the chain)
        var originalCaller = this.GetOriginalCallerPublicKey();
        
        // Read the CCap that authorized this call
        var callerCCap = this.GetCallerCapability();
        
        return Task.FromResult(_state.State.Details);
    }

    public async Task PlaceOrderAsync(OrderRequest request)
    {
        // Grain-to-grain call — node trust handles auth,
        // original caller identity propagates automatically
        var inventory = GrainFactory.GetGrain<IInventoryGrain>(request.ItemId);
        await inventory.ReserveAsync(request.Quantity);
    }

    public Task CancelOrderAsync(string reason)
    {
        // Only callers with "admin" CCap reach here
        _state.State.Cancelled = true;
        return _state.WriteStateAsync();
    }
}
```

**What happens when a client calls `GetDetailsAsync()`:**
1. Client's outgoing filter: `GrainResourceInference` → `scynapse.app.IOrderGrain.GetDetailsAsync`
2. Wallet finds CCap matching resource + action `read`
3. Filter attaches: caller public key, serialized CCap bytes, bearer proof (CCap ID signed with caller's key)
4. Silo's incoming filter: deserializes CCap, verifies signature + chain + time bounds + attenuation
5. Checks `[RequireCapability(Action = "read")]` matches CCap's action
6. Verifies bearer proof (caller owns the key in the CCap)
7. Sets verified context in RequestContext
8. Grain method executes

**Grains without security attributes:** Default to `AllowAnonymous = true`. This is necessary because Orleans system grains (MembershipTable, directory, reminders) have no attributes. Only explicitly annotated grains enforce security.

### 3.3 High-Security Grains (Strict Mode)

For grains where you need to verify the end-user even when called from another grain within the cluster:

```csharp
[SecurityPolicy(RequiresAuthentication = true, RequiresCallerCapability = true)]
public interface IFinancialGrain : IGrainWithStringKey
{
    [RequireCapability(Action = "transfer")]
    Task TransferAsync(decimal amount, string destination);
}
```

With `RequiresCallerCapability = true`, the incoming filter requires a CCap even from trusted silos. The outgoing filter on the calling silo must have a matching CCap in its wallet (or propagate the original client's CCap if it matches).

### 3.4 Issuing CCaps from Grain Code

Grains can issue CCaps to their callers at runtime:

```csharp
[SecurityPolicy(RequiresAuthentication = true)]
public interface ISubscriptionGrain : IGrainWithStringKey
{
    [RequireCapability(Action = "subscribe")]
    Task<byte[]> SubscribeAsync(); // Returns serialized CCap
}

public class SubscriptionGrain : Grain, ISubscriptionGrain
{
    public Task<byte[]> SubscribeAsync()
    {
        var ccap = this.IssueCCapToCaller(
            action: "read",
            resource: "scynapse.app.IContentGrain.*",
            expiresAt: DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds());
        
        return Task.FromResult(ccap.Serialize());
    }
}

// Client side — store the returned CCap
var subGrain = client.GetGrain<ISubscriptionGrain>("premium");
var ccapBytes = await subGrain.SubscribeAsync();
var wallet = client.ServiceProvider.GetRequiredService<ICCapWallet>();
wallet.Store(SignedAssertion.Deserialize(ccapBytes));
// Now can access IContentGrain with the newly acquired CCap
```

The CCap is signed by the silo's node key. Its proof chain goes: org-identity → node-delegation → CCap. Any silo trusting the same org root will accept it.

### 3.5 Client Authentication (Login Flow)

The `SecurityGatewayGrain` solves the bootstrap paradox: how does a client get its first CCap? The gateway requires identity (a valid delegation chain) but not a CCap — it's the one grain that issues CCaps without requiring one.

```csharp
[SecurityPolicy(RequiresAuthentication = true)]
public interface ISecurityGatewayGrain : IGrainWithStringKey
{
    Task<CCapBundle> AuthenticateAsync(byte[] delegationChainCbor);
    Task<byte[]?> RequestCapabilityAsync(string resource, string action);
    Task<CCapBundle> RefreshAsync(byte[] expiringCCapsCbor);
}
```

For clients that don't have pre-provisioned CCaps:

```csharp
var client = new ClientBuilder()
    .UseScynapseSecurity(config.GetSection("ScynapseSecurity"))
    // Config has key + delegation but NO bootstrapCapabilities
    .Build();
await client.Connect();

// Authenticate via gateway
var gateway = client.GetGrain<ISecurityGatewayGrain>("default");
var bundle = await gateway.AuthenticateAsync(myDelegationChainCbor);

// Store received CCaps
var wallet = client.ServiceProvider.GetRequiredService<ICCapWallet>();
foreach (var ccapBytes in bundle.Capabilities)
    wallet.Store(SignedAssertion.Deserialize(ccapBytes));

// Now make secured grain calls
var grain = client.GetGrain<IOrderGrain>("order-1");
var details = await grain.GetDetailsAsync();
```

The `DefaultSecurityGatewayGrain` issues CCaps based on the delegation chain's scope. Override with your own implementation for custom authorization logic:

```csharp
// Register custom gateway in silo DI
services.AddSingleton<ISecurityGatewayGrain, MyCustomGatewayGrain>();
```

### 3.6 Error Handling

Security failures throw `ScynapseSecurityException` with a structured `FailureCode`:

```csharp
try
{
    await grain.CancelOrderAsync("test");
}
catch (ScynapseSecurityException ex)
{
    switch (ex.FailureCode)
    {
        case SecurityFailureCode.MissingAuthentication:
            // No CCap in wallet matching this grain/action
            break;
        case SecurityFailureCode.InsufficientCapability:
            // CCap exists but action doesn't match
            break;
        case SecurityFailureCode.ExpiredAssertion:
            // CCap expired (caught client-side by wallet)
            break;
        case SecurityFailureCode.BearerProofFailed:
            // Caller doesn't own the CCap's subject key
            break;
        case SecurityFailureCode.ChainVerificationFailed:
            // CCap's proof chain is invalid
            break;
        case SecurityFailureCode.UntrustedRoot:
            // Chain doesn't reach a trusted root
            break;
    }
}
```

The incoming filter also logs structured events via `ILogger` for diagnostics.

---

## Part 4: The Scy CLI

### 4.1 Installation

```bash
dotnet tool install -g scy
```

### 4.2 Organization Setup

An **organization** is a trust domain — the root from which all authority flows. In a typical deployment, you have one organization per independent deployment.

```bash
# Create organization
scy init org --name "Acme Corp" --dir ./acme
```

This creates:
```
./acme/
├── org.seed              # Private key (PROTECT THIS)
├── org.pub               # Public key (share freely)
└── org-identity.assertion # Self-signed identity assertion
```

### 4.3 Node (Silo) Setup

```bash
scy init node --name silo-1 --org ./acme
```

This creates:
```
./acme/silo-1/
├── node.seed                    # Node private key
├── node.pub                     # Node public key
├── node-delegation.assertion    # Org → Node delegation
└── silo-security.json           # Ready-to-use config for UseScynapseSecurity
```

The `node-delegation.assertion` is automatically signed by the org key (read from `./acme/org.seed`). The delegation chain is correct by construction — you can't get it wrong.

### 4.4 User Setup

```bash
scy init user --name alice --org ./acme
```

Creates:
```
./acme/alice/
├── user.seed
├── user.pub
└── user-delegation.assertion   # Org → Alice delegation
```

### 4.5 Granting Capabilities

```bash
# Alice can read/write all order grain methods
scy grant --to alice --resource "scynapse.app.IOrderGrain.>" --action "read,write" --org ./acme

# Alice can read any inventory grain method
scy grant --to alice --resource "scynapse.app.IInventoryGrain.*" --action "read" --org ./acme
```

CCaps are saved to `./acme/alice/ccaps/`.

### 4.6 Deployment Bundles

```bash
# Generate everything silo-1 needs to deploy
scy bundle --node silo-1 --org ./acme
# Output: ./acme/silo-1/deploy/ (config + all peer assertions)

# Generate everything alice needs
scy bundle --user alice --org ./acme
# Output: ./acme/alice/deploy/ (config + delegation + all CCaps)
```

Deploy the `deploy/` directory to the target machine. Point `appsettings.json` at the config file.

### 4.7 Inspecting and Verifying

```bash
# Human-readable view of an assertion (Spectre.Console table)
scy inspect ./acme/alice/ccaps/orders-read.ccap

# Verify an assertion chain against a trusted root
scy verify ./acme/alice/ccaps/orders-read.ccap --root ./acme/org.pub
```

### 4.8 Development Quick Start

```bash
scy dev quickstart --dir ./dev
# Generates everything: org, node, user, delegation chains, wildcard CCaps, configs
# WARNING: development mode keys
```

### 4.9 File Formats

| Extension | Contents | Secret? | Format |
|-----------|----------|---------|--------|
| `.seed` | 1 byte key type + 32 bytes Ed25519 seed | **YES** | Binary |
| `.pub` | Encoded public key string (e.g., `OABC123...`) | No | Text (single line) |
| `.assertion` | Signed Assertion | No | CBOR binary |
| `.ccap` | Signed Assertion (Capability claim type) | Context-dependent | CBOR binary (same as `.assertion`) |
| `-security.json` | UseScynapseSecurity config | No (contains file paths, not secrets) | JSON |

### 4.10 Directory Convention

```
./acme/
├── org.seed, org.pub, org-identity.assertion
├── silo-1/ (node.seed, node.pub, node-delegation.assertion, silo-security.json, deploy/)
├── silo-2/ (...)
└── alice/ (user.seed, user.pub, user-delegation.assertion, ccaps/, deploy/)
```

### 4.11 Complete Operational Workflow

```bash
dotnet tool install -g scy
scy init org --name "MyOrg" --dir ./myorg
scy init node --name silo-1 --org ./myorg
scy init node --name silo-2 --org ./myorg
scy init user --name alice --org ./myorg
scy grant --to alice --resource "scynapse.app.>" --action "read" --org ./myorg
scy bundle --node silo-1 --org ./myorg
scy bundle --user alice --org ./myorg
# Deploy bundles, point appsettings.json, start silos and clients
```

---

## Part 5: Technical Internals

### 5.1 Project Structure

```
src/
├── Scynapse.Security/                    # Core — NO Orleans dependency
│   ├── ScynapseKeyType.cs               # 8 key type enum
│   ├── ScynapseKeyPair.cs               # Ed25519 wrapper (Generate, Sign, Verify, Export)
│   ├── ScynapseKeyEncoding.cs           # Base32 + CRC16 + typed prefix
│   ├── Base32.cs                        # RFC 4648 (custom, no external dep)
│   ├── Crc16.cs                         # CRC-16/CCITT-FALSE
│   ├── Assertions/
│   │   ├── ClaimType.cs                 # 7 claim types
│   │   ├── Claims.cs                    # CapabilityClaim, DelegationClaim, etc.
│   │   ├── SignedAssertion.cs           # Immutable, content-addressed
│   │   ├── AssertionBuilder.cs          # Fluent builder + convenience factories
│   │   └── AssertionSerializer.cs       # CBOR (CTAP2 canonical form)
│   ├── Verification/
│   │   ├── IAssertionStore.cs           # Async interface
│   │   ├── InMemoryAssertionStore.cs    # Thread-safe, with revocation
│   │   ├── INonceStore.cs
│   │   ├── InMemoryNonceStore.cs        # TTL-based expiry
│   │   ├── IAttenuationChecker.cs
│   │   ├── DefaultAttenuationChecker.cs # Pattern matching, narrowing, temporal
│   │   ├── AssertionVerifier.cs         # Recursive chain walker (max depth 32)
│   │   ├── VerificationResult.cs
│   │   ├── SubjectNameMatcher.cs        # Dot-separated with *.> wildcards
│   │   └── ByteMemoryEqualityComparer.cs
│   ├── Transport/
│   │   ├── ScynapseCertificateFactory.cs      # ECDSA bridge + Ed25519 extension
│   │   ├── ScynapseRemoteCertificateValidator.cs
│   │   └── ScynapseSecurityOptions.cs
│   ├── IO/
│   │   ├── KeyFileFormat.cs             # .seed / .pub file I/O
│   │   └── AssertionFileFormat.cs       # .assertion / .ccap file I/O
│   └── Configuration/
│       └── SecurityConfigurationLoader.cs  # IConfiguration binding
│
├── Scynapse.Security.Orleans/           # Orleans integration
│   ├── ScynapseIncomingCallFilter.cs    # THE enforcement point
│   ├── ScynapseOutgoingCallFilter.cs    # Wallet-based CCap selection + caller propagation
│   ├── SecurityPolicyAttribute.cs       # [SecurityPolicy(...)]
│   ├── RequireCapabilityAttribute.cs    # [RequireCapability(Action = "...")]
│   ├── GrainSecurityPolicy.cs
│   ├── AttributeBasedPolicyProvider.cs  # Reads attributes, caches per type
│   ├── GrainSecurityExtensions.cs       # GetCallerPublicKey, GetOriginalCallerPublicKey, IssueCCapToCaller
│   ├── GrainResourceInference.cs        # Interface → scynapse.app.{type}.{method}
│   ├── ICCapWallet.cs
│   ├── InMemoryCCapWallet.cs            # Thread-safe, expiry-aware, wildcard matching
│   ├── ISecurityGatewayGrain.cs         # Authentication entry point
│   ├── DefaultSecurityGatewayGrain.cs
│   ├── ScynapseSecurityException.cs     # Structured FailureCode
│   ├── SecurityFailureCode.cs           # Enum of all failure types
│   ├── DevelopmentModeHelper.cs         # Auto-generation for dev mode
│   ├── ScynapseSecurityLifecycleParticipant.cs
│   ├── ScynapseSecuritySiloBuilderExtensions.cs   # UseScynapseSecurity on ISiloBuilder
│   └── ScynapseSecurityClientBuilderExtensions.cs  # UseScynapseSecurity on IClientBuilder
│
├── Scy/                                 # CLI tool
│   ├── Scy.csproj                       # dotnet tool, Spectre.Console.Cli
│   ├── Program.cs
│   ├── Commands/
│   │   ├── InitCommand.cs               # init org, init node, init user
│   │   ├── GrantCommand.cs              # issue CCaps
│   │   ├── BundleCommand.cs             # deployment bundles
│   │   ├── InspectCommand.cs            # human-readable assertion viewer
│   │   ├── VerifyCommand.cs             # chain verification
│   │   └── DevCommand.cs               # dev quickstart
│   ├── IO/
│   │   └── ConfigFileFormat.cs          # JSON config generation
│   ├── Context/
│   │   └── OrgContext.cs                # Reads org directory structure
│   └── Rendering/
│       └── SpectreRenderers.cs          # Tables for inspect output
│
test/
├── Scynapse.Security.Tests/            # 173 unit tests
├── Scynapse.Security.Orleans.Tests/    # 31 unit tests
└── Scynapse.Security.Integration.Tests/ # 9 integration tests (real TestCluster)
```

### 5.2 NuGet Dependencies

| Package | Version | Purpose |
|---------|---------|---------|
| `NSec.Cryptography` | 25.4.0 | Ed25519, Blake2b-256, X25519 (libsodium wrapper) |
| `PeterO.Cbor` | 4.5.5 | CBOR serialization for assertions |
| `Spectre.Console` | 0.49.x | CLI TUI rendering (Scy.exe only) |
| `Spectre.Console.Cli` | 0.49.x | CLI command routing (Scy.exe only) |
| `System.CommandLine` | 2.0.0-beta4 | CLI argument parsing (Scy.exe only) |

Custom implementations (no external dependency): Base32 (RFC 4648), CRC-16/CCITT-FALSE.

**NuGet source configuration:** `PeterO.Cbor` and Spectre packages resolve from nuget.org via package source mapping in `NuGet.config`. All other packages resolve from Azure DevOps feeds. The mapping is surgical — only specified package prefixes go to nuget.org.

### 5.3 Serialization

Assertions are serialized as CBOR (Concise Binary Object Representation) in CTAP2 canonical form:
- Integer map keys (not string keys) for compactness
- Sorted extension keys for deterministic output
- Same content always produces same bytes → same Blake2b-256 hash

The serialization is in `AssertionSerializer`. The format is internal to Scynapse and explicitly marked as plastic — it can change. The important invariant is: serialize → hash → sign → deserialize → verify-signature always works.

### 5.4 The Incoming Call Filter (Enforcement Point)

`ScynapseIncomingCallFilter` is the single enforcement point. Every grain call passes through it. The logic:

```
Is caller a trusted Node (valid delegation chain from trusted root)?
├── YES: Does grain have RequiresCallerCapability = true?
│   ├── YES: Require CCap verification (strict mode)
│   └── NO: Allow (node trust)
└── NO: Does grain have RequiresAuthentication = true?
    ├── YES: Require CCap verification
    │   ├── CCap present + valid: Allow
    │   └── CCap missing/invalid: Reject (ScynapseSecurityException)
    └── NO: Allow (anonymous, default for unannotated grains)
```

### 5.5 The Outgoing Call Filter (CCap Selection)

`ScynapseOutgoingCallFilter` runs on the calling side (silo or client):

1. Infer resource URI: `GrainResourceInference.FromGrainMethod(interfaceType, method)` → `scynapse.app.IOrderGrain.GetDetails`
2. Infer action: read `[RequireCapability(Action = "...")]` from method
3. Search wallet: `wallet.FindCapability(resource, action)` using `SubjectNameMatcher`
4. If found: attach to RequestContext — `CallerPublicKey`, CCap bytes, `BearerProof` (sign CCap ID with caller's private key)
5. If existing `OriginalCallerKey` in RequestContext: preserve it (don't overwrite — this is caller propagation for grain-to-grain)

### 5.6 Configuration Loading

`SecurityConfigurationLoader` reads from `IConfigurationSection`:

- `NodeSeedFile` → reads `.seed` file (33 bytes: 1 type + 32 seed) → creates `ScynapseKeyPair`
- `TrustedRoots` → reads `.pub` files → parses encoded key strings → adds to trusted root set
- `BootstrapAssertionFiles` → reads `.assertion` files → deserializes CBOR → assertion store
- `PeerAssertionDirectory` → reads all `.assertion` files in directory → peer assertion store
- `BootstrapCapabilityFiles` → reads `.ccap` files → wallet
- `EnableTls`, `RequireMutualTls`, `DevelopmentMode` → boolean flags

### 5.7 ECDSA Bridge (TLS Transport Detail)

.NET's SslStream cannot use Ed25519 keys for TLS handshakes (even on .NET 9). The bridge:

1. `ScynapseCertificateFactory.CreateSelfSigned(keypair)` generates:
   - An ephemeral ECDSA P-256 key (for TLS handshake mechanics)
   - A self-signed X.509 certificate with the ECDSA key
   - The Ed25519 public key embedded in custom X.509 extension OID `1.3.6.1.4.1.99999.1.1`
   - Extension value: 1 byte key type + 32 bytes Ed25519 public key

2. `ScynapseRemoteCertificateValidator.Validate(cert)`:
   - Extracts Ed25519 key from the extension
   - Checks against pre-validated peer cache (set of known-trusted public keys)
   - Ignores X.509 CA chain validation (these are self-signed certs)

The ECDSA key has no security meaning in Scynapse. It's plumbing. All identity verification uses the Ed25519 key from the extension. Code is commented with "replace when .NET supports Ed25519 in TLS handshakes."

---

## Part 6: Test Coverage

213 tests total, all passing on .NET 9.

| Suite | Tests | What It Covers |
|-------|-------|---------------|
| `Scynapse.Security.Tests` | 173 | Key generation, encoding, signing, assertions, CBOR serialization, chain verification, attenuation, replay, wildcards, configuration loading |
| `Scynapse.Security.Orleans.Tests` | 31 | Call filters, policy provider, wallet, resource inference, transport certs, subject name matching |
| `Scynapse.Security.Integration.Tests` | 9 | Real TestCluster: valid CCap, caller identity, anonymous access, wrong action, expired CCap, no CCap, cross-silo grain-to-grain calls |

---

## Part 7: Known Limitations

### TLS-Level Assertion Verification

TLS certificates are created and presented, but the `RemoteCertificateValidation` callback uses `AllowAnyRemoteCertificate()` in the current implementation. Identity enforcement happens entirely at the grain call filter level. A rogue node could establish a TLS connection but would be blocked from making any authorized grain calls. This is defense-in-depth we're deferring, not a total security gap.

### Streams and Events

Orleans streams (SMS, EventHub) bypass the grain call filter pipeline. Stream publications and event deliveries are not secured. For security-sensitive data flow, use grain method calls instead of streams. This is documented as a Phase 2 concern.

### In-Memory Stores Only

`InMemoryAssertionStore` and `InMemoryNonceStore` lose everything on silo restart. Revocations don't propagate across silos. For multi-silo production clusters, a distributed assertion store is needed (Phase 2: grain-backed or CNS-backed store).

### No Online Key Rotation

Rotating a node's key requires restart and re-provisioning of delegation chains. Online rotation is Phase 2.

### Wallet CCaps Are Reusable

CCaps in the wallet are presented on every matching call. They're bearer tokens verified by signature, not one-time-use tickets. A CCap remains valid until its `expires_at` timestamp.

### AllowAnonymous Default

Unannotated grains default to anonymous access. This is necessary for Orleans system grains but means developers must explicitly add `[SecurityPolicy(RequiresAuthentication = true)]` to every grain they want secured. Forgetting the attribute leaves a grain open.

### No Automatic CCap Delivery to Caller Wallet

When a grain issues a CCap via `IssueCCapToCaller`, the serialized CCap is returned as bytes. The caller must manually deserialize and store it in their wallet. There is no automatic delivery mechanism. Future enhancement.

### FindBySubjectAsync Returns First Match

`FindBySubjectAsync` in the assertion store returns the first matching assertion. The TLS validator may get the wrong assertion type if multiple assertions exist for the same subject. Fix: filter by claim type. Documented for prioritized fix.

### No Per-Property RequireCapability on StateTask

StateTask properties go through grain-level call filters but cannot have per-property `[RequireCapability]` attributes. Security is grain-level only for StateTask. Future codegen extension.

---

## Part 8: Where v1 Leaves Us — Future Roadmap

### The Component Model (Phase 2)

Scynapse's central vision: "Component is the network." Each Component type forms a virtual network across all Nodes running it. Components are the primary unit of isolation, deployment, and trust.

**What changes when Components arrive:**

| Aspect | v1 (Orleans paradigm) | Phase 2 (Component Model) |
|--------|----------------------|---------------------------|
| Trust boundary | Grain type | Component |
| Security policy | `[SecurityPolicy]` attributes | Component type definition metadata |
| Resource namespace | `scynapse.app.{grain}.{method}` | `scynapse.component.{type}.{grain}.{method}` |
| Policy provider | `AttributeBasedPolicyProvider` | `ComponentModelPolicyProvider` |
| Assertion store | InMemory | CNS-backed distributed |
| Node trust | Silos trust each other | Components in same trust domain trust each other |
| Default policy | AllowAnonymous (Orleans compatibility) | RequireAuth (Component-native) |

**What carries forward unchanged:**
- All of Layer 0 (crypto primitives)
- All of Layer 1 (assertion format, serialization)
- All of Layer 2 (verification, chain walking, attenuation)
- The grain call filter pattern
- The mTLS transport
- The Scy.exe CLI (extended with Component commands)
- The subject namespace scheme (extended with `scynapse.component.*` prefix)

The migration is interface swaps behind stable abstractions: `IAssertionStore`, `IGrainSecurityPolicyProvider`, `IAttenuationChecker`.

### The CNS (Scynapse Name System)

The subject namespace (`scynapse.app.IOrderGrain.PlaceOrder`) is designed to become CNS-resolvable names. In v1, name inference is compile-time (from .NET type names). In Phase 2, the CNS resolves names at runtime across the cluster. The dot-separated hierarchical format with wildcards is chosen for this reason — it's the same pattern NATS uses for subject-based addressing, proven at massive scale.

### Chainmail Routing Fabric

Each Component's virtual network forms a hash ring. The security model maps onto this: membership in a Component's ring is attested by assertion, contact points between rings are trust bridges, and routing capability is bounded by assertion scope. The subject namespace positions become addresses in the routing fabric.

### Democratic Anonymity

The assertion format already reserves space for threshold de-anonymization (Shamir's Secret Sharing). Pseudonymous identities would be first-class — same Ed25519 keys, same CCaps, same verification. The binding between pseudonym and real identity is encrypted and sharded across trustees. Different Components can declare different anonymity acceptance levels. This is designed in the architecture doc but not yet implemented.

### NATS Integration

Scynapse's subject namespace is deliberately NATS-compatible (dot-separated, `*`/`>` wildcards). The security model mirrors NATS's NKeys + JWT. If Scynapse uses NATS for infrastructure messaging (which the architecture envisions), the subject patterns and security tokens could interoperate. A NATS "account" maps to a Scynapse Component. A NATS "subject" maps to a grain address. The mapping is structural, not superficial.

**NATS Alignment:**

| NATS | Scynapse |
|------|----------|
| Subject | Subject namespace URI |
| Account | Component / grain type |
| NKey | ScynapseKeyPair |
| JWT | Signed Assertion |
| `nsc` | `scy` |
| `*` / `>` wildcards | Same wildcards, same semantics |

### Future Features (Data Structures Reserved)

- **Impersonation runtime** — claim type 0x06 exists, verification handles it, runtime integration is Phase 2
- **Channel binding** — extensions field reserves space for TLS session material
- **Component security policies** — the structured `ComponentSecurityPolicy` schema is designed, implementation is Phase 2

---

*Scynapse v1. 213 tests. .NET 9. Branch: `claude/review-scynapse-security-UG35t`.*
