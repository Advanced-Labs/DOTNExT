# M1 Vertical Spike B1 Task Board (Spec-to-Code Bridge)

Date: 2026-03-09  
Branch (planned): `codex/m1-b1-vertical-spike`  
Type: bounded production-constrained spike

## 1. Objective

Validate one complete, production-constrained flow against the real Scynapse security/call pipeline and compare it to conformance semantics.

Primary goal:

1. reduce spec drift risk by proving a minimal end-to-end bridge now.

Spike mode:

1. implementation-first diagnostic exercise,
2. not a full bridge-infrastructure delivery in first pass.

---

## 2. Scope

In scope:

1. one mediated security-sensitive call flow through existing Orleans call-filter path,
2. normalized trace emission from integration path,
3. deterministic pass/fail outcomes comparable with harness expectations.

Out of scope:

1. broad architecture refactor,
2. full conversion of harness fixtures to production tests,
3. closure of all F-fronts.

---

## 3. Target Flow

Candidate vertical flow:

1. authenticated call from client through outgoing filter,
2. incoming filter verification path (`CCap`, bearer proof, policy/action/resource checks),
3. success and bounded deny variants recorded with normalized trace output.

Primary production anchors:

1. `src/Scynapse/src/Scynapse.Security.Orleans/ScynapseOutgoingCallFilter.cs`
2. `src/Scynapse/src/Scynapse.Security.Orleans/ScynapseIncomingCallFilter.cs`
3. `src/Scynapse/test/Scynapse.Security.Integration.Tests/ScynapseSecurityIntegrationTests.cs`

---

## 4. Workstreams

## W1 Spike Contract Definition

1. `B1-W1-T1`: define minimal typed spike-contract for selected flow.
2. `B1-W1-T2`: define normalized trace schema for integration run output.
3. `B1-W1-T3`: map expected outcomes to deterministic IDs/reason tokens.

Exit criteria:

1. contract and trace schema are explicit and reviewable.

## W2 Integration Instrumentation (Test-Only)

1. `B1-W2-T1`: add non-invasive instrumentation hooks in test path.
2. `B1-W2-T2`: capture ordered events for call setup, verification, and terminal outcome.
3. `B1-W2-T3`: ensure instrumentation can run repeatedly with stable ordering.

Exit criteria:

1. integration run emits stable normalized traces.

## W3 Scenario Set

1. `B1-W3-T1`: pass scenario (valid capability path).
2. `B1-W3-T2`: deny scenario (invalid signature or missing capability).
3. `B1-W3-T3`: deny scenario (policy/action mismatch).

Exit criteria:

1. each scenario has explicit expected terminal outcome and reason identity.

## W4 Conformance Comparison

1. `B1-W4-T1`: compare integration-trace outcomes against conformance expectations.
2. `B1-W4-T2`: classify mismatches:
   1. harness mismatch,
   2. implementation mismatch,
   3. contract ambiguity.
3. `B1-W4-T3`: produce a spike closure note with discovered constraints.

Exit criteria:

1. mismatch classifications are explicit and actionable.

## W5 Gap-Analysis Readout (Diagnostic Closure)

1. `B1-W5-T1`: write plain-language summary of what the production path currently does.
2. `B1-W5-T2`: write technical mapping from fixture semantics to implementation behavior.
3. `B1-W5-T3`: list required design decisions and classify each as:
   1. baseline update,
   2. implementation change,
   3. deferred exploration.

Exit criteria:

1. readout is actionable for project lead decisions and spec refinement.

---

## 5. Deliverables

1. spike contract/mapping note (new doc),
2. integration test updates for selected flow,
3. normalized trace output artifact format,
4. spike closure summary with findings and follow-up actions.

---

## 6. Acceptance Criteria

1. selected flow runs through real production-oriented filter path, not harness simulation only,
2. at least one pass and two fail paths are validated with deterministic expected outcomes,
3. repeated runs are stable,
4. discovered implementation constraints are captured and fed back into design docs,
5. diagnostic gap-analysis readout is produced before deciding on deeper bridge infrastructure work.

---

## 7. Risks

1. over-expanding the spike into subsystem redesign,
2. ambiguous trace schema that prevents reliable comparison,
3. test-environment coupling that makes runs flaky.

Mitigation:

1. keep scope to one flow and bounded deny variants,
2. treat this as feasibility + constraint discovery, not architecture completion.

---

## 8. Definition of Done

1. W1..W5 exit criteria are satisfied,
2. spike closure summary is published and linked from continuity docs,
3. M1 closure decision is revisited using `M1-Exit-Criteria.md` with spike evidence.

---

## 9. Suggested Execution Ownership

1. implementation-side execution: Claude + Louis
2. spec/conformance review of findings: Codex
3. closure and priority decision: Louis

---

## 10. Execution Status (Pass 1)

Date: 2026-03-09  
Branch: `codex/m1-b1-vertical-spike`

Completed:

1. W1 baseline artifacts now exist:
   - `B1-Diagnostic-Flow-Mapping.md`
   - `F5-F6-B1-Decision-Delta-Set.md`
2. W2 instrumentation implemented (test-only trace sink + normalized events):
   - `ISecurityFlowTraceSink`
   - `SecurityFlowTraceEvent`
   - canonical event names in `SecurityFlowTraceNames`
   - emission in outgoing/incoming security call filters
3. W3 scenario coverage expanded and validated in integration tests:
   - pass flow trace assertions,
   - explicit `InsufficientCapability` path,
   - explicit `ChainVerificationFailed` path.
4. Validation run:
   - `dotnet test src/Scynapse/test/Scynapse.Security.Integration.Tests/Scynapse.Security.Integration.Tests.csproj -c Debug`
   - result: 11/11 pass.

Completed in pass 1:

1. W5 diagnostic closure readout is complete:
   - `M1-B1-Spike-Pass1-Readout.md`.

## 11. Execution Status (Pass 2: Bounded W4 Comparator Follow-Up)

Date: 2026-03-09  
Branch: `codex/m1-b1-vertical-spike`

Completed:

1. W4 comparator automation delivered (bounded follow-up only):
   - `SecurityTraceBridgeComparator.cs` added under integration tests.
2. B1 comparator wired into representative scenarios:
   - pass flow (`ValidCCapPass`),
   - deny flow (`InsufficientCapabilityDeny`),
   - deny flow (`BrokenProofChainDeny`).
3. Comparator output classifies mismatches as:
   - `ImplementationMismatch`,
   - `HarnessMismatch`,
   - `ContractAmbiguity`.
4. Validation reruns:
   - `dotnet test src/Scynapse/test/Scynapse.Security.Integration.Tests/Scynapse.Security.Integration.Tests.csproj -c Debug` -> 11/11 pass,
   - full conformance pack rerun (`S1..S5`, `M1-S1..M1-S12`) -> 135/135 effective pass.

Result:

1. W1..W5 exit criteria satisfied.
2. B1 is complete for M1 closure evidence and no additional M1 micro-slice was opened.
