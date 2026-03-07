# Scynapse Security — Implementation Guide

## Meta / Recovery Context

**Version:** 2.0 — Post-Implementation Status Report  
**Date:** 2026-03-06  
**Branch:** `claude/review-security-docs-QnVa8` (pushed, all tests passing)  
**Companion document:** `scynapse-security-architecture.md` — the *why*. READ THAT FIRST.

**What Scynapse is:** A fork/evolution of Microsoft Orleans (distributed actor platform). Currently uses Orleans's Silo/Client/Grain paradigm. Evolving toward a Component Model where "Component is the network." Security was designed to work on the current Orleans paradigm FIRST (Phase 1), then evolve with the Component Model (Phase 2).

**Key architectural decisions (from the architecture doc):**
- Ed25519 is THE identity primitive (`NSec.Cryptography`)
- The Signed Assertion is the single universal primitive (identity, capability, relation, delegation, revocation, impersonation — all one format)
- Trust boundary is the Component (future) / Grain type (current Orleans approximation)
- mTLS default transport, TLS as bootstrap ramp
- No ACLs — capability-based auth only (CCaps)
- CCaps are challengeable and channel-bindable
- Identity and Capability are unified: identity is the degenerate/root case of capability

---

## Phase 1 Status: IMPLEMENTED — with known gaps

### What Was Built

174 tests passing: 142 security unit tests + 26 Orleans unit tests + 6 integration tests.  
Target framework: net9.0.

#### Layer 0: Cryptographic Primitives — COMPLETE ✓

| File | Purpose |
|------|---------|
| `ScynapseKeyType.cs` | 8 entity types: Organization, Domain, Node, ComponentType, Instance, User, Encryption, Seed |
| `ScynapseKeyPair.cs` | Ed25519 keypair wrapper: Generate, FromSeed, FromPublicKey (verify-only), Sign, Verify, ExportSeed |
| `ScynapseKeyEncoding.cs` | Base32 + CRC16 + typed prefix encoding (NATS NKeys-inspired) |
| `Base32.cs` | RFC 4648 Base32 encode/decode (custom, avoids external dependency) |
| `Crc16.cs` | CRC-16/CCITT-FALSE for checksum integrity |

**Packages:** `NSec.Cryptography` 25.4.0 (Ed25519, Blake2b-256, X25519 — all from libsodium)

**40 unit tests** covering generation, deterministic seeds, sign/verify, verify-only keys, cross-key failure, dispose, all key types, encode/decode roundtrips, corruption detection.

#### Layer 1: Signed Assertion Core — COMPLETE ✓

| File | Purpose |
|------|---------|
| `ClaimType.cs` | 7 claim types: Identity (0x01), Capability (0x02), Delegation (0x03), Relation (0x04), Revocation (0x05), Impersonation (0x06), Extension (0xFF) |
| `Claims.cs` | `CapabilityClaim`, `DelegationClaim`, `RelationClaim`, `RevocationClaim` records with CBOR serialize/deserialize |
| `SignedAssertion.cs` | Immutable assertion class, content-addressed by Blake2b-256, `VerifySignature()` |
| `AssertionBuilder.cs` | Fluent builder + convenience factories: `CreateIdentity`, `CreateCapability`, `CreateDelegation`, `CreateRelation`, `CreateRevocation` |
| `AssertionSerializer.cs` | CBOR serialization (CTAP2 canonical form, integer map keys, sorted extension keys) via `PeterO.Cbor` |

**Packages:** `PeterO.Cbor` 4.5.5 (from nuget.org with package source mapping — only `PeterO.*` resolves from nuget.org, all other packages from Azure DevOps feeds)

**28 unit tests** covering creation + signing for all types, serialization roundtrip with signature verification, content hash integrity, tampering detection, extension fields, nonce preservation, builder validation.

#### Layer 2: Chain Verification — COMPLETE ✓

