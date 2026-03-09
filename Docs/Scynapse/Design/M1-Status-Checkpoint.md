# Scynapse M1 Status Checkpoint

## 1. Why This Exists

M1 introduces implementation-forward work after M0 closure. This checkpoint prevents drift while expanding from wire closure into runtime bridge slices.

Continuity anchors:

1. `Docs/Scynapse/Design/EXECUTIVE-MEMORY.md`
2. `Docs/Scynapse/Design/METHODOLOGY.md`
3. `Docs/Scynapse/Design/SESSION-LOG.md`

---

## 2. M1 Direction

M1 focus:

1. close deferred wire decisions with deterministic conformance (`M1-S1`)
2. bridge conformance flows toward runtime-adjacent execution (`M1-S2`)
3. connect bounded security-adapter verification realism (`M1-S3`)
4. map strict security-adapter failure semantics deterministically (`M1-S4`)
5. add relation-token integrity determinism for inline transport (`M1-S5`)
6. add reference-token lookup/rebinding determinism for reference transport (`M1-S6`)
7. add reference-grant guard determinism for reference transport (`M1-S7`)
8. add reference-grant proof binding determinism for reference transport (`M1-S8`)
9. add reference-grant freshness/replay determinism for reference transport (`M1-S9`)
10. add reference-grant claim-binding determinism for reference transport (`M1-S10`)
11. preserve M0 semantic invariants while increasing execution realism

Guardrails:

1. no reintroduction of silo/client topology assumptions
2. no regression of S1..S5 deterministic behavior
3. no unreviewed error-ID churn

---

## 3. Where We Are Now

### 3.1 M1-S1 (Complete)

Artifacts:

1. `Docs/Scynapse/Design/M1-S1-Task-Board.md`
2. `Docs/Scynapse/Design/M1-S1-Closure.md`
3. `Docs/Scynapse/Design/Fixtures/M1-S1/README.md`
4. `Docs/Scynapse/Design/Fixtures/M1-S1/TV-601..TV-610`

Status:

1. `D3`, `D5`, `D7`, `D8` locked
2. harness/profile support added (`slice_profile: "M1-S1"`)
3. baseline rerun stable:
   - S1 14/14
   - S2 8/8
   - S3 4/4
   - S4 4/4
   - S5 3/3
   - M1-S1 10/10

### 3.2 M1-S2 (Complete)

Artifacts:

1. `Docs/Scynapse/Design/M1-S2-Task-Board.md`
2. `Docs/Scynapse/Design/M1-S2-Closure.md`
3. `Docs/Scynapse/Design/Fixtures/M1-S2/README.md`
4. `Docs/Scynapse/Design/Fixtures/M1-S2/TV-701..TV-706`

Status:

1. runtime bridge profile implemented (`slice_profile: "M1-S2"`)
2. `RouteData` transport-path deterministic checks added
3. bridge transit assertion checks added
4. baseline rerun stable:
   - S1 14/14
   - S2 8/8
   - S3 4/4
   - S4 4/4
   - S5 3/3
   - M1-S1 10/10
   - M1-S2 6/6

### 3.3 M1-S3 (Complete)

Artifacts:

1. `Docs/Scynapse/Design/M1-S3-Task-Board.md`
2. `Docs/Scynapse/Design/M1-S3-Closure.md`
3. `Docs/Scynapse/Design/Fixtures/M1-S3/README.md`
4. `Docs/Scynapse/Design/Fixtures/M1-S3/TV-801..TV-805`

Status:

1. security-adapter profile implemented (`slice_profile: "M1-S3"`)
2. strict mode integrated with `Scynapse.Security.Verification` primitives
3. mock mode deterministic fail/success controls added
4. deterministic replay/signature deny mapping added
5. baseline rerun stable:
   - S1 14/14
   - S2 8/8
   - S3 4/4
   - S4 4/4
   - S5 3/3
   - M1-S1 10/10
   - M1-S2 6/6
   - M1-S3 5/5

### 3.4 M1-S4 (Complete)

Artifacts:

