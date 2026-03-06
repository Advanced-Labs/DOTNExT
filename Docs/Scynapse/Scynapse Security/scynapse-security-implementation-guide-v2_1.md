# Scynapse Security -- Implementation Guide

## Meta / Recovery Context

**Version:** 3.0 -- Phase 1 Completion Plan with Workflow Simulations
**Date:** 2026-03-06
**Revision from:** v2.0 (Post-Implementation Status Report)
**Companion document:** `scynapse-security-architecture_3.md` -- the *why*. READ THAT FIRST.

**What Scynapse is:** A fork/evolution of Microsoft Orleans (distributed actor platform). Currently uses Orleans's Silo/Client/Grain paradigm. Evolving toward a Component Model where "Component is the network." Security was designed to work on the current Orleans paradigm FIRST (Phase 1), then evolve with the Component Model (Phase 2).

**What changed in v3.0:** After the v2.0 implementation and review, significant additional work was done (CCap wallet, client builder, grain resource inference, revocation claims, bootstrap capabilities, `IssueCCapToCaller()`, peer assertions, default AllowAnonymous policy). This revision:
1. Acknowledges all work done to date (174 tests passing)
2. Identifies remaining holes for a **fully working Phase 1** -- covering all Orleans paradigm workflows
3. Provides **design options** for each hole with pros/cons
4. Simulates **complete workflows** for every actor (developer, silo admin, client app, grain-to-grain) to validate coherence
5. Designs the **Scy.exe** CLI tool as the operational backbone

**Key architectural decisions (from the architecture doc):**
- Ed25519 is THE identity primitive (`NSec.Cryptography`)
- The Signed Assertion is the single universal primitive
- Trust boundary is the Component (future) / Grain type (current Orleans approximation)
- mTLS default transport, TLS as bootstrap ramp
- No ACLs -- capability-based auth only (CCaps)
- CCaps are challengeable and channel-bindable
- Identity and Capability are unified

---

## Phase 1 Status: 174 Tests Passing -- Gaps Identified

### What Is Built and Working

| Layer | Status | Tests |
|-------|--------|-------|
| Layer 0: Cryptographic Primitives | COMPLETE | 40 unit tests |
| Layer 1: Signed Assertion Core | COMPLETE | 28 unit tests |
| Layer 2: Chain Verification | COMPLETE | 44 unit tests |
| Layer 3: Transport Security | COMPLETE with caveats | 14 unit tests |
| Layer 4: Orleans Integration | COMPLETE | 25 unit + 6 integration |

**Key additions since v2.0 review:**
- `ICCapWallet` + `InMemoryCCapWallet` -- client-side CCap storage with per-call selection
- `IClientBuilder.UseScynapseSecurity()` -- client-side security configuration
- `GrainResourceInference` -- automatic `scynapse:grain/{type}` URI derivation
- `RevocationClaim` -- structured revocation payload with CBOR serialization
- `GrainSecurityExtensions.IssueCCapToCaller()` -- grains can issue CCaps at runtime
- `BootstrapCapabilities` in options -- pre-loaded CCaps for client startup
- `PeerAssertions` in options -- pre-shared assertion chains for peer verification
- Default `AllowAnonymous=true` for unannotated grains (Orleans system grain compatibility)

### Project Structure

```
src/
  Scynapse.Security/                    # Core -- NO Orleans dependency
    ScynapseKeyType.cs, ScynapseKeyPair.cs, ScynapseKeyEncoding.cs
    Base32.cs, Crc16.cs
    Assertions/ (ClaimType, Claims, SignedAssertion, AssertionBuilder, AssertionSerializer)
    Verification/ (IAssertionStore, InMemoryAssertionStore, INonceStore, InMemoryNonceStore,
                   IAttenuationChecker, DefaultAttenuationChecker, AssertionVerifier,
                   VerificationResult, ByteMemoryEqualityComparer)
    Transport/ (ScynapseCertificateFactory, ScynapseRemoteCertificateValidator,
                ScynapseSecurityOptions)

  Scynapse.Security.Orleans/            # Orleans integration
    ScynapseIncomingCallFilter.cs       # THE enforcement point
    ScynapseOutgoingCallFilter.cs       # Wallet-based CCap selection
    SecurityPolicyAttribute.cs, RequireCapabilityAttribute.cs
    GrainSecurityPolicy.cs, AttributeBasedPolicyProvider.cs
    GrainSecurityExtensions.cs          # GetCallerPublicKey(), IssueCCapToCaller()
    GrainResourceInference.cs           # scynapse:grain/{type} URIs
    ICCapWallet.cs, InMemoryCCapWallet.cs
    ScynapseSecurityException.cs
    ScynapseSecurityLifecycleParticipant.cs
    ScynapseSecuritySiloBuilderExtensions.cs
    ScynapseSecurityClientBuilderExtensions.cs

test/
  Scynapse.Security.Tests/             # 112 unit tests
  Scynapse.Security.Orleans.Tests/     # 26 unit + 14 transport tests
  Scynapse.Security.Integration.Tests/ # 6 integration tests
```

---

## Part 1: Gap Analysis -- What's Missing for Complete Phase 1

Phase 1 goal: **A fully working security model for the Orleans paradigm (Silo/Client/Grain) that a developer can configure, deploy, and operate.** This means all standard Orleans workflows must work with security enabled.

### Gap A: No Operational Tooling (Scy.exe CLI)

**What's missing:** Keys, assertions, and CCaps can only be created/managed programmatically in C#. No CLI tool exists for operators/administrators.

**Impact:** Every deployment requires custom C# code to bootstrap security. No way to inspect, debug, or rotate credentials without rebuilding.

