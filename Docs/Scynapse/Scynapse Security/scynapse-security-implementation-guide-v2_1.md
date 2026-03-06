# Scynapse Security -- Implementation Guide

## Meta / Recovery Context

**Version:** 3.0 -- Phase 1 Completion Plan with Full Workflow Analysis
**Date:** 2026-03-06
**Previous version:** 2.0 (Post-Implementation Status Report)
**Companion document:** `scynapse-security-architecture_3.md` -- the *why*. READ THAT FIRST.

**What changed in v3.0:** Comprehensive workflow analysis identified gaps in the current design for all Orleans paradigm actors (grain developers, client developers, silo operators, the Orleans runtime itself). Each gap is analyzed with design options, pros/cons, and a recommended approach. A Scy.exe CLI tool is designed as the operational backbone. Full workflow simulations demonstrate end-to-end correctness.

---

## Current State Summary

**What Scynapse is:** A fork/evolution of Microsoft Orleans (distributed actor platform). Currently uses Orleans's Silo/Client/Grain paradigm. Evolving toward a Component Model (Phase 2).

**What's built (174 tests passing):**
- Layer 0: Cryptographic Primitives (Ed25519, key encoding, Blake2b) -- COMPLETE
- Layer 1: Signed Assertion Core (CBOR, content-addressing, all claim types) -- COMPLETE
- Layer 2: Chain Verification (chain walker, attenuation, nonce, revocation) -- COMPLETE
- Layer 3: Transport Security (ECDSA bridge certs, remote validator) -- COMPLETE with caveats
- Layer 4: Orleans Integration (call filters, wallet, policy, client builder) -- COMPLETE

**What's NOT built (the subject of this document):**
- No CLI tooling for key/assertion management
- TLS transport-level identity verification bypassed (call filter is sole enforcement)
- Cross-silo CCap flow untested
- No configuration file loading (keys/assertions in code only)
- No Scynapse event/property security investigation
- No development-mode auto-generation
- No assertion persistence (in-memory only)

---

## Part 1: Orleans Workflow Actors and Their Security Stories

To verify that our security design is complete, we must trace every actor's complete lifecycle through the system. There are five distinct actors in the Orleans security paradigm:

### Actor 1: The Organization Administrator (Ops)
Sets up the trust hierarchy. Generates root keys, delegates to nodes and users.

### Actor 2: The Silo Operator (DevOps)
Deploys and configures silos. Must provision each silo with identity, delegation chains, and peer knowledge.

### Actor 3: The Grain Developer (Dev)
Writes grain interfaces and implementations. Annotates security requirements. May issue CCaps from grain code.

### Actor 4: The Client Developer (Dev)
Writes applications that connect to the cluster as external clients. Must obtain and manage CCaps.

### Actor 5: The Orleans Runtime (System)
Internal Orleans components: MembershipTable grain, grain directory, reminders, streaming, placement, etc. These must work without security attributes.

---

## Part 2: Full Workflow Simulations (Current State)

### Workflow 1: Organization Bootstrap

**Actor:** Organization Administrator
**Goal:** Create the trust root and delegate authority to nodes and users.

```
Step 1: Generate Organization keypair
  CURRENT: Must write C# code — ScynapseKeyPair.Generate(ScynapseKeyType.Organization)
  GAP: No CLI tool. No way to do this outside of code.

Step 2: Export/store the Organization seed securely
  CURRENT: orgKey.ExportSeed() returns bytes. Developer stores manually.
  GAP: No standard format, no file format, no key vault integration.

Step 3: Create Organization self-signed identity assertion
  CURRENT: AssertionBuilder.CreateIdentity(orgKey)
  GAP: Only available as C# API.

Step 4: Generate Node keypair(s)
  CURRENT: Same as Step 1 — must be code.

Step 5: Create delegation assertion (Org -> Node)
  CURRENT: AssertionBuilder.CreateDelegation(orgKey, nodeKey.PublicKeyBytes, ...)
  GAP: Must know the API. No CLI equivalent.

Step 6: Generate User keypair(s) and issue CCaps
  CURRENT: Same pattern. AssertionBuilder.CreateCapability(...)
  GAP: Same — code-only.

Step 7: Distribute keys and assertions to silos and clients
  CURRENT: Pass objects in code. No file format. No distribution mechanism.
  GAP: CRITICAL. No serialization-to-file, no import-from-file.
```

**HOLE IDENTIFIED: The entire provisioning workflow is code-only.** There is no operational tooling for key management, assertion creation, inspection, or distribution. A non-developer cannot operate the security system.

### Workflow 2: Silo Startup with Security

**Actor:** Silo Operator
**Goal:** Start a silo that participates in the secured cluster.

```
Step 1: Load node keypair from configuration
  CURRENT: Must be constructed in C# code from a seed.
  GAP: No config file format. No appsettings.json integration.

Step 2: Load trusted roots
  CURRENT: Must be raw byte arrays added to ScynapseSecurityOptions.TrustedRoots
  GAP: No way to load from a public key file.

Step 3: Load bootstrap assertions (own identity chain)
  CURRENT: Must be pre-constructed SignedAssertion objects
  GAP: No way to load from assertion files.

Step 4: Load peer assertions (other silos' identity chains)
  CURRENT: ScynapseSecurityOptions.PeerAssertions exists
  GAP: Same — must be pre-constructed objects. No file loading.

Step 5: Call UseScynapseSecurity()
  CURRENT: Works. Registers DI, filters, lifecycle participant.
  OK (for code-configured scenarios).

Step 6: Silo joins cluster
  CURRENT: Orleans clustering works normally.

Step 7: TLS handshake with peers
  CURRENT: TLS middleware exists but assertion verification is bypassed.
  PARTIAL GAP: Connection is encrypted but peer identity is not verified at TLS level.

Step 8: Grain calls begin
  CURRENT: Incoming/outgoing call filters active.
  OK (for annotated grains).
```

**HOLES IDENTIFIED:**
1. No configuration file loading for keys/assertions.
2. TLS peer verification bypassed.
3. For multi-silo clusters, each silo needs ALL peer assertion chains pre-loaded. With N silos, this is O(N) assertion files per silo, all pre-provisioned.

### Workflow 3: Grain Developer Workflow

**Actor:** Grain Developer
**Goal:** Write a secure grain that enforces access control.

