# Scynapse Security — Phase 1 Completion Guide

## Meta / Recovery Context

**Version:** 4.0 — Consolidated Phase 1 Completion Plan  
**Date:** 2026-03-06  
**Author:** CAI (Claude Opus 4.6, project architect) consolidating CCW1 + CCW2 analyses  
**Previous versions:** v2.0 (post-implementation status), v3.0 (two independent CCW analyses)  
**Companion documents:**  
- `scynapse-security-architecture_3.md` — the *why* (invariants, unified assertion model, prior art)  
- This file — the *what remains* and *how to finish Phase 1*  

**If you're reading this after context compaction:** This document captures the complete Phase 1 completion plan for Scynapse's security system. Phase 1 = "fully working security on the Orleans paradigm (Silo/Client/Grain)." Phase 2 = "security on Scynapse's new Component Model." We are finishing Phase 1.

**What's already built (174 tests passing, branch `claude/review-security-docs-QnVa8`):**
- Layer 0: Ed25519 keys, NATS-style encoding, Blake2b-256 (40 tests)
- Layer 1: Signed Assertions, CBOR serialization, all claim types (28 tests)
- Layer 2: Chain verification, attenuation, replay prevention (44 tests)
- Layer 3: ECDSA-bridge TLS certs with Ed25519 in extension (14 tests)
- Layer 4: Orleans call filters, wallet, policy attributes, client builder (25 unit + 6 integration)

**Key architectural decisions (from architecture doc):**
- Ed25519 identity primitive, Signed Assertion as single universal primitive
- Identity and Capability unified (identity is degenerate/root case of capability)
- Component is the trust boundary (grain type as Phase 1 approximation)
- mTLS default, TLS as bootstrap ramp
- No ACLs — capability-based auth only
- "Component is the network" (future)

---

## Part 1: Gap Analysis — Consolidated from Two Independent Reviews

Two independent CCW analyses identified overlapping gaps. Both converged on the same core solutions, which validates the approach. This section consolidates their findings with CAI additions for Phase 2 compatibility.

### Gap 1: Naming and Resource Addressing (CRITICAL — Neither CCW identified this properly)

**The problem the CCWs saw:** CCap resource URIs are flat strings like `scynapse:grain/IMyGrain`. They treated this as a solved problem.

**The deeper problem:** Scynapse's future Component Model needs hierarchical, namespaced addressing. The CNS (Scynapse Name System) needs to resolve these names. Security resource URIs and name resolution are the SAME problem. If we design flat resource URIs now and hierarchical names later, we get a painful merge.

**Additionally:** NATS has a mature subject-based addressing system (`orders.us.east.>` with wildcards) that maps directly to its security model (accounts can publish/subscribe to subject patterns). Scynapse should learn from this: the naming scheme IS the security scheme's resource vocabulary.

**Design: Scynapse Subject Namespace (SSN) — minimal, NATS-inspired**

Resource URIs in Scynapse follow a hierarchical dot-separated pattern with wildcard support:

```
scynapse.system.membership          # Orleans MembershipTable grain
scynapse.system.directory           # Grain directory
scynapse.system.security.gateway    # SecurityGatewayGrain
scynapse.system.security.assertions # ClusterAssertionDirectoryGrain
scynapse.app.{namespace}.{grain}.{method}  # Application grains
scynapse.app.inventory.IInventoryGrain.GetItem   # Specific method
scynapse.app.orders.*               # All order grains (wildcard)
scynapse.app.>                      # All application grains (recursive wildcard)
```

**Wildcard rules (from NATS):**
- `*` matches one segment: `scynapse.app.*.GetItem` matches any grain's GetItem
- `>` matches one or more trailing segments: `scynapse.app.>` matches everything under app
- Exact match is strongest, then `*`, then `>`

**Why this matters for security:** CCap resource fields use these URIs. Delegation resource patterns use these wildcards. The attenuation checker already does pattern matching — it just needs to use dot-separated hierarchical patterns instead of flat strings.

