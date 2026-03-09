# M1-S1 Closure (Deferred Wire Decisions)

Date: 2026-03-08  
Implementation branch: `codex/m1-s1-wire-closure`

## 1. Scope Closed

M1-S1 closed deferred wire decisions from M0:

1. `D3` identifier typed-string strictness
2. `D5` normalization versioning (`expr_norm_v`)
3. `D7` deny envelope `policy_ref` policy
4. `D8` relation token serialization boundary

---

## 2. Deterministic Validation Results

Harness runs on closure pass:

1. S1: 14/14 effective pass
2. S2: 8/8 effective pass
3. S3: 4/4 effective pass
4. S4: 4/4 effective pass
5. S5: 3/3 effective pass
6. M1-S1: 10/10 effective pass

Total closure baseline: 43/43 effective pass.

---

## 3. Locked Outcomes

1. `D3`: typed identifier canonical form `<prefix>:<value>` with locked prefix set.
2. `D5`: `expr_norm` requires integer `expr_norm_v`; supported set currently `{1}`.
3. `D7`: policy-causal deny codes require `policy_ref`.
4. `D8`: `HandshakeAccept` token transport boundary:
   - required `token_transport`
   - required `relation_token_ref` + `relation_token_cid`
   - `relation_token_blob` inline-only

---

## 4. Artifacts Updated

1. `M0-B-Wire-Lock-Open-Decisions.md`
2. `M0-B-Message-Field-Matrix.md`
3. `M0-B-Wire-Examples.md`
4. `M0-B-Conformance-Harness-Checklist.md`
5. `M0-B-Protocol-Test-Vectors.md`
6. `M0-B-Orleans-Compatibility-Profile.md`
7. `M0-B-Protocol-Skeleton.md`
8. `src/Scynapse/playground/FabricS1Prototype/ConformanceEngine.cs`
9. `Docs/Scynapse/Design/Fixtures/M1-S1/*`

---

## 5. Next Step

1. begin M1-S2 runtime-bridge slice from this closure baseline
2. preserve S1..S5 + M1-S1 deterministic behavior and error-ID stability