**Required for workflows:** Silo setup, client provisioning, key rotation, debugging, CCap issuance, revocation.

### Gap B: No Configuration File Loading

**What's missing:** `ScynapseSecurityOptions` must be populated in code. No JSON/YAML config loading. No integration with `IConfiguration` / `appsettings.json`.

**Impact:** Security configuration is code, not config. Can't change trusted roots, swap keys, or update CCaps without recompilation.

### Gap C: TLS Transport Verification Not Exercised

**What's missing:** `ScynapseRemoteCertificateValidator` exists but `RemoteCertificateValidation` callback uses `AllowAnyRemoteCertificate()`. TLS-level identity verification is bypassed. No integration tests with TLS enabled.

**Impact:** Rogue nodes can establish TLS connections (but are blocked at grain call filter level). Defense-in-depth gap. The sync-over-async issue in the TLS callback needs resolution.

### Gap D: No Cross-Silo CCap Flow Verification

**What's missing:** No test proves RequestContext (carrying CCap) survives Orleans's cross-silo message forwarding. If grain A on silo 1 calls grain B on silo 2, does the CCap flow correctly?

**Impact:** Multi-silo clusters may silently lose security context. Critical for production.

### Gap E: Grain-to-Grain Call Security Model Undefined

**What's missing:** When Grain A calls Grain B during processing of a client request, what identity/CCap does Grain A present? Options:
1. Forward the original client's CCap (delegation chain)
2. Use the silo's node identity (ambient authority)
3. Use a grain-specific identity
4. Configurable per grain type

Currently: the outgoing filter attaches whatever is in the wallet, which for silo-internal calls means the node's identity with broad CCaps. This is **ambient authority** -- the very thing capability-based security is designed to prevent.

### Gap F: Scynapse Feature Security (Events, Properties, Dynamic Grains)

**What's missing:** Scynapse's custom features may bypass the call filter pipeline:
- **StateTask properties** (`await grain.Name`): These generate Get/Set grain methods -- they go through call filters. SAFE.
- **Orleans Streams/SMS**: Stream subscriptions use internal grain calls. If the SMS stream providers are unannotated grains, they pass through as AllowAnonymous. NEEDS INVESTIGATION.
- **Dynamic Grain Access** (`DynamicGrainReference`): Uses `IGrainFactory.GetGrain()` + DLR dispatch -- goes through normal grain call pipeline. SAFE (but resource URI inference needs to handle dynamic types).
- **Plugin Grain Loading**: Assembly loading itself doesn't go through grain calls. Package integrity verification is missing. NEEDS ATTENTION for production.

### Gap G: Assertion Persistence

**What's missing:** `InMemoryAssertionStore` loses everything on restart. In a multi-silo cluster, each silo has its own isolated store.

**Impact:** Revocations are lost on restart. Assertion chains must be re-bootstrapped. No shared state across silos for real-time revocation.

### Gap H: Key Rotation Mechanism

**What's missing:** No way to rotate a node's Ed25519 key without full restart and re-provisioning of all delegation chains.

### Gap I: Error Reporting and Diagnostics

**What's missing:** Security failures return generic `ScynapseSecurityException`. No structured error codes, no diagnostic logging, no way for operators to understand *why* a call was rejected.

---

## Part 2: Design Options for Each Gap

### Gap A Options: Scy.exe CLI Tool

**Recommended: Option A1 -- Scy.exe with Spectre.Console + System.CommandLine**

A standalone CLI tool using Spectre.Console for rich TUI output and System.CommandLine for command parsing.

#### Command Structure

```
scy
  keygen          Generate Ed25519 keypairs
    --type        Key type: Organization|Domain|Node|User|ComponentType|Instance
    --output      Output file path (writes seed file)
    --format      Output format: seed-file|env-var|json

  identity        Create identity assertions
    --key         Path to seed file (issuer = subject)
    --output      Output assertion file

  delegate        Create delegation assertions
    --issuer      Issuer seed file path
    --subject     Subject public key (encoded string) or seed file
    --types       Allowed claim types (comma-separated)
    --resource    Resource pattern (default: "scynapse:*")
    --action      Action pattern (default: "*")
    --depth       Max delegation depth (default: 5)
    --expires     Expiration (duration like "30d" or ISO timestamp)
    --output      Output assertion file

  issue-ccap      Issue a capability assertion
    --issuer      Issuer seed file path
    --subject     Subject public key or seed file
    --resource    Resource URI or pattern
    --action      Action (e.g., "read", "write", "invoke", "*")
    --proofs      Proof assertion files (comma-separated paths)
    --expires     Expiration
    --output      Output assertion file

  revoke          Create a revocation assertion
    --issuer      Issuer seed file path
    --target      Assertion file to revoke (reads its content hash)
    --reason      Optional reason string
    --output      Output revocation file

  inspect         Human-readable dump of an assertion or key file
    --file        Path to assertion file or seed file
    --format      text|json|table (default: table, uses Spectre)
    --chain       Also resolve and display proof chain

  verify          Verify an assertion chain
    --file        Assertion file to verify
    --trusted     Trusted root public keys or assertion files
    --store       Directory of assertion files to use as store

  bundle          Create a configuration bundle (all assertions for a role)
    --silo        Generate silo security bundle
    --client      Generate client security bundle
    --org-key     Organization seed file
    --node-key    Node/client seed file
    --output      Output directory or JSON bundle

  init            Interactive setup wizard (Spectre TUI)
    --org         Initialize an organization (generates org key + identity)
    --silo        Initialize a silo (generates node key, creates delegation)
    --client      Initialize a client (generates user key, creates CCap)
    --dev         Development mode (auto-trust, broad CCaps, no TLS)

  rotate          Key rotation workflow
    --old-key     Current seed file
    --new-key     New seed file (or auto-generate)
    --issuer      Parent authority that must re-delegate
    --output      New delegation assertion

  status          Show security status of a running silo/cluster
    --endpoint    Silo endpoint to query
```