**Why this matters for Phase 2:** When the Component Model arrives, Components naturally occupy a namespace position: `scynapse.component.{type}.{grain}.{method}`. The CNS resolves names in this namespace. Security resource URIs are CNS names. One system, not two.

**What changes in current code:**
- `GrainResourceInference` changes from `scynapse:grain/IMyGrain` to `scynapse.app.{namespace}.{grainType}` (or `scynapse.app.{grainType}` if no namespace)
- `DefaultAttenuationChecker` pattern matching uses dot-separated hierarchical matching with `*` and `>` wildcards
- CCap `resource` fields use the new URI scheme
- This is a string format change, not a structural change — the assertion format is unchanged

**NATS precedent:** NATS subjects are dot-separated, support `*` and `>` wildcards, and their security model (account-level publish/subscribe permissions) uses the same subject patterns. This is a proven design at massive scale.

### Gap 2: No Provisioning Tooling (Scy.exe CLI)

**Both CCWs agree:** A CLI tool is required. CCW1's command tree is more complete.

**NATS precedent:** NATS has `nsc` (NATS Security Credentials) which is specifically designed around the Operator→Account→User hierarchy. It knows the trust topology and generates correct configurations. Scy.exe should similarly know about Scynapse's topology.

**Design: Scy.exe — topology-aware, NATS `nsc`-inspired**

Scy.exe is not just a key/assertion factory. It understands the Scynapse trust hierarchy: Organization → Domain → Node → User. It enforces correct delegation chains and generates complete, correct configurations.

```
scy init org --name "Acme Corp" --dir ./acme
    # Generates: org.seed, org.pub, org-identity.assertion
    # Creates a "context" directory that subsequent commands reference

scy init node --name silo-1 --org ./acme
    # Generates: silo-1/node.seed, node.pub, node-delegation.assertion
    # Delegation chain is automatically correct (org → node)
    # Generates: silo-1/silo-security.json (ready for UseScynapseSecurity)

scy init node --name silo-2 --org ./acme
    # Same pattern. Scy.exe knows there are now 2 nodes.

scy init user --name alice --org ./acme
    # Generates: alice/user.seed, user.pub, user-delegation.assertion

scy grant --to alice --resource "scynapse.app.orders.>" --action "read,write" --org ./acme
    # Issues a CCap from org to alice for all order grains
    # Automatically includes correct proof chain

scy grant --to alice --resource "scynapse.app.inventory.*" --action "read" --org ./acme
    # Another CCap, scoped narrower

scy bundle --node silo-1 --org ./acme
    # Generates complete silo-1 config: all peer assertions, all known user delegations
    # Ready to deploy

scy bundle --user alice --org ./acme
    # Generates complete client config: delegation chain, all CCaps, trusted root

scy inspect ./acme/alice/user-delegation.assertion
    # Spectre.Console table showing issuer, subject, claims, chain, expiry

scy verify ./acme/alice/read-orders.ccap --root ./acme/org.pub
    # Walks and verifies the chain

scy dev quickstart --dir ./dev
    # Generates everything for single-machine development
    # WARNING: dev mode keys

scy status --endpoint localhost:11111
    # Query running silo's security state (future)
```

**Key difference from generic CLI:** `scy init node --org ./acme` doesn't just generate a key — it reads the org context, creates a correctly-chained delegation, generates a complete config file, and knows about other nodes in the org. This is what `nsc` does for NATS and it's what makes it actually usable.

**Implementation:** New project `src/Scy/`. References `Scynapse.Security` only (no Orleans dependency). Uses Spectre.Console for TUI and System.CommandLine for routing.

### Gap 3: No Configuration File Loading

**Both CCWs agree:** `IConfiguration` binding is required.

**Design:**

```json
{
  "ScynapseSecurity": {
    "NodeSeedFile": "./keys/node.seed",
    "TrustedRoots": ["./keys/org.pub"],
    "BootstrapAssertionFiles": [
      "./assertions/org-identity.assertion",
      "./assertions/node-delegation.assertion"
    ],
    "PeerAssertionDirectory": "./assertions/peers/",
    "BootstrapCapabilityFiles": [],
    "EnableTls": true,
    "RequireMutualTls": true,
    "DevelopmentMode": false
  }
}
```

