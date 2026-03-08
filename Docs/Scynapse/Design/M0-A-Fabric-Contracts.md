# Scynapse M0-A Fabric Contracts (Draft)

## 1. Purpose

This document defines the M0-A design baseline for Scynapse fabric contracts:

- canonical terminology (hybrid naming set)
- core architecture invariants
- CNS graph model v0
- resolution and routing relation states
- parent policy inheritance schema v0

This is a design draft, not an implementation commitment.

---

## 2. Locked M0-A Decisions

1. Hybrid naming set is adopted (`Varia`, `Varion`, with conservative aliases).
2. Interaction model is `mediated-first`, with `direct-upgrade` only when policy and both peers allow it.
3. Parent policy inheritance defaults to non-weakenable.
4. Delegated weakening is future-capable and will use scoped grants (expected to map well to CCaps).
5. CNS remains a working token name until scope is finalized.

---

## 3. Canonical Lexicon (Hybrid)

### 3.1 Canonical Terms

| Canonical Term | Meaning |
|---|---|
| `Scyname Node` (Node) | The only participant type in Scynapse. A Node can both serve and consume. There are no silo-less clients. |
| `Varia` | A Scynapse component-level software organism and trust/runtime boundary unit. |
| `Varia Type` | A type/member surface owned by one Varia. |
| `Varion` | A virtual object instance in a Varia context. |
| `Cell` | The per-Node runtime partition for one Varia (its compute/memory/state/network envelope on that Node). |
| `Hive` | The distributed runtime envelope of one Varia across Nodes (`Hive = union of Cells(Varia)`). |
| `CNS` | Working label for Scynapse global naming/systemic graph substrate. |

### 3.2 Conservative Aliases

| Alias Family | Maps To | Usage Status |
|---|---|---|
| `Virtual Component` / `Hyper Component` | `Varia` | Allowed for transition and external communication |
| `Virtual Type` / `Hyper Type` | `Varia Type` | Allowed for transition and external communication |
| `Virtual Object` / `Hyper Object` | `Varion` | Allowed for transition and external communication |
| `Virtual Memory` / `Hyper Memory` | `Cell` | Allowed with context; avoid in low-context protocol docs |

Notes:

1. `Virtual Memory` is valid as a bridge term, but can be confused with OS/runtime VM semantics. Prefer `Cell` in core protocol/spec docs.
2. `Hyper` and `Virtual` aliases are both acceptable in M0-M1; canonical terms remain `Varia/Varion/Cell/Hive`.

### 3.3 Orleans Crosswalk (for migration discussion)

| Orleans Term | Scynapse Direction |
|---|---|
| `Silo` | No global equivalent. Isolation shifts to per-Varia `Cell` on each Node. |
| `Grain` | `Varion` (conceptual analog). |
| `Cluster` | Replaced by dynamic Node participation per Varia (`Hive`). |
| `Client` | Removed as a first-class role; all participants are Nodes. |

### 3.4 Writing Rules (v0)

1. Public docs may use hybrid form on first mention: `Varia (Virtual/Hyper Component)`.
2. Protocol and schema docs should use canonical terms only.
3. Use Orleans terms only inside migration/crosswalk sections.
4. Do not refer to Nodes as "clients" even when they only consume in a scenario.
5. Prefer `mediated-first` and `direct-upgrade` wording over `direct-first`.

### 3.5 Morphology and Plurals (v0)

1. `Varia` singular, `Varias` plural.
2. `Varion` singular, `Varions` plural.
3. `Cell` singular, `Cells` plural.
4. `Hive` singular, `Hives` plural.
5. Lowercase generic use in prose (`a varia`, `a varion`) is allowed, but schema identifiers should preserve canonical capitalization.

### 3.6 Open Naming Hooks (kept intentionally open)

1. CNS expansion text (`Cyber/Cybernetic/Central`, `Name/Namespace/Nervous`, `System/Systemic`) stays open.
2. If CNS scope formally expands beyond naming, acronym meaning can be revised without renaming existing protocol fields.
3. Potential CYBR alignment is not blocked by this lexicon; no commitment is made in M0-A.

---

## 4. Core Invariants

`I1` Node Unification  
Every runtime participant is a Scyname Node that can host Cells and initiate interactions.

