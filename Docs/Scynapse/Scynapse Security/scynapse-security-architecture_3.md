# Scynapse Security Architecture

## Document Status & Meta

**Version:** 0.2.0-draft — Foundational Design (Consolidated)
**Date:** 2026-03-05
**Author:** Louis (architect), Claude Opus 4.6 (design partner)
**Context:** This document was produced during an intensive design session. It captures not only specifications but the reasoning, rejected alternatives, open questions, and contextual understanding behind each decision. This is intentional: specifications without understanding are fragile. Future sessions must be able to reconstruct the *why* from this document alone.

**Relationship to other Scynapse work:** This document builds on the Component Model session (Feb 2026) which established: "Component is the network" (each Component type forms a virtual network across all Nodes running it), the twelve requirements including Capability-Based Security, the two gravitational centers of Type and Identity, participation symmetry, trust-variance across arbitrary scales, NATS as foundational fieldwork with identified divergences, and the chainmail hash ring routing fabric concept. It also builds on the Client Principals architecture from the NewOrleans era (AccountGrain/SessionGrain/ConnectionGrain embodiment), though that architecture is not imported wholesale — its insights are extracted and adapted.

**What this document is NOT:** This is not an MVP. This is not a phased rollout plan. This is the foundational security architecture that the rest of Scynapse will be built atop. Cutting corners here to ship faster is architecturally irresponsible — these are the decisions that are hardest to change later. Everything here is designed to be plastically extensible: invariants are chosen to enable evolution, not constrain it.

### What This Document Does NOT Cover

- The Component Model specification itself (separate document)
- Wire protocol / serialization format (deliberately left plastic)
- Specific Component capability vocabularies (domain-specific, defined per Component)
- The Scynapse Name System (CNS) design (separate, depends on this document)
- Implementation timeline or phasing

### Terminology

- **Node**: A Scynapse runtime instance. Hosts multiple Components. Analogous to an Orleans Silo but without the client/server split — everything is a Node.
- **Component**: A type that participates in the Scynapse network. Components form their own virtual networks across Nodes. The "new Silo" in terms of isolation boundary. Scynapse's analog to the concept of "grain type" in Orleans, but elevated to a first-class networked entity.
- **Instance**: A running activation of a Component with specific identity and state. Analogous to a grain activation.
- **CCap**: Cryptographic Capability — a signed token carrying authorization.

---

## Part I: Foundations

### 1. The Problem Space

Scynapse is a distributed actor platform (evolving beyond Microsoft Orleans) where:

- **Components form virtual networks across physical Nodes.** A Component type running on Nodes A, B, C forms a single logical network. This is the primary organizational unit, not the physical topology.
- **Trust varies continuously.** From intra-Node (high trust, same process boundary) to inter-organization/federation (potentially zero trust). There is no fixed set of trust tiers — the system must accommodate arbitrary trust gradients.
- **Every participant is both consumer and provider** (participation symmetry). There is no clean client/server split. A Node that uses a Component also serves it. Authorization cannot be purely directional.
- **The platform has no fixed specs yet.** The security architecture must accommodate Scynapse's ongoing R&D without becoming a constraint. Plasticity is a first-order requirement.

The security system must answer four questions at every interaction:

1. **Who are you?** (Identity / AuthN) — verifiable without central authority. HOWEVER: Scynapse explicitly supports pseudonymous and partially-anonymous identities. "Who are you" may resolve to "a verifiable cryptographic identity whose binding to a real-world entity is encrypted and sharded" rather than a name. See §Invariant 8 and §Democratic Anonymity.
2. **What can you do?** (Capability / AuthZ) — carried as tokens, not looked up in tables. Tokens are delegatable (you can pass your authority to others) and attenuatable (each delegation can only narrow, never widen, the granted rights).
3. **Is this channel secure?** (Transport) — encrypted and mutually authenticated.
4. **Can I trust this interaction's provenance?** (Delegation / Audit) — chain of authority is cryptographically verifiable. Every capability token carries its proof chain — the sequence of delegations from the root authority to the current bearer.

### 2. Rejected Approaches and Why

**Access Control Lists (ACLs):** Scynapse explicitly rejects ACL-based authorization. ACLs require a central policy store, create a lookup dependency on every operation, scale poorly across trust boundaries, and cannot be delegated or attenuated. In a system where Components form networks across organizational boundaries, there is no single authority that could maintain a global ACL. Capability-based authorization is strictly more general: you can implement ACL semantics atop capabilities (a capability server that issues tokens based on policy lookups) but you cannot implement capability delegation atop ACLs without reinventing capabilities.

**OAuth2 / OIDC:** These are designed for the web's centralized trust model — a user authenticates with a trusted identity provider, which issues tokens that resource servers validate by calling back to the IdP. This model assumes: (a) the IdP is always reachable, (b) the IdP is trusted by all parties, (c) the trust topology is hub-and-spoke. None of these hold for Scynapse's federated, potentially-disconnected, trust-variant topology. Elements of OAuth2 (signed JWT tokens, scoped access) are preserved in spirit but the architecture is fundamentally different.

**X.509 PKI as separate infrastructure:** Traditional PKI requires Certificate Authorities, CRL/OCSP responders, and certificate lifecycle management as infrastructure separate from the application's identity model. In Scynapse, identity IS the PKI — the same Ed25519 keypairs used for authentication also serve as certificate material. There is no separate CA infrastructure to maintain. This is a key simplification (detailed in Part III).

### 3. Foundational Insight: The Unified Signed Assertion

This is the central architectural discovery of this design session and the most important idea in this document.

#### The observation

Identity and Capability are conventionally treated as separate subsystems. Identity answers "who are you?", Capability answers "what can you do?" But examining their structure reveals they are the same thing at different levels of specificity:

**A Capability** is a signed assertion: "Issuer I grants Subject S the right to perform Action A on Resource R." It is verified by walking the proof chain back to a root authority (the resource owner).

**An Identity** is a signed assertion: "Entity E exists and controls keypair K." It is self-signed — the issuer and subject are the same. It is the root/degenerate case of capability: the "action" is "exists as", the "resource" is the public key itself, and the proof chain is empty (this is the root).

**A Relation** (replacing the concept of "Account") is a signed assertion: "Entity A recognizes Entity B in context C." It is signed by A, establishing a directed relationship.

**A Delegation** is a signed assertion: "Entity A authorizes Entity B to issue further assertions of type T within scope S." It is the mechanism by which authority propagates.

**An Impersonation** is a signed assertion: "Entity A authorizes Entity B to act as A within scope S." It is explicit delegation with full provenance — the receiving Component always sees both the actual caller and the delegated identity.

All five follow the same pattern: a signed claim linking entities, with a verification chain. They differ only in claim type and chain structure.

#### Why this matters

Unifying these into a single primitive — the **Signed Assertion** — yields:

- **One token format** instead of separate identity tokens, capability tokens, and relation records.
- **One verification function** that walks the assertion chain regardless of what's being verified.
- **One library** to implement, test, and maintain.
- **Composability:** assertions can reference other assertions naturally. A capability can embed a relation. A delegation can reference an identity. No cross-system mapping needed.
- **The human at the root:** a human generates a keypair. Their first assertion is self-signed identity: "I exist." Everything else — delegation to Nodes, from Nodes to Components, from Components to instances, from instances to peers — flows from that root through chains of assertions. The human's private key IS the meta-capability from which all authority derives.

#### The deeper unification (Identity as degenerate Capability)

This insight emerged from first-principles analysis during the design session:

A Capability gives access — it links an authorized subject to something (a resource, an action) which can be resolved to or identified by an ID. An Identity also gives access — it links a private key holder to a presence in the system, without which nothing is reachable or callable. Both are signed relations between two things. Both are verified cryptographically.

The human user's relationship to their Identity is itself structurally a capability grant: the human "issues" their Identity to their Node(s)/Component(s) by providing the private key (or its derivative: a session key, an unlocked wallet, a signed-in OS session). Without this "issuance," the Node has no authority to act. The Identity IS a capability — the most fundamental one — granted by the physical-world entity (human, HSM, secure enclave) that controls the private key.