```
Step 1: Define grain interface with security policy
  CURRENT:
    [SecurityPolicy(RequiresAuthentication = true)]
    public interface IMyGrain : IGrainWithStringKey
    {
        [RequireCapability(Action = "read")]
        Task<string> GetDataAsync();

        [RequireCapability(Action = "write")]
        Task SetDataAsync(string data);
    }
  OK. Clear, declarative.

Step 2: Implement grain
  CURRENT: Standard grain implementation. Security is transparent.
  OK.

Step 3: Read caller identity from grain code
  CURRENT: this.GetCallerPublicKey() — returns byte[] or null
  OK.

Step 4: Issue CCap to caller from grain code
  CURRENT: this.IssueCCapToCaller("read")
  PARTIAL GAP: Returns SignedAssertion but the caller must manually extract it
  from the method return value and store it in their wallet.

Step 5: Custom authorization logic in grain code
  CURRENT: Read caller key, read presented CCap, make decisions in grain code.
  OK — framework gives the primitives; grain code composes them.

Step 6: Grain-to-grain calls (downstream calls)
  CURRENT: Outgoing call filter attaches the silo's node key + whatever CCap
  is in the wallet matching the target grain.
  GAP: When grain A calls grain B on behalf of the original client, what
  identity is presented? The node's identity, not the client's. The client's
  CCap may not authorize the node to call grain B.
  THIS IS A CRITICAL WORKFLOW HOLE — see "Grain-to-Grain Delegation" below.
```

**HOLES IDENTIFIED:**
1. CCap delivery from grain to caller has no framework-level channel.
2. **Grain-to-grain calls lose the original caller's identity and CCap.** This is the most significant workflow hole in the entire design.

### Workflow 4: External Client Workflow

**Actor:** Client Developer
**Goal:** Connect to a Scynapse cluster and make secure grain calls.

```
Step 1: Obtain client keypair and CCaps
  CURRENT: Must be done in code. No file loading.
  GAP: Same provisioning hole as Workflow 1.

Step 2: Configure IClientBuilder with security
  CURRENT:
    clientBuilder.UseScynapseSecurity(new ScynapseSecurityOptions {
        NodeKeyPair = clientKey,  // "NodeKeyPair" is semantically misleading for clients
        TrustedRoots = { orgPub },
        BootstrapAssertions = { orgIdentity },
        PeerAssertions = { nodeDelegation },
        BootstrapCapabilities = { myCCap }
    });
  PARTIAL GAP: Works but semantics are confused (NodeKeyPair for a client?).

Step 3: Connect to cluster gateway
  CURRENT: Orleans client connects normally. TLS if configured.
  GAP: No TLS-level identity verification (same as silo).

Step 4: Make a grain call
  CURRENT: Outgoing filter selects CCap from wallet, attaches to RequestContext.
  OK.

Step 5: Receive response
  CURRENT: Normal Orleans response.
  OK.

Step 6: Acquire new CCaps at runtime
  CURRENT: Must call a grain method that returns a CCap, then manually store it.
  GAP: No automatic wallet population. No "login" flow. No CCap request protocol.
```

**HOLES IDENTIFIED:**
1. `NodeKeyPair` naming is semantically wrong for clients. Should be `IdentityKeyPair` or have a client-specific option.
2. No "login" or "authenticate" flow — clients must pre-have all their CCaps.
3. No CCap acquisition protocol.

### Workflow 5: Orleans Runtime Internal Operations

**Actor:** Orleans Runtime
**Goal:** System grains and internal operations must function without breaking security.

```
Step 1: MembershipTable grain operations
  CURRENT: MembershipTable grain has no [SecurityPolicy] attribute.
  Default policy: AllowAnonymous=true for unannotated grains.
  OK.

Step 2: Grain Directory operations
  CURRENT: Same — unannotated, default AllowAnonymous.
  OK.

Step 3: Reminder service operations
  CURRENT: Same — unannotated system grains pass through.
  OK.

Step 4: Streaming operations (SMS/EventHub)
  CURRENT: Stream agents are internal. If they go through call filters: allowed.
  If they bypass call filters: no security concern (internal only).
  NEEDS INVESTIGATION.

Step 5: Grain activation on remote silo (placement)
  CURRENT: When Orleans places a grain on a different silo, the placement
  message goes silo-to-silo. This is internal messaging, not a grain call.
  No security filter applies.
  OK (but TLS would protect the channel).

Step 6: GrainTypeDirectory (Scynapse-specific)
  CURRENT: GTD is a user-facing grain. If it has [SecurityPolicy], it requires
  CCaps. If not, it's anonymous.
  DESIGN DECISION NEEDED: Should GTD require authentication?
```

**HOLES IDENTIFIED:**
1. Streaming security is uninvestigated.
2. GTD grain security policy needs a design decision.

---

## Part 3: Critical Workflow Holes Analysis

### HOLE 1: No Provisioning Tooling (Scy.exe CLI)

**Problem:** Every security operation (key generation, assertion creation, inspection, verification) requires writing C# code. No operational tooling exists.

**Impact:** System cannot be deployed or operated without developer involvement. Impossible to rotate keys, issue CCaps, or inspect assertion chains without modifying source code.

**Design Options:**

#### Option A: Scy.exe CLI Tool (RECOMMENDED)

A standalone CLI tool named `Scy.exe` built with `Spectre.Console` (TUI) and `System.CommandLine` (command routing).

**Commands:**
```
scy key gen --type Organization --output org.key
scy key gen --type Node --output node.key
scy key gen --type User --output user.key
scy key show org.key                          # Shows public key, type, encoded form
scy key export-pub org.key --output org.pub   # Export public key only

scy assertion identity --key org.key --output org-identity.assertion
scy assertion delegate --issuer org.key --subject node.pub \
    --allow-types Capability,Delegation \
    --resource "scynapse:*" --action "*" \
    --output node-delegation.assertion
scy assertion ccap --issuer org.key --subject user.pub \
    --resource "scynapse:grain/IMyGrain" --action "read" \
    --expires 24h \
    --output user-read.ccap
scy assertion revoke --issuer org.key --target node-delegation.assertion \
    --reason "Key compromised" --output revocation.assertion
scy assertion inspect user-read.ccap          # Human-readable dump (Spectre table)
scy assertion verify user-read.ccap --trusted-root org.pub

scy config generate --type silo \
    --key node.key \
    --trusted-root org.pub \
    --bootstrap org-identity.assertion,node-delegation.assertion \
    --output silo-security.json
scy config generate --type client \
    --key user.key \
    --trusted-root org.pub \
    --ccap user-read.ccap \
    --output client-security.json

scy dev quickstart                            # Interactive TUI: generates full dev setup
                                              # (org key, node key, user key, all assertions)
```