`I2` Per-Varia Isolation  
Resources on a Node are partitioned by Varia into Cells. Cells from different Varias are isolated by default.

`I3` Hive Composition  
A Varia's runtime presence is the set of its Cells across participating Nodes (`Hive = union of Cells(Varia)`).

`I4` Mediation First  
Initial locate/handshake is mediated by policy-compliant paths, not assumed direct endpoint disclosure.

`I5` Direct Upgrade is Conditional  
Direct node-to-node transport can be established only if both endpoints consent and effective policy allows it.

`I6` Namespace-Policy Primacy  
Ancestor namespace policy can constrain descendant interactions. Child scope cannot weaken hard parent constraints.

`I7` Dynamic CNS  
Names, bindings, members, versions, and policies are live and mutable with observable change events.

`I8` Unified Addressing Semantics  
The naming hierarchy should remain compatible with security and routing semantics (one hierarchy, multiple uses).

`I9` Pay-for-What-You-Use  
Security/performance profiles are selectable per Varia or namespace policy envelope; expensive guarantees are opt-in unless required by policy.

`I10` Capability-Friendly Evolution  
Future delegated policy overrides must be representable as signed scoped grants (compatible with CCap-style semantics).

---

## 5. CNS Graph Model v0

### 5.1 First-Class Addressable Entities

1. namespaces (spaces)
2. Varia descriptors
3. Varia types
4. Varions
5. members (methods/properties/events/streams)
6. value-typed names
7. policy envelopes
8. relation endpoints

Any type/object can become a namespace host and can contain owned or guest members, subject to policy.

### 5.2 Minimal Record Shape (conceptual)

```json
{
  "name": "<root>.Adult.Games.RedX",
  "kind": "namespace|varia|type|varion|member|value",
  "owner": "pubkey-or-principal",
  "version": "semantic-or-content-addressed",
  "bindings": [
    {
      "target": "node-or-object-ref",
      "valid_from": "timestamp",
      "valid_to": "timestamp|null",
      "health": "optional"
    }
  ],
  "policy_ref": "policy-id-or-inline",
  "revision": 42
}
```

### 5.3 Mutation Events (minimum)

1. `NameCreated`
2. `NameDeleted`
3. `NameMovedOrRenamed`
4. `BindingAdded`
5. `BindingRemoved`
6. `MemberChanged`
7. `ValueChanged`
8. `PolicyChanged`
9. `VersionChanged`

---

## 6. Resolution and Relation Lifecycle v0

### 6.1 State Model

| State | Meaning | Typical Exit |
|---|---|---|
| `ResolveIntent` | Parse CNS expression and intent class (resolve/invoke/observe/etc). | `DiscoverPath` |
| `DiscoverPath` | Find candidate policy-compliant path (cache, known parent, or walk). | `PolicyEvaluate` |
| `PolicyEvaluate` | Compute effective ancestor/target policy envelope. | `DisclosurePlan` or `Deny` |
| `DisclosurePlan` | Determine allowed endpoint disclosure level and relation class. | `MediatedHandshake` or `Deny` |
| `MediatedHandshake` | Establish relation through allowed mediator(s). No direct endpoint assumption. | `RelayedSession` or `DirectUpgradeProbe` |
| `RelayedSession` | Operate through relay/parent mediator. | `DirectUpgradeProbe` or `Completed` |
| `DirectUpgradeProbe` | Attempt direct endpoint exchange only if policy and both endpoints permit disclosure and direct transport. | `DirectSession` or back to `RelayedSession` |
| `DirectSession` | Endpoint-to-endpoint transport (for example mTLS) under relation token. | `Completed` |
| `Completed` | Interaction done; caches/subscriptions may be updated. | terminal |
| `Deny` | Policy or trust failure. | terminal |

### 6.2 Route Modes

1. `ParentMediated`: parent namespace node mediates by policy. Baseline mode.
2. `RelayMediated`: third-party relay path mediates where parent mediation is not sufficient or not desired.
3. `AnonymousRelay`: endpoint identity and/or location shielding is required.
4. `DirectUpgraded`: direct endpoint transport only after mediated establishment plus explicit policy/consent gates.