This means the entire security system can be modeled as a single type of thing: signed assertions forming chains. The roots are self-signed identities (which are meta-capabilities). Everything else is derived capability/delegation/relation flowing down those chains.

This unification simplifies the design without reducing its power. We adopt it.

#### Why Not Two Separate Systems?

Rejected alternative: separate Identity and Capability subsystems (the NATS model — NKeys for identity, JWTs for capability, separate libraries, separate verification).

Reasons for rejection:
- Two verification code paths = two attack surfaces, two places for bugs, two things to keep consistent
- The boundary between "who you are" and "what you can do" is less crisp than it appears — impersonation, delegation, pseudonymous operation, and capability-as-introduction all blur the line
- Scynapse's trust-variance requirement means the same assertion might function as "identity proof" in one context and "capability proof" in another (e.g., membership in a Component network is both an identity claim and a capability)
- The Relation concept (replacing fixed "Accounts") doesn't fit cleanly into either an identity-only or capability-only model — it's both

#### What this means for the old "AccountGrain" concept

In earlier NewOrleans designs, there were AccountGrain (persistent identity), SessionGrain (logical session), and ConnectionGrain (physical connection). These were useful conceptual layers but:

- **The term "Grain" presumes grain infrastructure exists**, which it doesn't at the security bootstrap layer. Security must be operational before any actor/grain/Component infrastructure is available. The security layer is *below* the actor model.
- **"Account" is not a universal concept.** Different Components have different relationships with their users. Some have rich accounts, some have anonymous interactions. What's universal is Identity + Relations; "accounts" are Component-specific state built atop these.
- **The three layers (persistent identity / logical session / physical connection) remain structurally sound** as an analysis of engagement levels. But they manifest as assertions, not as specific grain types:
  - The **persistent identity** is the root self-signed assertion.
  - The **logical session** is a time-bounded delegation assertion from the identity to a session keypair.
  - The **physical connection** is the mTLS channel, authenticated by the session keypair.

The design does not prescribe how these layers are implemented in the actor/Component system. That depends on Scynapse's actor model, which is still evolving. The security architecture provides the cryptographic substrate; the actor system builds atop it however it needs to.

---

## Part II: Invariants

These are commitments made now because changing them later would be structurally expensive. Each invariant is chosen to be maximally enabling — opening doors rather than closing them.

### Invariant 1: Ed25519 as the cryptographic identity primitive

Every identity in Scynapse — Node, Component type, Component instance, user, session, sub-actor — is an Ed25519 keypair. The public key IS the identity. X25519 (the Diffie-Hellman form of the same curve) for key agreement when encrypted point-to-point channels are needed beyond TLS.

**Why Ed25519:**
- Deterministic signatures (same input produces same signature), no random number generator dependency during signing. This eliminates a class of implementation bugs that have historically compromised ECDSA (PlayStation 3 hack, Bitcoin transaction malleability).
- 64-byte signatures, 32-byte public keys, 32-byte seeds — compact for embedding in tokens and transmitting in protocols.
- Approximately 128-bit security level — equivalent to RSA-3072 but orders of magnitude faster (~15,000 signatures/sec on commodity hardware).
- Universal library support across languages and platforms.
- Birational equivalence with X25519 (Montgomery form of the same curve), meaning an Ed25519 key can be converted to an X25519 key for Diffie-Hellman key agreement when needed. One keypair, two functions (signing via Ed25519, key agreement via X25519). Neither is "stronger" — they operate on the same underlying curve with the same security level. The choice is purely functional.

**Why not RSA:** Key sizes (2048+ bits), signature sizes, and computation costs are all worse. No advantage for our use case.

**Why not ECDSA (P-256):** Requires a secure random number generator for every signature. Implementation flaws in RNG have historically led to private key extraction. Ed25519's deterministic signing eliminates this attack surface entirely. P-256 is also a NIST curve with lingering suspicion about parameter selection; no practical advantage over Ed25519.

**Why not post-quantum (Dilithium, SPHINCS+):** Keys and signatures are 10–100x larger; standardization is still settling. Post-quantum algorithms can be added as an additional layer later without changing the architecture — sign with Ed25519 AND a PQ algorithm (dual-signature). Our assertion format is extensible enough to accommodate this. Practical quantum computers threatening 128-bit ECC are years away; when the time comes, dual-signing is an additive change, not an architectural rewrite.

**Key encoding:** We adopt typed, self-describing key encoding with human-readable prefixes, inspired by NATS NKeys' approach (which itself references Stellar's Base32 + CRC16 encoding). The exact encoding scheme (Base32+CRC16 vs. Base58 vs. something else) is deferred, but the principle is committed: looking at a key string tells you what kind of entity it identifies.

**Key type prefixes:**

| Prefix | Entity Type | Description |
|--------|-------------|-------------|
| O | Organization | Root of an organizational trust domain |
| D | Domain | Sub-division of an organization (optional depth) |
| N | Node | A running Scynapse runtime instance |
| T | Component Type | A Component's type-level identity (the "type IS the network" key) |
| I | Instance | A specific activation of a Component |
| U | User | A human or human-equivalent external identity |
| X | Curve/Encryption | X25519 key for encryption (derived from Ed25519 identity) |
| P | Private/Seed | The private seed (never transmitted, only stored locally) |

These prefixes are for human readability and quick identification. The cryptographic material is the same (Ed25519) regardless of prefix. NATS uses prefixes like 'N' = server, 'C' = cluster, 'O' = operator, 'A' = account, 'U' = user. Scynapse defines its own alphabet reflecting its entity types.

**X25519 for key agreement:** When establishing encrypted channels beyond TLS (e.g., application-level encryption between Components), X25519 Diffie-Hellman provides the shared secret. That shared secret is then used to key a symmetric cipher (typically XSalsa20-Poly1305 or ChaCha20-Poly1305 — the NaCl Box construction). This is the same pattern NATS XKeys uses.

**.NET libraries (verified available):**
- **`NSec.Cryptography`** (NuGet) — recommended primary library. Modern .NET API based on libsodium. Provides Ed25519, X25519, Blake2b, ChaCha20-Poly1305, Argon2id. Strongly typed key model prevents misuse.
- **`nkeys.net`** (GitHub: nats-io/nkeys.net) — NATS's official .NET NKeys library. Ed25519 with NATS-specific Base32+CRC16 key encoding. Useful as reference for our encoding.
- **`jwt.net`** (GitHub: nats-io/jwt.net) — NATS's official .NET JWT library. Demonstrates building signed claims using NKeys/Ed25519.
- **`NaCl.Net`** (NuGet) — C# port of NaCl Box (Curve25519XSalsa20Poly1305) for public-key authenticated encryption.
- **`TweetNaclSharp.Fast`** (NuGet) — TweetNaCl port, includes X25519 Box and Ed25519.
- **`System.Security.Cryptography`** (built-in since .NET 9) — Native Ed25519 support without external dependencies.

### Invariant 2: The Signed Assertion is the single universal primitive

Identity, capability, relation, delegation, impersonation, and revocation are all expressed as signed assertions with the same structure, same verification logic, same chain-walking algorithm. They differ only in claim semantics.

**Structure of a Signed Assertion (semantic, not wire format):**

```
SignedAssertion {
    version:     uint                    // format version for forward compatibility
    id:          ContentHash             // Blake2b-256 of all content fields (self-referencing CID)
    issuer:      PublicKey               // Ed25519 public key of the entity making this assertion
    subject:     PublicKey               // Ed25519 public key of the entity this assertion is about
    claim:       ClaimPayload            // type-tagged, extensible claim data
    scope: {
        not_before:  Timestamp?          // optional: valid from
        expires_at:  Timestamp?          // optional: valid until
        nonce:       bytes?              // optional: replay prevention
    }
    proofs:      ContentHash[]           // parent assertions in the delegation chain
    extensions:  Map<string, bytes>      // reserved for future use (anonymity, channel binding, etc.)
    signature:   Ed25519Signature        // issuer's signature over all above fields
}
```

**Claim types (initial set, extensible):**