1. `Docs/Scynapse/Design/M1-S4-Task-Board.md`
2. `Docs/Scynapse/Design/M1-S4-Closure.md`
3. `Docs/Scynapse/Design/Fixtures/M1-S4/README.md`
4. `Docs/Scynapse/Design/Fixtures/M1-S4/TV-901..TV-906`

Status:

1. strict failure-mode profile implemented (`slice_profile: "M1-S4"`)
2. strict failure-mode controls added on `HandshakeProof` (`strict_failure_mode`)
3. deterministic strict failure IDs added (`E3080`..`E3084`)
4. baseline rerun stable:
   - S1 14/14
   - S2 8/8
   - S3 4/4
   - S4 4/4
   - S5 3/3
   - M1-S1 10/10
   - M1-S2 6/6
   - M1-S3 5/5
   - M1-S4 6/6

### 3.5 M1-S5 (Complete)

Artifacts:

1. `Docs/Scynapse/Design/M1-S5-Task-Board.md`
2. `Docs/Scynapse/Design/M1-S5-Closure.md`
3. `Docs/Scynapse/Design/Fixtures/M1-S5/README.md`
4. `Docs/Scynapse/Design/Fixtures/M1-S5/TV-1001..TV-1004`

Status:

1. relation-token integrity profile implemented (`slice_profile: "M1-S5"`)
2. M1-S1 token-boundary checks are enforced in M1-S5 handshake accepts
3. inline token CID integrity mismatch deterministic deny added (`E3091`)
4. baseline rerun stable:
   - S1 14/14
   - S2 8/8
   - S3 4/4
   - S4 4/4
   - S5 3/3
   - M1-S1 10/10
   - M1-S2 6/6
   - M1-S3 5/5
   - M1-S4 6/6
   - M1-S5 4/4

### 3.6 M1-S6 (Complete)

Artifacts:

1. `Docs/Scynapse/Design/M1-S6-Task-Board.md`
2. `Docs/Scynapse/Design/M1-S6-Closure.md`
3. `Docs/Scynapse/Design/Fixtures/M1-S6/README.md`
4. `Docs/Scynapse/Design/Fixtures/M1-S6/TV-1101..TV-1105`

Status:

1. reference-token guard profile implemented (`slice_profile: "M1-S6"`)
2. M1-S1 token-boundary checks are enforced in M1-S6 handshake accepts
3. deterministic reference guard IDs added (`E3100`..`E3106`)
4. baseline rerun stable:
   - S1 14/14
   - S2 8/8
   - S3 4/4
   - S4 4/4
   - S5 3/3
   - M1-S1 10/10
   - M1-S2 6/6
   - M1-S3 5/5
   - M1-S4 6/6
   - M1-S5 4/4
   - M1-S6 5/5

### 3.7 M1-S7 (Complete)

Artifacts:

1. `Docs/Scynapse/Design/M1-S7-Task-Board.md`
2. `Docs/Scynapse/Design/M1-S7-Closure.md`
3. `Docs/Scynapse/Design/Fixtures/M1-S7/README.md`
4. `Docs/Scynapse/Design/Fixtures/M1-S7/TV-1201..TV-1207`

Status:

1. reference-grant guard profile implemented (`slice_profile: "M1-S7"`)
2. M1-S1 token-boundary and M1-S6 reference lookup checks are enforced in M1-S7 handshake accepts
3. deterministic reference grant IDs added (`E3110`..`E3116`)
4. baseline rerun stable:
   - S1 14/14
   - S2 8/8
   - S3 4/4
   - S4 4/4
   - S5 3/3
   - M1-S1 10/10
   - M1-S2 6/6
   - M1-S3 5/5
   - M1-S4 6/6
   - M1-S5 4/4
   - M1-S6 5/5
   - M1-S7 7/7

### 3.8 M1-S8 (Complete)

Artifacts:

1. `Docs/Scynapse/Design/M1-S8-Task-Board.md`
2. `Docs/Scynapse/Design/M1-S8-Closure.md`
3. `Docs/Scynapse/Design/Fixtures/M1-S8/README.md`
4. `Docs/Scynapse/Design/Fixtures/M1-S8/TV-1301..TV-1313`

Status:

