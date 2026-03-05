# Scynapse Security Phase 1 — Implementation Review Report

**Date:** 2026-03-05
**Reviewer:** Claude Opus 4.6 (at Louis's request)
**Scope:** Compare Phase 1 implementation against architecture/implementation-guide specs, assess tests, identify gaps

---

## Executive Summary

Phase 1 delivered **strong cryptographic foundations** and **solid Orleans integration scaffolding**. The core primitives (Ed25519 keys, Signed Assertions, CBOR serialization, Blake2b-256 content addressing, chain verification with attenuation) are fully implemented and well-tested at the unit level. The Orleans call filter integration is architecturally correct.

However, the system is **not yet usable end-to-end**. There are no integration tests proving the complete flow works on actual Orleans clusters. Critical operational pieces are missing: no key generation tooling, no CCap issuance workflow, no client SDK, no bootstrap ramp, and no way for grain types to issue or request capabilities at runtime. The tests (132 methods, not 151 as claimed) validate building blocks in isolation but do not prove the assembled system works.

**Bottom line:** The foundation is solid, but significant work remains before a developer could use Scynapse with security enabled.

---

## Part 1: What Was Specified vs. What Was Built

### Layer 0: Cryptographic Primitives

| Spec Item | Status | Notes |
|-----------|--------|-------|
| Ed25519 keypair management (NSec) | DONE | `ScynapseKeyPair` — generate, sign, verify, seed import/export |
| Key type prefixes (NATS-inspired) | DONE | 8 types: Organization, Domain, Node, ComponentType, Instance, User, Encryption, Seed |
| Base32 + CRC16 encoding | DONE | Custom Base32 (RFC 4648, no padding) + CRC-16/CCITT-FALSE |
| Encoded key strings | DONE | `ToEncodedPublicKey()`, `ToEncodedSeed()`, roundtrip decode |
| X25519 key agreement | NOT DONE | KeyType.Encryption enum exists but no DH key agreement API |

### Layer 1: Signed Assertion Core

| Spec Item | Status | Notes |
|-----------|--------|-------|
| SignedAssertion structure | DONE | All fields: version, id, issuer, subject, claim, scope, proofs, extensions, signature |
| Blake2b-256 content hash IDs | DONE | Deterministic content addressing |
| Ed25519 signing over content+hash | DONE | Signature over `content_bytes \|\| id` |
| CBOR serialization (PeterO.Cbor) | DONE | Deterministic CTAP2 canonical encoding |
| ClaimType enum (7 types) | DONE | Identity, Capability, Delegation, Relation, Revocation, Impersonation, Extension |
| AssertionBuilder with convenience factories | DONE | `CreateIdentity`, `CreateCapability`, `CreateDelegation`, `CreateRelation` |
| CapabilityClaim, DelegationClaim, RelationClaim | DONE | Full binary serialization |
| ImpersonationClaim | PARTIAL | ClaimType enum exists, no serialization or verification logic |
| RevocationClaim | PARTIAL | ClaimType exists, no structured claim payload (target hash, reason) |

### Layer 2: Verification

| Spec Item | Status | Notes |
|-----------|--------|-------|
| AssertionVerifier with full chain walk | DONE | Recursive proof resolution, signature + scope + chain |
| VerifyLocal (fast, no chain walk) | DONE | Signature + temporal scope only |
| IAssertionStore + InMemoryAssertionStore | DONE | Store, resolve, revoke, find-by-subject |
| INonceStore + InMemoryNonceStore | DONE | TTL-based cleanup |
| IAttenuationChecker + DefaultAttenuationChecker | DONE | Pattern matching, scope narrowing, depth limits, temporal narrowing |
| Trusted roots verification | DONE | Chain must terminate at trusted root public key |
| Revocation checking | DONE | `IsRevokedAsync()` in verification pipeline |
| Channel binding | NOT DONE | Spec describes TLS session binding; not implemented |

### Layer 3: Transport Security

| Spec Item | Status | Notes |
|-----------|--------|-------|
| Self-signed X.509 cert from Ed25519 key | DONE | Via ECDSA P-256 bridge (Ed25519 embedded as X.509 extension) |
| Custom remote cert validation | DONE | `ScynapseRemoteCertificateValidator` — extracts Ed25519, walks assertion chain |
| TLS server/client middleware | DONE | `TlsServerConnectionMiddleware`, `TlsClientConnectionMiddleware` |
| mTLS for silo-to-silo | PARTIALLY DONE | Code exists but not wired into `UseScynapseSecurity()` correctly |
| TLS bootstrap ramp (TLS -> identity negotiation -> mTLS upgrade) | NOT DONE | Spec describes 3-phase bootstrap; no implementation |
| Client SDK TLS integration | NOT DONE | No `IClientBuilder.UseScynapseSecurity()` extension |

### Layer 4: Orleans Integration

| Spec Item | Status | Notes |
|-----------|--------|-------|
| ScynapseIncomingCallFilter | DONE | Reads CCap from RequestContext, verifies signature/chain/bearer/action |
| ScynapseOutgoingCallFilter | DONE | Attaches caller key, CCap bytes, bearer proof to RequestContext |
| SecurityPolicyAttribute | DONE | `RequiresAuthentication`, `AllowAnonymous` |
| RequireCapabilityAttribute | DONE | `Action`, `Resource` on grain methods |
| AttributeBasedPolicyProvider | DONE | Reflection-based, cached |
| GrainSecurityExtensions | DONE | `GetCallerPublicKey()`, `GetCallerCapability()` from grain code |
| UseScynapseSecurity() one-line setup | DONE | Registers DI, cert, validator, filters, lifecycle |
| ScynapseSecurityLifecycleParticipant | DONE | Loads bootstrap assertions at lifecycle stage First |

### Layer 5 (Implied): Operational Concerns

| Spec Item | Status | Notes |
|-----------|--------|-------|
| CLI tool for key generation | NOT DONE | No `scynapse` CLI or equivalent |
| Auto-generation of silo identity on first start | NOT DONE | Caller must supply `NodeKeyPair` |
| Persistent assertion store | NOT DONE | Only in-memory |
| Distributed assertion store (CNS-backed) | NOT DONE | Future, per spec |
| Key rotation | NOT DONE | No mechanism |
| Revocation broadcast/propagation | NOT DONE | Manual `Revoke()` only |
| Configuration to swap store implementations | NOT DONE | Hardcoded to InMemory in UseScynapseSecurity |

---

## Part 2: Test Analysis

### Test Count

The claim was "151 tests across all 5 layers." Actual count from code analysis:

| Test File | Methods | Layer |
|-----------|---------|-------|
| ScynapseKeyPairTests.cs | 14 | 0 - Crypto |
| ScynapseKeyEncodingTests.cs | 11 | 0 - Crypto |
| Crc16Tests.cs | 2 | 0 - Crypto |
| SignedAssertionTests.cs | 30 | 1 - Assertions |
| ChainVerificationTests.cs | 22 | 2 - Verification |
| AttenuationCheckerTests.cs | 9 | 2 - Verification |
| StoreTests.cs | 8 | 2 - Verification |
| TransportSecurityTests.cs | 12 | 3 - Transport |
| IncomingCallFilterTests.cs | 8 | 4 - Orleans |
| OutgoingCallFilterTests.cs | 1 | 4 - Orleans |
| PolicyProviderTests.cs | 7 | 4 - Orleans |
| SecurityPolicyTests.cs | 5 | 4 - Orleans |
| GrainSecurityExtensionsTests.cs | 4 | 4 - Orleans |
| TlsConnectionTests.cs | 2 | 3 - Transport |
| **TOTAL** | **~135** | |

Note: Some test methods are `[Theory]` with multiple `[InlineData]` parameters. Depending on how you count (test methods vs. test cases), the count varies between 132-135 methods and perhaps 151 individual runs when theories expand. The discrepancy is likely in counting theory cases as separate tests.

### Quality Assessment of Tests

#### Layer 0 (Crypto) — Excellent

Tests cover key generation randomness, deterministic seeding, sign/verify roundtrips, tamper detection, wrong-key rejection, verify-only restrictions, disposal, all key types, encoding roundtrips, CRC corruption detection, and format compliance. These are thorough and test real functionality.

#### Layer 1 (Assertions) — Excellent

30 tests covering all claim types, serialization roundtrips, content hash determinism, tamper detection on subject/signature/ID fields, extension preservation, proof ordering, builder validation, and deterministic signing. Strong coverage.

#### Layer 2 (Verification) — Excellent

22 chain verification tests covering: trusted/untrusted roots, 1-deep and 3-deep chains, broken chains, expired/not-yet-valid assertions, resource/action scope enforcement, delegation narrowing, temporal narrowing, replay prevention, max depth enforcement, revocation, tampered signatures, local verification, a full operator→node→session→capability scenario. 9 attenuation tests, 8 store tests. Comprehensive unit coverage.

#### Layer 3 (Transport) — Good but Incomplete

Certificate creation tests are solid (7 tests for cert factory, 5 for remote validator). The `TlsConnectionTests.TlsEndToEnd` is the **only test that spawns actual Orleans silos** — it creates a `TestCluster` and verifies grain calls work over TLS with various certificate configurations.

**Critical gap:** This TLS test does NOT test Scynapse security features. It tests the Connections.Security TLS middleware (which appears to be adapted from the original Orleans TLS sample), not the Scynapse-specific assertion-chain-based certificate validation. It uses standard X.509 certs, not Ed25519-derived certs.

#### Layer 4 (Orleans Integration) — Weak

All 25 Orleans integration tests use **mock grain call contexts** (`TestIncomingGrainCallContext`, `TestOutgoingGrainCallContext`). No test spawns an actual Orleans silo with the security system active. This means:

- The call filters are tested in isolation but never proven to work in the real Orleans pipeline
- RequestContext flow across silo boundaries is not tested
- The `UseScynapseSecurity()` extension method is never called in any test
- No test proves a grain call actually succeeds or fails based on security policy in a running cluster

### Verdict: Are the Tests Sufficient to Claim "Work Accomplished and Successfully Tested"?

**No.** The tests prove the individual security primitives work correctly. They do not prove the integrated system works. Specifically:

1. **No test spawns a Scynapse-secured cluster and makes a grain call.** The TLS test spawns a cluster but without Scynapse security. The security tests use mocks but no cluster.

2. **No test proves CCaps flow through real Orleans serialization.** The CCap is serialized/deserialized in unit tests, but never passed through actual Orleans RequestContext serialization across a network boundary.

3. **No test proves mTLS works between two Scynapse silos.** The cert factory and validator are tested in isolation. No test connects two silos using Ed25519-derived certificates.

4. **No test proves the bearer proof mechanism works end-to-end.** Bearer proof is tested in the mock call filter, but never in a real grain call.

---

## Part 3: Answering Specific Questions

### How Are IDs/Keys Generated?

Keys are generated programmatically via `ScynapseKeyPair.Generate(keyType)`. There is **no CLI tool** (`scynapse.exe` or similar). There is **no auto-generation** — the caller of `UseScynapseSecurity()` must supply a pre-generated `NodeKeyPair` and a set of `TrustedRoots`.

**Current flow to set up a silo:**
```csharp
// 1. Generate keys programmatically (where? when? up to the developer)
var orgKey = ScynapseKeyPair.Generate(ScynapseKeyType.Organization);
var nodeKey = ScynapseKeyPair.Generate(ScynapseKeyType.Node);

// 2. Create assertion chain (manually, in application code)
var orgIdentity = AssertionBuilder.CreateIdentity(orgKey);
var nodeDelegation = AssertionBuilder.CreateDelegation(
    orgKey, nodeKey.PublicKeyBytes,
    new[] { ClaimType.Capability, ClaimType.Delegation },
    new[] { orgIdentity.Id.ToArray() });

// 3. Wire up the silo (must supply everything)
builder.UseScynapseSecurity(new ScynapseSecurityOptions {
    NodeKeyPair = nodeKey,
    TrustedRoots = { orgKey.PublicKeyBytes },
    BootstrapAssertions = { orgIdentity, nodeDelegation }
});
```

**What's missing:** A key provisioning workflow. In a real deployment, you'd want:
- A CLI tool to generate org/node keys and export encoded seeds
- A way to store seeds securely (environment variables, key vault, file)
- A silo startup path that loads seeds from configuration
- Optionally, auto-generation on first start with seed persistence

### How Do Silos Know If Other Silos Are Legit?

The design is: when a TLS connection is established between silos, the custom certificate validator (`ScynapseRemoteCertificateValidator`) extracts the peer's Ed25519 public key from the certificate's custom extension, then looks up assertion chains for that key in the `IAssertionStore`, and verifies the chain terminates at a trusted root.

**What works:**
- Certificate creation with embedded Ed25519 identity (`ScynapseCertificateFactory`)
- Remote certificate validation logic (`ScynapseRemoteCertificateValidator`)
- Assertion chain verification (`AssertionVerifier`)
- Unit tests confirm each piece works independently

**What does NOT work yet:**
- **Pre-sharing assertion chains:** Both silos need each other's delegation chains in their assertion stores BEFORE the TLS handshake. There is no mechanism to distribute these assertions. The `BootstrapAssertions` option loads the local node's chain, but not the remote node's.
- **No actual integration:** `UseScynapseSecurity()` creates the certificate and validator, but the integration between the `Scynapse.Security.Transport` validator and the `Scynapse.Connections.Security` TLS middleware is not wired. These are two separate projects with different approaches to TLS.
- **No silo-to-silo test:** This has never been tested with two actual silos.

### How Are Capabilities (CCaps) Issued, Stored, Requested, and Supplied?

#### Issuance

CCaps are created via `AssertionBuilder.CreateCapability(issuer, subject, resource, action, proofs)`. This is a static factory method. There is:

- **No grain-level CCap issuance API.** A grain type cannot "issue CCaps" to callers as part of its interface.
- **No automatic CCap issuance.** The developer must create CCaps manually in application code.
- **No CCap request protocol.** There is no mechanism for a client to say "I need a CCap for grain X, action Y" and receive one.

#### Storage

CCaps (being `SignedAssertion`s) are stored in `IAssertionStore`. Currently, this is only `InMemoryAssertionStore`. The outgoing call filter requires the CCap bytes to be available — but there is no API to "find the right CCap for this grain call." The developer would need to manage their own CCap lookup.

#### Presentation

The `ScynapseOutgoingCallFilter` attaches CCap bytes to `RequestContext` on every outgoing grain call. But:

- **Where does the filter get the CCap?** Looking at the implementation: the outgoing filter takes a configured CCap via constructor injection. There is **no dynamic CCap selection** per grain call. It appears to attach the same CCap to every call — which is architecturally wrong for a capability system where different grain calls need different capabilities.
- **No CCap wallet/registry.** There is no component that maps (grain type, action) → appropriate CCap.

#### Verification on Incoming Calls

The `ScynapseIncomingCallFilter` correctly:
1. Reads CCap bytes from RequestContext
2. Deserializes the assertion
3. Verifies signature, chain, temporal scope, revocation, replay
4. Checks bearer proof (caller proves they own the CCap's subject key)
5. Checks action matches `[RequireCapability]` attribute on the grain method

This is the strongest part of the implementation.

#### What Grains Can Do

- **Read caller identity:** `GrainSecurityExtensions.GetCallerPublicKey()` — works
- **Read presented CCap:** `GrainSecurityExtensions.GetCallerCapability()` — works
- **Issue CCaps:** Not possible from grain code currently
- **Require CCaps:** Via `[RequireCapability(Action = "read")]` attribute — works
- **Custom authorization logic:** Possible in grain code by reading verified context, but no framework support

#### What Clients Can Do

- **Present CCaps:** Via `ScynapseOutgoingCallFilter` on client — the filter exists but client-side `UseScynapseSecurity()` is not implemented
- **Store CCaps:** No client-side CCap wallet
- **Request CCaps:** No protocol for CCap acquisition
- **Auto-attach CCaps to grain calls:** Not implemented for per-call CCap selection

### Was Any of This Tested?

**CCap issuance:** Tested in unit tests (`AssertionBuilder.CreateCapability` in `SignedAssertionTests`).
**CCap storage:** Tested in unit tests (`InMemoryAssertionStore` in `StoreTests`).
**CCap presentation via RequestContext:** Tested with mock contexts (`IncomingCallFilterTests`, `OutgoingCallFilterTests`).
**CCap on actual grain calls:** **Not tested.**
**CCap selection per grain/action:** **Not implemented, not tested.**
**Client CCap workflow:** **Not implemented, not tested.**

---

## Part 4: What's Missing to Actually Use Scynapse Security

### 4.1 Key Provisioning (Priority: HIGH)

**Problem:** No operational way to generate, store, or distribute keys.

**What's needed:**

```
Option A: CLI Tool
$ scynapse-keygen --type organization --output org.seed
  Organization Public Key: OABC123...
  Seed written to: org.seed

$ scynapse-keygen --type node --output node.seed
  Node Public Key: NABC123...
  Seed written to: node.seed

$ scynapse-issue-delegation --issuer org.seed --subject NABC123... \
    --allow-types capability,delegation --output node-delegation.assertion

Option B: Configuration-Based
// appsettings.json
{
  "Scynapse": {
    "Security": {
      "NodeSeed": "SNABC123...",          // or from env var, key vault
      "TrustedRoots": ["OABC123..."],
      "BootstrapAssertionFiles": ["./assertions/node-delegation.bin"]
    }
  }
}

Option C: Auto-Generation (dev mode)
builder.UseScynapseSecurity(options => {
    options.AutoGenerateIdentity = true;  // Generate on first start, persist seed
    options.SeedStoragePath = "./data/node.seed";
    options.TrustAllLocalSilos = true;    // Dev mode only
});
```

**Where to implement in source:**
- New project: `Scynapse.Security.Tools` (CLI tool)
- In `ScynapseSecuritySiloBuilderExtensions.cs`: add configuration overloads
- In `ScynapseSecurityOptions.cs`: add auto-generation and configuration-loading options

### 4.2 Client-Side Security (Priority: HIGH)

**Problem:** No `IClientBuilder.UseScynapseSecurity()`. External clients cannot participate in the security system.

**What's needed:**

```csharp
// In Scynapse.Security.Orleans:
public static class ScynapseSecurityClientBuilderExtensions
{
    public static IClientBuilder UseScynapseSecurity(
        this IClientBuilder builder,
        ScynapseSecurityOptions options)
    {
        builder.ConfigureServices(services =>
        {
            // Same core registrations as silo
            services.AddSingleton(options);
            services.AddSingleton(options.NodeKeyPair); // "ClientKeyPair" semantically
            services.AddSingleton<IAssertionStore, InMemoryAssertionStore>();
            services.AddSingleton<INonceStore, InMemoryNonceStore>();
            services.AddSingleton<IAttenuationChecker, DefaultAttenuationChecker>();

            // Outgoing filter for clients
            services.AddSingleton<IOutgoingGrainCallFilter, ScynapseOutgoingCallFilter>();
        });

        // Configure TLS to silo gateway
        // ...

        return builder;
    }
}
```

**Where to implement:**
- `Scynapse.Security.Orleans/ScynapseSecurityClientBuilderExtensions.cs` (new file)
- Modify `ScynapseOutgoingCallFilter` to support per-call CCap selection

### 4.3 CCap Wallet / Registry (Priority: HIGH)

**Problem:** The outgoing call filter has no way to select the appropriate CCap for each grain call. The current design appears to use a single CCap for all calls.

**What's needed:**

```csharp
// New interface: maps grain calls to appropriate CCaps
public interface ICCapWallet
{
    /// <summary>
    /// Find a valid CCap that authorizes the given action on the given resource.
    /// Returns null if no matching CCap is available.
    /// </summary>
    SignedAssertion? FindCapability(string resource, string action);

    /// <summary>
    /// Store a CCap received from a grain or peer.
    /// </summary>
    void Store(SignedAssertion ccap);

    /// <summary>
    /// Remove expired or revoked CCaps.
    /// </summary>
    void Cleanup();
}

// Modified outgoing filter:
public sealed class ScynapseOutgoingCallFilter : IOutgoingGrainCallFilter
{
    private readonly ICCapWallet _wallet;
    private readonly ScynapseKeyPair _identity;

    public async Task Invoke(IOutgoingGrainCallContext context)
    {
        // Derive resource from target grain type
        var grainType = context.Grain.GetType();
        var resource = $"scynapse:grain/{grainType.FullName}";

        // Derive action from method attribute or method name
        var action = GetRequiredAction(context.InterfaceMethod) ?? "invoke";

        // Find matching CCap
        var ccap = _wallet.FindCapability(resource, action);
        if (ccap != null)
        {
            RequestContext.Set(ScynapseSecurityConstants.CallerPublicKeyKey,
                _identity.PublicKeyBytes);
            RequestContext.Set(ScynapseSecurityConstants.CCapKey,
                ccap.Serialize());
            RequestContext.Set(ScynapseSecurityConstants.BearerProofKey,
                _identity.Sign(ccap.Id.Span));
        }

        await context.Invoke();
    }
}
```

**Where to implement:**
- `Scynapse.Security/ICCapWallet.cs` (new)
- `Scynapse.Security/InMemoryCCapWallet.cs` (new)
- Modify `ScynapseOutgoingCallFilter` to use `ICCapWallet`
- Register `ICCapWallet` in `UseScynapseSecurity()`

### 4.4 CCap Issuance by Grains (Priority: MEDIUM)

**Problem:** Grain types cannot issue CCaps to callers. The spec envisions grains as resource owners that grant capabilities.

**What's needed:**

```csharp
// Extension method for grain code:
public static class GrainSecurityExtensions
{
    // Existing:
    public static byte[]? GetCallerPublicKey(this Grain grain) { ... }
    public static SignedAssertion? GetCallerCapability(this Grain grain) { ... }

    // NEW: Issue a CCap to the caller
    public static SignedAssertion IssueCCapToCaller(
        this Grain grain,
        string action,
        string? resource = null,
        long? expiresAt = null)
    {
        var callerKey = grain.GetCallerPublicKey()
            ?? throw new InvalidOperationException("No authenticated caller");

        var grainKey = /* grain's signing key - needs to be available */;
        resource ??= $"scynapse:grain/{grain.GetType().FullName}";

        // The grain's delegation from the node serves as proof
        var proofs = /* delegation chain */;

        return AssertionBuilder.CreateCapability(
            grainKey, callerKey, resource, action, proofs, expiresAt);
    }
}
```

**Challenge:** Grains don't currently have their own keypairs. In the current design, the node key is the only signing key. For grain-level CCap issuance, either:
- Grains use the node key to issue CCaps (simpler, but grains can't independently manage authority)
- Grains get their own keypairs (per the Component Model vision), requiring component-type-level key management

**Where to implement:**
- Extend `GrainSecurityExtensions.cs`
- Add grain/component key management to `ScynapseSecurityLifecycleParticipant`

### 4.5 Assertion Pre-sharing Between Silos (Priority: HIGH)

**Problem:** The remote certificate validator needs to find assertion chains for remote peers, but assertions are only loaded for the local node via `BootstrapAssertions`. Remote nodes' delegation chains are never loaded.

**What's needed:**

```csharp
// Option 1: Pre-shared assertions (simplest)
new ScynapseSecurityOptions {
    // ... existing
    TrustedPeerAssertions = {
        // Load delegation chains for known peer silos
        LoadAssertions("./assertions/silo2-delegation.bin"),
        LoadAssertions("./assertions/silo3-delegation.bin"),
    }
}

// Option 2: On-demand assertion exchange
// During TLS handshake, peers exchange their delegation chains
// as part of a custom post-handshake protocol

// Option 3: Assertion discovery service
// A well-known endpoint serves assertion chains for cluster members
```

**Where to implement:**
- `ScynapseRemoteCertificateValidator` needs access to peer assertion chains
- `ScynapseSecurityOptions` needs a `TrustedPeerAssertions` collection
- Or: implement assertion exchange protocol in `TlsServerConnectionMiddleware`

### 4.6 Wiring Scynapse.Security.Transport into Scynapse.Connections.Security (Priority: HIGH)

**Problem:** Two separate TLS systems exist:
1. `Scynapse.Connections.Security` — the TLS middleware (adapted from Orleans TLS sample). Uses standard X.509 certs.
2. `Scynapse.Security.Transport` — the Ed25519 cert factory and assertion-chain validator. Not plugged into the middleware.

**What's needed:** Connect them. The `UseScynapseSecurity()` method needs to:
1. Create an Ed25519-derived cert via `ScynapseCertificateFactory`
2. Set it as the server cert in `TlsServerAuthenticationOptions`
3. Set `ScynapseRemoteCertificateValidator.Validate` as the `RemoteCertificateValidationCallback`
4. Configure `RemoteCertificateMode.RequireCertificate` for silo-to-silo (mTLS)

**Where to implement:**
- `ScynapseSecuritySiloBuilderExtensions.UseScynapseSecurity()` — the wiring code
- May need adapters between the two TLS option types

### 4.7 DI Configuration for Swappable Stores (Priority: LOW)

**Problem:** `UseScynapseSecurity()` hardcodes `InMemoryAssertionStore` and `InMemoryNonceStore`. The interfaces exist for swapping, but the DI registration doesn't support it.

**What's needed:**

```csharp
builder.UseScynapseSecurity(options => {
    options.NodeKeyPair = nodeKey;
    options.TrustedRoots.Add(orgKey.PublicKeyBytes);
    options.BootstrapAssertions.Add(orgIdentity);
}, services => {
    // Override default implementations
    services.AddSingleton<IAssertionStore, RedisAssertionStore>();
    services.AddSingleton<INonceStore, DistributedNonceStore>();
});
```

**Where to implement:** `ScynapseSecuritySiloBuilderExtensions.cs` — add service configuration callback

---

## Part 5: Missing and Required Tests

### Tier 1: Must-Have Integration Tests

These tests are required to claim the system works:

#### Test 1: Two Silos, mTLS, Secured Grain Call

```
Setup:
- Organization key (trusted root)
- Two node keys with delegation chains from org
- TestCluster with 2 silos, each using UseScynapseSecurity()
- A grain with [SecurityPolicy(RequiresAuthentication = true)]
  and [RequireCapability(Action = "read")] on a method

Test cases:
a) Client has valid CCap → grain call succeeds, grain reads caller identity
b) Client has no CCap → grain call rejected with ScynapseSecurityException
c) Client has expired CCap → rejected
d) Client has CCap for wrong action → rejected
e) Client has CCap signed by untrusted issuer → rejected
f) Bearer proof with wrong key → rejected
```

#### Test 2: Cross-Silo Grain Call with CCap Flow

```
Setup:
- 3-silo cluster with mTLS
- Grain A on silo 1, Grain B on silo 2
- Grain A calls Grain B during a client request

Test cases:
a) CCap flows through RequestContext across silo boundary
b) Silo 2 independently verifies the CCap (not trusting silo 1's verification)
c) Chain verification works with assertions distributed across assertion stores
```

#### Test 3: Client-to-Gateway Connection

```
Setup:
- Silo cluster with UseScynapseSecurity()
- External client with UseScynapseSecurity()
- Client has its own keypair and delegation from org

Test cases:
a) Client connects to gateway, establishes mTLS
b) Client makes authenticated grain call
c) Client with wrong cert is rejected at TLS layer
```

### Tier 2: Important Functional Tests

#### Test 4: Full Delegation Chain Flow

```
Organization → Domain → Node → Session → CCap
- Each level delegates to the next
- Final CCap is verified on grain call
- Attenuation is enforced at each level
```

#### Test 5: Revocation Propagation

```
- Issue CCap, verify it works
- Revoke the CCap
- Subsequent calls with revoked CCap fail
- Revocation of parent delegation invalidates all child capabilities
```

#### Test 6: Anonymous and Pseudonymous Access

```
- Grain with [SecurityPolicy(AllowAnonymous = true)]
- Grain with [SecurityPolicy(AllowAnonymous = false)]
- Calls without any security context succeed on anonymous grain
- Calls without security context fail on authenticated grain
```

### Tier 3: Missing Negative Tests

- Forged assertion (valid format, invalid signature)
- Replayed CCap on different connection
- Clock skew handling (NotBefore slightly in future)
- Concurrent calls with same nonce
- Extremely deep delegation chains (performance)
- Assertion store failure (what happens when store is unavailable?)

---

## Part 6: Corrections to the Phase 1 Summary

The Phase 1 summary claimed several items that need qualification:

| Claim | Reality |
|-------|---------|
| "151 tests across all 5 layers" | ~132-135 test methods. Some expand to more cases via [Theory]. Count is close but not exact. |
| "mTLS transport using ECDSA bridge pattern" | The ECDSA bridge cert factory works. But it is NOT wired into the actual silo transport. Two separate TLS systems exist and are not connected. |
| "Orleans call filter integration — the incoming filter is THE enforcement point" | Correct architecturally. Tested only with mocks, never on a real silo. |
| "UseScynapseSecurity() — one line to wire the entire system" | The method exists and registers DI services, but it does NOT currently configure actual TLS transport between silos. Partially functional. |
| "Secure by default. Bearer proof prevents stolen tokens." | Bearer proof logic is implemented in the call filter. Never tested end-to-end. |

---

## Part 7: Summary of Gaps by Priority

### Blocking (Cannot Use System Without These)

1. **Wire TLS transport** — Connect `Scynapse.Security.Transport` to `Scynapse.Connections.Security` in `UseScynapseSecurity()`
2. **CCap wallet / per-call CCap selection** — Outgoing filter needs to select appropriate CCap per grain call
3. **Assertion pre-sharing** — Remote peers' delegation chains must be available for TLS validation
4. **Client builder extension** — `IClientBuilder.UseScynapseSecurity()` for external clients
5. **End-to-end integration test** — Prove the complete system works on a real cluster

### High Priority (Needed for Practical Use)

6. **Key provisioning workflow** — CLI tool or configuration-based key loading
7. **Per-grain-type resource inference** — Automatic resource URI derivation from grain interface type
8. **Swappable DI stores** — Allow overriding InMemory stores without forking `UseScynapseSecurity`
9. **Error handling and logging** — Security failures should be logged with actionable information

### Medium Priority (For Complete Feature Set)

10. **CCap issuance from grain code** — Let grains issue capabilities to callers
11. **Impersonation claim support** — Full verification logic for ClaimType.Impersonation
12. **Revocation assertion creation** — `AssertionBuilder.CreateRevocation()` with structured payload
13. **TLS bootstrap ramp** — The 3-phase TLS→identity→mTLS upgrade protocol

### Low Priority (Future / Phase 2)

14. **Persistent assertion store**
15. **Key rotation mechanism**
16. **X25519 encryption API**
17. **Channel binding to TLS session**
18. **Distributed nonce store**

---

*End of report. This document should serve as a roadmap for Phase 1 completion and Phase 2 planning.*
