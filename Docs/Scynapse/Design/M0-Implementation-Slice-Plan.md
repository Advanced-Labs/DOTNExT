# M0 Implementation Slice Plan (Draft)

## 1. Purpose

Define a pragmatic first implementation plan derived from M0-B vectors and conformance gates.

Primary objective:

1. deliver one end-to-end vertical slice that proves the mediated-first model with deterministic behavior.

---

## 2. Planning Inputs

1. `M0-B-Protocol-Test-Vectors.md`
2. `M0-B-Conformance-Harness-Checklist.md`
3. `M0-B-Message-Field-Matrix.md`
4. `M0-B-Error-Mapping.md`
5. `M0-B-State-Transition-Matrix.md`

---

## 3. Slice Selection Criteria

1. must exercise core M0 invariants (mediated-first, policy gate, deterministic errors)
2. must include at least one success and one deny path
3. must be small enough for rapid iteration
4. should minimize dependencies on unresolved wire-lock items

---

## 4. Proposed Slices

### Slice S1 (Recommended First): Resolve + Mediated Handshake

Vectors:

1. TV-001 (resolve success)
2. TV-002 (path not found)
3. TV-003 (parent-mediated handshake, no direct-upgrade)
4. TV-012/TV-013 (ambiguous resolution deny/success with selector)

Why first:

1. highest architectural value with lowest coupling to advanced features
2. validates routing/policy relation baseline before upgrades and streaming behavior

### Slice S2: Direct Upgrade Path

Vectors:

1. TV-004 (upgrade success)
2. TV-014 (upgrade reject with fallback continuity)

### Slice S3: Encrypted Endpoint Disclosure Grants

Vectors:

1. TV-005 (grant success)
2. TV-006 (grant missing deny)

### Slice S4: Observation + Replay

Vectors:

1. TV-007, TV-008 (follow-moves behavior)
2. TV-009, TV-010 (resume success/failure)

### Slice S5: Policy Inheritance Hard-Lock

Vectors:

1. TV-011

---

## 5. S1 Scope Contract (First Deliverable)

### 5.1 In Scope

1. `ResolveRequest`, `ResolveResponse`, `ResolveReferral`, `ResolveDeny`
2. `HandshakeInit`, `HandshakeChallenge`, `HandshakeProof`, `HandshakeAccept`, `HandshakeDeny`
3. minimal relation token issuance and validation
4. deterministic deny mapping for resolve + handshake families
5. conformance checks L1-L4 for S1 message families

### 5.2 Out of Scope

1. direct-upgrade execution
2. encrypted endpoint grant path
3. observation stream/replay mechanics

---

## 6. S1 Milestones

1. `M1`: schema and envelope validation for resolve/handshake messages
2. `M2`: state-machine enforcement for resolve + mediated handshake
3. `M3`: deterministic deny mapping + remediation payloads
4. `M4`: harness execution for S1 vectors with pass/fail reporting

---

## 7. S1 Acceptance Criteria

1. TV-001, TV-002, TV-003, TV-012, TV-013 pass in harness
2. all invalid transitions in S1 domain fail with mapped deterministic code
3. no endpoint disclosure in flows marked `hidden`
4. audit payload includes required fields for regulated profile paths
5. coverage report generated for harness layers L1-L4

---

## 8. Risks and Mitigations

1. Risk: enum drift between conceptual and wire names  
   Mitigation: use normalization tables from M0-A and wire enums in S1 schemas.
2. Risk: hidden Orleans topology assumptions in helpers  
   Mitigation: enforce compatibility profile tags (`A/N/D`) in PR checklist.
3. Risk: over-scoping first slice  
   Mitigation: keep S1 strictly resolve + mediated handshake only.

---

## 9. Immediate Next Action

Completed:

1. S1 implementation task board: `M0-S1-Task-Board.md`
2. S1 closure baseline: `M0-S1-Closure.md` (14/14 effective pass)
3. S2 implementation task board: `M0-S2-Task-Board.md` (8/8 effective pass)
4. S3 implementation task board: `M0-S3-Task-Board.md` (4/4 effective pass)

Current next action:

1. start bounded S4 planning for observation/replay vectors (`TV-007`..`TV-010`)
2. maintain baseline stability across S1/S2/S3 while adding S4 scope