```csharp
// One-line silo configuration
siloBuilder.UseScynapseSecurity(
    builder.Configuration.GetSection("ScynapseSecurity"));

// One-line client configuration  
clientBuilder.UseScynapseSecurity(
    builder.Configuration.GetSection("ScynapseSecurity"));
```

**Implementation:** Add `SecurityConfigurationLoader` to `Scynapse.Security` that reads file paths, loads keys/assertions, and populates `ScynapseSecurityOptions`. Add `UseScynapseSecurity(IConfigurationSection)` overloads.

### Gap 4: Grain-to-Grain Call Delegation (THE critical architectural gap)

**Both CCWs converge on Hybrid model.** CCW2's framing is cleaner.

**Design: Two Trust Levels Operating Simultaneously**

1. **Node-Level Trust (default for silo-originated calls):** If the calling silo has a valid delegation chain from a trusted root, grain calls from that silo are allowed for grain types that don't opt into strict mode. This matches Orleans's inherent model where silos trust each other.

2. **Caller-Level Trust (required for external client calls):** Clients must present a CCap matching the grain's `[RequireCapability]`.

3. **Original Caller Propagation:** The original client's identity flows through `RequestContext` through the entire call chain. Available via `this.GetOriginalCallerPublicKey()`.

4. **Strict Mode (opt-in per grain):** `[SecurityPolicy(RequiresCallerCapability = true)]` — forces CCap verification even for silo-originated calls. Use for high-security grains that must verify the end-user regardless of how the call arrived.

**What changes in code:**
- `ScynapseOutgoingCallFilter`: if `OriginalCallerKey` exists in RequestContext, preserve it. Don't overwrite with silo identity.
- `ScynapseIncomingCallFilter`: check if caller is a trusted Node (via assertion chain). If yes and grain allows node trust (default), allow. If no, require CCap.
- New `RequestContext` keys: `Scynapse.OriginalCallerKey`, `Scynapse.OriginalCallerCCap` (preserved from initial client call through entire chain)
- New attribute flag: `[SecurityPolicy(RequiresCallerCapability = true)]`
- New extension: `this.GetOriginalCallerPublicKey()` on grains

**Phase 2 compatibility:** When Components arrive, "node trust" generalizes to "Component trust" — Components that share a trust domain (same Node, or explicitly trusting each other) get the equivalent of node trust. Components in different trust domains require CCaps. Same mechanism, broader scope.

### Gap 5: Client Authentication Flow

**Both CCWs agree:** SecurityGatewayGrain is the right approach.

**Design:**

```csharp
// Provided by Scynapse.Security.Orleans
[SecurityPolicy(RequiresAuthentication = true)]  // needs identity, not CCap
public interface ISecurityGatewayGrain : IGrainWithStringKey
{
    Task<CCapBundle> AuthenticateAsync(byte[] delegationChainCbor);
    Task<SignedAssertion?> RequestCapabilityAsync(string resource, string action);
    Task<CCapBundle> RefreshAsync(byte[] expiringCCapsCbor);
}
```

The gateway requires identity (the client must have a delegation chain proving who they are) but does NOT require a CCap. This breaks the bootstrap paradox. The gateway's job is to issue CCaps based on the client's identity and the application's policy.

Scynapse provides a default implementation. Application developers can override with custom logic (role-based, database-backed, etc.).

**Client workflow:**
1. Client connects with key + delegation chain (no CCaps)
2. Calls `ISecurityGatewayGrain.AuthenticateAsync()` 
3. Gateway verifies identity, issues CCaps based on policy
4. Client stores CCaps in wallet
5. Subsequent grain calls use wallet-based CCap selection (already implemented)

### Gap 6: Silo-to-Silo Peer Assertion Discovery

**Both CCWs agree:** `IClusterAssertionDirectoryGrain` is needed for dynamic clusters.

**Design:**

