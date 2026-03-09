# Scynapse Spec-to-Code Bridge Plan

Date: 2026-03-09  
Status: active (approved at M1 closure)  
Scope: planning/design only

## 1. Purpose

Define how deterministic fixture/harness semantics become production-verifiable behavior in the real Scynapse codebase without losing traceability or weakening error determinism.

This plan bridges:

1. protocol conformance artifacts (`Docs/Scynapse/Design/*`, fixtures, E-series IDs),
2. production-oriented Scynapse runtime/security code (`src/Scynapse/src/*`),
3. integration verification (`src/Scynapse/test/*`).

---

## 2. Current Gap

Current state:

1. conformance engine validates JSON fixtures with deterministic state transitions and stable error IDs,
2. bounded strict-mode integration with `Scynapse.Security` exists in harness (`M1-S3/M1-S4`),
3. production pipeline validation is not yet trace-linked to fixture-level protocol oracle.

Risk:

1. spec drift if harness semantics and production behavior evolve independently,
2. delayed discovery of Orleans/runtime constraints on protocol assumptions.

---

## 3. Bridge Model

The bridge is a staged model (not harness replacement and not disconnected test silos).

For first execution pass, run a lightweight diagnostic mapping before full bridge infrastructure.

## B0.5: Diagnostic Mapping Pass (First)

1. pick one representative fixture flow,
2. map fixture concepts/messages/fields to current production code behavior manually,
3. capture direct analogs, missing analogs, and semantic mismatch candidates,
4. publish findings as gap-analysis doc before deeper infrastructure work.

## B0: Canonical Contract Baseline

1. freeze normative fields/IDs/gate precedence for the active M1 baseline,
2. declare authoritative mapping source files:
   1. `M0-B-Protocol-Skeleton.md`
   2. `M0-B-Message-Field-Matrix.md`
   3. `M0-B-Error-Mapping.md`
   4. fixture packs.

## B1: Typed Contract Layer

1. define typed protocol DTOs for envelope and message bodies,
2. maintain explicit schema parity with fixture contracts,
3. add mapping table `fixture_field -> typed_property`.

## B2: Runtime Trace Adapter

1. instrument production-oriented path(s) to emit normalized protocol traces,
2. emit deterministic event set for comparison:
   1. operation lifecycle events,
   2. gate evaluation outcomes,
   3. terminal deny/accept outcomes.

## B3: Comparative Conformance

1. run fixture oracle and production-trace adapter over equivalent scenarios,
2. compare:
   1. state progression,
   2. deny code outcome,
   3. machine-checkable error/reason identity,
3. fail on semantic mismatch.

---

## 4. Initial Bridge Target (B1 Spike Input)

Initial end-to-end target flow:

1. mediated handshake path with reference transport semantics,
2. strict verification path touching `Scynapse.Security.Orleans` call filter pipeline,
3. bounded pass/fail scenarios with deterministic outcome expectations.

First-pass execution mode:

1. diagnostic mapping and gap analysis first,
2. then decide whether to proceed with full B0/B1/B2/B3 implementation scope.

Initial production anchors:

1. `src/Scynapse/src/Scynapse.Security.Orleans/ScynapseIncomingCallFilter.cs`
2. `src/Scynapse/src/Scynapse.Security.Orleans/ScynapseOutgoingCallFilter.cs`
3. `src/Scynapse/test/Scynapse.Security.Integration.Tests/ScynapseSecurityIntegrationTests.cs`

---

## 5. Workstreams

## W1 Contract Mapping

1. produce typed contract catalog and mapping table for selected flow,
2. define versioning policy for typed-contract changes,
3. define normalization requirements for optional/missing fields.

## W2 Runtime Trace Emission

1. define minimal non-invasive trace interface for integration tests,
2. capture gate decision evidence and terminal outcomes,
3. enforce deterministic ordering and timestamps at trace level.

## W3 Comparative Validation

1. define equivalent scenario set between fixture harness and runtime tests,
2. implement comparator rules and mismatch diagnostics,
3. classify mismatches as:
   1. harness defect,
   2. production defect,
   3. contract ambiguity requiring design decision.

## W4 Governance and Drift Control

1. require spec-to-code traceability note in every new M-slice closure,
2. require bridge compatibility check before marking slice fully closed,
3. maintain explicit deferred list for intentionally unmatched semantics.

---

## 6. Acceptance Criteria

Bridge plan is considered active/ready when:

1. typed contract mapping exists for at least one complete flow,
2. runtime trace adapter produces stable normalized traces,
3. comparative checks run in CI for the chosen flow,
4. mismatch triage process is documented and used.

Bridge plan is considered effective when:

1. at least one production-constrained vertical spike passes with no unresolved semantic mismatches,
2. new slice additions include explicit spec-to-code impact notes.

---

## 7. Out of Scope

1. immediate full runtime refactor to new Scynapse architecture fronts,
2. replacement of existing harness with production tests,
3. full closure of F1..F10/F11 fronts before bridge start.

---

## 8. Follow-On Artifacts

1. `M1-Vertical-Spike-B1-Task-Board.md`
2. `M1-Exit-Criteria.md`
3. `B1-Diagnostic-Flow-Mapping.md`
4. `F5-F6-B1-Decision-Delta-Set.md`
5. `M1-B1-Spike-Pass1-Readout.md`
6. future: `Scynapse-Protocol-Typed-Contract-Mapping.md` (to be created during W1)
