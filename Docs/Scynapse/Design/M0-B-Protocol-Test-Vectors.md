# M0-B Protocol Test Vectors (Draft)

## 1. Purpose

Provide executable-style test vectors for M0-B protocol behavior.

Each vector includes:

1. scenario id
2. preconditions
3. message sequence
4. expected state trace
5. expected outcome (success or deterministic deny code)

---

## 2. Conventions

1. Message names follow `M0-B-Protocol-Skeleton.md`.
2. States follow `M0-B-State-Transition-Matrix.md`.
3. Deny codes follow `M0-B-Error-Mapping.md`.
4. `N1`, `N2`, `P` denote requester node, target node, and parent mediator.

---

## 3. Test Vectors

### TV-001 Cold Resolve Miss, Full Walk, Success

Preconditions:

1. requester cache miss
2. no direct authority hint
3. path exists under reachable namespace

Sequence:

1. `ResolveRequest` (`expr_raw`, `operation_class=meta`)
2. one or more `ResolveReferral`
3. `ResolveResponse` with canonical scope and candidate bindings

Expected state trace:

1. `ResolveIntent -> DiscoverPath -> PolicyEvaluate -> DisclosurePlan -> MediatedHandshake -> RelayedSession -> Completed`

Expected outcome:

1. success
2. no deny code

### TV-002 Cold Resolve Miss, Path Not Found

Preconditions:

1. requester cache miss
2. namespace walk completes with no authority for target

Sequence:

1. `ResolveRequest`
2. terminal `ResolveDeny`

Expected state trace:

1. `ResolveIntent -> DiscoverPath -> Deny`

Expected outcome:

1. `PathNotFound`
2. `retryable=false` by default

### TV-003 Parent-Mediated Handshake, No Direct Upgrade Allowed

Preconditions:

1. parent policy enforces mediated route
2. direct disclosure disallowed

Sequence:

1. `HandshakeInit`
2. `HandshakeChallenge`
3. `HandshakeProof`
4. `HandshakeAccept` (`route_mode=parent_mediated`, `disclosure_level=hidden|mediator_visible`)

Expected state trace:

1. `MediatedHandshake -> RelayedSession -> Completed`

Expected outcome:

1. success in relayed mode
2. any `RouteUpgradeProbe` attempt must yield `UpgradeRejected` or `DisclosureDenied`

### TV-004 Relayed to Direct Upgrade Success

Preconditions:

1. relation already active in `RelayedSession`
2. policy allows direct upgrade
3. both peers consent and trust checks pass

Sequence:

1. `RouteUpgradeProbe`
2. `RouteUpgradeAccept`
3. operation message over direct route

Expected state trace:

1. `RelayedSession -> DirectUpgradeProbe -> DirectSession -> Completed`

Expected outcome:

1. success
2. fallback relayed route remains valid

### TV-005 Encrypted Endpoint Resolve with Valid Grant

Preconditions:

1. endpoint directory mode is encrypted
2. requester has valid `resolve.endpoint` grant

Sequence:

1. `ResolveRequest` (`operation_class=endpoint`)
2. `GrantPresent` proof path
3. `ResolveResponse` with permitted endpoint disclosure payload

Expected state trace:

1. `ResolveIntent -> DiscoverPath -> PolicyEvaluate -> DisclosurePlan -> MediatedHandshake -> RelayedSession -> Completed`

Expected outcome:

1. success
2. disclosure level does not exceed grant scope

### TV-006 Encrypted Endpoint Resolve Denied (Missing Grant)

Preconditions:

1. endpoint directory mode is encrypted
2. requester lacks `resolve.endpoint` grant

Sequence:

1. `ResolveRequest` (`operation_class=endpoint`)
2. deny response

Expected state trace:

1. `ResolveIntent -> DiscoverPath -> PolicyEvaluate -> DisclosurePlan -> Deny`

Expected outcome:

1. `GrantMissing`
2. remediation must include required action + scope

### TV-007 Observe Subtree with `follow_moves=true`

Preconditions:

1. subscription scope is subtree
2. default `follow_moves=true`

Sequence:

1. `ObserveOpen`
2. `ObserveAck`
3. rename/move occurs under subtree
4. `ObserveEvent` (`event_type=NameMoved`)
5. subsequent descendant events continue on same subscription

Expected state trace:

1. `ObserveIdle -> ObservePendingAck -> ObserveActive -> ObserveActive`

Expected outcome:

1. subscription follows moved descendants
2. no resubscribe required

### TV-008 Observe Exact Path with `follow_moves=false`

Preconditions:

1. subscription scope is exact path
2. default `follow_moves=false`

Sequence:

1. `ObserveOpen`
2. `ObserveAck`
3. exact target renamed
4. structural `ObserveEvent` is emitted
5. no further value/meta events for new path on same subscription

Expected state trace:

1. `ObserveIdle -> ObservePendingAck -> ObserveActive`

Expected outcome:

1. old exact path binding ends
2. caller must re-open for new path

### TV-009 Replay Resume Within Retention Window

Preconditions:

1. active subscription with cursor
2. temporary transport break
3. replay window retained

Sequence:

1. `ObserveResume` (cursor)
2. `ObserveAck` with resumed cursor
3. replayed `ObserveEvent` sequence

Expected state trace:

1. `ObserveActive -> ObserveResuming -> ObserveActive`

Expected outcome:

1. success
2. monotonic revisions preserved

### TV-010 Replay Resume Outside Retention Window

Preconditions:

1. subscription cursor older than retention window

Sequence:

1. `ObserveResume`
2. `ObserveGap` with cause

Expected state trace:

1. `ObserveActive -> ObserveGap`

Expected outcome:

1. `ReplayWindowExpired`
2. remediation points to full resubscribe from head

### TV-011 Parent Hard Policy Blocks Child Weakening

Preconditions:

1. ancestor policy hard-locks constraint
2. child attempts weaker route/disclosure policy
3. no delegated override grant

Sequence:

1. policy-affecting request (`PolicyDelta` or handshake/resolve under weakened claim)
2. deny response

Expected state trace:

1. `PolicyEvaluate -> Deny`

Expected outcome:

1. `PolicyDenied`
2. include policy revision in deny payload

### TV-012 Ambiguous Resolution (Fail-Closed)

Preconditions:

1. multiple candidate bindings match expression
2. caller provides no selector hints

Sequence:

1. `ResolveRequest`
2. `ResolveDeny`

Expected state trace:

1. `ResolveIntent -> DiscoverPath -> Deny`

Expected outcome:

1. `AmbiguousResolution`
2. remediation includes selector strategy hints

### TV-013 Ambiguous Resolution Resolved with Selector

Preconditions:

1. same ambiguity as TV-012
2. caller supplies selector hints

Sequence:

1. `ResolveRequest` with selector hints
2. `ResolveResponse`

Expected state trace:

1. `ResolveIntent -> DiscoverPath -> PolicyEvaluate -> DisclosurePlan -> MediatedHandshake -> RelayedSession -> Completed`

Expected outcome:

1. success

### TV-014 Direct Upgrade Rejected, Fallback Preserved

Preconditions:

1. relayed session active
2. upgrade gate fails (policy or trust or disclosure)

Sequence:

1. `RouteUpgradeProbe`
2. `RouteUpgradeReject`
3. operation continues over relayed path

Expected state trace:

1. `RelayedSession -> DirectUpgradeProbe -> RelayedSession -> Completed`

Expected outcome:

1. `UpgradeRejected` or specific gate code
2. relayed fallback continuity confirmed

---

## 4. Minimal Coverage Map

1. Resolve: TV-001, TV-002, TV-012, TV-013
2. Handshake/Route: TV-003, TV-004, TV-014
3. Endpoint grant/disclosure: TV-005, TV-006
4. Observation/replay: TV-007, TV-008, TV-009, TV-010
5. Policy inheritance: TV-011

### 4.1 S1 Transition-Edge Negative Extension Set

1. TV-101: `ResolveResponse` before `ResolveRequest` context
2. TV-102: required field omission (`ResolveRequest.expr_raw`)
3. TV-103: invalid deny-code mapping
4. TV-104: handshake terminal message (`HandshakeAccept`) before proof
5. TV-105: route-upgrade response before probe
6. TV-106: message after terminal state
7. TV-107: ambiguous resolve path without selector hints but success response
8. TV-108: ambiguous resolve path with empty selector hints but success response
9. TV-109: mediated handshake rejection must remain terminal

### 4.2 Slice Ownership Map

1. S1 owned vectors:
   - TV-001, TV-002, TV-003, TV-012, TV-013
   - TV-101, TV-102, TV-103, TV-104, TV-105, TV-106, TV-107, TV-108, TV-109
2. S2 owned vectors:
   - TV-004, TV-014
   - TV-201 (policy gate deny path)
   - TV-202 (disclosure gate deny path)
   - TV-203 (grant missing gate deny path)
   - TV-204 (grant expired gate deny path)
   - TV-205 (trust insufficient gate deny path)
   - TV-206 (invalid accept with failed gates; expected fail by exact error ID)
3. S3 owned vectors:
   - TV-005 (encrypted endpoint resolve with valid grant)
   - TV-006 (encrypted endpoint resolve deny with missing grant)
   - TV-301 (grant proof message before resolve context; expected fail by exact error ID)
   - TV-302 (endpoint response without required grant proof path; expected fail by exact error ID)

---

## 5. Next Step

Completed:

1. runnable harness checklist and fixture contract (`M0-B-Conformance-Harness-Checklist.md`)
2. S1 fixture pack execution complete with deterministic baseline (`Docs/Scynapse/Design/Fixtures/S1`)
3. S2 fixture pack execution complete with deterministic baseline (`Docs/Scynapse/Design/Fixtures/S2`)
4. S3 fixture pack execution complete with deterministic baseline (`Docs/Scynapse/Design/Fixtures/S3`)

Current next step:

1. open bounded S4 planning for observation/replay vectors (`TV-007`..`TV-010`)
2. preserve S1/S2/S3 baseline behavior and machine-checkable error ID stability
3. keep deferred wire decisions (`D3`, `D5`, `D7`, `D8`) untouched until explicitly unlocked