```csharp
[SecurityPolicy(AllowAnonymous = true)]  // bootstrap paradox
public interface IClusterAssertionDirectoryGrain : IGrainWithStringKey
{
    Task RegisterSiloAsync(byte[] publicKey, List<byte[]> assertionChainCbor);
    Task<List<byte[]>?> GetSiloAssertionsAsync(byte[] publicKey);
    Task<List<byte[]>> GetAllTrustedSiloKeysAsync();
}
```

Silos register on startup. TLS validator queries for peer chains. Fallback: if directory unavailable, TLS allows connection (call filter remains enforcement point — maintaining current defense model).

**Phase 2 compatibility:** The directory grain generalizes to the CNS. When the CNS exists, assertion resolution becomes a CNS query. The interface stays the same; the implementation swaps.

### Gap 7: Scynapse Feature Security (CCW1 found critical issues)

**CCW1's critical findings (CCW2 missed these):**

1. **`IPluginGrainLoader`** — NO access control. Any code in the silo can load arbitrary assemblies. Must be protected: `[SecurityPolicy(RequiresAuthentication = true)]` + `[RequireCapability(Action = "admin")]`.

2. **`IGrainTypeDirectoryGrain`** — Exposes deployment topology with no auth. Must be annotated: `[SecurityPolicy(RequiresAuthentication = true)]` for enumeration, admin CCap for write operations.

3. **Orleans Streams/SMS** — Bypass grain call filters. Document as Phase 1 limitation. Security-sensitive operations should use grain calls, not streams.

4. **StateTask properties** — Code-generated methods go through call filters. Safe at grain-level policy. Per-property `[RequireCapability]` is Phase 2 enhancement.

5. **Async+ persistence** — Remnant from NewOrleans era. If still in codebase, state is unencrypted and has no access control. Document as known limitation or remove.

### Gap 8: TLS Transport Verification

**Design (from CCW1):** Pre-validated peer cache.

During silo startup, validate all known peer assertions and cache validated public keys. TLS callback does a synchronous `HashSet` lookup. No sync-over-async. Fast.

When `IClusterAssertionDirectoryGrain` provides a new peer's assertions, validate and add to cache. This gives both static (pre-configured) and dynamic (directory-discovered) peer verification.

**Also fix:** `FindBySubjectAsync` — must filter by `ClaimType.Delegation` to avoid returning capabilities when delegations are needed.

### Gap 9: Development Mode

```csharp
siloBuilder.UseScynapseSecurity(options => {
    options.DevelopmentMode = true;
    // Auto-generates: org key, node key, delegation, wildcard CCap
    // Logs WARNING every startup
    // All clients auto-trusted
});
```

Also: `scy dev quickstart` generates all files for single-machine development.

### Gap 10: Error Reporting and Diagnostics

Add `SecurityFailureCode` enum to `ScynapseSecurityException`. Add `ILogger` to call filters with structured log events. This is small but critical for operability.

---

## Part 2: Priority-Ordered Implementation Plan

### Priority 1: BLOCKING (Cannot claim Phase 1 done without these)

| # | Task | Effort | Fills |
|---|------|--------|-------|
| 1 | Subject namespace scheme (dot-separated URIs with wildcards) | Medium | Gap 1 |
| 2 | Grain-to-grain Hybrid model (node trust + caller propagation) | Medium | Gap 4 |
| 3 | IConfiguration loading + file I/O for keys/assertions | Medium | Gap 3 |
| 4 | Scy.exe CLI: `init org`, `init node`, `init user`, `grant`, `bundle` | Medium | Gap 2 |
| 5 | ISecurityGatewayGrain interface + default implementation | Medium | Gap 5 |
| 6 | DevelopmentMode auto-generation | Small | Gap 9 |
| 7 | Cross-silo CCap flow integration test (2+ silo TestCluster) | Small | Gap — untested |
| 8 | Structured error codes + ILogger in call filters | Small | Gap 10 |
| 9 | Protect IPluginGrainLoader + IGrainTypeDirectoryGrain | Small | Gap 7 |

