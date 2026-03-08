# M0-B State Transition Matrix (Draft)

## 1. Purpose

Define deterministic state transitions for M0-B protocol flows:

1. resolution and relation lifecycle
2. route upgrade lifecycle
3. observation lifecycle
4. grant/access lifecycle

Each transition specifies valid triggers and deterministic failure codes on invalid paths.

---

## 2. Transition Notation

Columns used in transition tables:

1. `Current`
2. `Trigger`
3. `Preconditions`
4. `Next`
5. `On Failure`

Failure codes come from the canonical taxonomy in M0-B.

---

## 3. Resolution and Relation Lifecycle

### 3.1 Valid Transitions

| Current | Trigger | Preconditions | Next | On Failure |
|---|---|---|---|---|
| `ResolveIntent` | `ResolveRequest` | expression parseable | `DiscoverPath` | `PathNotFound` |
| `DiscoverPath` | internal lookup/referral walk | path candidate found | `PolicyEvaluate` | `PathNotFound` or `MediatorUnavailable` |
| `PolicyEvaluate` | policy engine evaluation | applicable policy resolved | `DisclosurePlan` | `PolicyDenied` |
| `DisclosurePlan` | disclosure evaluation | disclosure contract derivable | `MediatedHandshake` | `DisclosureDenied` |
| `MediatedHandshake` | `HandshakeAccept` | challenge/proof passed | `RelayedSession` | `TrustInsufficient` or `GrantMissing` or `GrantExpired` |
| `MediatedHandshake` | `HandshakeDeny` | none | `Deny` | code carried from deny |
| `RelayedSession` | operation complete | no upgrade requested | `Completed` | `MediatorUnavailable` |
| `RelayedSession` | `RouteUpgradeProbe` | relation active | `DirectUpgradeProbe` | `UpgradeRejected` |
| `DirectUpgradeProbe` | `RouteUpgradeAccept` | all upgrade gates satisfied | `DirectSession` | `UpgradeRejected` or `DisclosureDenied` or `GrantMissing` or `GrantExpired` |
| `DirectUpgradeProbe` | `RouteUpgradeReject` | none | `RelayedSession` | none |
| `DirectSession` | operation complete | none | `Completed` | `TrustInsufficient` |
| `DirectSession` | transport/policy fallback event | fallback route exists | `RelayedSession` | `MediatorUnavailable` |

### 3.2 Invalid Transition Rules

| Invalid Attempt | Deterministic Response |
|---|---|
| `ResolveResponse` received before `ResolveRequest` context exists | `TrustInsufficient` |
| `RouteUpgradeProbe` from `MediatedHandshake` without accepted relation token | `UpgradeRejected` |
| enter `DirectSession` without `RouteUpgradeAccept` | `UpgradeRejected` |
| endpoint disclosure requested while disclosure level is `Hidden` | `DisclosureDenied` |
| operation continues after `Deny` terminal state | `TrustInsufficient` |

---

## 4. Route Upgrade Sub-Lifecycle

### 4.1 Valid Transitions

| Current | Trigger | Preconditions | Next | On Failure |
|---|---|---|---|---|
| `RelayedSession` | `RouteUpgradeProbe` | relation token valid, route upgrade allowed by policy | `DirectUpgradeProbe` | `PolicyDenied` or `UpgradeRejected` |
| `DirectUpgradeProbe` | endpoint disclosure check | disclosure permitted | `DirectUpgradeProbe` (stay) | `DisclosureDenied` |
| `DirectUpgradeProbe` | grant validation | endpoint grant present if required | `DirectUpgradeProbe` (stay) | `GrantMissing` or `GrantExpired` |
| `DirectUpgradeProbe` | trust/attestation validation | trust sufficient | `DirectUpgradeProbe` (stay) | `TrustInsufficient` |
| `DirectUpgradeProbe` | `RouteUpgradeAccept` | all gates passed | `DirectSession` | `UpgradeRejected` |
| `DirectUpgradeProbe` | `RouteUpgradeReject` | none | `RelayedSession` | none |

