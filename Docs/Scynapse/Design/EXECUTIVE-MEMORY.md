# Scynapse Executive Memory (Re-Entry File)

Last updated: 2026-03-09

## 1. Mission Snapshot

Current mission: close and checkpoint M1-S12 reference-grant issuer-binding work, then open the next bounded M1 slice while preserving deterministic S1..S5 + M1-S1 + M1-S2 + M1-S3 + M1-S4 + M1-S5 + M1-S6 + M1-S7 + M1-S8 + M1-S9 + M1-S10 + M1-S11 + M1-S12 behavior.

Latest checkpoint status (2026-03-09):

1. active branch: `codex/m1-s12-grant-issuer-binding`
2. continuity files synchronized
3. closure rerun confirmed 135/135 effective pass (S1..S5 + M1-S1 + M1-S2 + M1-S3 + M1-S4 + M1-S5 + M1-S6 + M1-S7 + M1-S8 + M1-S9 + M1-S10 + M1-S11 + M1-S12)

Current active path:

1. M0-A complete draft
2. M0-B structured draft with wire-lock decisions
3. S1 prototype closed with deterministic message-driven conformance baseline
4. S2 direct-upgrade slice implemented (profile-gated, fallback-preserving)
5. S3 endpoint-grant slice implemented (encrypted endpoint disclosure gate semantics)
6. S4 observation/replay slice implemented (observe lifecycle and replay-expiry semantics)
7. S5 policy hard-lock slice implemented (policy inheritance deterministic deny semantics)
8. M1-S1 wire-closure slice implemented (D3/D5/D7/D8 locked with deterministic fixture coverage)
9. M1-S2 runtime-bridge slice implemented (RouteData path semantics + transit assertions)
10. M1-S3 security-adapter slice implemented (strict/mock proof verification + deterministic deny mapping)
11. M1-S4 strict failure mapping slice implemented (deterministic strict temporal/revocation/chain fail IDs)
12. M1-S5 relation-token integrity slice implemented (inline token CID integrity deterministic deny mapping)
13. M1-S6 reference-token guard slice implemented (reference lookup unresolved/rebinding/CID-mismatch deterministic deny mapping)
14. M1-S7 reference-grant guard slice implemented (grant-state deterministic deny mapping for reference transport)
15. M1-S8 reference-grant proof-binding slice implemented (active-grant proof verification deterministic deny mapping)
16. M1-S9 reference-grant freshness/replay slice implemented (active-grant freshness/replay deterministic deny mapping)
17. M1-S10 reference-grant claim-binding slice implemented (request-context to grant-claim deterministic binding checks)
18. M1-S11 reference-grant challenge-session nonce-binding slice implemented (challenge/proof/accept nonce deterministic binding checks)
19. M1-S12 reference-grant issuer-binding slice implemented (requested-issuer to active-grant issuer deterministic binding checks)

---

## 2. Done / Doing / Next

### Done