### Priority 2: HIGH (Needed for practical deployment)

| # | Task | Effort | Fills |
|---|------|--------|-------|
| 10 | IClusterAssertionDirectoryGrain | Medium | Gap 6 |
| 11 | Pre-validated peer cache for TLS | Medium | Gap 8 |
| 12 | Scy.exe CLI: `inspect`, `verify` (with Spectre.Console) | Small | Gap 2 |
| 13 | Fix FindBySubjectAsync to filter by claim type | Tiny | Gap 8 |
| 14 | Strengthen WhoAmI integration test | Tiny | — |
| 15 | Document stream security limitation | Tiny | Gap 7 |

### Priority 3: ENHANCEMENT (Complete feature set)

| # | Task | Effort | Fills |
|---|------|--------|-------|
| 16 | Scy.exe CLI: `rotate`, `revoke`, `status` | Medium | Gap 2 |
| 17 | Grain-backed assertion store | Medium | — |
| 18 | CCap auto-delivery channel | Medium | — |
| 19 | `[State(ReadAction, WriteAction)]` codegen extension | Medium | Gap 7 |
| 20 | Scy.exe `dev quickstart` interactive wizard | Small | Gap 9 |

---

## Part 3: Subject Namespace Design (Detailed)

This is the piece neither CCW addressed properly and it's critical for Phase 2 compatibility.

### URI Scheme

All Scynapse resources are addressed with dot-separated hierarchical names:

```
scynapse.{domain}.{path...}

Domains:
  system    — Orleans/Scynapse infrastructure
  app       — Application grains
  component — (Phase 2) Component types
```

### System Namespace (Protected)

```
scynapse.system.membership          # MembershipTable operations
scynapse.system.directory           # Grain directory
scynapse.system.reminder            # Reminder service
scynapse.system.stream              # Stream infrastructure
scynapse.system.security.gateway    # ISecurityGatewayGrain
scynapse.system.security.assertions # IClusterAssertionDirectoryGrain
scynapse.system.security.admin      # Security administration
scynapse.system.graintypes          # IGrainTypeDirectoryGrain
scynapse.system.plugins             # IPluginGrainLoader
```

System namespace policy: `scynapse.system.*` defaults to node-trusted (silos can call without CCaps). External clients need explicit CCaps for system operations.

### Application Namespace

```
scynapse.app.{grainInterface}.{method}

Examples:
scynapse.app.IOrderGrain.PlaceOrder
scynapse.app.IOrderGrain.*          # All methods on IOrderGrain
scynapse.app.IInventoryGrain.GetItem
scynapse.app.>                      # All application grains
```

Application namespace policy: Determined by `[SecurityPolicy]` and `[RequireCapability]` attributes on grain interfaces.

### Phase 2 Extension (Designed Now, Implemented Later)

```
scynapse.component.{componentType}.{grainInterface}.{method}

Examples:
scynapse.component.InventoryService.IInventoryGrain.GetItem
scynapse.component.InventoryService.>  # Everything in InventoryService
```

When Components arrive, `scynapse.app.*` becomes a compatibility alias that maps to the default Component.

### Wildcard Rules

| Pattern | Matches | Example |
|---------|---------|---------|
| Exact | One specific name | `scynapse.app.IOrderGrain.PlaceOrder` |
| `*` | One segment | `scynapse.app.*.GetItem` matches any grain's GetItem |
| `>` | One or more trailing | `scynapse.app.>` matches all app grains and methods |

### Impact on Existing Code

- `GrainResourceInference.FromGrainInterface(Type)` → returns `scynapse.app.{interfaceName}`
- `GrainResourceInference.FromGrainMethod(Type, MethodInfo)` → returns `scynapse.app.{interfaceName}.{methodName}`
- `DefaultAttenuationChecker` — pattern matching updated to use dot-separated hierarchy with `*` and `>` wildcards
- `ICCapWallet.FindCapability(resource, action)` — matching updated for hierarchical patterns
- All existing CCap creation code — resource URIs change format (from `scynapse:grain/X` to `scynapse.app.X`)

