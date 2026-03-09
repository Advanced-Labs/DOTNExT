# M1-S5 Task Board (Relation Token Integrity Slice)

Date: 2026-03-08  
Branch: `codex/m1-s5-token-integrity`

## 1. Scope

Extend M1 security/runtime conformance with deterministic relation-token integrity checks:

1. add profile `slice_profile: "M1-S5"`
2. carry forward M1-S1 token-boundary field enforcement
3. enforce inline relation token CID integrity (`sha256(blob)`)
4. preserve all prior slice behavior and regression stability

---

## 2. Workstreams

### W1 Harness/Profile Extension

1. `M1-S5-W1-T1`: add `M1-S5` profile recognition and routing in conformance engine.
2. `M1-S5-W1-T2`: apply M1-S1 token-boundary field checks to `M1-S5` handshake accepts.
3. `M1-S5-W1-T3`: keep M1-S4 strict verification behavior unchanged while extending token-integrity depth.

Exit criteria:

1. M1-S5 profile executes without regressing M1-S4 strict verification behavior.

### W2 Token Integrity Enforcement

1. `M1-S5-W2-T1`: enforce inline transport CID integrity in `HandshakeAccept`.
2. `M1-S5-W2-T2`: map inline CID mismatch to deterministic deny path.
3. `M1-S5-W2-T3`: keep reference transport boundary behavior deterministic.

Exit criteria:

1. inline token CID mismatch is deterministic and machine-checkable.

### W3 Fixtures and Regression

1. `M1-S5-W3-T1`: add isolated fixture pack `Fixtures/M1-S5`.
2. `M1-S5-W3-T2`: add pass/fail vectors for inline match/mismatch and reference boundary behavior.
3. `M1-S5-W3-T3`: rerun S1..S5 + M1-S1 + M1-S2 + M1-S3 + M1-S4 + M1-S5.

Exit criteria:

1. all packs pass effective conformance with deterministic IDs.

### W4 Documentation and Continuity

1. `M1-S5-W4-T1`: sync protocol/vector/matrix/checklist/compatibility docs.
2. `M1-S5-W4-T2`: update continuity checkpoints and session log.

Exit criteria:

1. M1-S5 closure state is deterministic for post-compaction re-entry.

---

## 3. Definition of Done

1. `M1-S5` relation-token integrity checks implemented with stable deterministic error surface.
2. fixture pack `TV-1001..TV-1004` passes effective conformance.
3. prior slices remain regression-green.
4. continuity/checkpoint docs synchronized.

---

## 4. Progress Snapshot (2026-03-08)

1. W1: complete
2. W2: complete
3. W3: complete
4. W4: complete