1. M0-A contracts baseline (`M0-A-Fabric-Contracts.md`)
2. M0-B protocol skeleton and supporting matrices
3. Orleans reuse/adaptation matrix and compatibility profile
4. S1 task board + fixture pack + conformance checklist
5. branch baseline commit pushed: `9f6a66743a` on `codex/m0-design-foundation`
6. root `AGENTS.md` created with mandatory orientation protocol
7. methodology continuity layer established (`METHODOLOGY.md`, this file, `SESSION-LOG.md`)
8. continuity layer commit pushed: `6b9b472793` on `codex/m0-design-foundation`
9. pre-compaction refresh protocol added and pushed: `3ee989e892` on `codex/m0-design-foundation`
10. implementation branch created and pushed: `codex/s1-prototype`
11. S1 prototype harness scaffolded under `src/Scynapse/playground/FabricS1Prototype`
12. first S1 harness run executed against `TV-001/002/003/012/013` with 5/5 pass
13. S1 wire-lock decisions `D1`, `D2`, `D4`, `D6` locked and propagated to protocol/field/decision docs
14. S1 harness upgraded to derive observed state traces from message flow and compare against expected traces
15. S1 fixture pack extended with expected-fail negative vectors (`TV-101`, `TV-102`, `TV-103`)
16. S1 harness supports `expect_conformance` mode (`pass`/`fail`) with 8/8 effective pass
17. wire examples synchronized to locked S1 profile (`M0-B-Wire-Examples.md`)
18. expected-fail vectors now support `expected_error_contains` for reason-level rejection checks
19. S1 harness replaced heuristic trace derivation with explicit message-driven operation context execution
20. structured machine-checkable error IDs added to harness output (`[Layer:ErrorId]`)
21. fixture contract extended with `expected_error_ids` (preferred expected-fail oracle)
22. transition-edge negative vectors added (`TV-104`..`TV-109`)
23. repeated harness execution confirms deterministic/reproducible 14/14 effective pass
24. Orleans compatibility profile updated with S1 hardening behavior `A/N/D` classification notes
25. S1 closure artifact added (`M0-S1-Closure.md`) with lock/defer register and handoff boundaries
26. S1 closure commit pushed on `codex/s1-prototype` (`04e297587d`)
27. S2 implementation branch created: `codex/s2-direct-upgrade`
28. fixture profile contract extended with `slice_profile` (`S1` default, explicit `S2`)
29. `RouteUpgradeProbe` S2 gate fields added (`policy_allowed`, `disclosure_allowed`, `grant_status`, `trust_sufficient`)
30. profile-aware conformance execution implemented (`S1` mediated-only posture, `S2` gate-evaluated direct-upgrade)
31. deterministic S2 error IDs added for reject-code mismatch and invalid-accept paths
32. isolated S2 fixture pack added (`Docs/Scynapse/Design/Fixtures/S2`): TV-004, TV-014, TV-201..TV-206
33. regression results stable: S1 14/14 effective pass, S2 8/8 effective pass, repeated runs reproducible
34. S2 implementation commit pushed on `codex/s2-direct-upgrade` (`7bfa560428`)
35. S3 implementation branch created: `codex/s3-endpoint-grants`
36. S3 profile support added for encrypted endpoint disclosure grant validation (`slice_profile: S3`)
37. S3 endpoint fixtures added (`Docs/Scynapse/Design/Fixtures/S3`): TV-005, TV-006, TV-301, TV-302
38. deterministic S3 error IDs added for grant-path ordering and proof-path failures
39. regression results stable: S1 14/14 effective pass, S2 8/8 effective pass, S3 4/4 effective pass with repeated S3 run reproducibility
40. S4 implementation branch created: `codex/s4-observe-replay`
41. S4 profile support added for observation/replay conformance (`slice_profile: S4`)
42. S4 fixture pack added (`Docs/Scynapse/Design/Fixtures/S4`): TV-007..TV-010
43. observe/replay lifecycle transitions implemented in harness (`ObserveOpen/Ack/Event/Gap/Resume/Close`)
44. S4 regression results stable: S1 14/14, S2 8/8, S3 4/4, S4 4/4 with repeated S4 run reproducibility
45. S4 implementation commit pushed on `codex/s4-observe-replay` (`dfa863e3ce`)
46. S5 implementation branch created: `codex/s5-policy-inheritance`
47. S5 profile support added for policy inheritance hard-lock conformance (`slice_profile: S5`)
48. S5 fixture pack added (`Docs/Scynapse/Design/Fixtures/S5`): TV-011, TV-501, TV-502
49. deterministic S5 error IDs added for policy deny ordering and deny-code mismatch paths
50. regression results stable: S1 14/14, S2 8/8, S3 4/4, S4 4/4, S5 3/3 with repeated S5 run reproducibility
51. M0 closure summary artifact added (`M0-Conformance-Closure.md`)
52. M0 exit review completed (`M0-Exit-Review.md`)
53. M1 entry plan completed (`M1-Entry-Plan.md`)
54. M1-S1 first execution board completed (`M1-S1-Task-Board.md`)
55. baseline rerun on planning branch confirmed stable: S1 14/14, S2 8/8, S3 4/4, S4 4/4, S5 3/3
56. M1-S1 harness/profile implemented with wire-closure validators (`slice_profile: "M1-S1"`)
57. M1-S1 fixture pack added (`Docs/Scynapse/Design/Fixtures/M1-S1`, TV-601..TV-610)
58. deferred wire decisions (`D3`, `D5`, `D7`, `D8`) locked in wire decision authority doc
59. cross-pack rerun stable: S1 14/14, S2 8/8, S3 4/4, S4 4/4, S5 3/3, M1-S1 10/10
60. M1-S1 closure artifact added (`M1-S1-Closure.md`) and M1 checkpoint initialized (`M1-Status-Checkpoint.md`)
61. M1-S2 task board added (`M1-S2-Task-Board.md`)
62. M1-S2 harness/profile implemented (`slice_profile: "M1-S2"`) with runtime bridge route-data checks
63. `RouteData` deterministic runtime IDs added (`E3062`..`E3067`) with deterministic deny behavior
64. M1-S2 fixture pack added (`Docs/Scynapse/Design/Fixtures/M1-S2`, TV-701..TV-706)
65. cross-pack rerun stable: S1 14/14, S2 8/8, S3 4/4, S4 4/4, S5 3/3, M1-S1 10/10, M1-S2 6/6
66. M1-S2 closure artifact added (`M1-S2-Closure.md`) and M1 checkpoint synchronized
67. M1-S3 implementation branch created: `codex/m1-s3-security-adapter`
68. harness upgraded to `net9.0` and linked to `Scynapse.Security` for bounded verification integration
69. M1-S3 profile support added (`slice_profile: "M1-S3"`) with `HandshakeProof` verification-mode contract (`mock|strict`)
70. strict-mode adapter session added using `AssertionVerifier` + `InMemoryNonceStore` for deterministic replay/signature behavior
71. deterministic M1-S3 runtime IDs added (`E3070`, `E3071`, `E3072`, `E3073`)
72. M1-S3 fixture pack added (`Docs/Scynapse/Design/Fixtures/M1-S3`, TV-801..TV-805)
73. cross-pack rerun stable: S1 14/14, S2 8/8, S3 4/4, S4 4/4, S5 3/3, M1-S1 10/10, M1-S2 6/6, M1-S3 5/5
74. M1-S4 implementation branch created: `codex/m1-s4-security-failure-mapping`
75. M1-S4 profile support added (`slice_profile: "M1-S4"`) with `HandshakeProof.strict_failure_mode` contract
76. strict verification path extended with deterministic failure-mode mapping (`expired`, `revoked`, `unresolvable_proof`, `not_yet_valid`)
77. deterministic M1-S4 IDs added (`E3080`, `E3081`, `E3082`, `E3083`, `E3084`)
78. M1-S4 fixture pack added (`Docs/Scynapse/Design/Fixtures/M1-S4`, TV-901..TV-906)
79. cross-pack rerun stable: S1 14/14, S2 8/8, S3 4/4, S4 4/4, S5 3/3, M1-S1 10/10, M1-S2 6/6, M1-S3 5/5, M1-S4 6/6
80. M1-S5 implementation branch created: `codex/m1-s5-token-integrity`
81. M1-S5 profile support added (`slice_profile: "M1-S5"`) with relation-token integrity enforcement
82. M1-S1 token-boundary validation reused for M1-S5 handshake accept path
83. deterministic inline token CID mismatch deny ID added (`E3091_M1S5_TOKEN_CID_MISMATCH`)
84. M1-S5 fixture pack added (`Docs/Scynapse/Design/Fixtures/M1-S5`, TV-1001..TV-1004)
85. cross-pack rerun stable: S1 14/14, S2 8/8, S3 4/4, S4 4/4, S5 3/3, M1-S1 10/10, M1-S2 6/6, M1-S3 5/5, M1-S4 6/6, M1-S5 4/4
86. M1-S6 implementation branch created: `codex/m1-s6-reference-token-guard`
87. M1-S6 profile support added (`slice_profile: "M1-S6"`) with reference-token resolution/rebinding guard behavior
88. deterministic M1-S6 IDs added (`E3100`..`E3106`) for unresolved/rebinding/mismatch/schema failures
89. M1-S6 fixture pack added (`Docs/Scynapse/Design/Fixtures/M1-S6`, TV-1101..TV-1105)
90. cross-pack rerun stable: S1 14/14, S2 8/8, S3 4/4, S4 4/4, S5 3/3, M1-S1 10/10, M1-S2 6/6, M1-S3 5/5, M1-S4 6/6, M1-S5 4/4, M1-S6 5/5
91. M1-S7 implementation branch created: `codex/m1-s7-reference-grant-guard`
92. M1-S7 profile support added (`slice_profile: "M1-S7"`) with reference-grant status contract on reference transport
93. deterministic M1-S7 IDs added (`E3110`..`E3116`) for grant-status and grant-ref schema/runtime deny paths
94. M1-S7 fixture pack added (`Docs/Scynapse/Design/Fixtures/M1-S7`, TV-1201..TV-1207)
95. closure rerun stable: S1 14/14, S2 8/8, S3 4/4, S4 4/4, S5 3/3, M1-S1 10/10, M1-S2 6/6, M1-S3 5/5, M1-S4 6/6, M1-S5 4/4, M1-S6 5/5, M1-S7 7/7
96. M1-S8 implementation branch created: `codex/m1-s8-reference-grant-proof-binding`
97. M1-S8 profile support added (`slice_profile: "M1-S8"`) with active-grant proof verification checks on reference transport
98. deterministic M1-S8 IDs added (`E3120`..`E3135`) for grant-proof schema/runtime deny paths
99. M1-S8 fixture pack added (`Docs/Scynapse/Design/Fixtures/M1-S8`, TV-1301..TV-1313)
100. closure rerun stable: S1 14/14, S2 8/8, S3 4/4, S4 4/4, S5 3/3, M1-S1 10/10, M1-S2 6/6, M1-S3 5/5, M1-S4 6/6, M1-S5 4/4, M1-S6 5/5, M1-S7 7/7, M1-S8 13/13
101. M1-S8 closure committed and pushed on `codex/m1-s8-reference-grant-proof-binding` (`150a424da2`)
102. M1-S9 implementation branch created: `codex/m1-s9-grant-proof-freshness-replay`
103. M1-S9 profile support added (`slice_profile: "M1-S9"`) with active-grant freshness/replay checks on reference transport
104. deterministic M1-S9 IDs added (`E3140`..`E3151`) for freshness/replay schema/runtime deny paths
105. M1-S9 fixture pack added (`Docs/Scynapse/Design/Fixtures/M1-S9`, TV-1401..TV-1410)
106. closure rerun stable: S1 14/14, S2 8/8, S3 4/4, S4 4/4, S5 3/3, M1-S1 10/10, M1-S2 6/6, M1-S3 5/5, M1-S4 6/6, M1-S5 4/4, M1-S6 5/5, M1-S7 7/7, M1-S8 13/13, M1-S9 10/10
107. M1-S9 closure committed and pushed on `codex/m1-s9-grant-proof-freshness-replay` (`cdb31da89a`)
108. M1-S10 implementation branch created: `codex/m1-s10-reference-grant-claim-binding`
109. M1-S10 profile support added (`slice_profile: "M1-S10"`) with claim-binding source and active-grant claim-field checks
110. deterministic M1-S10 IDs added (`E3160`..`E3174`) for claim-binding schema/runtime deny paths
111. M1-S10 fixture pack added (`Docs/Scynapse/Design/Fixtures/M1-S10`, TV-1501..TV-1514)
112. closure rerun stable: S1 14/14, S2 8/8, S3 4/4, S4 4/4, S5 3/3, M1-S1 10/10, M1-S2 6/6, M1-S3 5/5, M1-S4 6/6, M1-S5 4/4, M1-S6 5/5, M1-S7 7/7, M1-S8 13/13, M1-S9 10/10, M1-S10 14/14
113. M1-S10 closure committed and pushed on `codex/m1-s10-reference-grant-claim-binding` (`55bce2fe96`)
114. M1-S11 implementation branch created: `codex/m1-s11-grant-challenge-binding`
115. M1-S11 profile support added (`slice_profile: "M1-S11"`) with challenge/proof/accept nonce-binding contracts
116. deterministic M1-S11 IDs added (`E3180`..`E3191`) for nonce-binding schema/runtime deny paths
117. M1-S11 fixture pack added (`Docs/Scynapse/Design/Fixtures/M1-S11`, TV-1601..TV-1612)
118. closure rerun stable: S1 14/14, S2 8/8, S3 4/4, S4 4/4, S5 3/3, M1-S1 10/10, M1-S2 6/6, M1-S3 5/5, M1-S4 6/6, M1-S5 4/4, M1-S6 5/5, M1-S7 7/7, M1-S8 13/13, M1-S9 10/10, M1-S10 14/14, M1-S11 12/12
119. M1-S12 implementation branch created: `codex/m1-s12-grant-issuer-binding`
120. M1-S12 profile support added (`slice_profile: "M1-S12"`) with requested-issuer and active-grant issuer-claim contracts
121. deterministic M1-S12 IDs added (`E3200`..`E3210`) for issuer-binding schema/runtime deny paths
122. M1-S12 fixture pack added (`Docs/Scynapse/Design/Fixtures/M1-S12`, TV-1701..TV-1710)
123. closure rerun stable: S1 14/14, S2 8/8, S3 4/4, S4 4/4, S5 3/3, M1-S1 10/10, M1-S2 6/6, M1-S3 5/5, M1-S4 6/6, M1-S5 4/4, M1-S6 5/5, M1-S7 7/7, M1-S8 13/13, M1-S9 10/10, M1-S10 14/14, M1-S11 12/12, M1-S12 10/10
124. M1-S12 task/closure artifacts added (`M1-S12-Task-Board.md`, `M1-S12-Closure.md`)
125. Claude collaboration bootstrap docs added (`Scynapse-Plan-Report-For-Claude.md`, `AI-Collab-Operating-Model.md`, `AI-Task-0001-M1-S12-Followups.md`)