Wire enum normalization:

1. `ParentMediated` -> `parent_mediated`
2. `RelayMediated` -> `relay_mediated`
3. `AnonymousRelay` -> `anonymous_relay`
4. `DirectUpgraded` -> `direct_upgraded`

### 6.3 Endpoint Disclosure Levels

1. `Hidden`: peer endpoint coordinates are never disclosed.
2. `MediatorVisible`: mediator can resolve both peers; peers receive opaque route handles.
3. `MutualVisible`: both peers receive direct coordinates only after explicit policy and consent checks.

Wire enum normalization:

1. `Hidden` -> `hidden`
2. `MediatorVisible` -> `mediator_visible`
3. `MutualVisible` -> `mutual_visible`

By default, resolution does not imply direct endpoint disclosure.

### 6.4 Direct Upgrade Gates (all required)

1. Effective policy allows direct transport for the operation class.
2. Both endpoints advertise compatible direct transport capability.
3. Trust and attestation checks pass for both endpoints.
4. Both endpoints explicitly consent to endpoint disclosure.
5. A relayed fallback path remains valid.
6. Relation token includes route mode, scope, and expiry constraints.
7. If endpoint registration is encrypted, a valid endpoint-disclosure grant is present.

### 6.5 Worst-Case Name Walk Baseline

When cache/referral misses occur:

1. walk up from requester namespace anchor
2. reach lowest common ancestor context
3. walk down toward target namespace
4. obtain authoritative referral/binding
5. return by best permitted path (typically mediated; direct only if upgrade gates pass)

This is the correctness baseline, not the performance target.

### 6.6 Deterministic Deny Reasons (v0 set)

1. `PathNotFound`
2. `PolicyDenied`
3. `DisclosureDenied`
4. `TrustInsufficient`
5. `UpgradeRejected`
6. `MediatorUnavailable`

---

## 7. Parent Policy Inheritance Schema v0

### 7.1 Envelope Model (conceptual)

```json
{
  "policy_id": "string",
  "inheritance_mode": "hard|delegable",
  "interaction": {
    "default_route_mode": "parent_mediated|relay_mediated|direct_upgrade_allowed",
    "direct_disclosure_allowed": false,
    "required_checks": ["age18", "org_membership", "capability_present"]
  },
  "endpoint_directory": {
    "registration_mode": "plain|encrypted",
    "disclosure_mode": "none|mediator_only|capability_gated",
    "grant_required_for_direct_upgrade": true,
    "grant_ttl_seconds": 900
  },
  "child_override": {
    "enabled": false,
    "allowed_children": [],
    "max_depth": 0,
    "allowed_operations": []
  }
}
```

### 7.2 Effective Policy Rules

1. Evaluate policies root-to-leaf.
2. Hard constraints are non-weakenable at descendants.
3. Descendants may always strengthen constraints.
4. Weakening is valid only when an explicit delegated override grant exists and matches child/depth/operation scope.
5. Missing grant means deny weakening.

### 7.3 Capability Alignment (planned)

Delegated override grants are expected to map to signed scoped capabilities, for example:

- action class: `policy.override`
- resource scope: namespace subtree
- constraints: allowed operations, max depth, expiry, issuer chain

Endpoint disclosure and fast resolve rights should also map to scoped capabilities, for example:

- action class: `resolve.endpoint`
- resource scope: specific name, subtree, or relation class
- constraints: disclosure level, transport class, TTL, optional one-time use

### 7.4 Encrypted Endpoint Registration (future-compatible pattern)

Goal: allow selected resolvers to obtain endpoint-level data without requiring full parent walk every time, while keeping endpoint data encrypted at rest in CNS.

Baseline pattern:

1. Target publishes endpoint descriptor in encrypted form.
2. CNS stores ciphertext plus grant metadata, not plaintext endpoint coordinates.
3. Authorized resolver presents a capability proof (`resolve.endpoint`) and receives only what its grant scope allows.
4. Resolver decrypts endpoint data locally and may attempt direct upgrade if section 6.4 gates pass.

Leak-resistance guidance:

1. Avoid one long-lived shared encryption key for all resolvers.
2. Use short-lived epoch keys for endpoint descriptors.
3. Wrap per-epoch keys per authorized resolver (or per trust group) so one leak does not expose all recipients.
4. Rotate keys on revocation, suspicious access, or relation topology changes.

CNS memory/retention guidance:

1. Keep only active grant references and bounded historical revisions.
2. Use revision windows and TTL expiration for ciphertext snapshots.
3. Compact superseded grants and endpoint versions asynchronously.

This model is intentionally compatible with "pay for what you use": components can stay fully mediated, or opt into capability-gated fast-path disclosure for trusted peers.

---

## 8. Observation and Subscription Model v0

### 8.1 Subscription Modes

1. `OnChange`: push updates when matching revisions occur.
2. `IntervalSnapshot`: periodic snapshots (`every X`).
3. `Predicate`: update only when a predicate evaluates true.
4. `Mixed`: combine `OnChange` with periodic reconciliation.

### 8.2 Observable Scopes

1. namespace subtree
2. specific Varia / Varia Type / Varion
3. specific member (method/property/event/stream metadata)
4. value-typed names
5. binding and endpoint metadata
6. policy and relation metadata
7. version and shape changes (including member signature drift)

### 8.3 Authorization Model

Subscriptions are policy-gated operations. Authorization should be capability-compatible.

Recommended action classes:

1. `observe.meta` for topology, schema, and policy metadata.
2. `observe.value` for value payload updates.
3. `observe.endpoint` for endpoint/binding disclosure classes.
4. `observe.policy` for policy evolution notifications.

Scope and constraints:

1. subtree or exact-name scope
2. event class filter
3. max frequency / minimum interval
4. replay window limit
5. expiry and revocation linkage

### 8.4 Event Envelope (conceptual)

```json
{
  "event_id": "content-or-sequence-id",
  "scope": "<root>.Namespace.Path",
  "event_type": "NameMoved|ValueChanged|PolicyChanged|BindingAdded|...",
  "revision": 1742,
  "timestamp": "utc",
  "payload_ref": "inline-or-content-addressed-ref",
  "delivery_class": "meta|value|policy|binding",
  "policy_proof_ref": "optional",
  "trace": {
    "source_node": "id",
    "relay_mode": "parent_mediated|relay_mediated|direct_upgraded"
  }
}
```

### 8.5 Delivery Guarantees (M0-A baseline)

1. best-effort delivery with monotonic revision markers per scope.
2. at-least-once delivery for authorized subscriptions when transport is available.
3. replay from cursor/revision window when policy permits.
4. deterministic duplicate handling via `event_id`.
5. deterministic gap signaling when replay window has expired.

### 8.6 Cursor, Replay, and Compaction

1. every stream exposes a cursor (`revision` or opaque token).
2. subscribers can resume from last acknowledged cursor.
3. history retention is bounded by policy and storage profile.
4. compaction may collapse many value changes into checkpoint snapshots.
5. compaction must preserve monotonic ordering metadata.

### 8.7 Dynamic Rename/Move Semantics

1. rename/move emits structural events that include prior and new canonical path.
2. subscriptions bound to subtree scope follow moved descendants if policy allows.
3. subscriptions bound to exact path do not implicitly follow unless `follow_moves=true`.
4. tooling consumers must treat rename/move as first-class, not delete+create guesses.

### 8.8 Tooling Profile (IDE/Shell/LSP)

Observation must support interactive tooling scenarios:

1. low-latency metadata updates for dot-chain exploration and intellisense.
2. optional value previews for value-typed names (subject to `observe.value` rights).
3. schema/signature drift notifications for member evolution.
4. resolver hints for transforming dynamic references into typed/static bindings when available.
5. degraded mode fallback: metadata-only if value rights are absent.

### 8.9 Cost Profiles (Pay-for-What-You-Use)

1. `Lite`: metadata-only, no replay, low retention.
2. `Standard`: metadata + bounded replay.
3. `Rich`: value-bearing events + replay + predicate evaluation.
4. `Regulated`: policy-audited event access and longer retention windows.

Profiles are namespace and policy configurable. Higher guarantees imply higher resource cost.

### 8.10 Normative Defaults (locked for M0-A)

