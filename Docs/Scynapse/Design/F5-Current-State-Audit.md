# F5 Current State Audit: Trust, Capabilities, and Credential Lifecycle

Date: 2026-03-09
Author: Claude Opus 4.6
Purpose: Map existing Scynapse security implementation for spec-to-code bridge work

---

## Plain-Language Summary

**What this is:** A map of what Scynapse already has working for trust, capabilities, and credential lifecycle. This is not new analysis -- it points to existing comprehensive documentation with context relevant to Codex's protocol/conformance work.

**Why it matters:** F5 design should build on what exists (213 tests passing), not start from blank paper. Several concepts in the conformance harness (proof verification, nonce replay, grant chains, deny mapping) already have production implementations.

**What happens next:** Codex uses this to identify which conformance concepts have direct code analogs, which have partial analogs, and which are genuinely new design.

---

## 1. Source Documentation (Read These)

The security system is extensively documented across four files, plus the Vision docs. Here's what's in each and which sections matter for the bridge work.

### 1.1 Architecture Document (The "Why")

**File:** `Docs/Scynapse/Scynapse Security Development/scynapse-security-architecture_3.md`
**Length:** ~877 lines
**Authors:** Louis (architect) + Claude Opus 4.6 (design partner)

**What's in it:** Foundational security architecture including:
- The Unified Signed Assertion model (identity, capability, delegation, relation, revocation, impersonation -- all one primitive)
- 10 core security invariants
- Rejected alternatives and why (ACLs, OAuth2, X.509 PKI)
- Ed25519 identity primitive design
- Capability-based authorization (CCaps) with attenuation
- Trust model (node-level + caller-level + strict mode)
- Transport security (ECDSA P-256 bridge for TLS, Ed25519 in X.509 extension)
- Democratic anonymity design (Shamir's Secret Sharing for threshold de-anonymization)

**Relevant to Codex's work:**
- Part I Section 3 (Unified Signed Assertion) -- this IS the proof/verification model that the conformance harness simulates
- Part I Section 4-5 (CCap structure, attenuation) -- maps to the conformance engine's grant/claim semantics
- Part II (transport, channel binding) -- maps to handshake challenge/nonce binding in M1-S11
- The invariants -- these are the Locked Commitments that any protocol spec must preserve

### 1.2 Implementation Guide (The "What Was Built")

**File:** `Docs/Scynapse/Scynapse Security Development/scynapse-security-implementation-guide-v2_1.md`
**Length:** ~840 lines

**What's in it:** File-by-file, layer-by-layer implementation status:
- Layer 0: Cryptographic primitives (ScynapseKeyPair, key encoding, Base32, CRC16) -- 40 tests
- Layer 1: Signed Assertion core (ClaimType, Claims, SignedAssertion, AssertionBuilder, CBOR serialization) -- 28 tests
- Layer 2: Chain verification (AssertionVerifier, AssertionStore, NonceStore, AttenuationChecker) -- 44 tests
- Layer 3: Transport security (certificate factory, remote cert validator) -- 14 tests
- Layer 4: Orleans integration (call filters, wallet, policy attributes, SecurityGateway, grain resource inference) -- 25 unit + 6 integration tests
- Scy CLI: topology-aware provisioning tool

**Relevant to Codex's work:**
- Layer 2 is the direct analog to M1-S3/M1-S4 strict verification mode
- Layer 4 call filters are the production anchors for the B1 vertical spike
- The Scy CLI shows how grant issuance/chain construction works in practice

### 1.3 Phase 1 Review (Spec vs. Reality Gap Table)

**File:** `Docs/Scynapse/Scynapse Security Development/scynapse-security-phase1-review.md`

**What's in it:** A line-by-line comparison of what was specified vs. what was actually built, organized by layer. Includes status columns (DONE / PARTIAL / NOT DONE) and notes on each gap.

**Relevant to Codex's work:** This is the ready-made gap analysis for B1. It already identifies:
- What's fully implemented (most of Layers 0-2)
- What's partial (ImpersonationClaim, RevocationClaim payloads)
- What's missing (channel binding, X25519 key agreement, distributed stores)

### 1.4 Phase 1 Completion Guide (What Remains)

**File:** `Docs/Scynapse/Scynapse Security Development/scynapse-security-phase1-completion-guide-v4.md`

**What's in it:** Consolidated gap analysis from two independent reviews, with:
- Gap 1: Naming and resource addressing (NATS-inspired subject namespace design)
- Remaining implementation gaps with proposed solutions
- Phase 2 compatibility considerations

**Relevant to Codex's work:** The subject namespace design (`scynapse.app.{grain}.{method}` with NATS-style wildcards) is directly relevant to the CNS/resolution semantics in M0-A contracts.

### 1.5 Vision Documents (What Carries Forward)

**File:** `Docs/Scynapse/Scynapse-Vision.md` -- Part 7 (Security's Relationship to Vision) and Part 9 (What Carries Forward)
**File:** `Docs/Scynapse/Scynapse-V1.md` -- Full v1 technical documentation

**What's in them:** Explicit carry-forward vs. change tables showing which v1 security primitives are permanent and which are Orleans-specific.

---

## 2. Direct Mapping to Conformance Concepts

Here's how Codex's protocol/conformance concepts map to existing production code:

| Conformance Concept | Production Analog | Status |
|---|---|---|
| HandshakeProof verification (strict mode) | `AssertionVerifier.VerifyAsync()` chain walk | Exists, 44 tests |
| Nonce replay prevention | `INonceStore` / `InMemoryNonceStore` with TTL | Exists, tested |
| Grant chain / delegation | `DelegationClaim` + `DefaultAttenuationChecker` | Exists, tested |
| Claim attenuation (scope narrowing) | `IAttenuationChecker.Check(parent, child)` | Exists, 44 tests |
| Temporal bounds (expired/not-yet-valid) | `AssertionVerifier` checks `NotBefore`/`ExpiresAt` | Exists, tested |
| Revocation | `IAssertionStore.IsRevokedAsync()` | Exists, basic |
| Deny code mapping | `VerificationResult` with `FailureReason` enum | Exists, partial |
| Subject/scope matching | `GrainResourceInference` + NATS-style wildcard matching | Exists |
| Bearer proof generation | `ScynapseOutgoingCallFilter` signs CCap content hash | Exists |
| Proof verification at enforcement point | `ScynapseIncomingCallFilter` full pipeline | Exists, 6 integration tests |
| Reference token transport | Not directly analogous | Gap |
| Challenge nonce binding (M1-S11) | Not implemented (closest: nonce store for replay) | Gap |
| Reference grant status lifecycle | Not implemented | Gap |
| Deterministic error ID surface (E-series) | `VerificationResult.FailureReason` (enum, not string IDs) | Partial - different format |

---

## 3. Key Gaps for Bridge Work

Things the conformance harness models that don't yet exist in production:

1. **Reference token transport mode** -- the harness distinguishes inline vs. reference token transport. Production currently only has inline CCap flow via RequestContext.

2. **Challenge-session nonce binding** -- the harness validates that challenge nonces flow through HandshakeChallenge -> HandshakeProof -> HandshakeAccept. Production has nonce stores but no challenge-response handshake protocol -- verification happens at call-filter time, not through a multi-message handshake.

3. **Deterministic error ID surface** -- the harness uses string IDs (`E3080_M1S4_STRICT_EXPIRED`, etc.). Production uses a `FailureReason` enum. These need to be reconciled -- either the harness adopts the production enum, production adopts string IDs, or a mapping table bridges them.

4. **Multi-message handshake protocol** -- the conformance harness models a stateful multi-step handshake (Init -> Challenge -> Proof -> Accept/Deny). Production currently has a single-step call filter (outgoing filter attaches CCap, incoming filter verifies it). The handshake protocol is a new architectural layer that sits above/before the call filter.

5. **Grant lifecycle management** -- the harness models grant states (active, expired, revoked, missing). Production has `IsRevokedAsync()` and temporal bounds but no first-class "grant status" concept.

---

## 4. Decision Maturity Classification

Per the R&D methodology tiers:

| Area | Tier | Rationale |
|---|---|---|
| Ed25519 identity, Signed Assertions, CBOR serialization | Locked Commitment | 213 tests, proven, carries forward |
| Chain verification, attenuation, replay prevention | Locked Commitment | Fully tested, architecturally sound |
| CCap model (bearer tokens, delegation, scope narrowing) | Locked Commitment | Core security design, proven |
| Orleans call filter integration pattern | Design Baseline | Pattern survives, Orleans-specific types will change |
| Subject namespace (NATS-style dot-separated) | Design Baseline | Working, extensible, but CNS may reshape it |
| Multi-message handshake protocol (M0-B) | Design Baseline | Conformance-validated but not yet production-proven |
| Reference token transport, grant lifecycle | Explored Direction | Conformance-modeled, no production analog yet |
| Challenge nonce binding, issuer binding | Explored Direction | Conformance-modeled, no production analog yet |

---

## 5. Production Code Locations

For the B1 spike, these are the files that matter:

```
src/Scynapse/src/Scynapse.Security/                    # Zero Orleans dependency
  Assertions/AssertionVerifier.cs                       # Chain verification
  Assertions/AssertionBuilder.cs                        # Assertion construction
  Assertions/SignedAssertion.cs                         # Core primitive
  Assertions/InMemoryAssertionStore.cs                  # Store implementation
  Assertions/InMemoryNonceStore.cs                      # Replay prevention
  Assertions/DefaultAttenuationChecker.cs               # Scope narrowing
  Identity/ScynapseKeyPair.cs                           # Ed25519 keys
  Claims/Claims.cs                                      # All claim types

src/Scynapse/src/Scynapse.Security.Orleans/             # Orleans integration
  ScynapseIncomingCallFilter.cs                         # THE enforcement point
  ScynapseOutgoingCallFilter.cs                         # Auto CCap attachment
  ScynapseSecurityServiceCollectionExtensions.cs        # DI setup
  ICCapWallet.cs / InMemoryCCapWallet.cs                # Client-side wallet
  GrainResourceInference.cs                             # URI derivation from grain types

src/Scynapse/test/
  Scynapse.Security.Tests/                              # 142 unit tests
  Scynapse.Security.Orleans.Tests/                      # 26 Orleans unit tests
  Scynapse.Security.Integration.Tests/                  # 6 full integration tests
```

---

*This audit maps existing documentation and code. No new analysis was needed -- the implementation is well-documented. The value here is connecting what exists to what the conformance work needs.*