**File formats:**
- `.key` — Seed file. Binary: 1 byte key type prefix + 32 bytes Ed25519 seed. MUST be kept secret.
- `.pub` — Public key file. Text: encoded public key string (e.g., `OABC123DEF...`). Safe to share.
- `.assertion` — Signed assertion. Binary: CBOR-encoded SignedAssertion. Can be shared.
- `.ccap` — Alias for `.assertion` when the claim type is Capability. Same format.
- `silo-security.json` / `client-security.json` — Configuration files loadable by `UseScynapseSecurity()`.

**Configuration file format (JSON):**
```json
{
  "scynapseSecurity": {
    "keyFile": "node.key",
    "trustedRoots": ["org.pub"],
    "bootstrapAssertions": ["org-identity.assertion", "node-delegation.assertion"],
    "peerAssertions": ["silo2-delegation.assertion"],
    "bootstrapCapabilities": [],
    "enableTls": true,
    "requireMutualTls": true
  }
}
```

**Pros:**
- Complete operational workflow without writing code
- Inspectable (human-readable output via Spectre.Console)
- Composable (pipe-friendly, scriptable)
- Standard file formats enable distribution via any file transfer mechanism
- Integrates with appsettings.json for standard .NET configuration

**Cons:**
- New project to create and maintain
- Binary assertion format must be stable (breaking changes invalidate files)

**Implementation location:** `src/Scynapse/src/Scy/` (new project)

#### Option B: PowerShell Module

A PowerShell module wrapping the C# security library.

**Pros:** PowerShell is ubiquitous in .NET DevOps; discoverable via `Get-Command`.
**Cons:** Windows-centric; .NET developers expect dotnet tools; less composable than CLI.

#### Option C: dotnet Tool

Package Scy.exe as a `dotnet tool` installable via `dotnet tool install -g scy`.

**Pros:** Standard .NET ecosystem; easy distribution via NuGet.
**Cons:** Requires .NET SDK on the machine (OK for dev, not always available in production).

**RECOMMENDATION:** Option A (Scy.exe) with Option C packaging (as a dotnet tool). Build the CLI, distribute it as a dotnet tool. This gives both standalone use and ecosystem integration.

---

### HOLE 2: No Configuration File Loading

**Problem:** `ScynapseSecurityOptions` only accepts in-memory objects. No way to load from appsettings.json or files.

**Design Options:**

#### Option A: IConfiguration Binding (RECOMMENDED)

Add an extension method that binds from `IConfiguration`:

```csharp
siloBuilder.UseScynapseSecurity(configuration.GetSection("ScynapseSecurity"));
```

Internally:
1. Read `keyFile` path, load seed, create `ScynapseKeyPair`
2. Read `trustedRoots` paths, load public keys
3. Read `bootstrapAssertions` paths, deserialize CBOR
4. Read `peerAssertions` paths, deserialize CBOR
5. Read `bootstrapCapabilities` paths, deserialize CBOR
6. Read boolean flags (`enableTls`, `requireMutualTls`)

**Pros:** Standard .NET pattern. Works with environment variables, Azure Key Vault, etc.
**Cons:** File path resolution must be robust (relative vs absolute, working directory).

#### Option B: Fluent File-Loading API

```csharp
siloBuilder.UseScynapseSecurity(options => {
    options.LoadKeyFromFile("node.key");
    options.AddTrustedRootFromFile("org.pub");
    options.AddBootstrapAssertionFromFile("org-identity.assertion");
});
```

**Pros:** Explicit, type-safe.
**Cons:** Still requires code changes to modify configuration.

**RECOMMENDATION:** Both Option A and Option B. IConfiguration binding for production; fluent API for programmatic scenarios.

---

### HOLE 3: Grain-to-Grain Delegation (THE CRITICAL HOLE)

**Problem:** When a client calls Grain A, and Grain A calls Grain B, the outgoing call filter on Grain A's silo attaches the *silo's node identity* and whatever CCap the *silo* has for Grain B. The original client's identity and CCap are lost. This breaks the capability model:

```
Client (UserKey, has CCap for GrainA:read)
  -> Grain A (on Silo1)
     -> Grain B (on Silo2 or same silo)
        Question: Who is the caller? Silo1's NodeKey, not the Client's UserKey.
        Question: What CCap is presented? Whatever Silo1 has, not the Client's.
```

This is the most fundamental workflow hole. In a capability system, downstream calls should either:
(a) Carry the original caller's capability chain (delegation propagation), or
(b) Use the silo's own authority (node-level trust), or
(c) Use a specific on-behalf-of (impersonation) mechanism.

**Design Options:**

#### Option A: RequestContext Propagation (Transparent Delegation) — RECOMMENDED for Phase 1

The client's identity and CCap already flow through `RequestContext`. The issue is that the *outgoing call filter overwrites them* with the silo's own identity. Fix: the outgoing filter should NOT overwrite if a caller identity already exists in the RequestContext.

**Behavior:**
- Client -> Silo: Outgoing filter (client-side) sets CallerKey, CCap, BearerProof
- Silo receives: Incoming filter verifies. CCap is valid for GrainA.
- GrainA -> GrainB: Outgoing filter (silo-side) checks if CallerKey is already in RequestContext
  - If YES (propagation mode): forward the existing CallerKey + CCap + BearerProof unchanged
  - If NO (silo-originated call): use silo's own identity