| File | Purpose |
|------|---------|
| `IAssertionStore.cs` | Async interface: `ResolveAsync`, `StoreAsync`, `IsRevokedAsync`, `FindBySubjectAsync` |
| `InMemoryAssertionStore.cs` | Thread-safe in-memory implementation with `Revoke()` |
| `INonceStore.cs` | `HasSeen`/`Record` interface |
| `InMemoryNonceStore.cs` | TTL-based expiry cleanup |
| `IAttenuationChecker.cs` | `Check(parent, child)` interface |
| `DefaultAttenuationChecker.cs` | Identity→anything, Delegation→Capability (pattern matching), Delegation→Delegation (narrowing: subset types, narrower patterns, decremented depth), temporal attenuation |
| `VerificationResult.cs` | `IsValid`, `FailureReason`, `FailedAssertion` |
| `AssertionVerifier.cs` | Recursive chain walker with configurable max depth (default 32) |
| `ByteMemoryEqualityComparer.cs` | Structural equality for `ReadOnlyMemory<byte>` |

**44 unit tests** covering valid chains (1-deep, 3-deep), broken chains, expired/not-yet-valid, attenuation violations, replay detection, max depth, revocation, tampering, pattern matching.

#### Layer 3: Transport Security — COMPLETE with caveats ⚠️

| File | Purpose |
|------|---------|
| `ScynapseCertificateFactory.cs` | Self-signed X.509 cert: ECDSA P-256 for TLS handshake + Ed25519 public key in custom X.509 extension (OID `1.3.6.1.4.1.99999.1.1`). Includes EKU OIDs. Documented as platform workaround for SslStream lacking Ed25519 TLS support. |
| `ScynapseRemoteCertificateValidator.cs` | Extracts Ed25519 key from peer cert extension, verifies assertion chain against trusted roots |
| `ScynapseSecurityOptions.cs` | Config: `NodeKeyPair`, `TrustedRoots`, `BootstrapAssertions`, `PeerAssertions`, `BootstrapCapabilities`, `EnableTls`, `RequireMutualTls` |

**14 unit tests** covering cert creation, Ed25519 key extraction, assertion chain validation, rejection of untrusted peers.

**Caveats (see "Known Gaps" below):** TLS is wired into Orleans's transport pipeline but disabled in TestCluster integration tests (`EnableTls=false`). The `RemoteCertificateValidation` callback uses `AllowAnyRemoteCertificate()` — identity verification happens at the call filter level, not TLS handshake level. This sidesteps the sync-over-async deadlock risk but means **TLS cert-level assertion chain verification is not exercised by tests.**

#### Layer 4: Orleans Integration — COMPLETE ✓

| File | Purpose |
|------|---------|
| `ScynapseIncomingCallFilter.cs` | THE enforcement point: verifies CCap chain, bearer proof, action/resource match |
| `ScynapseOutgoingCallFilter.cs` | Wallet-based per-call CCap selection, attaches caller identity + CCap + bearer proof to `RequestContext` |
| `SecurityPolicyAttribute.cs` | `[SecurityPolicy]` on grain interfaces |
| `RequireCapabilityAttribute.cs` | `[RequireCapability(Action = "...")]` on grain methods |
| `AttributeBasedPolicyProvider.cs` | Reads grain type attributes, caches per type |
| `GrainSecurityExtensions.cs` | `GetCallerPublicKey()`, `GetCallerCapability()`, `IssueCCapToCaller()` |
| `ScynapseSecurityLifecycleParticipant.cs` | Loads bootstrap assertions + peer assertions + capabilities at `ServiceLifecycleStage.First` |
| `ScynapseSecuritySiloBuilderExtensions.cs` | `ISiloBuilder.UseScynapseSecurity()` — single entry point |
| `ScynapseSecurityClientBuilderExtensions.cs` | `IClientBuilder.UseScynapseSecurity()` — client-side entry point |
| `ICCapWallet.cs` + `InMemoryCCapWallet.cs` | CCap storage with wildcard matching, expiry filtering, thread safety |
| `GrainResourceInference.cs` | Derives `scynapse:grain/{type}` resource URIs from grain interfaces |
| `ScynapseSecurityException.cs` | Serializable exception (inherits `ScynapseException`, `[GenerateSerializer]`) |