### NATS Alignment

NATS uses identical pattern: subjects are dot-separated, `*` matches one token, `>` matches remaining. NATS accounts have publish/subscribe permissions expressed as subject patterns. This is battle-tested at massive scale.

The alignment means: if Scynapse ever needs to interop with NATS (which it likely will, given it uses NATS as foundational infrastructure), the subject patterns are directly compatible.

---

## Part 4: Workflow Simulations (Post-Completion)

### Workflow 1: Organization Bootstrap

```bash
# Admin installs Scy.exe
$ dotnet tool install -g scy

# Initialize organization
$ scy init org --name "Acme Corp" --dir ./acme
  ✓ Organization key: OABC123...
  ✓ Identity assertion: ./acme/org-identity.assertion
  ✓ Seed file: ./acme/org.seed (KEEP SECRET)

# Initialize silos  
$ scy init node --name silo-1 --org ./acme
  ✓ Node key: NXYZ789...
  ✓ Delegation: org → silo-1
  ✓ Config: ./acme/silo-1/silo-security.json

$ scy init node --name silo-2 --org ./acme
  ✓ Node key: NQRS456...  
  ✓ Delegation: org → silo-2
  ✓ Config: ./acme/silo-2/silo-security.json

# Initialize user
$ scy init user --name alice --org ./acme
  ✓ User key: UALICE01...
  ✓ Delegation: org → alice

# Grant capabilities  
$ scy grant --to alice --resource "scynapse.app.IOrderGrain.>" --action "read,write" --org ./acme
  ✓ CCap: alice can read/write all IOrderGrain methods

$ scy grant --to alice --resource "scynapse.app.IInventoryGrain.*" --action "read" --org ./acme
  ✓ CCap: alice can read IInventoryGrain

# Generate deployment bundles
$ scy bundle --node silo-1 --org ./acme
  ✓ ./acme/silo-1/deploy/ (config + all peer assertions)

$ scy bundle --user alice --org ./acme  
  ✓ ./acme/alice/deploy/ (config + delegation + all CCaps)
```

### Workflow 2: Grain Developer

```csharp
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

public class OrderGrain : Grain, IOrderGrain
{
    public async Task<OrderDetails> GetDetailsAsync()
    {
        var caller = this.GetCallerPublicKey();        // direct caller (node or client)
        var original = this.GetOriginalCallerPublicKey(); // original end-user
        return _state.State.Details;
    }

    public async Task PlaceOrderAsync(OrderRequest request)
    {
        // Grain-to-grain call — node trust, original caller propagates
        var inventory = GrainFactory.GetGrain<IInventoryGrain>(request.ItemId);
        await inventory.ReserveAsync(request.Quantity);
        // InventoryGrain sees: caller=NodeKey (trusted), originalCaller=alice
    }
}

// High-security grain that requires end-user CCap even from silos
[SecurityPolicy(RequiresAuthentication = true, RequiresCallerCapability = true)]
public interface IFinancialGrain : IGrainWithStringKey
{
    [RequireCapability(Action = "transfer")]
    Task TransferAsync(decimal amount, string destination);
}
```

### Workflow 3: External Client

```csharp
// Configure from generated config
var client = new ClientBuilder()
    .UseScynapseSecurity(config.GetSection("ScynapseSecurity"))
    .Build();
await client.Connect();

// Option A: Pre-provisioned CCaps (from scy bundle output)
var grain = client.GetGrain<IOrderGrain>("order-123");
var details = await grain.GetDetailsAsync(); // CCap auto-selected from wallet

// Option B: Login flow
var gateway = client.GetGrain<ISecurityGatewayGrain>("default");
var bundle = await gateway.AuthenticateAsync(myDelegationChainCbor);
var wallet = client.ServiceProvider.GetRequiredService<ICCapWallet>();
foreach (var ccap in bundle.Capabilities)
    wallet.Store(SignedAssertion.Deserialize(ccap));
// Now make grain calls with dynamically acquired CCaps

// Unauthorized call — fails cleanly
try { await grain.CancelOrderAsync("test"); }
catch (ScynapseSecurityException ex) 
    when (ex.FailureCode == SecurityFailureCode.MissingAuthentication)
{ /* no admin CCap in wallet */ }
```