| Type ID | Name | Meaning | Issuer/Subject |
|---------|------|---------|----------------|
| 0x01 | Identity | "I exist as this key" | issuer == subject (self-signed) |
| 0x02 | Capability | "Subject may do action on resource" | issuer has authority over resource |
| 0x03 | Delegation | "Subject may issue assertions within scope" | issuer delegates its authority |
| 0x04 | Relation | "Issuer recognizes subject in context" | directed relationship |
| 0x05 | Revocation | "Target assertion is revoked" | issuer is original issuer or authorized revoker |
| 0x06 | Impersonation | "Subject may act as issuer within scope" | issuer is the delegating identity |
| 0xFF | Extension | "Custom claim type, identified by URI" | depends on extension semantics |

**Claim payload structures:**

```
IdentityClaim {
    key_type: KeyTypePrefix              // What kind of entity (Node, Component, User, etc.)
    metadata: ExtensionMap?              // Optional: Component type info, version, etc.
    anonymity_binding: EncryptedBinding? // Optional: encrypted binding to real identity (see Democratic Anonymity)
}

CapabilityClaim {
    resource:    string                  // URI (Component type, instance, etc.)
    action:      string                  // Component-defined verb
    constraints: Map<string, bytes>      // optional (rate limits, IP restrictions, etc.)
}

DelegationClaim {
    allowed_claim_types:  uint8[]        // what assertion types subject may issue
    resource_pattern:     string?        // restrict to matching resources
    action_pattern:       string?        // restrict to matching actions
    max_depth:            uint8?         // further delegation depth limit
    scope_template: ScopeConstraints     // the boundary within which the delegate operates
}

RelationClaim {
    relation_type: string                // Component-defined relation vocabulary ("member", "subscriber", "administrator", ...)
    context: ExtensionMap?               // Relation-specific metadata
}

ImpersonationClaim {
    // issuer is the delegating identity, subject is the acting-on-behalf entity
    attenuated_to: ScopeConstraints      // What scope the impersonator has (always <= issuer's own scope)
}

RevocationClaim {
    target: ContentHash                  // The assertion being revoked (content-addressed reference)
    reason: string?                      // Optional human/machine-readable reason
}
```

**Content-addressed IDs:** Each assertion's ID is the Blake2b-256 hash of its content fields (minus the ID field itself, to avoid circularity). This gives us immutable references, deduplication, Merkle-style chain verification, and future compatibility with content-addressed storage. Same principle as UCAN's CIDs and Git's SHA hashes.

**Proof chain uses content hashes, not embedded assertions:** Embedding parents would cause exponential size growth with chain depth. Content hashes keep tokens compact. Verifiers resolve parent assertions from available storage (local cache, peer, gossip, CNS lookup).

**The extensions field:** A `Map<string, bytes>` for anything we don't know we need today. Namespaced keys (e.g., `scynapse.anon.shard`, `scynapse.channel.tls_hash`) avoid collisions. Unknown extensions are ignored by verifiers that don't understand them, preserved by systems that forward assertions. This is the primary plasticity mechanism in the assertion format.

**They all verify the same way.** One verification function, one chain-walking algorithm, one token format. This is not a theoretical nicety — it halves the implementation surface and eliminates an entire class of bugs (mismatches between "identity verification" and "capability verification" subsystems).

### Invariant 3: The trust boundary is the Component

Security verification happens at the Component level. Co-located Components on the same Node are still distinct security domains.

**Why this, not the Node:** Because "Component is the network." Component X on Nodes A, B, C forms one security domain. Component Y on Nodes A, B, D forms a different security domain. If the Node were the boundary, Components X and Y on Node A would share a security context — but they may have different trust policies, different capability vocabularies, and different owners.

**In practice:** When Component instance X1 on Node A calls Component instance Y1 on the same Node A, the call still goes through capability verification. Transport encryption may be skipped (OS process boundary provides confidentiality), but authorization is identical to cross-Node calls. This ensures a compromised Component cannot automatically access other Components on the same Node.

**Opt-in relaxation:** Components that explicitly trust each other and are co-deployed can opt into lighter verification. But this is opt-in, not default.

**Performance note:** Component-level isolation on the same Node doesn't require transport encryption (the OS process boundary provides confidentiality). It requires capability verification — which is a signature check, not a TLS handshake. This is fast (microseconds, ~0.1ms per assertion).

**Future-proofing:** If Components can migrate between Nodes, their security properties must travel with them, not be inherited from the Node they happen to be on.

### Invariant 4: mTLS is the default transport, with TLS as a bootstrap ramp

**Why mTLS everywhere is feasible for Scynapse:**

In traditional systems, mTLS is expensive because identity and certificates are separate infrastructures (PKI as independent infra with CAs, CRLs, OCSP, renewal). In Scynapse, **there is no separate PKI.** Every entity already has an Ed25519 keypair as its identity (Invariant 1). A TLS certificate is a derivative artifact of that identity:

- A Node's TLS certificate is signed by its parent in the identity hierarchy (its Organization, its Domain — whatever level is relevant). This IS a delegation assertion. The delegation assertion IS the certificate. Same data structure, same verification.
- "Certificate issuance" is assertion issuance. "Revocation" is a revocation assertion. No separate infrastructure.
- Every Node that can present a valid assertion chain can establish mTLS. Since every Node must have an identity to participate at all, every Node can do mTLS.

**In Scynapse, everything is a Node.** There is no fundamental Server/Client distinction; roles are contextual. A Node that looks like a "client" is still callable, still participates in Component networks, still has a full identity. This eliminates the "client certificate at scale" concern entirely — there are no "clients" in the traditional sense, only Nodes with varying capabilities.

.NET implementation: `SslStream` with custom certificate validation that verifies against the Scynapse assertion chain rather than the system trust store. TLS 1.3 with X25519 key exchange and Ed25519 certificates. Each Node's TLS certificate is derived from its Ed25519 identity keypair. The certificate chain mirrors the assertion chain (Node cert signed by Organization key, etc.).

**The bootstrap ramp:**

A bare TLS connection (server-authenticated only) provides access to a minimal bootstrap surface:

1. **Discovery:** What Components does this Node host? What are their security policies?
2. **Identity negotiation:** Present public key + proof chain. Verify and upgrade to mTLS.
3. **Assertion acquisition:** Obtain delegation/capability assertions for specific Components.

```
TLS-only (unauthenticated peer) → bootstrap surface only
    ↓ (peer presents identity + proofs, challenge-response)
mTLS (mutually authenticated) → full surface
    ↓ (peer presents CCaps for specific Components)
Component-level access → per-Component authorization
```

Every stage narrows what's needed and widens what's available. No entity needs pre-existing certificates to make first contact — they just need their keypair and an assertion chain to a mutually trusted root.

This means "client" and "server" are contextual and transient — a Node is a "client" only during bootstrap, then becomes a peer. This aligns with Scynapse's principle that everything is a Node with no permanent client/server distinction.

### Invariant 5: Identity hierarchy is self-similar and unbounded in depth

NATS has Operator → Account → User, period. Scynapse rejects fixed-depth hierarchy.

An Organization delegates to a Domain, which delegates to a Node, which delegates to a Component instance, which can further delegate to sub-actors — to arbitrary depth. The same assertion format, verification algorithm, and attenuation rules apply at every level. Adding a new organizational layer requires no protocol changes.

**Why this matters for trust-variance:** Different deployments have different organizational shapes. Small: Human → Node → Component. Large federated: Consortium → Org → Division → DC → Rack → Node → Component → Sub-actor. The security model doesn't care — it walks the chain regardless of length.

### Invariant 6: Components declare their security policy as part of their type definition

The platform provides mechanisms. Components choose policies. This is non-negotiable for a platform that must not force everything to one point on the faster/cheaper ↔ safer/more-expensive spectrum.

A Component's security policy (part of its type definition) includes:

- **Required authentication level:** anonymous, pseudonymous, named identity, specific authority
- **Required verification depth:** any valid chain, chain rooted in specific authority, channel binding required
- **Capability vocabulary:** what actions/resources this Component recognizes
- **Anonymity policy:** pseudonymity accepted? threshold configuration for democratic de-anonymization?
- **Transport requirements:** mTLS required? callback verification? specific cipher suites?
- **Impersonation policy:** delegation/on-behalf-of accepted? with what constraints?