1. `follow_moves` default is `true` for subtree subscriptions.
2. `follow_moves` default is `false` for exact-path subscriptions.
3. Tooling default profile is `Standard`.
4. If `observe.value` is not authorized, tooling degrades to metadata-only mode.
5. If replay retention is unavailable, responders must emit deterministic gap signaling (not silent truncation).

---

## 9. CNS Expression Language v0 (Draft Shape)

### 9.1 Tiering

1. Tier 0: plain addressing, URL-simple.
2. Tier 1: dot-chaining with members, calls, indexers.
3. Tier 2: richer query/conditional expressions.

### 9.2 Required Usability Features

1. short or implicit root access
2. well-known namespace constants
3. namespace variables/aliases for long paths

### 9.3 Core Syntax Profile (M0-A subset)

The M0-A subset is intentionally small and tooling-friendly:

1. address paths: `.A.B.C`
2. member access: `.A.B.C.Member`
3. function-like calls: `.A.B.C.Get("x")`
4. index/key selectors: `.A.B.List[key="v"]`
5. chained composition: `.A.B.Get("x").Register(...)`

Identifier guidance:

1. segment separators use dot `.`.
2. segments should be case-preserving and case-sensitive by default.
3. allowed segment character set and escaping rules are deferred to M0-B grammar lock.

### 9.4 Variables and Well-Known Names

Variables/aliases are first-class for shortening deep paths:

1. `let` aliases are local to the expression/session scope.
2. well-known roots (for example `Systems`, `Users`, `Me`, `Social`) are environment-provided bindings.
3. aliases are expansion-only; they do not bypass authorization.

Illustrative:

```text
let red = .Adult.Games.RedX
red.Matchmaking.Queue[region="ca-east"]
```

### 9.5 Evaluation Semantics (v0)

1. Evaluate left-to-right over a dynamic CNS graph.
2. Each hop can resolve to one or more candidates; ambiguity handling is policy/tooling-defined.
3. Each operation (resolve/read/invoke/observe) is authorized at execution time, not parse time.
4. Mutable graph changes during evaluation can yield drift; evaluators should surface drift explicitly.
5. Failures should map to deterministic deny/error classes (section 6.6 and authorization rules).

### 9.6 Authorization and Disclosure Semantics

1. Name visibility does not imply endpoint disclosure.
2. Expression success at metadata level does not imply value-level or endpoint-level rights.
3. Endpoint-aware operations must satisfy route/disclosure gates from section 6 and policy from section 7.
4. Expressions that require elevated disclosure should fail closed when grants are absent.

### 9.7 Tooling Interop Contract (IDE/Shell/LSP)

1. Dot-chain exploration should resolve using `observe.meta` rights.
2. Value previews require `observe.value`.
3. Tooling may propose typed/static binding replacements for dynamic expressions when stable targets exist.
4. Tooling must preserve optional dynamic mode; static replacement is advisory, not mandatory.
5. Missing rights should degrade gracefully (metadata-only hints, explicit locks on restricted members).

### 9.8 Examples (illustrative only)

```text
.Adult.Games.RedX
A.B.C.GetGame("Name").Register(...)
let red = .Adult.Games.RedX
red.Matchmaking.Queue[region="ca-east"]
Me.Projects["Scynapse"].Varias.Auth.GetPolicy()
```

---

## 10. M0-A Out of Scope

1. final wire format choices
2. final transport stack
3. full query grammar and parser details
4. full federation governance
5. implementation plan for anonymity schemes

---

## 11. Handoff to M0-B

M0-B should lock:

1. wire-level message contracts for resolve/handshake/referral/upgrade
2. trust and vouch proof envelope fields
3. relation token structure and lifecycle
4. cache invalidation and referral TTL strategy
5. deny/error taxonomy and deterministic failure behavior

Companion scaffold:

1. `Docs/Scynapse/Design/M0-B-Protocol-Skeleton.md`

---

## 12. Review Checklist

1. Does the lexicon feel right (`Varia/Varion/Hive/Cell`)?
2. Is the mediated-first model captured correctly?
3. Are the parent inheritance constraints correct for your intended governance model?
4. Is the dynamic CNS/subscription baseline sufficient for IDE/Shell real-time scenarios?
5. Which section should be tightened first in the next pass?