### Doing

1. define and sequence the next bounded M1 slice from M1-S12 closure baseline

### Next

1. open next M1 task board and branch from `codex/m1-s12-grant-issuer-binding`
2. preserve S1/S2/S3/S4/S5 + M1-S1 + M1-S2 + M1-S3 + M1-S4 + M1-S5 + M1-S6 + M1-S7 + M1-S8 + M1-S9 + M1-S10 + M1-S11 + M1-S12 fixture and error-ID stability

---

## 3. Non-Negotiable Invariants (Do Not Regress)

1. Node unification: no silo-less client role.
2. Per-Varia isolation by `Cell`; distributed runtime by `Hive`.
3. Mediated-first lifecycle; direct path only as conditional upgrade.
4. Parent policy hard inheritance by default.
5. Disclosure and routing are policy-governed and capability-compatible.
6. Ambiguity defaults to fail-closed (`AmbiguousResolution`).
7. CNS is dynamic and observable.

---

## 4. Open Decision Register (Current)

Locked:

1. `D1` enum encoding strategy
2. `D2` timestamp wire representation
3. `D3` typed identifier encoding strictness
4. `D4` proof reference encoding
5. `D5` normalization versioning details
6. `D6` body key dictionary freeze policy
7. `D7` deny envelope required-field policy
8. `D8` relation token serialization boundary

