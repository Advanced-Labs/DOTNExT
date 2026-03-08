# M0 S1 Task Board (Resolve + Mediated Handshake)

## 1. Scope

S1 implements the first executable vertical slice:

1. resolve flow
2. parent-mediated handshake flow
3. deterministic deny behavior for this scope

Reference vectors:

1. TV-001
2. TV-002
3. TV-003
4. TV-012
5. TV-013

---

## 2. Workstreams

### W1 Message Schemas and Validation

1. `S1-W1-T1`: define runtime DTOs for `Resolve*` and `Handshake*` messages
2. `S1-W1-T2`: implement envelope validator (`R/O/C` checks)
3. `S1-W1-T3`: implement family-specific body validator
4. `S1-W1-T4`: add schema unit tests for required/conditional fields

Exit criteria:

1. all schema checks pass for S1 vectors
2. invalid payloads fail with deterministic errors

### W2 State Guard Engine

1. `S1-W2-T1`: implement state context model (`ResolveIntent` ... `Completed/Deny`)
2. `S1-W2-T2`: implement transition validator from state matrix
3. `S1-W2-T3`: enforce terminal-state behavior
4. `S1-W2-T4`: add invalid-transition tests

Exit criteria:

1. all S1 valid transitions accepted
2. all invalid transitions mapped to expected deny codes

### W3 Deterministic Deny Mapper

1. `S1-W3-T1`: implement deny code mapping for resolve/handshake families
2. `S1-W3-T2`: implement retryability default assignment
3. `S1-W3-T3`: implement remediation hint population
4. `S1-W3-T4`: add deny envelope conformance tests

Exit criteria:

1. deny codes are always allowed for message type
2. remediation payload present when required

### W4 Mediated Relation Path

1. `S1-W4-T1`: implement `HandshakeInit -> Challenge -> Proof -> Accept/Deny`
2. `S1-W4-T2`: implement minimal relation token issue/verify
3. `S1-W4-T3`: enforce no-direct-upgrade in S1 scope
4. `S1-W4-T4`: add policy gate stubs for future expansion

Exit criteria:

1. TV-003 passes
2. direct-upgrade attempts in S1 return deterministic rejection path

### W5 Resolve Engine Baseline

1. `S1-W5-T1`: implement resolve request handler
2. `S1-W5-T2`: implement referral handling + path-not-found deny
3. `S1-W5-T3`: implement ambiguity fail-closed + selector success
4. `S1-W5-T4`: add policy/disclosure checkpoints

Exit criteria:

1. TV-001/TV-002/TV-012/TV-013 pass

### W6 Harness and Fixtures

1. `S1-W6-T1`: convert TV-001/002/003/012/013 into fixture files
2. `S1-W6-T2`: implement harness assertions for L1-L4
3. `S1-W6-T3`: add per-vector report output
4. `S1-W6-T4`: wire run summary (pass/fail + coverage)

Exit criteria:

1. fixtures execute deterministically
2. run report generated

---

## 3. Dependency Order

1. W1 -> W2 -> W3
2. W1 + W2 + W3 -> W4 + W5
3. W4 + W5 -> W6

---

## 4. Definition of Done (S1)

1. all S1 vectors pass in harness
2. no unresolved deny-code mismatches
3. no unresolved state-transition mismatches
4. conformance gates G1-G4 pass for S1 scope
5. execution report archived with trace samples

---

## 5. Immediate Kickoff Checklist

1. create code modules for W1-W3 foundations
2. scaffold fixture format and vector loader (fixture pack created under `Docs/Scynapse/Design/Fixtures/S1/`)
3. run first red test (`TV-002`) to validate fail-path plumbing

---

## 6. Progress Snapshot (2026-03-08)

1. W1 baseline: in progress (prototype schema/field validation implemented in `src/Scynapse/playground/FabricS1Prototype`)
2. W2 baseline: in progress (state-trace transition validator implemented)
3. W3 baseline: in progress (deterministic deny mapper baseline implemented)
4. W4-W5: pending (message-driven relation/resolve engine behavior still to harden)
5. W6 baseline: in progress (fixture loader and run summary implemented; first run 5/5 pass on TV-001/002/003/012/013)