These declarations are machine-readable and published as part of the Component's interface. Nodes and peers can inspect them before connecting.

**Structured form:**

```
ComponentSecurityPolicy {
    // Identity requirements
    min_identity_level: enum { Anonymous, Pseudonymous, Named, Attested }
    pseudonymity_config: PseudonymityConfig?    // If Pseudonymous is accepted: threshold params, trustee requirements

    // Capability verification
    require_channel_binding: bool               // CCaps must be bound to TLS session?
    require_origin_callback: bool               // Callback to source IP before accepting CCap?
    require_proof_chain_depth: uint?             // Minimum delegation chain length (prevents over-delegated CCaps)
    max_proof_chain_depth: uint?                 // Maximum (prevents deep chains that are hard to audit)

    // Transport requirements
    min_tls_version: TlsVersion                 // Minimum TLS version accepted
    require_mtls: bool                           // Require mutual TLS? (should almost always be true)

    // Delegation policy
    allow_delegation: bool                       // Can CCaps issued by this Component be further delegated?
    max_delegation_depth: uint?                  // How far can delegation chains extend?

    // Impersonation policy
    allow_impersonation: bool                    // Can entities act on behalf of others for this Component?
    impersonation_scope: ScopeTemplate?          // What scope is permitted for impersonation?

    // Revocation stance
    max_ccap_lifetime: Duration?                 // Maximum CCap validity period (forces reissuance)
    revocation_check: enum { None, Cached, RealTime }  // How aggressively to check revocations

    // Anonymity acceptance
    anonymity_policy: AnonymityPolicy?           // What anonymity levels are accepted for what actions? (see Democratic Anonymity)

    // Extension point
    custom_policies: ExtensionMap?               // Component-specific additional policies
}
```