### Workflow 4: Development Mode

```csharp
// Zero-friction development setup
siloBuilder.UseScynapseSecurity(options => {
    options.DevelopmentMode = true;
    // Auto-generates all security artifacts
    // WARNING logged on every startup
});

// OR via CLI
$ scy dev quickstart --dir ./dev
  ✓ Generated all keys, assertions, configs for development
  WARNING: Development mode — DO NOT use in production
```

### Workflow 5: Silo-to-Silo (Automatic)

```
Silo 1 starts → loads keys from config → registers with ClusterAssertionDirectory
Silo 2 starts → loads keys → registers
Silo 1 connects to Silo 2:
  → TLS handshake (ECDSA bridge cert with Ed25519 in extension)
  → Pre-validated peer cache: Silo 2's key is known → TRUSTED
  → mTLS established
Client calls grain on Silo 1, grain forwards to Silo 2:
  → RequestContext carries: OriginalCallerKey, CallerKey (node), CCap, BearerProof
  → Silo 2 incoming filter: caller is trusted node → ALLOW (default policy)
  → Grain on Silo 2 can read original caller identity for audit
```

---

## Part 5: Phase 1 Completion Criteria

Phase 1 is done when:

1. **`scy init org/node/user` + `scy grant` + `scy bundle`** produce correct, deployable configurations without writing C# code
2. **Silos configure from JSON** generated by Scy.exe via `UseScynapseSecurity(IConfigurationSection)`
3. **Clients authenticate** via `ISecurityGatewayGrain` and acquire CCaps dynamically
4. **Grain-to-grain calls** work with node trust (hybrid model), original caller identity propagates
5. **Cross-silo CCap flow** verified in 2+ silo integration test
6. **Subject namespace** uses dot-separated hierarchical patterns with `*` and `>` wildcards
7. **DevelopmentMode** provides zero-friction setup
8. **IPluginGrainLoader + IGrainTypeDirectoryGrain** are access-controlled
9. **All Orleans system grains** function without interference (AllowAnonymous default)
10. **Structured error codes** and diagnostic logging in call filters

---

## Part 6: Scy.exe Design

### Project Structure

```
src/Scy/
├── Scy.csproj                    # Console app, dotnet tool
├── Program.cs                    # Command tree entry point
├── Commands/
│   ├── InitCommand.cs            # init org, init node, init user
│   ├── GrantCommand.cs           # grant (issue CCap)
│   ├── BundleCommand.cs          # bundle (deployment package)
│   ├── InspectCommand.cs         # inspect assertion/key files
│   ├── VerifyCommand.cs          # verify assertion chains
│   ├── DevCommand.cs             # dev quickstart
│   └── RotateCommand.cs          # key rotation (Priority 3)
├── IO/
│   ├── KeyFileFormat.cs          # .seed (33 bytes: 1 type + 32 seed), .pub (text: encoded key)
│   ├── AssertionFileFormat.cs    # .assertion/.ccap (CBOR binary)
│   └── ConfigFileFormat.cs       # JSON config generation
├── Context/
│   └── OrgContext.cs             # Reads org directory structure, knows topology
└── Rendering/
    └── SpectreRenderers.cs       # Tables, trees for inspect output
```

### Dependencies

```xml
<PackageReference Include="Spectre.Console" Version="0.49.*" />
<PackageReference Include="Spectre.Console.Cli" Version="0.49.*" />
<ProjectReference Include="../Scynapse.Security/Scynapse.Security.csproj" />
```

### File Formats

| Extension | Content | Secret? |
|-----------|---------|---------|
| `.seed` | 1 byte key type + 32 bytes Ed25519 seed | YES — never share |
| `.pub` | Single line: encoded public key string (e.g., `OABC123...`) | No — share freely |
| `.assertion` | CBOR-encoded SignedAssertion binary | No — share freely |
| `.ccap` | Same as `.assertion` (alias for Capability claim type) | Context-dependent |
| `-security.json` | Configuration for `UseScynapseSecurity()` | Contains file paths, not secrets |

