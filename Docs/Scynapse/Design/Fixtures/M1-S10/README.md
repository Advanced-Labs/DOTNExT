# M1-S10 Fixture Pack (Reference Grant Claim Binding)

Scope:

1. `slice_profile: "M1-S10"` extends M1-S9 with deterministic claim-binding checks for active reference grant proofs.
2. `HandshakeInit` requires claim-binding source fields:
   - `requester_subject_ref` (typed identifier)
   - `requested_scope` (non-empty string)
   - `requested_ops` (non-empty string array)
3. `HandshakeAccept` with `token_transport=reference` and `reference_grant_status=active` requires:
   - `reference_grant_claim_subject_ref` (typed identifier)
   - `reference_grant_claim_scope` (non-empty string)
   - `reference_grant_claim_action` (non-empty string)
4. Runtime gate order in M1-S10 remains deterministic:
   - M1-S5 token integrity
   - M1-S7 grant status
   - M1-S8 grant proof
   - M1-S9 freshness/replay
   - M1-S10 claim binding
   - M1-S6 lookup/rebinding

Vectors:

1. `TV-1501` strict active claim binding pass.
2. `TV-1502` mock active claim binding pass.
3. `TV-1503` missing `requester_subject_ref` (expected fail).
4. `TV-1504` invalid `requester_subject_ref` (expected fail).
5. `TV-1505` invalid `requested_scope` (expected fail).
6. `TV-1506` invalid `requested_ops` (expected fail).
7. `TV-1507` missing claim subject ref (expected fail).
8. `TV-1508` missing claim scope (expected fail).
9. `TV-1509` missing claim action (expected fail).
10. `TV-1510` claim fields present for non-active grant (expected fail).
11. `TV-1511` claim subject mismatch runtime deny.
12. `TV-1512` claim scope mismatch runtime deny.
13. `TV-1513` claim action mismatch runtime deny.
14. `TV-1514` mismatch precedence uses subject mismatch first.