Authority file:

1. `M0-B-Wire-Lock-Open-Decisions.md`

---

## 5. Immediate Reading Order for Any New Agent

1. `METHODOLOGY.md`
2. `M0-Status-Checkpoint.md`
3. `M0-B-Protocol-Skeleton.md`
4. `M0-S1-Task-Board.md`
5. `M0-S2-Task-Board.md`
6. `M0-S3-Task-Board.md`
7. `M0-S4-Task-Board.md`
8. `M0-S5-Task-Board.md`
9. `M0-Conformance-Closure.md`
10. `M0-Exit-Review.md`
11. `M1-Entry-Plan.md`
12. `M1-S1-Task-Board.md`
13. `M1-S1-Closure.md`
14. `M1-Status-Checkpoint.md`
15. `M1-S2-Task-Board.md`
16. `M1-S2-Closure.md`
17. `M1-S3-Task-Board.md`
18. `M1-S3-Closure.md`
19. `M1-S4-Task-Board.md`
20. `M1-S4-Closure.md`
21. `M1-S5-Task-Board.md`
22. `M1-S5-Closure.md`
23. `M1-S6-Task-Board.md`
24. `M1-S6-Closure.md`
25. `M1-S7-Task-Board.md`
26. `M1-S7-Closure.md`
27. `M1-S8-Task-Board.md`
28. `M1-S8-Closure.md`
29. `M1-S9-Task-Board.md`
30. `M1-S9-Closure.md`
31. `M1-S10-Task-Board.md`
32. `M1-S10-Closure.md`
33. `M1-S11-Task-Board.md`
34. `M1-S11-Closure.md`
35. `M1-S12-Task-Board.md`
36. `M1-S12-Closure.md`
37. `Scynapse-Plan-Report-For-Claude.md`
38. `AI-Collab-Operating-Model.md`
39. latest entry in `SESSION-LOG.md`

