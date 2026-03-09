# M1-S9 Task Board (Reference Grant Proof Freshness + Replay Slice)

Date: 2026-03-08  
Branch: `codex/m1-s9-grant-proof-freshness-replay`

## 1. Scope

Extend M1-S8 reference-grant proof checks with deterministic freshness/replay behavior:

1. add profile `slice_profile: "M1-S9"`
2. enforce freshness/replay schema contract on `HandshakeAccept` for active reference grant
3. enforce deterministic runtime deny mapping for stale/replayed proof outcomes
4. preserve M1-S5 -> M1-S7 -> M1-S8 -> M1-S9 -> M1-S6 gate order for reference handshake acceptance

---

## 2. Workstreams

### W1 Harness/Profile Extension

1. `M1-S9-W1-T1`: add `M1-S9` profile recognition and routing in conformance engine.
2. `M1-S9-W1-T2`: preserve M1-S1/M1-S5/M1-S6/M1-S7/M1-S8 checks under M1-S9.
3. `M1-S9-W1-T3`: enforce deterministic gate order in `HandshakeAccept`.

Exit criteria:

1. M1-S9 executes with deterministic gate order and no regressions in prior slices.

### W2 Freshness/Replay Schema and Runtime

1. `M1-S9-W2-T1`: enforce `reference_grant_proof_freshness_status` contract for active grant.
2. `M1-S9-W2-T2`: enforce `reference_grant_proof_replay_status` contract for active grant.
3. `M1-S9-W2-T3`: forbid freshness/replay fields when grant status is non-active.
4. `M1-S9-W2-T4`: map stale/replayed outcomes to deterministic deny IDs/codes.

Exit criteria:

1. freshness/replay failures are deterministic and machine-checkable.

### W3 Fixtures and Regression

1. `M1-S9-W3-T1`: add isolated fixture pack `Fixtures/M1-S9`.
2. `M1-S9-W3-T2`: cover schema and runtime failures for freshness/replay paths.
3. `M1-S9-W3-T3`: rerun S1..S5 + M1-S1 + M1-S2 + M1-S3 + M1-S4 + M1-S5 + M1-S6 + M1-S7 + M1-S8 + M1-S9.

Exit criteria:

1. all packs pass effective conformance with deterministic IDs.

### W4 Documentation and Continuity

1. `M1-S9-W4-T1`: sync protocol/matrix/checklist/vector/compatibility/wire docs.
2. `M1-S9-W4-T2`: update continuity checkpoints and session log.

Exit criteria:

1. M1-S9 closure state is deterministic for post-compaction re-entry.

---

## 3. Definition of Done

1. `M1-S9` freshness/replay checks implemented with stable deterministic error surface.
2. fixture pack `TV-1401..TV-1410` passes effective conformance.
3. prior slices remain regression-green.
4. continuity/checkpoint docs synchronized.

---

## 4. Progress Snapshot (2026-03-08)

1. W1: complete
2. W2: complete
3. W3: complete
4. W4: complete