**25 unit tests + 6 integration tests.** Integration tests use real `TestCluster` with `UseScynapseSecurity()`, covering: valid CCap succeeds, caller identity readable by grain, anonymous access when policy permits, wrong action rejected, expired CCap rejected, no CCap rejected.

#### What Was Built Beyond the Original Plan

The original implementation guide planned Layers 0-4 as building blocks. The review/gap-analysis phase added:

| Addition | Purpose |
|----------|---------|
| `ICCapWallet` + `InMemoryCCapWallet` | Client-side CCap storage with per-call selection. Original plan had no wallet concept — the outgoing filter was a stub. |
| `IClientBuilder.UseScynapseSecurity()` | Client-side security configuration. Original plan only covered silo-side. |
| `GrainResourceInference` | Automatic `scynapse:grain/{type}` URI derivation from grain interfaces. Original plan had manual resource URIs. |
| `RevocationClaim` | Structured revocation payload with CBOR serialization. Original plan had revocation as a claim type but no concrete data structure. |
| `GrainSecurityExtensions.IssueCCapToCaller()` | Grains can issue CCaps to authenticated callers at runtime. Original plan didn't cover runtime CCap issuance. |
| `BootstrapCapabilities` in options | Pre-loaded CCaps for client startup. Original plan had no CCap bootstrapping story. |
| `PeerAssertions` in options | Pre-shared assertion chains for peer verification. Original plan had no peer assertion distribution. |
| `EnableTls` toggle | Allows disabling TLS for test environments. Practical necessity discovered during integration testing. |
| Default policy: `AllowAnonymous=true` for unannotated grains | Critical fix: Orleans system grains (MembershipTable, etc.) have no security attributes. Default must allow them through. Only explicitly `[SecurityPolicy]`-annotated grains enforce authentication. |

---

## Known Gaps — What's NOT Done

### Gap 1: TLS Transport-Level Assertion Verification — NOT TESTED END-TO-END

**Status:** The code exists (`ScynapseRemoteCertificateValidator`) and is wired into `UseScynapseSecurity()`. But integration tests disable TLS (`EnableTls=false`) and the TLS callback uses `AllowAnyRemoteCertificate()`. Identity enforcement happens entirely at the grain call filter level, not the TLS handshake.

**Impact:** In production, a rogue node could establish a TLS connection to a silo without presenting a valid assertion chain. It would be stopped at the grain call filter level (no valid CCap), but the connection itself would succeed. This is defense-in-depth we're missing, not a total security failure.

**What's needed:** An integration test (possibly outside TestCluster, since TestCluster uses in-process transport) that: (a) starts two silos as separate processes, (b) configures `EnableTls=true` with assertion chain validation, (c) verifies that a silo with a valid chain can connect, (d) verifies that a silo with an invalid/missing chain is rejected at the TLS level. This requires a more complex test harness than TestCluster provides.