#### `scy init` Interactive Wizard

Using Spectre.Console's rich prompts:

```
$ scy init --org

  Scynapse Security -- Organization Setup

  Organization name: [Acme Corp]
  Key storage path: [./keys/]

  Generating Organization keypair...
  Public Key: OABC1234DEFG5678...
  Seed saved to: ./keys/org.seed

  Creating self-signed identity assertion...
  Identity assertion saved to: ./keys/org-identity.assertion

  IMPORTANT: Store org.seed securely. This is your root of trust.
  Share org-identity.assertion with all silos and clients.

$ scy init --silo --org-key ./keys/org.seed

  Scynapse Security -- Silo Setup

  Silo name (for reference): [silo-1]
  Key storage path: [./keys/silo-1/]

  Generating Node keypair...
  Public Key: NABC1234DEFG5678...
  Seed saved to: ./keys/silo-1/node.seed

  Creating delegation from Organization to Node...
  Delegation assertion saved to: ./keys/silo-1/node-delegation.assertion

  Silo configuration (add to appsettings.json):
  {
    "Scynapse:Security:NodeSeedFile": "./keys/silo-1/node.seed",
    "Scynapse:Security:TrustedRoots": ["OABC1234DEFG5678..."],
    "Scynapse:Security:BootstrapAssertions": [
      "./keys/org-identity.assertion",
      "./keys/silo-1/node-delegation.assertion"
    ]
  }
```

**Implementation:** New project `Scynapse.Security.Cli` with:
- Package refs: `Spectre.Console`, `System.CommandLine`
- References `Scynapse.Security` (core, no Orleans dependency)
- Tool name: `scy` (published as `dotnet tool install Scy`)

### Gap B Options: Configuration File Loading

**Option B1: IConfiguration integration** (RECOMMENDED)

```csharp
// In appsettings.json
{
  "Scynapse": {
    "Security": {
      "NodeSeedFile": "./keys/node.seed",
      "NodeSeedEnvironmentVariable": "SCYNAPSE_NODE_SEED",
      "TrustedRoots": ["OABC1234...", "ODEF5678..."],
      "BootstrapAssertionFiles": [
        "./assertions/org-identity.assertion",
        "./assertions/node-delegation.assertion"
      ],
      "PeerAssertionFiles": [
        "./assertions/silo2-delegation.assertion"
      ],
      "BootstrapCapabilityFiles": [
        "./assertions/admin-ccap.assertion"
      ],
      "EnableTls": true,
      "RequireMutualTls": true,
      "DevelopmentMode": false
    }
  }
}

// In Silo startup
builder.UseScynapseSecurity(builder.Configuration.GetSection("Scynapse:Security"));
```

**Option B2: Assertion bundle file** (a single JSON/CBOR file containing all assertions)

Pros: Single file deployment. Cons: Less flexible, harder to update individual assertions.

**Option B3: Key Vault integration** (Azure Key Vault, HashiCorp Vault)

Pros: Production-grade secret management. Cons: External dependency, more complex, Phase 2 concern.

**Decision: B1 for Phase 1, B3 as future extension.**

### Gap C Options: TLS Transport Verification

**Option C1: Synchronous assertion store lookup** (RECOMMENDED for Phase 1)

The `InMemoryAssertionStore` is actually synchronous (ConcurrentDictionary lookups). Wrap the async interface with a sync path specifically for the TLS callback:

```csharp
// In ScynapseRemoteCertificateValidator
bool ValidateCertificate(object sender, X509Certificate? cert, ...)
{
    // Extract Ed25519 key from cert extension
    var peerKey = ExtractEd25519Key(cert);

    // Synchronous lookup -- InMemoryAssertionStore is synchronous internally
    var assertions = _store.FindBySubjectSync(peerKey); // New sync method
    var delegation = assertions.FirstOrDefault(a => a.ClaimType == ClaimType.Delegation);

    if (delegation == null) return false;

    var result = _verifier.VerifySync(delegation, _trustedRoots); // Sync verify
    return result.IsValid;
}
```

**Option C2: Pre-validated peer cache**

During silo startup, validate all peer assertions and cache the results. TLS callback just checks the cache (a `HashSet<byte[]>` of validated peer public keys).

Pros: No sync-over-async. Fast. Cons: New peers require cache update.

**Option C3: Defer TLS verification, rely on call filters only**

Document that TLS provides confidentiality; call filters provide authz. Accept the defense-in-depth gap.

Pros: Simple. Cons: Rogue nodes consume connection resources.

**Decision: C2 for Phase 1 (pre-validated peer cache), C1 as refinement.**

### Gap D Options: Cross-Silo CCap Flow

**No design options needed -- this is a testing gap, not a design gap.**

`RequestContext` is documented to flow across silo boundaries in Orleans. Need to write a 2+ silo TestCluster test that:
1. Forces grain placement on specific silos (using `[PreferLocalPlacement]` or `StatelessWorker` avoidance)
2. Client calls grain A on silo 1, which calls grain B on silo 2
3. Verify grain B's incoming filter receives and validates the CCap

### Gap E Options: Grain-to-Grain Call Security

This is the most architecturally significant gap. When grain A calls grain B, what identity/CCap should be used?

**Option E1: Forward original caller's CCap (Transparent Delegation)**

The incoming filter stores the verified caller CCap. The outgoing filter re-attaches it for downstream calls. Grain B sees the original caller's identity.