1. reference-grant proof-binding profile implemented (`slice_profile: "M1-S8"`)
2. M1-S5 token integrity + M1-S7 grant status + M1-S8 grant proof + M1-S6 lookup guard order is enforced on `HandshakeAccept`
3. deterministic reference grant proof IDs added (`E3120`..`E3135`)
4. baseline rerun stable:
   - S1 14/14
   - S2 8/8
   - S3 4/4
   - S4 4/4
   - S5 3/3
   - M1-S1 10/10
   - M1-S2 6/6
   - M1-S3 5/5
   - M1-S4 6/6
   - M1-S5 4/4
   - M1-S6 5/5
   - M1-S7 7/7
   - M1-S8 13/13

### 3.9 M1-S9 (Complete)

Artifacts:

1. `Docs/Scynapse/Design/M1-S9-Task-Board.md`
2. `Docs/Scynapse/Design/M1-S9-Closure.md`
3. `Docs/Scynapse/Design/Fixtures/M1-S9/README.md`
4. `Docs/Scynapse/Design/Fixtures/M1-S9/TV-1401..TV-1410`

Status:

1. reference-grant freshness/replay profile implemented (`slice_profile: "M1-S9"`)
2. M1-S5 token integrity + M1-S7 grant status + M1-S8 grant proof + M1-S9 freshness/replay + M1-S6 lookup guard order is enforced on `HandshakeAccept`
3. deterministic reference grant freshness/replay IDs added (`E3140`..`E3151`)
4. baseline rerun stable:
   - S1 14/14
   - S2 8/8
   - S3 4/4
   - S4 4/4
   - S5 3/3
   - M1-S1 10/10
   - M1-S2 6/6
   - M1-S3 5/5
   - M1-S4 6/6
   - M1-S5 4/4
   - M1-S6 5/5
   - M1-S7 7/7
   - M1-S8 13/13
   - M1-S9 10/10

### 3.10 M1-S10 (Complete)

Artifacts:

1. `Docs/Scynapse/Design/M1-S10-Task-Board.md`
2. `Docs/Scynapse/Design/M1-S10-Closure.md`
3. `Docs/Scynapse/Design/Fixtures/M1-S10/README.md`
4. `Docs/Scynapse/Design/Fixtures/M1-S10/TV-1501..TV-1514`

Status:

1. reference-grant claim-binding profile implemented (`slice_profile: "M1-S10"`)
2. M1-S5 token integrity + M1-S7 grant status + M1-S8 grant proof + M1-S9 freshness/replay + M1-S10 claim-binding + M1-S6 lookup guard order is enforced on `HandshakeAccept`
3. deterministic M1-S10 claim-binding IDs added (`E3160`..`E3174`)
4. baseline rerun stable:
   - S1 14/14
   - S2 8/8
   - S3 4/4
   - S4 4/4
   - S5 3/3
   - M1-S1 10/10
   - M1-S2 6/6
   - M1-S3 5/5
   - M1-S4 6/6
   - M1-S5 4/4
   - M1-S6 5/5
   - M1-S7 7/7
   - M1-S8 13/13
   - M1-S9 10/10
   - M1-S10 14/14

---

## 4. Immediate Next Work (Ordered)

1. define and open the next bounded M1 slice task board
2. preserve locked wire decisions and M1-S3/M1-S4/M1-S5/M1-S6/M1-S7/M1-S8/M1-S9/M1-S10 deterministic error-ID behavior
3. keep S1..S5 + M1-S1 + M1-S2 + M1-S3 + M1-S4 + M1-S5 + M1-S6 + M1-S7 + M1-S8 + M1-S9 + M1-S10 regression suite green on each implementation pass

---

## 5. Done/Doing/Next Snapshot

1. `Done`: M0 exit review, M1 entry plan, M1-S1 closure, M1-S2 closure, M1-S3 closure, M1-S4 closure, M1-S5 closure, M1-S6 closure, M1-S7 closure, M1-S8 closure, M1-S9 closure, M1-S10 closure.
2. `Doing`: next bounded M1 slice selection and sequencing from M1-S10 closure baseline.
3. `Next`: open the next M1 task board and implementation branch.