---

## 6. Active Artifact Map

1. Contracts: `M0-A-Fabric-Contracts.md`
2. Protocol scaffold: `M0-B-Protocol-Skeleton.md`
3. Compatibility and migration:
   - `M0-B-Orleans-Compatibility-Profile.md`
   - `M0-Orleans-Reuse-Matrix.md`
4. Validation and execution:
   - `M0-B-Protocol-Test-Vectors.md`
   - `M0-B-Conformance-Harness-Checklist.md`
   - `M0-Implementation-Slice-Plan.md`
   - `M0-S1-Task-Board.md`
   - `M0-S1-Closure.md`
   - `M0-S2-Task-Board.md`
   - `Fixtures/S2/README.md`
   - `M0-S3-Task-Board.md`
   - `Fixtures/S3/README.md`
   - `M0-S4-Task-Board.md`
   - `Fixtures/S4/README.md`
   - `M0-S5-Task-Board.md`
   - `Fixtures/S5/README.md`
   - `M0-Conformance-Closure.md`
   - `M0-Exit-Review.md`
   - `M1-Entry-Plan.md`
   - `M1-S1-Task-Board.md`
   - `M1-S1-Closure.md`
   - `M1-Status-Checkpoint.md`
   - `Fixtures/M1-S1/README.md`
   - `M1-S2-Task-Board.md`
   - `M1-S2-Closure.md`
   - `Fixtures/M1-S2/README.md`
   - `M1-S3-Task-Board.md`
   - `M1-S3-Closure.md`
   - `Fixtures/M1-S3/README.md`
   - `M1-S4-Task-Board.md`
   - `M1-S4-Closure.md`
   - `Fixtures/M1-S4/README.md`
   - `M1-S5-Task-Board.md`
   - `M1-S5-Closure.md`
   - `Fixtures/M1-S5/README.md`
   - `M1-S6-Task-Board.md`
   - `M1-S6-Closure.md`
   - `Fixtures/M1-S6/README.md`
   - `M1-S7-Task-Board.md`
   - `M1-S7-Closure.md`
   - `Fixtures/M1-S7/README.md`
   - `M1-S8-Task-Board.md`
   - `M1-S8-Closure.md`
   - `Fixtures/M1-S8/README.md`
   - `M1-S9-Task-Board.md`
   - `M1-S9-Closure.md`
   - `Fixtures/M1-S9/README.md`
   - `M1-S10-Task-Board.md`
   - `M1-S10-Closure.md`
   - `Fixtures/M1-S10/README.md`
   - `M1-S11-Task-Board.md`
   - `M1-S11-Closure.md`
   - `Fixtures/M1-S11/README.md`
   - `M1-S12-Task-Board.md`
   - `M1-S12-Closure.md`
   - `Fixtures/M1-S12/README.md`
   - `Scynapse-Plan-Report-For-Claude.md`
   - `AI-Collab-Operating-Model.md`
   - `AI-Task-0001-M1-S12-Followups.md`
5. Wire lock:
   - `M0-B-Wire-Lock-Open-Decisions.md`
   - `M0-B-Wire-Examples.md`

---

## 7. Session Exit Checklist (Mandatory)

1. update this file if `Doing`/`Next`/open decisions changed
2. update milestone checkpoint if plan-level status changed
3. append concise session entry in `SESSION-LOG.md`
4. link new design artifacts from the relevant skeleton/index docs