```
Client --[CCap: user can read IOrderGrain]--> Grain A (IOrderGrain)
  Grain A --[forwards same CCap]--> Grain B (IInventoryGrain)
    Grain B verifies: does user's CCap cover IInventoryGrain? NO -> REJECTED
```

Pros: True capability model. No ambient authority. Cons: Original CCap may not cover downstream grain types. The caller must have CCaps for ALL grains in the chain, which they may not know about.

**Option E2: Silo-Ambient Authority (Current Behavior)**

Grain-to-grain calls within a silo use the node's broad delegation. Cross-silo calls use the node's peer identity.

```
Client --[CCap: user can read IOrderGrain]--> Grain A
  Grain A --[Node's ambient CCap: node can invoke *]--> Grain B
    Grain B sees: Node identity, accepts (node is trusted)
```

Pros: Simple. Works without CCap pre-provisioning for internal calls. Cons: Violates capability principle. If grain A is compromised, it has ambient authority to call anything.

**Option E3: Dual-Identity Model (RECOMMENDED)**

Grain calls carry TWO identities: the **original caller** (propagated via RequestContext) and the **acting grain** (via the node's signing key). The receiving grain's policy decides which to check:

```
Client --[CCap: user can read IOrderGrain]--> Grain A
  Grain A --[OriginalCaller=user, ActingAs=NodeKey/GrainA,
             NodeDelegation=node can invoke *]--> Grain B
    Grain B policy options:
      a) Trust node identity (fast, for internal infrastructure grains)
      b) Verify original caller has access (strict, for sensitive grains)
      c) Require both (belt and suspenders)
```

Implementation:
- `RequestContext` carries both `Scynapse.OriginalCallerKey` and `Scynapse.ActingNodeKey`
- `ScynapseIncomingCallFilter` checks the grain's `[SecurityPolicy]` to determine which identity to verify
- New attribute: `[SecurityPolicy(EnforceOriginalCaller = true)]` -- for grains that need to verify the end-user even in grain-to-grain calls

Pros: Flexible. Covers both infrastructure grains (that trust the node) and user-facing grains (that need original caller verification). Cons: More complex policy model.

**Option E4: Explicit CCap Acquisition by Grains**

Grain A, before calling Grain B, requests a scoped CCap from a security coordinator grain. This CCap is scoped to the specific operation.

```
Client --[CCap: user can process-order]--> OrderGrain
  OrderGrain --requests--> SecurityCoordinatorGrain: "I need invoke on IInventoryGrain for order processing"
  SecurityCoordinator --issues--> scoped CCap for OrderGrain to invoke IInventoryGrain
  OrderGrain --[scoped CCap]--> InventoryGrain
```

Pros: Minimum privilege, fully auditable. Cons: Extra round trip per call chain. Complexity. The security coordinator is a hot singleton.

**Decision: E3 (Dual-Identity) for Phase 1.** It provides the right balance: infrastructure grains work with node identity (no breaking change), sensitive user grains can enforce original caller checking, and the model extends naturally to Phase 2's Component-level identities.

### Gap F Options: Scynapse Feature Security

**StateTask Properties:** Already safe -- they generate standard grain methods that go through call filters. **No action needed.**

**Dynamic Grain Access (DynamicGrainReference):**
- `GrainResourceInference` needs to handle dynamic grain types (where the interface may not be statically known)
- The resource URI should be derived from the target grain's actual interface, not just the proxy type
- **Action:** Extend `GrainResourceInference` to handle `DynamicGrainReference` by reading the target grain type from the `GrainTypeMeta`

**Orleans Streams (SMS):**
- Stream subscriptions create implicit grain observers. These go through the normal Orleans messaging pipeline but bypass grain call filters (they're handled by the stream infrastructure)
- **Action for Phase 1:** Document that stream security is a Phase 2 concern. Streams in Phase 1 are intra-cluster infrastructure (same trust domain as node identity)

**Plugin Grain Loading:**
- Dynamically loaded assemblies can contain any grain types with any security attributes
- **Action for Phase 1:** Document that plugin grain security follows the same attribute-based model. Dynamically loaded grains with `[SecurityPolicy]` are enforced identically to statically loaded grains. Package signing/verification is a Phase 2 concern.

### Gap G Options: Assertion Persistence

**Option G1: Grain-backed assertion store** (RECOMMENDED for Phase 1)

Use an Orleans grain as the assertion store backend. This gives durability (via grain state persistence) and cluster-wide consistency.

```csharp
public interface IAssertionStoreGrain : IGrainWithStringKey
{
    Task StoreAsync(byte[] assertionBytes);
    Task<byte[]?> ResolveAsync(byte[] contentHash);
    Task RevokeAsync(byte[] contentHash);
    Task<bool> IsRevokedAsync(byte[] contentHash);
}
```

Pros: Uses existing Orleans persistence. Consistent across silos. Cons: Chicken-and-egg: the assertion store grain needs security infrastructure to work, but security needs the store. Solution: the store grain is `[SecurityPolicy(AllowAnonymous = true)]` -- it's infrastructure.

**Option G2: File-based assertion store**

Assertions persisted to a shared directory or volume.

Pros: Simple, no grain dependency. Cons: No cross-silo consistency without shared storage.

**Option G3: In-memory with bootstrap reload**

Keep `InMemoryAssertionStore` but reload assertions from config files on startup.

Pros: Simplest. Cons: Revocations lost on restart. No real-time cross-silo propagation.

**Decision: G3 for Phase 1 MVP, G1 as Phase 1 enhancement.** Bootstrap reload is sufficient for initial deployment. The grain-backed store is the right long-term solution for Phase 1 but can follow after the core workflows are working.

### Gap H: Key Rotation

**Phase 1 approach:** Scy.exe `rotate` command generates new key, creates new delegation from parent authority, outputs updated config. Operator deploys updated config and performs rolling restart.

Full online key rotation (without restart) is Phase 2.

### Gap I: Error Reporting

Add structured error codes to `ScynapseSecurityException`:

```csharp
public enum SecurityFailureCode
{
    MissingAuthentication = 1001,
    InvalidSignature = 1002,
    ExpiredAssertion = 1003,
    RevokedAssertion = 1004,
    InsufficientCapability = 1005,
    WrongAction = 1006,
    WrongResource = 1007,
    BearerProofFailed = 1008,
    ChainVerificationFailed = 1009,
    UntrustedRoot = 1010,
    ReplayDetected = 1011,
    MaxDepthExceeded = 1012
}
```

Add `ILogger` injection to `ScynapseIncomingCallFilter` with structured log events.

---

## Part 3: Complete Workflow Simulations

This section simulates every workflow end-to-end to validate the design is coherent and complete.

### Workflow 1: Organization Bootstrap (Administrator)

**Actor:** Cluster administrator
**Goal:** Set up a new Scynapse cluster with security enabled

#### Step-by-step:

```
1. Administrator installs Scy.exe
   $ dotnet tool install --global Scy

2. Generate organization root key
   $ scy init --org
   > Organization name: Acme Corp
   > Output directory: ./keys/acme
   > [GENERATES] org.seed, org-identity.assertion
   > Public Key: OABC1234...

3. Generate silo keys (one per silo)
   $ scy init --silo --org-key ./keys/acme/org.seed --name silo-1
   > [GENERATES] silo-1/node.seed, silo-1/node-delegation.assertion
   > [GENERATES] silo-1/appsettings.Security.json

   $ scy init --silo --org-key ./keys/acme/org.seed --name silo-2
   > [GENERATES] silo-2/node.seed, silo-2/node-delegation.assertion

4. Generate peer assertion bundles (each silo needs to know about others)
   $ scy bundle --silo --peers silo-1,silo-2 --dir ./keys/acme/
   > [GENERATES] silo-1/peer-assertions/ (contains silo-2's delegation)
   > [GENERATES] silo-2/peer-assertions/ (contains silo-1's delegation)

5. Deploy keys to silo machines (secure copy)
   $ scp -r keys/acme/silo-1/ admin@silo1.acme.com:/etc/scynapse/

6. Configure silo appsettings.json:
   {
     "Scynapse": {
       "Security": {
         "NodeSeedFile": "/etc/scynapse/silo-1/node.seed",
         "TrustedRoots": ["OABC1234..."],
         "BootstrapAssertionFiles": [
           "/etc/scynapse/silo-1/org-identity.assertion",
           "/etc/scynapse/silo-1/node-delegation.assertion"
         ],
         "PeerAssertionDirectory": "/etc/scynapse/silo-1/peer-assertions/",
         "EnableTls": true,
         "RequireMutualTls": true
       }
     }
   }

7. Start silos -- security initializes automatically via UseScynapseSecurity()
```

**Validation:** This workflow is complete. Scy.exe handles all manual steps. Configuration loading handles runtime setup. Peer assertions handle silo-to-silo trust.

### Workflow 2: Grain Developer (Writing Secured Grains)

**Actor:** Developer building application grains
**Goal:** Create grains with security policies

#### Step-by-step:

```csharp
// 1. Define secured grain interface
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

// 2. Implement grain
public class OrderGrain : Grain, IOrderGrain
{
    public async Task<OrderDetails> GetDetailsAsync()
    {
        // Can read caller identity
        var callerKey = this.GetCallerPublicKey();
        _logger.LogInformation("Order read by {CallerKey}",
            ScynapseKeyEncoding.Encode(ScynapseKeyType.User, callerKey));

        return _state.State.Details;
    }

    public async Task PlaceOrderAsync(OrderRequest request)
    {
        var callerKey = this.GetCallerPublicKey();
        _state.State.Details = new OrderDetails(request, callerKey);
        await _state.WriteStateAsync();

        // Call InventoryGrain -- uses Dual-Identity model (Option E3)
        // The original caller identity propagates via RequestContext
        var inventory = GrainFactory.GetGrain<IInventoryGrain>(request.ProductId);
        await inventory.ReserveAsync(request.Quantity);
    }

    public async Task CancelOrderAsync(string reason)
    {
        // Only admin CCap holders can cancel
        _state.State.Details = _state.State.Details with { Cancelled = true, Reason = reason };
        await _state.WriteStateAsync();
    }
}

// 3. Grain that issues CCaps to callers (runtime issuance)
[SecurityPolicy(RequiresAuthentication = true)]
public interface ISubscriptionGrain : IGrainWithStringKey
{
    [RequireCapability(Action = "subscribe")]
    Task<SignedAssertion> SubscribeAsync();
}

public class SubscriptionGrain : Grain, ISubscriptionGrain
{
    public Task<SignedAssertion> SubscribeAsync()
    {
        // Issue a time-limited read CCap to the caller
        var readCCap = this.IssueCCapToCaller(
            action: "read",
            resource: $"scynapse:grain/IContentGrain",
            expiresAt: DateTimeOffset.UtcNow.AddHours(24).ToUnixTimeSeconds());

        return Task.FromResult(readCCap);
    }
}
```

**Developer's mental model:**
1. Annotate grain interfaces with `[SecurityPolicy]` and `[RequireCapability]`
2. Use `this.GetCallerPublicKey()` to read who's calling
3. Use `this.IssueCCapToCaller()` to grant capabilities
4. Grain-to-grain calls propagate caller identity automatically (Dual-Identity)
5. Unannotated grains default to AllowAnonymous (Orleans system grains work)

**Validation:** Complete for Phase 1. Developer experience is clean. The Dual-Identity model means developers don't need to worry about CCap management for grain-to-grain calls.

### Workflow 3: Grain Developer with Scynapse Features

**Actor:** Developer using StateTask properties and Dynamic Grain Access
**Goal:** Ensure Scynapse features work with security

```csharp
// StateTask properties -- WORKS AUTOMATICALLY
[SecurityPolicy(RequiresAuthentication = true)]
public partial interface IPlayerGrain : IGrainWithStringKey
{
    // Code-generated Get/Set methods inherit the interface's [SecurityPolicy]
    // BUT: individual property methods don't have [RequireCapability] by default
    // The developer needs per-method capability control
}

public partial class PlayerGrain : Grain, IPlayerGrain
{
    public partial string Name { get; set; }
    public partial int Score { get; set; }
}
```

**Gap identified:** StateTask code-generated methods (GetName, SetName) don't have `[RequireCapability]` attributes. Options:

**Option F1: `[State]` attribute gains security properties** (RECOMMENDED)

```csharp
[State(ReadAction = "read", WriteAction = "write")]
public partial string Name { get; set; }

// Code generator produces:
// [RequireCapability(Action = "read")]
// Task<string> GetName();
// [RequireCapability(Action = "write")]
// Task SetName(string value);
```

**Option F2: Interface-level default action**

```csharp
[SecurityPolicy(RequiresAuthentication = true, DefaultReadAction = "read", DefaultWriteAction = "write")]
public partial interface IPlayerGrain : IGrainWithStringKey { }
```

**Option F3: No per-property security in Phase 1** -- the interface-level `[SecurityPolicy(RequiresAuthentication = true)]` is sufficient; any authenticated caller can access all properties.

**Decision: F3 for Phase 1 MVP, F1 as Phase 1 enhancement.** Interface-level auth is sufficient initially. Per-property capabilities can be added to the code generator later.

```csharp
// Dynamic Grain Access -- WORKS AUTOMATICALLY
dynamic grain = await dynamicClient.GetGrainDynamicAsync("MyApp.IOrderGrain", "order-1");

// This internally calls IGrainFactory.GetGrain() which goes through the normal pipeline
// The outgoing filter attaches CCaps based on the resolved grain interface type
// GrainResourceInference handles the type -> resource URI mapping

// BUT: the wallet needs a CCap matching "scynapse:grain/MyApp.IOrderGrain"
// Dynamic grain users must have pre-provisioned CCaps for the target grain types
OrderDetails details = await grain.GetDetailsAsync();
```

**Validation:** Dynamic grain access works because it goes through standard Orleans grain calls. The only requirement is that the caller's wallet contains appropriate CCaps.

### Workflow 4: External Client Application

**Actor:** Developer building a non-silo application that connects to a Scynapse cluster
**Goal:** Connect to cluster, authenticate, make secured grain calls

#### Step-by-step:

```
1. Administrator provisions client credentials using Scy.exe:
   $ scy init --client --org-key ./keys/acme/org.seed --name mobile-app
   > [GENERATES] mobile-app/user.seed
   > Public Key: UABC1234...

   $ scy issue-ccap \
       --issuer ./keys/acme/org.seed \
       --subject UABC1234... \
       --resource "scynapse:grain/IOrderGrain" \
       --action "read,write" \
       --proofs ./keys/acme/org-identity.assertion \
       --expires 30d \
       --output ./keys/acme/mobile-app/order-ccap.assertion

   $ scy issue-ccap \
       --issuer ./keys/acme/org.seed \
       --subject UABC1234... \
       --resource "scynapse:grain/IContentGrain" \
       --action "read" \
       --proofs ./keys/acme/org-identity.assertion \
       --expires 30d \
       --output ./keys/acme/mobile-app/content-ccap.assertion
```

```csharp
// 2. Client application code
var clientBuilder = new ClientBuilder()
    .UseLocalhostClustering() // or production clustering
    .UseScynapseSecurity(builder.Configuration.GetSection("Scynapse:Security"));

// appsettings.json for the client:
// {
//   "Scynapse": {
//     "Security": {
//       "NodeSeedFile": "./keys/user.seed",
//       "TrustedRoots": ["OABC1234..."],
//       "BootstrapAssertionFiles": ["./org-identity.assertion"],
//       "BootstrapCapabilityFiles": [
//         "./order-ccap.assertion",
//         "./content-ccap.assertion"
//       ]
//     }
//   }
// }

var client = clientBuilder.Build();
await client.Connect();

// 3. Make grain calls -- CCap auto-selected from wallet
var order = client.GetGrain<IOrderGrain>("order-123");
var details = await order.GetDetailsAsync();
// OutgoingFilter: wallet.FindCapability("scynapse:grain/IOrderGrain", "read") -> order-ccap
// Attaches: CallerPublicKey, CCap bytes, bearer proof
// IncomingFilter on silo: verifies chain, bearer, action match -> ALLOWED

// 4. Attempt unauthorized action
try
{
    await order.CancelOrderAsync("test");
    // OutgoingFilter: wallet.FindCapability("...", "admin") -> null (no admin CCap)
    // No CCap attached -> IncomingFilter rejects: "Authentication required"
}
catch (ScynapseSecurityException ex) when (ex.FailureCode == SecurityFailureCode.MissingAuthentication)
{
    Console.WriteLine("Not authorized to cancel orders");
}

// 5. Runtime CCap acquisition
var subscription = client.GetGrain<ISubscriptionGrain>("premium");
var readCCap = await subscription.SubscribeAsync();
// The grain returned a CCap as part of the response
// Client stores it in wallet for future use:
wallet.Store(readCCap);

// Now can read content:
var content = client.GetGrain<IContentGrain>("article-1");
var text = await content.GetTextAsync(); // Uses newly acquired readCCap
```

**Validation:** This workflow is complete. The key elements are:
- `scy init --client` provisions client credentials
- `scy issue-ccap` creates capabilities
- `UseScynapseSecurity()` with config loading handles client-side setup
- Wallet auto-selects CCaps per call
- Runtime CCap issuance from grains works via return values
- Unauthorized calls fail with structured exceptions

### Workflow 5: Silo-to-Silo Communication

**Actor:** Orleans runtime (automatic, not user-driven)
**Goal:** Silos authenticate to each other and forward grain calls securely

#### Step-by-step:

```
1. Silo 1 starts, loads security:
   - Reads node.seed -> creates ScynapseKeyPair
   - Loads org-identity + node-delegation into assertion store
   - Loads peer assertions (silo-2 delegation) into peer cache
   - Creates Ed25519-derived TLS certificate
   - Registers incoming/outgoing call filters

2. Silo 2 starts similarly

3. Silo 1 discovers Silo 2 via clustering (membership table)

4. Silo 1 opens TLS connection to Silo 2:
   a. TLS handshake (server = Silo 2's Ed25519-derived cert)
   b. Pre-validated peer cache check: Silo 2's public key is in Silo 1's peer cache -> TRUSTED
   c. mTLS: Silo 1 presents its cert, Silo 2 checks its peer cache -> TRUSTED
   d. Encrypted, mutually authenticated channel established

5. Client sends grain call to Silo 1 for a grain activated on Silo 2:
   a. Silo 1 receives call, incoming filter verifies client CCap -> VALID
   b. Silo 1 routes message to Silo 2 via the TLS channel
   c. RequestContext carries: OriginalCallerKey, ActingNodeKey, CCap bytes, bearer proof
   d. Silo 2 incoming filter verifies:
      - If target grain has [SecurityPolicy(EnforceOriginalCaller = true)]:
        verifies original caller's CCap chain
      - Otherwise: verifies node identity (trusted peer) -> ALLOWED
```

**Validation:** This workflow requires:
- Peer assertion pre-sharing (via `scy bundle` or `PeerAssertionDirectory` config)
- Pre-validated peer cache (Option C2) for TLS validation
- Dual-Identity model (Option E3) for cross-silo call forwarding
- All pieces are designed above. Complete.

### Workflow 6: Development Mode (Quick Start)

**Actor:** Developer trying Scynapse for the first time
**Goal:** Get security working with zero manual key management

#### Step-by-step:

```csharp
// Option 1: Dev mode via code
siloBuilder.UseScynapseSecurity(options =>
{
    options.DevelopmentMode = true;
    // Auto-generates: org key, node key, delegation chain, broad CCap
    // Logs warning: "DEVELOPMENT MODE: auto-generated keys, not for production"
});

clientBuilder.UseScynapseSecurity(options =>
{
    options.DevelopmentMode = true;
    // Auto-generates: user key, broad CCap for all grains
    // Auto-trusts any silo in the cluster
});

// Option 2: Dev mode via CLI
$ scy init --dev --output ./dev-keys/
> [GENERATES] all keys, assertions, CCaps for single-machine dev
> [GENERATES] dev-appsettings.json with all paths configured
> WARNING: Development mode keys. Do NOT use in production.
```

**Validation:** Complete. Critical for developer adoption. Must include prominent warnings.

### Workflow 7: Key Rotation

**Actor:** Cluster administrator
**Goal:** Rotate a silo's node key without extended downtime

```
1. Generate new key:
   $ scy rotate --old-key ./keys/silo-1/node.seed --org-key ./keys/acme/org.seed
   > New key generated: NXYZ9876...
   > New seed: ./keys/silo-1/node.seed.new
   > New delegation: ./keys/silo-1/node-delegation.assertion.new
   > Updated peer bundle: ./keys/silo-1/peer-assertions.new/

2. Deploy new key files to silo machine

3. Update appsettings.json to point to new files

4. Perform rolling restart:
   a. Stop silo-1
   b. Start silo-1 with new keys
   c. Other silos need updated peer assertions for silo-1
      (deploy updated peer assertion to silo-2, silo-3, etc.)
   d. Rolling restart other silos to pick up new peer assertions

5. Revoke old key:
   $ scy revoke --issuer ./keys/acme/org.seed \
       --target ./keys/silo-1/node-delegation.assertion \
       --reason "Key rotation"
   > Revocation assertion: ./keys/silo-1/node-delegation.revoked
   > Distribute this to all silos' assertion stores
```

**Validation:** This is operational but requires rolling restart. Online rotation (Phase 2) would use assertion store updates propagated via the cluster.

### Workflow 8: Runtime CCap Issuance and Revocation

**Actor:** Grain code
**Goal:** Issue capabilities to callers and revoke them later

```csharp
[SecurityPolicy(RequiresAuthentication = true)]
public interface IApiKeyGrain : IGrainWithStringKey
{
    [RequireCapability(Action = "admin")]
    Task<SignedAssertion> CreateApiKeyAsync(string[] allowedActions, int expiryDays);

    [RequireCapability(Action = "admin")]
    Task RevokeApiKeyAsync(byte[] assertionId);
}

public class ApiKeyGrain : Grain, IApiKeyGrain
{
    private readonly IAssertionStore _store;

    public async Task<SignedAssertion> CreateApiKeyAsync(string[] allowedActions, int expiryDays)
    {
        var callerKey = this.GetCallerPublicKey();

        // Issue scoped CCap
        var ccap = this.IssueCCapToCaller(
            action: string.Join(",", allowedActions),
            resource: "scynapse:grain/IDataGrain",
            expiresAt: DateTimeOffset.UtcNow.AddDays(expiryDays).ToUnixTimeSeconds());

        // Store for later revocation
        await _store.StoreAsync(ccap);
        _state.State.IssuedKeys.Add(ccap.Id.ToArray());
        await _state.WriteStateAsync();

        return ccap;
    }

    public async Task RevokeApiKeyAsync(byte[] assertionId)
    {
        await _store.RevokeAsync(assertionId);
        _state.State.IssuedKeys.RemoveAll(k => k.SequenceEqual(assertionId));
        await _state.WriteStateAsync();
    }
}
```

**Validation:** Works with current infrastructure. `IssueCCapToCaller()` uses the node key to sign. The returned CCap's proof chain goes: org-identity -> node-delegation -> ccap. This is verifiable by any silo that trusts the org root.

---

## Part 4: Implementation Plan (Ordered by Priority)

### Phase 1A: Core Workflow Enablement (Required)

| # | Task | Depends On | Effort |
|---|------|-----------|--------|
| 1 | Configuration file loading (`IConfiguration` integration) | -- | Medium |
| 2 | Dual-Identity model for grain-to-grain calls | -- | Medium |
| 3 | Pre-validated peer cache for TLS | -- | Small |
| 4 | Structured error codes + logging in call filters | -- | Small |
| 5 | Cross-silo CCap flow integration test | #2 | Small |
| 6 | 2-silo mTLS integration test | #3 | Medium |
| 7 | DevelopmentMode auto-generation | #1 | Small |

### Phase 1B: Operational Tooling (Required for usability)

| # | Task | Depends On | Effort |
|---|------|-----------|--------|
| 8 | Scy.exe CLI: `keygen`, `identity`, `delegate`, `issue-ccap` | -- | Medium |
| 9 | Scy.exe CLI: `inspect`, `verify` | #8 | Small |
| 10 | Scy.exe CLI: `init` wizard | #8 | Medium |
| 11 | Scy.exe CLI: `bundle`, `rotate`, `revoke` | #8 | Medium |
| 12 | Scy.exe CLI: `status` (query running silo) | #8 | Small |

### Phase 1C: Robustness (Required for production)

| # | Task | Depends On | Effort |
|---|------|-----------|--------|
| 13 | Fix `FindBySubjectAsync` to filter by claim type | -- | Tiny |
| 14 | Strengthen WhoAmI integration test | -- | Tiny |
| 15 | Assertion reload from config on startup | #1 | Small |
| 16 | Dynamic grain type resource URI inference | -- | Small |
| 17 | Document stream security model (Phase 2 deferral) | -- | Tiny |

### Phase 1D: Nice-to-Have Enhancements

| # | Task | Depends On | Effort |
|---|------|-----------|--------|
| 18 | Grain-backed assertion store | #1 | Medium |
| 19 | ImpersonationClaim verification logic | -- | Medium |
| 20 | StateTask per-property `[RequireCapability]` via code generator | -- | Medium |
| 21 | CCap auto-delivery channel (observer or RequestContext piggyback) | -- | Medium |

---

## Part 5: Scy.exe Detailed Design

### Project Setup

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net9.0</TargetFramework>
    <PackAsTool>true</PackAsTool>
    <ToolCommandName>scy</ToolCommandName>
    <AssemblyName>Scy</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Spectre.Console" Version="0.49.*" />
    <PackageReference Include="Spectre.Console.Cli" Version="0.49.*" />
    <PackageReference Include="System.CommandLine" Version="2.0.0-*" />
    <ProjectReference Include="../Scynapse.Security/Scynapse.Security.csproj" />
  </ItemGroup>
</Project>
```

**Note on CLI framework choice:** `Spectre.Console.Cli` provides command routing with rich Spectre output integration. If `System.CommandLine` is preferred for its middleware pipeline, it works well too. Both support the command tree structure above. The choice is implementation-time.

### Assertion File Format

Assertion files (`.assertion`) are CBOR-encoded `SignedAssertion` objects -- the same binary format used in `AssertionSerializer`. They can be inspected with `scy inspect`.

Seed files (`.seed`) contain the 32-byte Ed25519 seed encoded as Base32 with the key-type prefix (e.g., `PNABC1234...` for a Node seed).

### Security of Seed Files

Seed files contain private key material. Scy.exe should:
- Set file permissions to owner-only (chmod 600 on Unix)
- Warn if seed files have broad permissions
- Support reading seeds from environment variables (`NodeSeedEnvironmentVariable` config)
- Never write seeds to stdout unless explicitly requested with `--format env-var`

---

## Part 6: NuGet Dependencies Summary

**Core (already in use):**
- `NSec.Cryptography` 25.4.0
- `PeterO.Cbor` 4.5.5

**New for Scy.exe:**
- `Spectre.Console` (TUI rendering)
- `Spectre.Console.Cli` (command routing) -- OR `System.CommandLine`

**New for config loading:**
- `Microsoft.Extensions.Configuration` (already an Orleans dependency)
- `Microsoft.Extensions.Configuration.Json`

---

## References

### Specifications (Architecture Roots)
- UCAN v1.0.0-rc.1 -- github.com/ucan-wg/spec
- NATS Security -- docs.nats.io
- Ed25519 -- ed25519.cr.yp.to
- Channel Binding -- RFC 5929, RFC 8471

### .NET Libraries
- `NSec.Cryptography` 25.4.0
- `PeterO.Cbor` 4.5.5
- `Spectre.Console` -- spectreconsole.net

---

*Document reflects state as of 2026-03-06. Version 3.0: Phase 1 completion plan with full workflow simulations.*