### Org Directory Convention

```
./acme/                           # Organization root
├── org.seed                      # Organization private key (PROTECT)
├── org.pub                       # Organization public key
├── org-identity.assertion        # Self-signed identity
├── silo-1/                       # Per-node directory
│   ├── node.seed                
│   ├── node.pub
│   ├── node-delegation.assertion
│   ├── silo-security.json
│   └── deploy/                   # Deployment bundle
│       ├── silo-security.json
│       └── assertions/           # All needed assertions
├── silo-2/                       
│   └── ...
└── alice/                        # Per-user directory
    ├── user.seed
    ├── user.pub
    ├── user-delegation.assertion
    ├── ccaps/                    # Issued capabilities
    │   ├── orders-read.ccap
    │   └── inventory-read.ccap
    └── deploy/
        ├── client-security.json
        └── assertions/
```

---

## Part 7: What Carries Forward to Phase 2

| What | Phase 1 | Phase 2 |
|------|---------|---------|
| Crypto primitives | Unchanged | Unchanged |
| Signed Assertion format | Unchanged | Unchanged |
| Verification algorithm | Unchanged | Unchanged |
| Subject namespace | `scynapse.app.{grain}.{method}` | `scynapse.component.{type}.{grain}.{method}` |
| Trust boundary | Grain type (approximation) | Component (native) |
| Policy declaration | `[SecurityPolicy]` attributes | Component type definition metadata |
| Assertion store | InMemory / grain-backed | CNS-backed distributed |
| Call filters | Orleans `IIncoming/OutgoingGrainCallFilter` | Same pattern, Component-aware |
| Node trust | Silos trust each other | Components in same trust domain trust each other |
| Scy.exe | Org → Node → User | Org → Domain → Node → Component → Instance |
| Name resolution | GrainResourceInference (compile-time) | CNS (runtime, distributed) |

The migration path is interface swaps behind stable abstractions. No rewrites.

---

## Part 8: NuGet Dependencies

**Existing (in use):**
- `NSec.Cryptography` 25.4.0 — Ed25519, Blake2b-256, X25519
- `PeterO.Cbor` 4.5.5 — CBOR serialization

**New for Scy.exe:**
- `Spectre.Console` + `Spectre.Console.Cli` — TUI + command routing

**New for config loading:**
- `Microsoft.Extensions.Configuration.Json` (likely already an Orleans dependency)

**Custom (no external dependency):**
- `Base32.cs`, `Crc16.cs` — already implemented

---

## Part 9: Design Decisions Record

| Decision | Chosen | Rejected | Rationale |
|----------|--------|----------|-----------|
| Resource URI scheme | Dot-separated hierarchical (NATS-style) | Flat URIs, colon-separated | Phase 2 compatibility, NATS interop, proven at scale |
| Grain-to-grain | Hybrid (node trust + caller propagation) | Pure capability propagation, pure ambient | Balances Orleans compatibility with capability principles |
| Client auth | SecurityGatewayGrain + bootstrap CCaps | CCap request protocol, bootstrap-only | Familiar "login" pattern, breaks bootstrap paradox |
| Peer discovery | ClusterAssertionDirectoryGrain | Gossip, pre-shared only | Dynamic cluster membership, fallback to pre-shared |
| CLI model | Topology-aware (nsc-inspired) | Generic key/assertion factory | Generates correct chains, reduces operator error |
| Default policy | AllowAnonymous for unannotated | RequireAuth for all | Orleans system grain compatibility |
| Stream security | Document as limitation | Stream security filter | Phase 2, streams are intra-cluster |
| TLS validation | Pre-validated peer cache | Sync-over-async, bypass | Fast, no deadlock, defense-in-depth |

---

*Document version 4.0. Consolidated from two independent CCW analyses with CAI additions for Phase 2 compatibility and NATS-informed naming design. Ready for implementation.*