### 4.2 Direct Upgrade Gate Enforcement Order

Recommended evaluation order:

1. `PolicyDenied`
2. `DisclosureDenied`
3. `GrantMissing` / `GrantExpired`
4. `TrustInsufficient`
5. final `UpgradeRejected`

This keeps remediation predictable and minimal.

---

## 5. Observation Lifecycle

### 5.1 States

1. `ObserveIdle`
2. `ObservePendingAck`
3. `ObserveActive`
4. `ObserveGap`
5. `ObserveResuming`
6. `ObserveClosed`
7. `ObserveDenied`

### 5.2 Valid Transitions

| Current | Trigger | Preconditions | Next | On Failure |
|---|---|---|---|---|
| `ObserveIdle` | `ObserveOpen` | observe rights valid for scope/profile | `ObservePendingAck` | `PolicyDenied` or `GrantMissing` or `GrantExpired` |
| `ObservePendingAck` | `ObserveAck` | subscription accepted | `ObserveActive` | `MediatorUnavailable` |
| `ObserveActive` | `ObserveEvent` | monotonic revision and auth valid | `ObserveActive` | `TrustInsufficient` |
| `ObserveActive` | `ObserveGap` | detected missing range | `ObserveGap` | none |
| `ObserveGap` | `ObserveResume` | replay window available | `ObserveResuming` | `ReplayWindowExpired` |
| `ObserveResuming` | `ObserveAck` | resume accepted | `ObserveActive` | `PolicyDenied` or `GrantExpired` |
| `ObserveActive` | `ObserveClose` | none | `ObserveClosed` | none |
| `ObservePendingAck` | deny response | none | `ObserveDenied` | code carried from deny |

### 5.3 Invalid Transition Rules

| Invalid Attempt | Deterministic Response |
|---|---|
| `ObserveEvent` before `ObserveAck` | `TrustInsufficient` |
| `ObserveResume` from `ObserveIdle` (no subscription context) | `ReplayWindowExpired` |
| value payload requested without `observe.value` rights | `PolicyDenied` |
| endpoint metadata streamed while disclosure not allowed | `DisclosureDenied` |

---

## 6. Grant/Access Lifecycle

### 6.1 States

1. `GrantNone`
2. `GrantActive`
3. `GrantExpiredState`
4. `GrantRefusedState`

### 6.2 Valid Transitions

| Current | Trigger | Preconditions | Next | On Failure |
|---|---|---|---|---|
| `GrantNone` | `GrantPresent` | grant action/scope accepted | `GrantActive` | `PolicyDenied` |
| `GrantNone` | `GrantRefused` | none | `GrantRefusedState` | code carried from deny |
| `GrantActive` | time expiry | now > expiry | `GrantExpiredState` | none |
| `GrantExpiredState` | renewed `GrantPresent` | new valid grant | `GrantActive` | `GrantExpired` |

### 6.3 Invalid Transition Rules

| Invalid Attempt | Deterministic Response |
|---|---|
| use gated action in `GrantNone` state | `GrantMissing` |
| use gated action in `GrantExpiredState` | `GrantExpired` |
| escalate grant scope without policy authority | `PolicyDenied` |

---

## 7. Terminal State Behavior

1. `Completed`: no further action accepted under same one-shot operation context.
2. `Deny`: terminal for the active operation context; restart requires new request with new `msg_id`.
3. `ObserveClosed`: terminal for that subscription id.
4. `ObserveDenied`: terminal for that open attempt; may retry with changed proofs/policy.

---

## 8. Conformance Checklist (State Layer)

1. Every message handler validates current state before processing.
2. Invalid transitions emit deterministic code from this matrix.
3. No silent state coercion (for example auto-upgrading to direct mode).
4. All fallback transitions preserve trace/relation continuity.
5. Tests must include at least one invalid transition per message family.