**Also needed:** Fix the sync-over-async issue in the TLS validation callback. Currently avoided by using `AllowAnyRemoteCertificate()`. Options: make the assertion store lookup synchronous (it's in-memory), or use `Task.Run().GetAwaiter().GetResult()` to avoid sync context capture.

**Also needed:** `FindBySubjectAsync` currently returns first match. When TLS validation is re-enabled, it must filter by `ClaimType.Delegation` to avoid returning a capability assertion when a delegation is needed.

### Gap 2: No Key Provisioning Tooling — NO CLI

**Status:** Keys are generated programmatically via `ScynapseKeyPair.Generate()`. There is no CLI tool, no configuration file format, no auto-generation on first startup.

**Impact:** To use the security system, a developer must write C# code to: generate an Organization keypair, generate a Node keypair, create delegation assertions, serialize them, and pass them to `ScynapseSecurityOptions`. This is acceptable for development/testing but not for any real deployment.

**What's needed:**

A. **CLI tool** (`scynapse-security` or subcommand of a future `scynapse` CLI):
```
scynapse-security keygen --type Organization --output org.key
scynapse-security keygen --type Node --output node.key
scynapse-security delegate --issuer org.key --subject node.key --output node-delegation.assertion
scynapse-security issue-ccap --issuer org.key --subject user.key --resource "scynapse:grain/*" --action "*" --output user-ccap.assertion
scynapse-security inspect --file node-delegation.assertion   # human-readable dump
scynapse-security verify --file node-delegation.assertion --trusted-root org.pub
```

B. **Configuration file format** (JSON or YAML) for `ScynapseSecurityOptions`:
```json
{
  "nodeKeyFile": "node.key",
  "trustedRoots": ["org.pub"],
  "bootstrapAssertions": ["org-identity.assertion", "node-delegation.assertion"],
  "peerAssertions": ["other-node-delegation.assertion"],
  "bootstrapCapabilities": ["client-ccap.assertion"]
}
```

C. **Auto-generation option:** For development scenarios, `UseScynapseSecurity()` could have a `DevelopmentMode` that auto-generates a keypair and self-signs if no key is provided. MUST log a warning. MUST NOT be used in production.

### Gap 3: No TLS→mTLS Bootstrap Ramp (Gateway)

**Status:** Not implemented. The architecture doc describes a bootstrap sequence where an unauthenticated TLS connection is upgraded to mTLS via identity negotiation. Currently, connections are either TLS or not — there's no upgrade protocol.

**Impact:** New nodes or clients must have their identity and assertion chains pre-provisioned before connecting. There's no way for a node to connect to a cluster, present its identity, and be dynamically granted access. This limits deployment flexibility.

**What's needed:** A bootstrap protocol on the gateway connection:
1. Client connects with TLS (server-authenticated only).
2. Client sends a `BootstrapRequest` message containing its public key and assertion chain.
3. Server validates the chain against trusted roots.
4. If valid, server sends a `BootstrapResponse` with its own identity and available Components.
5. Connection may be upgraded to mTLS (if the transport supports renegotiation) or a new mTLS connection is established.

This is architecturally designed (in the architecture doc) but not yet implemented. It's more important for Phase 2 (where dynamic node joining matters) than for Phase 1 (where cluster membership is relatively static).

### Gap 4: No Runtime CCap Delivery Channel

**Status:** `GrainSecurityExtensions.IssueCCapToCaller()` exists — a grain can create a CCap for the caller. But the CCap is returned as a `SignedAssertion` object. There's no automatic mechanism to deliver it to the caller's wallet.

**Impact:** The caller must manually extract the CCap from the grain's return value and store it in their wallet. This works but is cumbersome. For a smooth developer experience, there should be an automatic "CCap grant" channel.

**What's needed:** Options (not mutually exclusive):
A. Return CCaps as part of the grain call response metadata (piggyback on `RequestContext` or a side channel).
B. A dedicated `ICCapGrantObserver` interface that clients implement to receive granted CCaps.
C. Simply document that grain methods returning CCaps should be stored by the caller. Let the application handle it.

Option C is sufficient for Phase 1. Options A and B are Phase 2 improvements.

### Gap 5: WhoAmI Test — Weak Assertion

**Status:** The integration test `GrainCanReadCallerIdentity` asserts the returned key is 32 bytes long but doesn't verify it's the *client's* specific key.

**Impact:** Minimal — the test would pass even if the wrong identity was being set. A stronger test would compare against the actual client's public key bytes.

**What's needed:** Thread the client's key seed through TestCluster configuration, reconstruct the key in the test, and assert byte-level equality.

### Gap 6: No Cross-Silo CCap Flow Test

**Status:** Integration tests use TestCluster (in-process). No test verifies that a CCap presented on Silo A successfully follows a grain call forwarded to Silo B (where the grain is activated on a different silo).

**Impact:** Orleans forwards grain calls across silos transparently. The `RequestContext` (carrying the CCap) should flow with the message. But this is untested.

**What's needed:** A TestCluster test with 2+ silos where a grain is activated on silo B and called from silo A. Verify the incoming filter on silo B receives and successfully verifies the CCap that was attached on silo A.

### Gap 7: Scynapse Extensions (Events, Properties) Security

**Status:** Not addressed. Scynapse's naturalized C# events (grain events bridged to SMS streams) and C# properties (StateTask<T>) are not covered by the security system.

**Impact:** If events or properties bypass the grain call filter pipeline, they have no security. If they go through call filters (because they're implemented as grain calls internally), they get security for free.

**What's needed:** Investigation into whether Scynapse's event/property implementations go through the standard grain call pipeline. If yes, document it. If no, add security hooks for them.

---

## NuGet Dependencies (Final)

**Core:**
- `NSec.Cryptography` 25.4.0 — Ed25519, Blake2b-256, X25519 (libsodium wrapper)
- `PeterO.Cbor` 4.5.5 — CBOR serialization (from nuget.org with `PeterO.*` package source mapping)

**Custom (no external dependency):**
- `Base32.cs` — RFC 4648 Base32 (custom implementation)
- `Crc16.cs` — CRC-16/CCITT-FALSE (custom implementation)

**Already in Scynapse/Orleans:**
- `System.Net.Security` (SslStream) — TLS/mTLS
- `System.Security.Cryptography` — ECDSA P-256 cert generation for TLS bridge
- Orleans serialization pipeline, ASP.NET Core Kestrel transport

---

## Project Structure

```
src/
├── Scynapse.Security/                          # Core — NO Orleans dependency
│   ├── ScynapseKeyType.cs
│   ├── ScynapseKeyPair.cs
│   ├── ScynapseKeyEncoding.cs
│   ├── Base32.cs
│   ├── Crc16.cs
│   ├── Assertions/
│   │   ├── ClaimType.cs
│   │   ├── Claims.cs                           # CapabilityClaim, DelegationClaim, RelationClaim, RevocationClaim
│   │   ├── SignedAssertion.cs
│   │   ├── AssertionBuilder.cs
│   │   └── AssertionSerializer.cs              # CBOR (CTAP2 canonical form)
│   ├── Verification/
│   │   ├── IAssertionStore.cs
│   │   ├── InMemoryAssertionStore.cs
│   │   ├── INonceStore.cs
│   │   ├── InMemoryNonceStore.cs
│   │   ├── IAttenuationChecker.cs
│   │   ├── DefaultAttenuationChecker.cs
│   │   ├── AssertionVerifier.cs
│   │   ├── VerificationResult.cs
│   │   └── ByteMemoryEqualityComparer.cs
│   └── Transport/
│       ├── ScynapseCertificateFactory.cs       # ECDSA bridge + Ed25519 in extension
│       ├── ScynapseRemoteCertificateValidator.cs
│       └── ScynapseSecurityOptions.cs
│
├── Scynapse.Security.Orleans/                  # Orleans integration
│   ├── ScynapseIncomingCallFilter.cs           # THE enforcement point
│   ├── ScynapseOutgoingCallFilter.cs           # Wallet-based CCap selection
│   ├── SecurityPolicyAttribute.cs
│   ├── RequireCapabilityAttribute.cs
│   ├── GrainSecurityPolicy.cs                  # Default: AllowAnonymous for unannotated grains
│   ├── AttributeBasedPolicyProvider.cs
│   ├── GrainSecurityExtensions.cs              # GetCallerPublicKey(), IssueCCapToCaller()
│   ├── GrainResourceInference.cs               # scynapse:grain/{type} URIs
│   ├── ICCapWallet.cs
│   ├── InMemoryCCapWallet.cs
│   ├── ScynapseSecurityException.cs
│   ├── ScynapseSecurityLifecycleParticipant.cs
│   ├── ScynapseSecuritySiloBuilderExtensions.cs
│   └── ScynapseSecurityClientBuilderExtensions.cs
│
test/
├── Scynapse.Security.Tests/                    # 40 + 28 + 44 = 112 unit tests
│   ├── ScynapseKeyPairTests.cs
│   ├── ScynapseKeyEncodingTests.cs
│   ├── Crc16Tests.cs
│   ├── SignedAssertionTests.cs
│   ├── ChainVerificationTests.cs
│   ├── AttenuationCheckerTests.cs
│   └── StoreTests.cs
│
├── Scynapse.Security.Orleans.Tests/            # 26 unit tests (call filters, policy, wallet)
│   ├── IncomingCallFilterTests.cs
│   ├── PolicyProviderTests.cs
│   ├── CCapWalletTests.cs
│   ├── RevocationClaimTests.cs
│   └── GrainResourceInferenceTests.cs
│
├── Scynapse.Security.Orleans.Tests/            # transport unit tests
│   └── TransportSecurityTests.cs               # 14 tests
│
└── Scynapse.Security.Integration.Tests/        # 6 integration tests (real TestCluster)
    └── ScynapseSecurityIntegrationTests.cs
```

---

## Design Decisions Made During Implementation (Not in Original Plan)

### Default Security Policy: AllowAnonymous for Unannotated Grains

**Problem:** Orleans has many internal system grains (MembershipTable, directory, etc.) that have no security attributes. The original design defaulted to `RequiresAuthentication=true` for all grains, which blocked Orleans system grains from functioning.

**Decision:** Unannotated grains default to `AllowAnonymous=true`. Only grains explicitly marked with `[SecurityPolicy(RequiresAuthentication = true)]` enforce authentication. This is "opt-in security" not "opt-out", which is less secure by default but necessary for Orleans compatibility.

**Implication for Phase 2:** When the Component Model arrives, the default should flip: Components should require authentication by default, with explicit opt-out for public endpoints. This is possible because Component-level infrastructure will be Scynapse-native (not inherited from Orleans system grains).

### Client-Side CCap Filtering (Fail-Fast)

**Observation:** The `InMemoryCCapWallet.FindCapability()` filters out expired and non-matching CCaps on the client side. This means the silo never sees invalid CCaps — it just sees "no CCap" (authentication required).

**Implication:** Error messages at the silo are always "Authentication required" (missing CCap), not "Invalid CCap" or "Expired CCap". The specific failure reason is client-local. This is actually good design (fail fast, don't send garbage over the wire) but changes the testing expectations — you can't test "silo rejects expired CCap" because the wallet filters it first.

### TLS Identity Verification Bypassed — Call Filter Is the Enforcement Point

**Decision:** The TLS `RemoteCertificateValidation` callback uses `AllowAnyRemoteCertificate()`. All identity/capability verification happens at the grain call filter level.

**Rationale:** Avoids sync-over-async deadlock risk in the TLS callback. Architecturally, TLS provides confidentiality, and the call filters provide authentication/authorization. Defense-in-depth at the TLS level is desirable but not critical.

**Risk:** A rogue node can establish a connection (encrypted) but cannot make any authorized grain calls (blocked by incoming filter). The gap is: the rogue node consumes a connection resource before being rejected.

---

## Phase 2: Forward-Looking (Component Model) — UNCHANGED

The Phase 2 design remains as originally specified. Key points:

**What preserves from Phase 1:** All of Layer 0 (crypto), Layer 1 (assertions), Layer 2 (verification). The grain call filter pattern. The mTLS transport.

**What changes:** Policy provider (attributes → Component type definitions), capability URI namespace (`scynapse:grain/{type}` → `scynapse:component/{type}/{grain}/{method}`), assertion store (in-memory → CNS-backed distributed), Component isolation on same Node (new mechanism needed).

**Migration path:** Interface swaps behind stable abstractions. `IAssertionStore`, `IGrainSecurityPolicyProvider`, `IAttenuationChecker` — all designed for Phase 2 implementation swaps without changing call sites.

---

## What's Needed to "Use Scynapse with Security" Today

For a developer to build a Scynapse application with the security system:

### Minimal Setup (Development)

```csharp
// 1. Generate keys (programmatic — no CLI yet)
using var orgKey = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
using var nodeKey = ScynapseKeyPair.Generate(ScynapseKeyType.Node);

// 2. Create identity + delegation chain
var orgIdentity = AssertionBuilder.CreateIdentity(orgKey);
var nodeDelegation = AssertionBuilder.CreateDelegation(
    orgKey, nodeKey.PublicKeyBytes,
    new[] { ClaimType.Capability, ClaimType.Delegation },
    new[] { orgIdentity.Id.ToArray() },
    resourcePattern: "scynapse:*", actionPattern: "*");

// 3. Configure silo
siloBuilder.UseScynapseSecurity(new ScynapseSecurityOptions
{
    NodeKeyPair = nodeKey,
    TrustedRoots = { orgKey.PublicKeyBytes.ToArray() },
    BootstrapAssertions = { orgIdentity, nodeDelegation },
    EnableTls = false  // or true for production
});

// 4. Annotate grains
[SecurityPolicy(RequiresAuthentication = true)]
public interface IMyGrain : IGrainWithStringKey
{
    [RequireCapability(Action = "read")]
    Task<string> GetDataAsync();
}

// 5. Client setup (with pre-provisioned CCap)
var userCCap = AssertionBuilder.CreateCapability(
    orgKey, userKey.PublicKeyBytes,
    "scynapse:grain/IMyGrain", "read",
    proofs: new[] { orgIdentity.Id.ToArray() });

clientBuilder.UseScynapseSecurity(new ScynapseSecurityOptions
{
    NodeKeyPair = userKey,
    TrustedRoots = { orgKey.PublicKeyBytes.ToArray() },
    BootstrapAssertions = { orgIdentity },
    PeerAssertions = { nodeDelegation },
    BootstrapCapabilities = { userCCap }
});

// 6. Make grain calls — security is automatic
var grain = client.GetGrain<IMyGrain>("my-id");
var data = await grain.GetDataAsync(); // CCap auto-selected from wallet, verified by silo
```

### What's Missing for Production Use

1. **CLI tooling** for key generation and assertion management (Gap 2)
2. **TLS transport-level verification** (Gap 1) — currently bypassed
3. **Cross-silo CCap flow validation** (Gap 6) — untested
4. **Event/property security** (Gap 7) — uninvestigated
5. **Key rotation** — no mechanism to rotate Node keys without restarting
6. **Assertion persistence** — `InMemoryAssertionStore` loses everything on restart
7. **Configuration file loading** — keys/assertions loaded from code, not config files

---

## References

### Specifications (Architecture Roots)
- UCAN v1.0.0-rc.1 — github.com/ucan-wg/spec (capability token model)
- NATS Security — docs.nats.io (NKeys, JWT, challenge-response)
- Ed25519 — ed25519.cr.yp.to (signature scheme)
- X25519 — RFC 7748 (key agreement)
- Channel Binding — RFC 5929, RFC 8471 (token/TLS binding)
- Shamir's Secret Sharing — Adi Shamir 1979 (future: threshold de-anonymization)

### .NET Libraries (In Use)
- `NSec.Cryptography` 25.4.0 — Ed25519, Blake2b-256, X25519
- `PeterO.Cbor` 4.5.5 — CBOR serialization

### .NET Libraries (Available, Not Yet Used)
- `nkeys.net` (nats-io/nkeys.net) — NATS NKeys for .NET (reference)
- `jwt.net` (nats-io/jwt.net) — NATS JWT for .NET (reference)
- `NaCl.Net` — NaCl Box (future: X25519 encrypted channels)

### UCAN Implementations (Reference Only — No C# Exists)
- TypeScript: `@ucans/ucans`
- Rust: `rs-ucan` (ucan-wg/rs-ucan)
- Go: `go-ucan` (ucan-wg/go-ucan)

---

*Document reflects state as of 2026-03-06. Branch: `claude/review-security-docs-QnVa8`. 174 tests passing.*