**The CCap scope problem:** The client's CCap is for `scynapse:grain/IMyGrain` action `read`. GrainB might be `IAnotherGrain` action `write`. The client's CCap won't match. Options:
- **Sub-option A1: Require the client's CCap to be broad enough** (resource pattern `scynapse:grain/*` covers both). Simplest but requires clients to have broad CCaps.
- **Sub-option A2: The silo creates a delegated CCap on-the-fly** (silo delegates the client's authority, attenuated, for the downstream call). More complex but preserves the principle of least privilege.
- **Sub-option A3: GrainA must explicitly provide a CCap for the GrainB call** (grain code manages delegation). Most explicit but most burdensome for grain developers.

**Pros:**
- Transparent to grain developers (no code changes in grain A)
- Preserves caller identity through the full call chain
- Easy to implement (modify outgoing filter logic)

**Cons:**
- Bearer proof may not verify on GrainB's silo if bearer proof is tied to a specific connection (channel binding)
- If the client's CCap is too narrow, downstream calls fail silently

#### Option B: Explicit Impersonation

GrainA explicitly creates an ImpersonationClaim: "Silo1 acts on behalf of Client for the purpose of calling GrainB."

```csharp
// In GrainA's method:
var clientKey = this.GetCallerPublicKey();
var impersonation = this.CreateImpersonation(clientKey, "scynapse:grain/IAnotherGrain", "write");
var anotherGrain = GrainFactory.GetGrain<IAnotherGrain>("key");
// Somehow attach impersonation to the call...
```

**Pros:** Explicit provenance. GrainB sees both the silo and the original client.
**Cons:** Heavy developer burden. Every grain-to-grain call requires explicit delegation code.

#### Option C: Node-Level Trust (Orleans-Compatible)

The silo's node identity is trusted to make calls on behalf of its grains. The incoming filter on the receiving silo checks that the caller is a trusted node (via delegation chain from Org), not that the caller has a specific CCap.

**Behavior:**
- Silo-to-silo calls: incoming filter checks that the calling silo's node key has a valid delegation chain. No per-call CCap required.
- Client-to-silo calls: CCap required (clients are untrusted).
- Grain-to-grain within cluster: trusted because silo-to-silo is trusted.

**Pros:**
- Simple. Matches how Orleans actually works (silos trust each other).
- No grain developer burden.
- Consistent with the existing AllowAnonymous default for system grains.

**Cons:**
- Loses per-call capability granularity for silo-to-silo calls.
- A compromised silo can call any grain on any other silo.
- Not truly capability-based for internal calls — reverts to perimeter security.

#### Option D: Hybrid (RECOMMENDED APPROACH)

Combine Options A and C:

1. **Silo-to-silo calls have node-level trust by default.** If the calling silo's node key has a valid delegation chain from a trusted root, the call is allowed for any grain method annotated with `[SecurityPolicy(TrustSiloIdentity = true)]` (or by default for unannotated grains).

2. **Client-to-silo calls always require CCaps.** External clients must present a CCap matching the grain's `[RequireCapability]` attribute.

3. **RequestContext propagation preserves client identity.** When a client's call triggers a grain-to-grain call, the original client's identity flows through `RequestContext`. The receiving grain can inspect it (`GetOriginalCallerPublicKey()`), but the authorization check uses the silo's node trust, not the client's CCap.

4. **Opt-in strict mode.** Grains that need per-call capability verification even for silo-originated calls can annotate with `[SecurityPolicy(RequiresCallerCapability = true)]`. In this mode, the outgoing filter must provide a CCap — either propagated from the client or issued by the grain/silo.

**Implementation changes:**
- Add `TrustSiloIdentity` flag to `SecurityPolicyAttribute` (default: true)
- Add `RequiresCallerCapability` flag (default: false)
- Modify `ScynapseIncomingCallFilter` to check: is caller a trusted node? If yes and `TrustSiloIdentity`, allow. Otherwise, require CCap.
- Add `GetOriginalCallerPublicKey()` to `GrainSecurityExtensions` — reads the propagated client identity from RequestContext
- Modify `ScynapseOutgoingCallFilter`: if caller identity exists in RequestContext, propagate it as `OriginalCallerPublicKey` alongside the silo's own `CallerPublicKey`

**Pros:**
- Works out of the box for typical Orleans patterns (silo-to-silo trusted)
- Preserves original caller identity for audit/logging
- Strict mode available for high-security grains
- Backward compatible with current implementation

**Cons:**
- Two security modes (node trust vs capability) adds complexity
- Grain developers must understand when to use strict mode

---

### HOLE 4: Client Authentication / Login Flow

**Problem:** Clients must pre-have all their CCaps before connecting. There's no way for a client to "log in" and receive CCaps dynamically.

**Design Options:**

#### Option A: Authentication Grain (RECOMMENDED)

A well-known grain type (`ISecurityGatewayGrain`) serves as the entry point for client authentication:

```csharp
// Provided by Scynapse.Security.Orleans:
[SecurityPolicy(RequiresAuthentication = true)]  // Requires identity, but no specific CCap
public interface ISecurityGatewayGrain : IGrainWithStringKey
{
    // Client presents its identity + delegation chain. Gateway verifies and issues CCaps.
    Task<CCapBundle> AuthenticateAsync(byte[] delegationChainCbor);

    // Client requests a specific CCap
    Task<SignedAssertion?> RequestCapabilityAsync(string resource, string action);

    // Refresh expiring CCaps
    Task<CCapBundle> RefreshAsync(byte[] expiringCCapsCbor);
}

[GenerateSerializer]
public sealed class CCapBundle
{
    [Id(0)] public List<byte[]> Capabilities { get; init; }  // CBOR-serialized CCaps
    [Id(1)] public long ExpiresAt { get; init; }
}
```

**Flow:**
1. Client connects with `UseScynapseSecurity()` providing only its key + delegation chain (no CCaps)
2. Client calls `ISecurityGatewayGrain.AuthenticateAsync()` with its delegation chain
3. Gateway verifies the chain, determines what CCaps the client should have (via policy)
4. Gateway issues CCaps and returns them
5. Client stores them in its wallet
6. Client proceeds to make grain calls

**Who implements the gateway?** The application developer. Scynapse provides the interface and a default implementation that issues CCaps based on the delegation chain's scope. Applications can override with custom logic (e.g., database-backed role mapping).

**Pros:**
- Clean "login" flow familiar to developers
- Dynamic CCap issuance based on identity
- Centralized policy enforcement
- Wallet auto-populated after login

**Cons:**
- Requires an unauthenticated-but-identified first call (gateway requires identity but not CCap)
- Single point of failure (mitigated by Orleans grain replication)

#### Option B: Bootstrap CCaps in Configuration

The client configuration file includes initial CCaps:

```json
{
  "scynapseSecurity": {
    "keyFile": "user.key",
    "bootstrapCapabilities": ["user-admin.ccap"]
  }
}
```

**Pros:** Simple. Works offline. No runtime dependency.
**Cons:** CCaps must be pre-provisioned. No dynamic issuance. Rotation requires configuration changes.

#### Option C: CCap Request Protocol (Over RequestContext)

A special RequestContext key signals "I need a CCap for this grain/action." The silo's incoming filter, instead of rejecting, redirects to a CCap issuance flow.

**Pros:** Transparent to grain code.
**Cons:** Complex. Mixes control flow with data flow. Hard to reason about.

**RECOMMENDATION:** Option A (Authentication Grain) as the primary flow, with Option B (bootstrap CCaps) for scenarios where the client knows its CCaps upfront.

---

### HOLE 5: Silo-to-Silo Assertion Discovery

**Problem:** For TLS-level peer verification, each silo needs the delegation chains of ALL other silos in its assertion store. With N silos, this is O(N) assertion chains per silo, all pre-provisioned.

**Design Options:**

#### Option A: Cluster-Wide Assertion Grain (RECOMMENDED)

A system grain that stores all silo assertion chains and serves them on request:

```csharp
// Scynapse system grain — AllowAnonymous because it's called during bootstrap
[SecurityPolicy(AllowAnonymous = true)]
public interface IClusterAssertionDirectoryGrain : IGrainWithStringKey
{
    Task RegisterSiloAssertionsAsync(byte[] publicKey, List<byte[]> assertionChainCbor);
    Task<List<byte[]>?> GetSiloAssertionsAsync(byte[] publicKey);
    Task<List<byte[]>> GetAllTrustedSiloKeysAsync();
}
```

**Flow:**
1. Silo starts, registers its own assertion chain with `IClusterAssertionDirectoryGrain`
2. When a new silo connects, the TLS validator queries the directory grain to get the peer's assertion chain
3. Directory grain itself is AllowAnonymous (bootstrap paradox: you need security to query the security service)

**Pros:** Dynamic. New silos auto-register. No O(N) pre-provisioning.
**Cons:** Bootstrap paradox — the directory grain must be reachable before TLS is verified. Requires that at least the initial silo-to-silo connection works without TLS-level assertion verification (which is currently the case — call filter is the enforcement point).

#### Option B: Gossip Protocol

When silos join the cluster, they gossip their assertion chains to all peers.

**Pros:** Fully decentralized. No single grain dependency.
**Cons:** Complex to implement. Orleans already has a membership gossip protocol but extending it for assertions adds complexity.

#### Option C: Pre-Shared Configuration (Current Design)

All peer assertion chains are provided in `PeerAssertions` configuration.

**Pros:** Simple. Deterministic.
**Cons:** O(N) configuration. Doesn't scale. Can't handle dynamic cluster membership.

**RECOMMENDATION:** Option A (Cluster Assertion Directory Grain) for Phase 1 completion. Keep Option C as fallback for static/small clusters.

---

### HOLE 6: StateTask Property Security

**Problem:** Scynapse's `StateTask<T>` properties generate `GetX()` and `SetX()` methods on grain interfaces. These are standard grain methods. Do they go through call filters?

**Analysis:** Yes. Code-generated `GetName()` and `SetName()` methods are standard grain interface methods. Orleans generates proxy invocations for them that go through the full grain call pipeline including call filters.

**However:** The `[RequireCapability]` attribute is on specific methods. Code-generated methods don't have it. They would follow the grain-level `[SecurityPolicy]` but not method-level capability requirements.

**Design Options:**

#### Option A: State Attribute Security Extension (RECOMMENDED)

Extend `[State]` to support capability requirements:

```csharp
[State(ReadAction = "read", WriteAction = "write")]
public partial string Name { get; set; }
// Generated: [RequireCapability(Action = "read")] GetName()
// Generated: [RequireCapability(Action = "write")] SetName()
```

**Pros:** Declarative. Integrated with existing State system. Codegen handles it.
**Cons:** Requires codegen changes.

#### Option B: Grain-Level Security Only

State properties inherit the grain-level `[SecurityPolicy]` but don't have method-level `[RequireCapability]`. If you need method-level control, use explicit methods instead of properties.

**Pros:** No changes needed.
**Cons:** No fine-grained property-level security (can't have read-public, write-private properties).

**RECOMMENDATION:** Option B for Phase 1 (no changes needed — properties already go through filters at grain level). Option A for a future enhancement.

---

### HOLE 7: Scynapse Events Security

**Problem:** Scynapse's naturalized C# events bridge to Orleans SMS streams. Do stream subscriptions and publications go through call filters?

**Analysis:** Orleans streams operate differently from grain calls:
- **Publishing:** A grain publishes to a stream provider. This is a local operation — no call filter.
- **Subscribing:** A consumer subscribes to a stream. Subscription may go through implicit/explicit subscription grains.
- **Receiving:** Stream events are delivered as messages to the consumer. These arrive through the Orleans messaging system but typically bypass the grain call filter pipeline (they use `IStreamSubscriptionHandle`, not `IOutgoingGrainCallFilter`).

**This means stream events likely bypass security.** This is a real gap.

**Design Options:**

#### Option A: Stream Security Filter (Phase 2)

Create `IStreamSecurityFilter` analogous to `IIncomingGrainCallFilter` for stream events. Requires modification to the streaming subsystem.

**Cons:** Significant implementation. Phase 2 work.

#### Option B: Secure Stream Wrapper (RECOMMENDED for Phase 1)

Provide a `SecureStreamPublisher<T>` and `SecureStreamSubscriber<T>` that wrap Orleans streams and add security checks:

```csharp
// Publishing: grain verifies caller has appropriate CCap before publishing
// Subscribing: wrapper verifies subscriber's identity before allowing subscription
```

**Pros:** No streaming subsystem changes. Application-level.
**Cons:** Opt-in. Developers must use the secure wrapper.

#### Option C: Document as Known Limitation

For Phase 1, document that streams are not secured and recommend using grain method calls for security-sensitive operations.

**RECOMMENDATION:** Option C for Phase 1 (document limitation). Option B as a quick follow-up if needed.

---

### HOLE 8: Development Mode / Quick Start

**Problem:** Setting up security for development is too much ceremony. Developers will skip security during development and never add it.

**Design Option: DevelopmentMode**

```csharp
siloBuilder.UseScynapseSecurity(options => {
    options.DevelopmentMode = true;
    // Auto-generates: org key, node key, identity, delegation
    // Logs WARNING: "Running in development mode — DO NOT USE IN PRODUCTION"
    // All clients auto-trusted (any identity accepted)
    // CCaps auto-issued for any request
});
```

The `scy dev quickstart` CLI command generates all necessary files for a development cluster.

**RECOMMENDATION:** Implement DevelopmentMode. It's critical for adoption.

---

## Part 4: Phase 1 Completion Plan

Based on the hole analysis above, here is the prioritized implementation plan for Phase 1 completion:

### Priority 1: BLOCKING (Cannot claim Phase 1 without these)

| # | Item | Effort | Fills Hole |
|---|------|--------|------------|
| 1a | Scy.exe CLI — key management commands | Medium | Hole 1 |
| 1b | Scy.exe CLI — assertion commands | Medium | Hole 1 |
| 1c | Configuration file format + IConfiguration loading | Medium | Hole 2 |
| 1d | Grain-to-grain delegation (Hybrid approach: node trust + propagation) | Medium | Hole 3 |
| 1e | ISecurityGatewayGrain interface + default implementation | Medium | Hole 4 |
| 1f | Cross-silo CCap flow integration test | Low | Phase 1 Review Gap 6 |
| 1g | DevelopmentMode auto-generation | Low | Hole 8 |

### Priority 2: HIGH (Needed for practical use)

| # | Item | Effort | Fills Hole |
|---|------|--------|------------|
| 2a | Scy.exe CLI — config generation + `dev quickstart` | Medium | Hole 1 |
| 2b | IClusterAssertionDirectoryGrain | Medium | Hole 5 |
| 2c | Fix WhoAmI test weak assertion | Low | Phase 1 Review Gap 5 |
| 2d | Document stream security limitation | Low | Hole 7 |
| 2e | Fix sync-over-async in TLS validator + re-enable TLS assertion verification | Medium | Phase 1 Review Gap 1 |

### Priority 3: MEDIUM (Complete feature set)

| # | Item | Effort | Fills Hole |
|---|------|--------|------------|
| 3a | `[State]` security attribute extension for codegen | Medium | Hole 6 |
| 3b | CCap auto-delivery via response RequestContext | Medium | Phase 1 Review Gap 4 |
| 3c | Swappable DI stores (IConfiguration-based) | Low | Phase 1 Review Gap |
| 3d | Scy.exe CLI — inspect/verify commands with Spectre.Console TUI | Medium | Hole 1 |

---

## Part 5: Complete Workflow Simulations (After Phase 1 Completion)

These simulations trace every workflow AFTER all Priority 1 items are implemented, demonstrating the system is complete and coherent.

### Simulation 1: Organization Bootstrap (Post-Completion)

```
ACTOR: Organization Administrator
TOOL: Scy.exe

$ scy key gen --type Organization --output ./keys/org.key
  Created Organization key.
  Public key: OABC123DEF456...
  Seed file: ./keys/org.key (KEEP SECRET)

$ scy key gen --type Node --output ./keys/silo1.key
  Created Node key.
  Public key: NXYZ789...
  Seed file: ./keys/silo1.key

$ scy key gen --type Node --output ./keys/silo2.key
  Created Node key.
  Public key: NQRS456...

$ scy key gen --type User --output ./keys/alice.key
  Created User key.
  Public key: UALICE001...

$ scy assertion identity --key ./keys/org.key --output ./assertions/org-identity.assertion
  Created identity assertion for OABC123DEF456...
  ID: blake2b:a1b2c3d4...

$ scy assertion delegate \
    --issuer ./keys/org.key \
    --subject NXYZ789... \
    --allow-types Capability,Delegation \
    --resource "scynapse:*" --action "*" \
    --output ./assertions/silo1-delegation.assertion
  Created delegation: OABC123... -> NXYZ789...

$ scy assertion delegate \
    --issuer ./keys/org.key \
    --subject NQRS456... \
    --allow-types Capability,Delegation \
    --resource "scynapse:*" --action "*" \
    --output ./assertions/silo2-delegation.assertion

$ scy assertion delegate \
    --issuer ./keys/org.key \
    --subject UALICE001... \
    --allow-types Capability \
    --resource "scynapse:grain/*" --action "*" \
    --output ./assertions/alice-delegation.assertion

$ scy key export-pub ./keys/org.key --output ./keys/org.pub

$ scy config generate --type silo \
    --key ./keys/silo1.key \
    --trusted-root ./keys/org.pub \
    --bootstrap ./assertions/org-identity.assertion,./assertions/silo1-delegation.assertion \
    --output ./config/silo1-security.json

$ scy config generate --type silo \
    --key ./keys/silo2.key \
    --trusted-root ./keys/org.pub \
    --bootstrap ./assertions/org-identity.assertion,./assertions/silo2-delegation.assertion \
    --output ./config/silo2-security.json

$ scy config generate --type client \
    --key ./keys/alice.key \
    --trusted-root ./keys/org.pub \
    --bootstrap ./assertions/org-identity.assertion,./assertions/alice-delegation.assertion \
    --output ./config/alice-security.json

RESULT: File system now contains:
  keys/org.key, org.pub, silo1.key, silo2.key, alice.key
  assertions/org-identity.assertion, silo1-delegation.assertion, silo2-delegation.assertion, alice-delegation.assertion
  config/silo1-security.json, silo2-security.json, alice-security.json
```

### Simulation 2: Silo Startup (Post-Completion)

```
ACTOR: Silo Operator
CODE: Silo startup (Program.cs)

var builder = Host.CreateApplicationBuilder(args);

// One line. Configuration loaded from JSON.
builder.Services.AddOrleans(siloBuilder => {
    siloBuilder.UseScynapseSecurity(
        builder.Configuration.GetSection("ScynapseSecurity"));
    // ... other Orleans config
});

// appsettings.json includes:
// { "ScynapseSecurity": { ... contents of silo1-security.json ... } }

// WHAT HAPPENS INTERNALLY:
// 1. UseScynapseSecurity reads config, loads key file, loads assertion files
// 2. Registers ScynapseIncomingCallFilter, ScynapseOutgoingCallFilter
// 3. Registers ICCapWallet (InMemoryCCapWallet)
// 4. Registers IAssertionStore, INonceStore, IAttenuationChecker
// 5. Registers ScynapseSecurityLifecycleParticipant
// 6. On lifecycle start: loads bootstrap assertions into store
// 7. If EnableTls: creates Ed25519-derived cert, configures TLS middleware
// 8. Silo joins cluster normally

// SILO-TO-SILO PEER DISCOVERY (with IClusterAssertionDirectoryGrain):
// 9. After joining, silo registers its assertion chain with the directory grain
// 10. When connecting to other silos, TLS validator queries directory for peer chains
// 11. (Fallback: if directory unavailable, TLS allows connection — call filter enforces)

RESULT: Silo running, secured, part of cluster.
```

### Simulation 3: Grain Developer Workflow (Post-Completion)

```
ACTOR: Grain Developer
CODE: Grain interface and implementation

// --- Interface ---
[SecurityPolicy(RequiresAuthentication = true)]
public interface IInventoryGrain : IGrainWithStringKey
{
    [RequireCapability(Action = "read")]
    Task<InventoryItem> GetItemAsync();

    [RequireCapability(Action = "write")]
    Task UpdateItemAsync(InventoryItem item);

    [RequireCapability(Action = "admin")]
    Task DeleteAsync();
}

// --- Implementation ---
public class InventoryGrain : Grain, IInventoryGrain
{
    private readonly IPersistentState<InventoryState> _state;

    public Task<InventoryItem> GetItemAsync()
    {
        // Security already verified by incoming filter.
        // Optionally read caller identity:
        var callerKey = this.GetCallerPublicKey();
        _logger.LogInformation("Read by {Caller}",
            ScynapseKeyEncoding.Encode(callerKey, ScynapseKeyType.User));

        return Task.FromResult(_state.State.Item);
    }

    public async Task UpdateItemAsync(InventoryItem item)
    {
        _state.State.Item = item;
        await _state.WriteStateAsync();
    }

    public async Task DeleteAsync()
    {
        _state.State = new InventoryState();
        await _state.ClearStateAsync();
        DeactivateOnIdle();
    }
}

// --- Grain that calls another grain ---
[SecurityPolicy(RequiresAuthentication = true)]
public interface IOrderGrain : IGrainWithStringKey
{
    [RequireCapability(Action = "create")]
    Task<OrderResult> PlaceOrderAsync(OrderRequest request);
}

public class OrderGrain : Grain, IOrderGrain
{
    public async Task<OrderResult> PlaceOrderAsync(OrderRequest request)
    {
        // Caller's identity + CCap verified by incoming filter.

        // Now call InventoryGrain — grain-to-grain call.
        // Using Hybrid model (Hole 3, Option D):
        //   - Outgoing filter uses SILO's node identity (node trust)
        //   - Original caller identity propagated in RequestContext
        //   - InventoryGrain's incoming filter checks: is caller a trusted node? YES.
        //   - If InventoryGrain had [SecurityPolicy(RequiresCallerCapability = true)],
        //     it would require the original client's CCap for "read".
        var inventory = GrainFactory.GetGrain<IInventoryGrain>(request.ItemId);
        var item = await inventory.GetItemAsync();

        // Original caller still visible:
        var originalCaller = this.GetOriginalCallerPublicKey();  // NEW API

        return new OrderResult { Success = true, Item = item };
    }
}

RESULT: Grain developer writes straightforward code. Security is declarative via attributes.
Grain-to-grain calls work transparently with node-level trust.
Original caller identity available for audit.
```

### Simulation 4: External Client Workflow (Post-Completion)

```
ACTOR: Client Developer
CODE: Client application

// --- Option A: Pre-provisioned CCaps ---
var client = new ClientBuilder()
    .UseScynapseSecurity(configuration.GetSection("ScynapseSecurity"))
    // configuration includes bootstrapCapabilities with pre-issued CCaps
    .Build();
await client.Connect();

var grain = client.GetGrain<IInventoryGrain>("item-42");
var item = await grain.GetItemAsync();
// Outgoing filter found CCap matching "scynapse:grain/IInventoryGrain" + "read"
// Attached to RequestContext. Silo verified. Grain call succeeded.


// --- Option B: Login Flow with SecurityGatewayGrain ---
var client = new ClientBuilder()
    .UseScynapseSecurity(configuration.GetSection("ScynapseSecurity"))
    // configuration has key + delegation but NO bootstrapCapabilities
    .Build();
await client.Connect();

// Authenticate and acquire CCaps
var gateway = client.GetGrain<ISecurityGatewayGrain>("default");
var bundle = await gateway.AuthenticateAsync(myDelegationChainCbor);
// Gateway verified my identity chain, issued CCaps based on my delegation scope

// Store CCaps in wallet
var wallet = client.ServiceProvider.GetRequiredService<ICCapWallet>();
foreach (var ccapBytes in bundle.Capabilities)
{
    var ccap = SignedAssertion.Deserialize(ccapBytes);
    wallet.Store(ccap);
}

// Now make grain calls — wallet has CCaps
var grain = client.GetGrain<IInventoryGrain>("item-42");
var item = await grain.GetItemAsync(); // CCap auto-selected from wallet

// --- Option C: Non-silo app (pure Orleans client) ---
// Same as above. The IClientBuilder.UseScynapseSecurity() works the same way.
// The client is NOT a silo. It connects via the Orleans gateway protocol.
// TLS encrypts the gateway connection. Call filter on the silo verifies CCaps.

RESULT: Client connects, authenticates, acquires CCaps, makes secure grain calls.
```

### Simulation 5: Development Mode Quick Start (Post-Completion)

```
ACTOR: Developer starting a new project
TOOL: Scy.exe + code

$ scy dev quickstart --output ./dev-security
  Generated development security configuration:
  ./dev-security/dev-org.key
  ./dev-security/dev-org.pub
  ./dev-security/dev-node.key
  ./dev-security/dev-user.key
  ./dev-security/dev-assertions/org-identity.assertion
  ./dev-security/dev-assertions/node-delegation.assertion
  ./dev-security/dev-assertions/user-delegation.assertion
  ./dev-security/dev-assertions/user-wildcard.ccap  (resource: *, action: *)
  ./dev-security/silo-security.json
  ./dev-security/client-security.json

  WARNING: These keys are for DEVELOPMENT ONLY. Do not use in production.

// In silo code:
siloBuilder.UseScynapseSecurity(options => {
    options.DevelopmentMode = true;
    // OR: load from generated config
});

// In client code:
clientBuilder.UseScynapseSecurity(options => {
    options.DevelopmentMode = true;
});

// DevelopmentMode behavior:
// - Auto-generates keys if none provided
// - Auto-creates delegation chains
// - Wildcard CCap issued to all clients (*)
// - Logs WARNING on every startup
// - NOT compiled into Release builds (conditional compilation)

RESULT: Zero-friction security setup for development.
```

### Simulation 6: Orleans Runtime Operations (Post-Completion)

```
ACTOR: Orleans Runtime (system grains)

MembershipTableGrain: No [SecurityPolicy] -> AllowAnonymous=true -> passes filter
GrainDirectoryPartition: Internal, not a grain call -> no filter
Reminder service: Internal system grain -> AllowAnonymous=true -> passes filter
StreamPubSubGrain: System grain -> AllowAnonymous=true -> passes filter
GrainTypeDirectoryGrain (Scynapse): No [SecurityPolicy] -> AllowAnonymous=true -> passes filter

IClusterAssertionDirectoryGrain: [SecurityPolicy(AllowAnonymous = true)] explicitly
ISecurityGatewayGrain: [SecurityPolicy(RequiresAuthentication = true)] but no CCap required

RESULT: All Orleans system operations work. Only user-annotated grains enforce security.
```

---

## Part 6: Scy.exe CLI Design

### Project Structure

```
src/Scynapse/src/Scy/
├── Scy.csproj                    # Console app, Spectre.Console + System.CommandLine
├── Program.cs                    # Entry point, command tree
├── Commands/
│   ├── KeyCommands.cs            # key gen, key show, key export-pub
│   ├── AssertionCommands.cs      # assertion identity, delegate, ccap, revoke, inspect, verify
│   ├── ConfigCommands.cs         # config generate
│   └── DevCommands.cs            # dev quickstart
├── IO/
│   ├── KeyFileFormat.cs          # .key and .pub file read/write
│   └── AssertionFileFormat.cs    # .assertion file read/write (CBOR)
└── Rendering/
    └── SpectreRenderers.cs       # Spectre.Console tables for inspect/show output
```

### Dependencies

```xml
<PackageReference Include="Spectre.Console" Version="0.49.*" />
<PackageReference Include="System.CommandLine" Version="2.0.0-beta4.*" />
<ProjectReference Include="../Scynapse.Security/Scynapse.Security.csproj" />
```

### Key File Format

```
Binary layout (33 bytes):
  [0]     Key type prefix byte (0x01=Org, 0x02=Domain, 0x03=Node, ...)
  [1..32] Ed25519 seed (32 bytes)

Note: Same seed bytes as ScynapseKeyPair.ExportSeed().
The prefix byte enables Scy.exe to reconstruct the typed key.
```

### Public Key File Format

```
Text (single line):
  OABC123DEF456GHI...    (encoded public key string)
```

### Assertion File Format

```
Binary: Raw CBOR bytes from AssertionSerializer.Serialize()
Extension: .assertion (generic) or .ccap (capability-specific, same format)
```

---

## Part 7: Updated Project Structure (Post Phase 1 Completion)

```
src/
├── Scynapse.Security/                          # Core — NO Orleans dependency
│   ├── (existing files unchanged)
│   ├── IO/
│   │   ├── KeyFileFormat.cs                    # NEW: .key/.pub file I/O
│   │   └── AssertionFileFormat.cs              # NEW: .assertion file I/O
│   └── Configuration/
│       └── SecurityConfigurationLoader.cs      # NEW: IConfiguration binding
│
├── Scynapse.Security.Orleans/                  # Orleans integration
│   ├── (existing files unchanged)
│   ├── ScynapseSecurityGatewayGrain.cs         # NEW: ISecurityGatewayGrain default impl
│   ├── ISecurityGatewayGrain.cs                # NEW: Authentication entry point
│   ├── IClusterAssertionDirectoryGrain.cs      # NEW: Peer assertion discovery
│   ├── ClusterAssertionDirectoryGrain.cs       # NEW: Default implementation
│   └── DevelopmentModeSecurityProvider.cs      # NEW: Auto-gen for dev mode
│
├── Scy/                                        # NEW: CLI tool
│   ├── Scy.csproj
│   ├── Program.cs
│   ├── Commands/
│   └── ...
│
test/
├── (existing test projects unchanged)
├── Scynapse.Security.Integration.Tests/
│   ├── ScynapseSecurityIntegrationTests.cs     # Existing 6 tests
│   ├── CrossSiloCCapFlowTests.cs               # NEW: Multi-silo CCap propagation
│   ├── SecurityGatewayTests.cs                 # NEW: Login flow tests
│   └── GrainToGrainDelegationTests.cs          # NEW: Node trust + propagation tests
└── Scy.Tests/                                  # NEW: CLI tool tests
    └── CliCommandTests.cs
```

---

## Part 8: Design Decisions Made During Implementation

(Preserved from v2.0)

### Default Security Policy: AllowAnonymous for Unannotated Grains

**Decision:** Unannotated grains default to `AllowAnonymous=true`. Necessary for Orleans system grain compatibility. Phase 2 flips this for Component-native grains.

### Client-Side CCap Filtering (Fail-Fast)

**Decision:** `InMemoryCCapWallet.FindCapability()` filters out expired and non-matching CCaps client-side. Silo sees "no CCap" not "invalid CCap."

### TLS Identity Verification Bypassed

**Decision:** `RemoteCertificateValidation` uses `AllowAnyRemoteCertificate()`. Call filter is the sole enforcement point. TLS provides confidentiality only. To be fixed in Priority 2e.

### NEW: Grain-to-Grain Delegation Model (Hybrid)

**Decision:** Silo-to-silo calls use node-level trust by default. Client identity propagated in RequestContext for audit. Opt-in strict mode via `[SecurityPolicy(RequiresCallerCapability = true)]` for grains that need per-call capability verification even from silos.

### NEW: Authentication Flow via SecurityGatewayGrain

**Decision:** Clients authenticate via a well-known grain. Gateway verifies identity chain and issues CCaps. Application provides the policy mapping (identity -> CCaps).

---

## Phase 2: Forward-Looking (Component Model) -- UNCHANGED

The Phase 2 design remains as originally specified. Key points:

**What preserves from Phase 1:** All of Layer 0 (crypto), Layer 1 (assertions), Layer 2 (verification). The grain call filter pattern. The mTLS transport.

**What changes:** Policy provider (attributes -> Component type definitions), capability URI namespace (`scynapse:grain/{type}` -> `scynapse:component/{type}/{grain}/{method}`), assertion store (in-memory -> CNS-backed distributed), Component isolation on same Node (new mechanism needed).

**Migration path:** Interface swaps behind stable abstractions. `IAssertionStore`, `IGrainSecurityPolicyProvider`, `IAttenuationChecker` -- all designed for Phase 2 implementation swaps without changing call sites.

---

## NuGet Dependencies

**Existing:**
- `NSec.Cryptography` 25.4.0 -- Ed25519, Blake2b-256, X25519
- `PeterO.Cbor` 4.5.5 -- CBOR serialization

**New (for Scy.exe):**
- `Spectre.Console` -- Rich terminal UI (tables, trees, progress bars)
- `System.CommandLine` -- Command-line parsing and routing

---

## References

### Specifications (Architecture Roots)
- UCAN v1.0.0-rc.1 -- github.com/ucan-wg/spec (capability token model)
- NATS Security -- docs.nats.io (NKeys, JWT, challenge-response)
- Ed25519 -- ed25519.cr.yp.to (signature scheme)
- X25519 -- RFC 7748 (key agreement)
- Channel Binding -- RFC 5929, RFC 8471 (token/TLS binding)

### .NET Libraries (In Use)
- `NSec.Cryptography` 25.4.0 -- Ed25519, Blake2b-256, X25519
- `PeterO.Cbor` 4.5.5 -- CBOR serialization

### .NET Libraries (New for Scy.exe)
- `Spectre.Console` -- Terminal UI
- `System.CommandLine` -- CLI framework

---

*Document reflects Phase 1 completion plan as of 2026-03-06. Builds on v2.0 status report and Phase 1 review findings.*