This declaration serves multiple purposes: documentation (anyone examining a Component knows its security model), enforcement (the Scynapse runtime can reject connections/CCaps that don't meet the declared policy), compatibility (two Components can check whether their policies are compatible before establishing a relationship), and evolution (policies are versioned with the Component; upgrades are explicit and auditable).

### Invariant 7: CCaps (Crypto-Capabilities) are challengeable and bindable

A CCap is a Signed Assertion with claim type `Capability`. Presenting it is necessary but not always sufficient.

**Bearer verification:** The CCap's subject must prove they control the corresponding private key. Prevents stolen CCaps from being used. Implementation: verifier sends nonce, bearer signs with subject key.

**Channel binding:** The CCap is cryptographically tied to the specific mTLS session. Even if intercepted, cannot be replayed on a different connection. Implementation: challenge includes TLS session material (Finished message hash or peer certificate hash). Based on RFC 5929 / RFC 8471 principles.

```
Challenge = Hash(nonce || TLS_session_hash || CCap_hash)
Bearer must sign Challenge with their identity key
```

This proves: (a) bearer holds the private key for the CCap's subject, (b) bearer is the entity on this specific mTLS connection, (c) the CCap hasn't been lifted from a different session.

**Origin callback:** For highest security, the verifier calls back to the CCap issuer's endpoint to confirm legitimacy and check revocation. Expensive but provides real-time guarantees. This defends against routing-layer attacks in virtual network topologies.

**Binding CCaps to mTLS sessions (Louis's key insight):** A CCap should be challengeable not just against the bearer's identity but against the cryptographic artifacts of the current mTLS connection. This means even if a CCap is validly held by the right identity, it's only accepted within the specific mTLS session it's presented over. This creates end-to-end binding: the CCap holder established an mTLS session (proving their identity at the transport layer), and the CCap is tied to that specific session (preventing any relay or replay). Components can even push this further by calling back to the IP attached to the mTLS connection.

Components choose which mechanisms to require via their security policy (Invariant 6). The platform provides all mechanisms; none are mandatory. A low-value read-only cache might skip channel binding. A financial ledger requires it.

### Invariant 8: Pseudonymous identity is first-class

The assertion format supports pseudonymous identities where the binding to a "real" identity is encrypted and sharded via threshold cryptography (Shamir's Secret Sharing). The capability system treats pseudonymous and named identities identically — no second-class citizens architecturally.

**At pseudonymous identity creation time:**
1. Generate a new Ed25519 keypair (the pseudonym).
2. Encrypt the binding between pseudonym and real identity.
3. Split the encryption key into N shares (Shamir's Secret Sharing).
4. Distribute shares to M independent trustees.
5. Set threshold K-of-N for reconstruction.

The pseudonymous keypair works identically to any other identity — issues assertions, receives capabilities, forms relations. The capability system treats it the same.

**Anonymity levels (concrete configurations):**

- **Weak anonymity**: K=2, N=3 — service operator + one external auditor can de-anonymize
- **Moderate anonymity**: K=5, N=8 — requires consensus among multiple independent parties
- **Strong anonymity**: K=15, N=20 — geographically distributed citizens + judges; de-anonymization requires a substantial democratic process
- **Named identity**: No encryption — public key is directly bound to real-world identity

**Component-level anonymity acceptance policy:**

Components declare what level of identity they accept and for what:
- "This Component requires Named identity" (financial services, healthcare, etc.)
- "This Component accepts Pseudonymous with threshold K≥5" (social platforms, forums)
- "This Component accepts Anonymous for read-only, Pseudonymous for write" (content platforms)

**Democratic de-anonymization:** Components declare their anonymity acceptance policy: threshold configuration, conditions for triggering reconstruction, adjudication process (who may initiate, who votes, role of AI judges), etc. Different Components may have very different thresholds.

**Design constraint:** No anonymity model may enable serious crime. AI agents actively detecting zero-tolerance violations (child exploitation, etc.) is a design-level commitment. The anonymity system provides privacy, not impunity.

**What we commit to now:** The IdentityClaim has an optional `anonymity_binding` field, and the `extensions` field reserves space for anonymity shard data, threshold configuration, and trustee references. The capability system treats pseudonymous and named identities identically. Components can declare their anonymity acceptance policy.

**What we leave plastic:** The specific threshold cryptography implementation (Shamir's Secret Sharing, Feldman's Verifiable Secret Sharing, or threshold ECDSA), trustee selection mechanisms, the de-anonymization governance protocol (voting, judicial review, AI-assisted evaluation), and the binding format (what "real-world identity" means in the encrypted binding — government ID? biometric hash? organizational attestation?).

**Relevant cryptographic techniques:**
- **Shamir's Secret Sharing**: Split a secret into N shares, any K can reconstruct. Information-theoretically secure.
- **Verifiable Secret Sharing (Feldman/Pedersen)**: Like Shamir's but each share can be verified as correct without reconstruction.
- **Threshold Signatures**: K-of-N parties collaboratively produce a single Ed25519 signature without any single party knowing the full key.
- **C#/.NET**: The `SecretSharingDotNet` NuGet package implements Shamir's. For Feldman VSS, libraries exist in Rust (`vsss-rs`) callable via interop, or implementable from primitives using `NSec.Cryptography`.

---

## Part III: Detailed Architecture

### 3.1 Identity Layer

Every Scynapse entity has an Ed25519 keypair as its identity. The public key IS the identity. There is no separate "identity registry" that maps keys to entities — the key is the entity, from the platform's perspective.

**Identity lifecycle:**

1. **Generation**: Entity generates Ed25519 keypair locally. This is the self-signed root assertion — the IdentityClaim with issuer == subject.
2. **Attestation**: Parent entity (e.g., Organization attesting a Node) issues a signed assertion binding this new key to its hierarchy. This IS the "certificate issuance" — no separate PKI step.
3. **Presentation**: When connecting to peers, entity presents its public key + attestation chain. Peers verify the chain.
4. **Rotation**: New keypair generated, new attestation from parent, old assertions optionally revoked. The assertion format's content-addressable references mean old and new keys can coexist during transition.
5. **Revocation**: Parent issues a RevocationClaim targeting the old attestation. Peers cache revocations.

**The human at the root:**

A human generates a keypair (or a device does it on their behalf). The first assertion is self-signed identity: "I exist." From that root, everything flows:

Human → delegates to their Node(s) → Node delegates to its Components → Components delegate to instances → instances issue capabilities to peers.

The human's private key is the meta-capability — the root from which all other authority derives. The platform's job is to make that delegation chain efficient, verifiable, and revocable without requiring the human to be online for every verification.

### 3.2 Verification Algorithm (Universal)

One algorithm for all assertion types:

```
VerifyAssertion(assertion, context) → VerificationResult:

    // Step 1: Structural integrity
    if assertion.id != Blake2b256(assertion content fields):
        → Invalid("content hash mismatch")

    // Step 2: Signature
    if !Ed25519.Verify(assertion.issuer, signed_content, assertion.signature):
        → Invalid("bad signature")

    // Step 3: Temporal scope
    if not_before set and now < not_before: → Invalid("not yet valid")
    if expires_at set and now > expires_at: → Invalid("expired")
    // Also check that temporal bounds are within parent assertion's bounds (attenuation)

    // Step 4: Replay prevention
    if nonce set and context.nonce_store.HasSeen(assertion.id):
        → Invalid("replay")
    // Store nonce in replay prevention cache with TTL = expires_at

    // Step 5: Chain verification
    if proofs is empty:
        if claim_type == Identity and issuer == subject:
            → Valid if issuer in context.trusted_roots, else Invalid("untrusted root")
        else:
            → Invalid("non-root assertion with no proofs")

    for each proof_id in proofs:
        parent = context.store.Resolve(proof_id)
        if parent is null: → Invalid("unresolvable proof")

        VerifyAssertion(parent, context)  // recursive

        if parent.subject != assertion.issuer: → Invalid("chain break")
        if !CheckAttenuation(parent, assertion): → Invalid("insufficient authority")
            // This assertion's scope must be equal to or narrower than its parent's
            // Capability actions must be a subset of parent's granted actions
            // Temporal bounds must be within parent's temporal bounds

    // Step 6: Channel binding (if context requires it)
    if context.require_channel_binding:
        if assertion does not include TLS session material matching current connection:
            → Invalid("channel binding mismatch — possible replay on different channel")

    → Valid
```

**Performance considerations:**
- Signature verification (Ed25519): ~0.1ms per assertion
- Chain walking: most chains are 2–5 assertions deep; total verification < 1ms
- Replay cache: in-memory hash set with TTL-based eviction; Bloom filter for very high throughput
- Caching: verified assertion chains can be cached (keyed by assertion hash) with TTL = min(expires_at across chain). This amortizes the cost of repeated verifications of the same capability.

### 3.3 Transport Layer

**Between Nodes (the common case):**
mTLS using TLS 1.3 with X25519 key exchange and Ed25519 certificates.

**mTLS Bootstrap Sequence:**

```
Phase 1: TLS Handshake (server-authenticated)
    Peer A → Peer B: TLS ClientHello
    Peer B → Peer A: ServerHello + Certificate (self-signed with Ed25519 key)
    TLS session established (encrypted, B authenticated, A anonymous)

Phase 2: Bootstrap Exchange (over TLS)
    A → B: BootstrapRequest { public_key, proof_chain[] }
    B verifies A's chain against trusted roots
    B → A: BootstrapResponse { identity_assertion, challenge_nonce, components[], policies[] }
    A → B: ChallengeResponse { nonce_signature, session_key_cert }
    B verifies nonce signature + A's certificate

Phase 3: mTLS Upgrade
    Renegotiate to mTLS (both sides verified)
    Full surface available

Phase 4: Component Access (over mTLS)
    A → Component: AccessRequest { ccap, channel_binding? }
    Component verifies per its declared policy
    Component → A: AccessGranted / AccessDenied
```

**If no attestation chain (truly new entity):**
Limited bootstrap surface only (request enrollment, browse public Components). Enrollment process is Component-specific (some Components auto-accept, others require approval).

**Within a Node (Component-to-Component on same process):**
No transport encryption needed (OS process boundary provides confidentiality). Capability verification still occurs — the same assertion checks, just without the TLS overhead. This is a signature verification (~0.1ms), not a network round trip.

### 3.4 Capability Layer (CCaps)

A CCap is a Signed Assertion with claim type `Capability`. It carries:
- What resource is being authorized (defined by the Component owning that resource)
- What action is permitted (Component-specific vocabulary)
- Under what constraints (temporal, rate, scope, channel-binding requirements)
- Proof chain back to the resource owner

**CCap lifecycle:**

1. **Issuance**: Resource owner (or delegate) creates and signs a CapabilityClaim for a Subject.
2. **Transmission**: CCap is transmitted to the Subject (over mTLS, or embedded in an assertion chain).
3. **Presentation**: Subject presents CCap when invoking an action on the resource's Component.
4. **Verification**: Receiving Component verifies the CCap per §3.2 verification algorithm.
5. **Channel binding** (if Component policy requires): CCap is tied to the presenting mTLS session.
6. **Delegation**: Subject can create a narrower CCap (attenuated) and delegate to another entity.
7. **Revocation**: Issuer creates a RevocationClaim. Revocation propagation is a design detail left plastic — short-lived CCaps with reissuance are the simplest approach.

---

## Part IV: Impersonation

Impersonation (acting on behalf of another identity) is supported via the ImpersonationClaim assertion type (0x06). The design is intentionally simple but extensible.

### Minimal Design

- Entity A issues a Signed Assertion: "I authorize Entity B to act as me within scope S"
- When B presents this to a Component, the Component sees both identities: the actual caller (B) and the delegated identity (A)
- The Component can make policy decisions on either or both
- Scope S is always attenuated — B can never have more authority through impersonation than A itself has

This is explicit delegation with full provenance, strictly more general than simple impersonation. The verifier sees both the actual caller and the delegated identity — no invisible privilege escalation.

### Why This Is Sufficient for Now

- It composes with the rest of the assertion model (it's just another assertion type with the same verification algorithm)
- It's transparent (the receiving Component always knows impersonation is happening)
- It supports both simple cases (service account acts on behalf of user) and complex cases (multi-level delegation chains with impersonation at different levels)
- It composes naturally with capability chains — an impersonation assertion can be combined with capability assertions to create "B may do action X on resource R, acting as A"

### What We Leave for Later

These are policy decisions, not structural ones. The assertion format supports them; the runtime behavior is added when needed:

- **Group impersonation**: acting on behalf of a set of identities (e.g., a committee)
- **Time-limited impersonation with automatic expiry**: already supported by the scope's `expires_at`, but operational workflow (notification, renewal) is deferred
- **Approval workflows**: impersonation requires real-time confirmation from the delegating identity

### Component Policy

Components declare their impersonation policy in their security policy declaration (Invariant 6):
- `allow_impersonation: bool` — Can entities act on behalf of others for this Component?
- `impersonation_scope: ScopeTemplate?` — What scope is permitted for impersonation?

---

## Part V: Relationship to Prior Art

### NATS/Synadia Security Model

**Adopted from NATS:**
- Ed25519 as identity primitive (NKeys).
- Challenge-response authentication (nonce signing, replay immunity).
- JWT-like signed claims carrying identity and permissions.
- Separation of signing keys from identity keys (key rotation without identity change).
- Human-readable key encoding with type prefixes.
- Decentralized verification (no callback to central server).

**Divergences from NATS:**
- NATS: fixed three-tier hierarchy (Operator → Account → User). Scynapse: self-similar, unbounded.
- NATS: Account is the security boundary. Scynapse: Component is the boundary.
- NATS: permissions as subject allowlists/denylists. Scynapse: delegatable, attenuatable capability tokens.
- NATS: no capability delegation chains. Scynapse: full chain verification with attenuation.
- NATS: separate NKeys (auth) and JWTs (authz). Scynapse: unified Signed Assertion.
- NATS: no built-in anonymity support. Scynapse: first-class pseudonymous identity with democratic de-anonymization.

**NATS .NET libraries usable in Scynapse:**
- `nkeys.net` — Ed25519 key operations with typed encoding. Reference or direct use.
- `jwt.net` — NATS JWT creation/verification. Pattern reference; our format differs.
- `NATS.Net` client — NKey auth + mTLS integration patterns.

### UCAN (User Controlled Authorization Networks)

**Adopted from UCAN:**
- Capabilities as signed, self-contained, delegatable tokens.
- Attenuation: delegations can only narrow rights.
- Proof chains for verifiable authority provenance.
- Content-addressed token IDs.
- No central authorization server.

**Divergences from UCAN:**
- UCAN: pure capability system. Scynapse: unified assertion (identity + capability + relation + delegation + impersonation + revocation).
- UCAN: DAG-CBOR/IPLD encoding. Scynapse: wire format deferred.
- UCAN: `did:key` identity. Scynapse: Ed25519 with own encoding (compatible with `did:key` if desired).
- UCAN: no Component/policy concept. Scynapse: Component-declared security policies.

**UCAN implementations exist in TypeScript, Rust, Go. None in C#/.NET.** Scynapse builds its own from primitives.

### Biscuit (by Clever Cloud)

Uses Datalog for capability description — declarative policy language embedded in tokens. Worth studying if we want more expressive capability constraints than simple action/resource pairs. Not adopted now but noted as a potential future influence.

### Orleans Security

Orleans has essentially no built-in security model. TLS is opt-in. No built-in auth. `RequestContext` flows caller metadata but it's advisory, not enforced.

Scynapse's security is built *below* the actor model. It is operational before any actor/Component infrastructure starts. Nodes authenticate to each other before exchanging any actor messages. Security infrastructure (identity management, capability verification, relation tracking) is NOT implemented "as Grains." These are platform-level services that exist BEFORE the actor system is available to user-level code.

The bootstrap sequence matters: identity and security must be operational before Components can activate, because Component activation itself requires identity verification. This is infrastructure that the runtime provides, not something built on top of the actor system. (Analogous to how an OS kernel's security module isn't a user-space process — it's part of the kernel that enables user-space to exist.)

User-visible identity primitives (what was theorized as AccountGrain/SessionGrain/ConnectionGrain in the NewOrleans era) will eventually be exposed through whatever actor-like mechanism Scynapse provides, but their implementation is platform-level. The exact form depends on how the actor model evolves.

---

## Part VI: Design Decisions — Detailed Reasoning

### Decision 1: Unified Signed Assertion vs. Separate Identity + Capability Systems

**Chosen:** Unified
**Rejected:** Separate (the NATS model)
**Rationale:** See §3 in Part I. Single verification path, composability, Relation concept doesn't fit either subsystem alone. Impersonation composes naturally.
**Risk:** If the unified model proves too constraining for some future assertion type, we may need to add special-case handling. Mitigated by the extensible claim type system and the extensions field.

### Decision 2: Component-Level Trust Boundary vs. Node-Level

**Chosen:** Component-level
**Rejected:** Node-level (the NATS "Account" model)
**Rationale:** "Component is the network" principle. Multi-tenant Nodes. Component migration.
**Risk:** Performance overhead of verifying capabilities for intra-Node Component communication. Mitigated by caching and the fact that it's signature verification (~0.1ms), not TLS.

### Decision 3: mTLS as Default vs. TLS + Application-Layer Auth

**Chosen:** mTLS default (TLS only for bootstrap)
**Rejected:** TLS + NKey-style auth as the normal mode
**Rationale:** In Scynapse, everything is a Node — there's no permanent "client" that can't present a certificate. The identity layer IS the PKI, so mTLS adds no infrastructure burden. mTLS gives transport-layer authentication, which is strictly stronger than application-layer-only.
**Risk:** None identified. The bootstrap ramp handles the "first connection" case.

### Decision 4: Build UCAN-like System in C# vs. Interop with Rust rs-ucan

**Chosen:** Build in C# from primitives
**Rejected:** Rust FFI interop
**Rationale:** Scynapse will almost certainly need its own assertion format adapted to the Component Model. Building from primitives (.NET Ed25519, CBOR libs) gives full ownership and iteration speed. Rust interop introduces FFI boundary friction (C-ABI wrappers, memory management, cross-platform builds, debugging across managed/unmanaged boundary) for a library whose exact format we'd modify anyway. With AI-assisted development, assembling the C# implementation from existing crypto primitives is a few focused sessions.
**Risk:** Divergence from UCAN ecosystem means no interop with UCAN-based systems (IPFS/Filecoin world). Acceptable: Scynapse is its own platform; interop adapters can be built later if needed.

### Decision 5: Ed25519 as Sole Signature Algorithm vs. Algorithm Agility

**Chosen:** Ed25519 only (for now), with extensibility provisions
**Rejected:** Full algorithm agility from day one
**Rationale:** Algorithm agility is a notorious source of complexity and vulnerabilities (see TLS history). One algorithm means one code path, one set of key sizes, one performance profile. Post-quantum algorithms can be added as a second layer (sign with Ed25519 AND PQ algo) when needed, without changing the architecture.
**Risk:** Ed25519 quantum vulnerability. Mitigated by the dual-signature extension path and the fact that practical quantum computers threatening 128-bit ECC are years away.

### Why not "just use JWT"?

Standard JWTs (RFC 7519) lack: proof chains (no delegation chaining), algorithm discipline (JOSE supports weak algorithms; Scynapse uses Ed25519 only), compact encoding (JSON is verbose for frequently-transmitted tokens), and content-addressed IDs. Extending JWT to meet our needs would produce something unrecognizable as JWT. Better to design cleanly.

### Why content-addressed assertion IDs?

Hash of contents gives: immutable references, deduplication, Merkle-style chain verification without trusting intermediaries, and natural compatibility with content-addressed storage. Same principle as UCAN's CIDs and Git's SHA hashes.

### Why the extensions field?

The `Map<string, bytes>` extensions field is the primary plasticity mechanism. Anything we don't know we need today: anonymity shard data, channel binding material, Component-specific metadata, versioning hints, provenance. Namespaced keys avoid collisions. Unknown extensions are ignored by verifiers, preserved by forwarders.

### Why Component-declared security policy?

Different Components have legitimately different security needs (latency-sensitive trading vs. medical records vs. public chat vs. financial services). The platform guarantees mechanism correctness; Components choose which mechanisms to require. This is the only approach compatible with Scynapse's "platform must not force a position on the speed/safety spectrum" principle.

---

## Part VII: Relationship to Prior Scynapse Design Concepts

### Connection to the Chainmail Routing Fabric

The chainmail concept (interlocking hash rings per Component type, with contact points forming the routing mesh) has a natural security dimension:

- Each Component's hash ring is a *trust domain*. Membership in the ring is attested by assertion.
- Contact points between rings (where two Component types share Nodes) are *trust bridges*. Cross-Component communication at a contact point can be verified locally (both Component identities are present on the same Node).
- The routing fabric's topology IS the trust topology. You can only route to a Component you have an assertion chain reaching.

This means the security model and the routing model reinforce each other rather than being separate concerns. A Node's position in the routing fabric is a function of its assertions (which Components it's attested for), and its routing capabilities are bounded by its capabilities (which Components it can address).

### Connection to NXIA/VAYRON (Long-term Vision)

The Signed Assertion model is designed to be compatible with (and eventually subsumable by) the NXIA vision's capability-governed channels and Memory System authority model. In NXIA R3 terms:
- A Signed Assertion is a capability token that governs access to SIP channels
- The assertion chain is the trust chain from resource owner to accessor
- Component-level isolation maps to VProcess isolation
- The self-similar hierarchy maps to NXIA's fractal addressing

Scynapse's security model is a .NET/C# implementation of principles that will eventually be native in NXIA. Building it now in Scynapse validates the model and produces a working system; migrating it to NXIA later is a transport change, not an architecture change.

### Connection to Orleans Messaging

Orleans uses `RequestContext` to flow metadata through grain calls. Scynapse's assertion chains need to flow similarly. The exact integration point depends on how deeply Scynapse modifies the Orleans messaging layer. This is an open question tracked in Part IX.

---

## Part VIII: Implementation Dependencies (Not Phases)

```
Layer 0: Cryptographic Primitives
    Ed25519 signing/verification      (NSec.Cryptography or nkeys.net)
    Blake2b-256 hashing               (NSec.Cryptography)
    X25519 key agreement              (NSec.Cryptography)
    Key encoding with typed prefixes  (custom, referencing nkeys.net)
    ↓
Layer 1: Signed Assertion Core
    Assertion data structure (in-memory)
    Serialization (start with CBOR or JSON for testing; format plastic)
    Content-addressed ID generation
    Assertion creation (sign with Ed25519)
    Single-assertion verification (no chain walking yet)
    ↓
Layer 2: Chain Verification
    Assertion store interface (resolve by content hash)
    In-memory store (testing)
    Chain walking algorithm
    Attenuation checking (identity + delegation initially)
    Nonce tracking / replay prevention
    ↓
Layer 3: Transport Security
    TLS configuration for Nodes
    mTLS using Ed25519-derived certificates
    Bootstrap sequence
    Challenge-response protocol
    ↓
Layer 4: Component Integration
    Component security policy declaration format
    CCap verification at Component boundary
    Channel binding
    Per-Component capability vocabulary
    ↓
Layer 5: Advanced (each independent)
    Pseudonymous identity + threshold de-anonymization
    Impersonation runtime (group, approval workflows)
    Federation trust bootstrap
    AI security monitoring/enforcement
```

---

## Part IX: Open Questions (Explicitly Deferred)

### Wire format
Options: CBOR (compact, self-describing, `PeterO.Cbor` .NET library, UCAN/IPLD ecosystem interop), Protobuf (extremely compact but not self-describing), custom binary (maximum control, maximum maintenance), JSON (human-readable, too verbose for production but useful for dev/debug). **Deferred until** serialization story is decided. Decision criteria: how important is interoperability with external systems vs. internal optimization.

### Revocation mechanism
Options: short-lived assertions + reissuance (simple, requires reissuance infrastructure), explicit revocation assertions (RevocationClaim targeting by content hash), epoch-based (all assertions reissued at epoch boundaries, revocation = omission), hybrid. Likely answer: combination of short-lived for low-value/high-volume and explicit revocation for long-lived/high-value. **Deferred until** per-Component consistency model is understood.

### Key distribution / discovery
Options: embedded in CNS registration (natural, since Components must register to be discoverable), gossip protocol (keys propagate through the Component's type-network — natural fit for "Component is the network"), TOFU (simple but vulnerable to initial MITM), web-of-trust (decentralized but complex), out-of-band. Likely answer: CNS registration as primary, with gossip as optimization. **Deferred until** CNS design advances.

### Built-in capability vocabulary
Universal verbs (invoke, subscribe, admin, delegate, query, mutate) depend on Component Model interface definition. **Deferred until** Component interface story is decided.

### Federation trust bootstrapping
Options: manual key exchange (out-of-band), DNS-based discovery (DANE/TLSA records), TOFU with progressive trust, web-of-trust / vouching by already-trusted domains. This is a policy/deployment decision, not an architectural one. The assertion chain model supports all of these approaches. **Deferred until** federation model is designed.

### Threshold de-anonymization protocol
Full Shamir's Secret Sharing protocol, trustee selection, vote mechanics, AI adjudication. Assertion format reserves space. **Deferred until** concrete anonymity deployment scenario exists.

### Key compromise recovery
An entity's Ed25519 key is compromised. What's the recovery procedure? Revoke all assertions signed by that key, reissue from parent, rotate. But the operational workflow depends on the identity hierarchy's depth and the urgency of the compromise.

### Assertion size budget
Capability tokens with long proof chains could grow large. Is there a practical limit? Can we use proof compression (e.g., aggregate signatures, Merkle proofs over assertion sets)?

### Orleans messaging integration
Orleans uses `RequestContext` to flow metadata through grain calls. Scynapse's assertion chains need to flow similarly. The exact integration point depends on how deeply Scynapse modifies the Orleans messaging layer.

### Federation governance
When two independent Scynapse domains federate, what are the mutual obligations? This is a policy question, not a technical one, but the technical infrastructure must support whatever policy is chosen.

---

## Part X: Glossary

| Term | Definition |
|------|-----------|
| **Signed Assertion** | The universal primitive: a signed claim linking entities. Encompasses identity, capability, relation, delegation, impersonation, revocation. |
| **CCap** | Crypto-Capability. A Signed Assertion with claim type Capability. |
| **Component** | A Scynapse type forming a virtual network across all Nodes running it. Primary unit of isolation and security boundary. |
| **Node** | A Scynapse runtime instance hosting one or more Components. |
| **Instance** | A running activation of a Component with specific identity and state. |
| **Proof chain** | Parent assertion IDs (content hashes) establishing authority behind an assertion. |
| **Attenuation** | Each delegation can only narrow, never widen, granted rights. |
| **Channel binding** | Tying a CCap to a specific mTLS session, preventing replay on other connections. |
| **Trusted root** | An identity assertion trusted without proof chain. Chain verification terminates here. |
| **Democratic anonymity** | Pseudonymity with threshold de-anonymization by independent trustees under defined conditions. |
| **Component security policy** | Structured declaration of a Component's security requirements (auth level, verification depth, capability vocab, anonymity, transport, impersonation). |
| **CNS** | Scynapse Name System. Distributed discovery. Design in progress. |
| **Bootstrap ramp** | Sequence upgrading unauthenticated TLS to mTLS with Component access. |
| **Content-addressed ID** | Blake2b-256 hash of assertion content, enabling immutable references and deduplication. |

---

## Part XI: References

### Specifications
- **UCAN v1.0.0-rc.1** — github.com/ucan-wg/spec — Capability tokens, delegation chains, attenuation
- **NATS Security** — docs.nats.io — NKeys, JWT, challenge-response
- **Ed25519** — ed25519.cr.yp.to — Signature scheme
- **X25519** — RFC 7748 — Key agreement
- **Shamir's Secret Sharing** — Adi Shamir 1979 — Threshold cryptography
- **Channel Binding** — RFC 5929, RFC 8471 — Token/TLS binding
- **Biscuit** — biscuitsec.org — Datalog-based authorization tokens (Clever Cloud)

### .NET Libraries

| Purpose | Library | NuGet Package | Notes |
|---------|---------|---------------|-------|
| Ed25519 signing/verify, X25519, Blake2b, AEAD | NSec.Cryptography | `NSec.Cryptography` | Based on libsodium. Modern Span<T> API. **Recommended primary choice.** |
| NATS-compatible NKeys | nkeys.net | `NATS.NKeys` | Official NATS .NET NKeys. Ed25519 with NATS-specific Base32+CRC16 encoding. Reference for our encoding. |
| NATS-compatible JWT | jwt.net | `NATS.Jwt` | Official NATS .NET JWT. Ed25519-signed claims. Pattern reference. |
| NaCl Box (X25519 encryption) | NaCl.Net | `NaCl.Net` | Curve25519XSalsa20Poly1305 — public-key authenticated encryption. |
| NaCl Box (alternative) | TweetNaclSharp | `TweetNaclSharp.Fast` | Port of TweetNaCl. X25519, Ed25519, Box, SecretBox. |
| CBOR encoding (if chosen) | PeterO.Cbor | `PeterO.Cbor` | RFC 7049 CBOR. If we go DAG-CBOR for wire format. |
| Base32/Base58 encoding | SimpleBase | `SimpleBase` | For key encoding scheme. |
| Secret Sharing | SecretSharingDotNet | `SecretSharingDotNet` | Shamir's Secret Sharing in C#. For democratic anonymity threshold scheme. |
| TLS / mTLS | System.Net.Security | (built-in) | SslStream with custom certificate validation. |
| Ed25519 (built-in) | System.Security.Cryptography | (built-in since .NET 9) | Native Ed25519 support without external dependencies. |

### UCAN Implementations (reference, not C#)
- TypeScript: `@ucans/ucans`, `iso-ucan`
- Rust: `rs-ucan` (ucan-wg/rs-ucan)
- Go: `go-ucan` (ucan-wg/go-ucan)
- **C#/.NET: None exists.** Scynapse builds from primitives.

---

## Part XII: Phase 1 Orleans Paradigm -- Gap Analysis and Design Decisions

**Added:** 2026-03-06 (v0.2.1)
**Context:** After implementation of Layers 0-4 and the Phase 1 review, this section documents the gaps identified when simulating all Orleans-paradigm workflows against the security model, the design options considered, and the decisions made.

### 12.1 The Grain-to-Grain Call Security Problem

**The problem:** When grain A calls grain B during processing of a client request, what identity and capability does grain A present to grain B?

This is the most architecturally significant gap in Phase 1. The existing outgoing call filter attaches whatever CCap is in the wallet, which for intra-silo grain calls means the node's broad delegation. This is **ambient authority** -- exactly what capability-based security is designed to prevent.

**Options considered:**

| Option | Description | Pros | Cons |
|--------|-------------|------|------|
| **E1: Forward original CCap** | Grain B sees the end-user's CCap | True capability model | User must hold CCaps for all transitive grains |
| **E2: Node ambient authority** | Grain B sees the node identity | Simple, works today | Violates capability principle |
| **E3: Dual-Identity** | Both original caller AND node identity propagated | Flexible, per-grain policy | More complex policy model |
| **E4: Explicit CCap acquisition** | Grain A requests scoped CCap from coordinator | Minimum privilege, auditable | Extra round trip, hot singleton |

**Decision: E3 (Dual-Identity Model).** Grain calls carry two identities via `RequestContext`:
- `Scynapse.OriginalCallerKey` -- the end-user who initiated the call chain
- `Scynapse.ActingNodeKey` -- the silo processing the current hop

The receiving grain's `[SecurityPolicy]` determines which to verify:
- `EnforceOriginalCaller = false` (default): trust the node identity (suitable for infrastructure grains)
- `EnforceOriginalCaller = true`: verify the original caller's CCap covers this grain (suitable for user-facing sensitive grains)

**Why this preserves the architecture's invariants:**
- Invariant 3 (Component trust boundary): The grain type controls its own verification policy
- Invariant 7 (CCaps are challengeable): Both identities are verifiable
- Plasticity: Phase 2 Components can define richer policies using the same two-identity mechanism

### 12.2 TLS Transport Verification Resolution

**The problem:** The TLS `RemoteCertificateValidation` callback is async-hostile. The assertion store lookup is async. Using `AllowAnyRemoteCertificate()` bypasses transport-level identity verification.

**Decision: Pre-validated peer cache.** During silo startup (`ScynapseSecurityLifecycleParticipant`), validate all peer assertions from `PeerAssertions` and `PeerAssertionDirectory` config. Cache validated public keys in a `HashSet<byte[]>`. The TLS callback performs a synchronous hash set lookup -- no async needed.

**Security properties preserved:**
- Rogue nodes without valid delegation chains are rejected at TLS level
- Legitimate peer identity is verified against the org trust root
- The call filter remains the capability enforcement point (TLS provides identity + confidentiality, call filter provides authorization)

### 12.3 Operational Tooling: Scy.exe

**Decision:** A standalone CLI tool named `Scy` (published as `dotnet tool install Scy`) handles all operational security tasks: key generation, assertion issuance, inspection, verification, configuration bundling, interactive setup, and key rotation.

**Technology:** `Spectre.Console` for TUI rendering, `Spectre.Console.Cli` or `System.CommandLine` for command routing. References `Scynapse.Security` core (no Orleans dependency).

**Command tree:**
```
scy keygen       -- Generate Ed25519 keypairs
scy identity     -- Create self-signed identity assertions
scy delegate     -- Create delegation assertions
scy issue-ccap   -- Issue capability assertions
scy revoke       -- Create revocation assertions
scy inspect      -- Human-readable dump of assertions/keys
scy verify       -- Verify assertion chains
scy bundle       -- Create deployment bundles
scy init         -- Interactive setup wizard (org, silo, client, dev)
scy rotate       -- Key rotation workflow
scy status       -- Query running silo security status
```

**File formats:**
- `.seed` files: Base32-encoded Ed25519 seeds with key-type prefix
- `.assertion` files: CBOR-encoded `SignedAssertion` objects (binary, same as wire format)
- Both inspectable via `scy inspect`

This tool fills the gap between the cryptographic primitives (Layer 0-2) and the runtime integration (Layer 4). Without it, the security system requires C# code for every operational task.

### 12.4 Configuration Loading

**Decision:** `IConfiguration` integration allowing `UseScynapseSecurity()` to accept a configuration section:

```csharp
builder.UseScynapseSecurity(config.GetSection("Scynapse:Security"));
```

Configuration keys: `NodeSeedFile`, `NodeSeedEnvironmentVariable`, `TrustedRoots[]`, `BootstrapAssertionFiles[]`, `PeerAssertionFiles[]`, `PeerAssertionDirectory`, `BootstrapCapabilityFiles[]`, `EnableTls`, `RequireMutualTls`, `DevelopmentMode`.

Seeds can be loaded from files or environment variables. Assertion files are CBOR-encoded `.assertion` files (created by Scy.exe). This bridges the gap between Scy.exe's output and the runtime's input.

### 12.5 Development Mode

**Decision:** A `DevelopmentMode` flag that auto-generates all security infrastructure with broad permissions. Essential for developer adoption. Must log prominent warnings and must never be used in production.

When `DevelopmentMode = true`:
- Auto-generates org key, node key, delegation chain
- Creates broad CCap (all resources, all actions)
- Trusts all cluster members automatically
- Disables TLS requirement
- Logs: `WARNING: Scynapse Security running in DEVELOPMENT MODE. All keys are auto-generated and ephemeral. Do NOT use in production.`

### 12.6 Scynapse Feature Integration Assessment

| Feature | Call Filter Coverage | Phase 1 Action |
|---------|---------------------|----------------|
| Standard grain calls | Full | Already working |
| StateTask properties | Full (generated Get/Set methods are grain calls) | No action needed |
| Dynamic Grain Access | Full (uses IGrainFactory internally) | Extend GrainResourceInference for dynamic types |
| Plugin grain loading | Full for grain calls; **NO access control on loader itself** | **PHASE 1 REQUIRED:** Protect `IPluginGrainLoader` with `[SecurityPolicy]` |
| GTD enumeration | Full (grain calls) but **open by default** | **PHASE 1 REQUIRED:** Add auth to `IGrainTypeDirectoryGrain` |
| Async+ persistence | State serialized via reflection, no encryption at rest | Document as Phase 2; intra-cluster infrastructure |
| Orleans Streams (SMS) | Partial (subscriptions bypass call filters) | Document as Phase 2; intra-cluster infrastructure |
| Grain Observers | Partial (observer registrations are grain calls; callbacks bypass filters) | Document as Phase 2 |
| Grain Timers/Reminders | Not applicable (silo-internal scheduling) | No action needed |

### 12.7 Error Reporting

**Decision:** Add structured `SecurityFailureCode` enum to `ScynapseSecurityException` and `ILogger` injection to call filters. Security failures must be diagnosable by operators without source code access.

Error codes: MissingAuthentication, InvalidSignature, ExpiredAssertion, RevokedAssertion, InsufficientCapability, WrongAction, WrongResource, BearerProofFailed, ChainVerificationFailed, UntrustedRoot, ReplayDetected, MaxDepthExceeded.

### 12.8 Workflow Validation Summary

All Orleans-paradigm workflows were simulated against the security design (detailed simulations in `scynapse-security-implementation-guide-v2_1.md`):

| Workflow | Status | Key Dependencies |
|----------|--------|-----------------|
| Organization bootstrap | Complete | Scy.exe `init --org` + `init --silo` |
| Grain developer (writing secured grains) | Complete | `[SecurityPolicy]` + `[RequireCapability]` attributes |
| Grain developer (Scynapse features) | Complete | StateTask, Dynamic Grains go through call filters |
| External client application | Complete | `UseScynapseSecurity()` + config loading + wallet |
| Silo-to-silo communication | Complete | Peer assertions + pre-validated cache + Dual-Identity |
| Development mode (quick start) | Complete | DevelopmentMode flag |
| Key rotation | Complete (requires rolling restart) | Scy.exe `rotate` |
| Runtime CCap issuance/revocation | Complete | `IssueCCapToCaller()` + assertion store |

---

*Living document. Revisions expected as Component Model, CNS, and federation story evolve.*
