# M1-S9 Fixture Pack (Reference Grant Proof Freshness + Replay)

Scope:

1. `slice_profile: "M1-S9"` extends M1-S8 with deterministic freshness/replay checks for active reference grant proofs.
2. `HandshakeAccept` with `token_transport=reference` and `reference_grant_status=active` requires:
   - `reference_grant_proof_freshness_status` (`fresh|stale`)
   - `reference_grant_proof_replay_status` (`clear|replayed`)
3. Runtime gate order in M1-S9 remains deterministic:
   - M1-S5 token integrity
   - M1-S7 grant status
   - M1-S8 grant proof binding
   - M1-S9 freshness/replay
   - M1-S6 reference lookup guard

Vectors:

1. `TV-1401` active grant + strict + fresh/clear (pass).
2. `TV-1402` active grant + mock + fresh/clear (pass).
3. `TV-1403` active grant missing freshness status (expected fail).
4. `TV-1404` active grant invalid freshness status (expected fail).
5. `TV-1405` active grant missing replay status (expected fail).
6. `TV-1406` active grant invalid replay status (expected fail).
7. `TV-1407` freshness/replay fields present for non-active grant status (expected fail).
8. `TV-1408` stale freshness runtime deny (expected fail).
9. `TV-1409` replayed runtime deny (expected fail).
10. `TV-1410` stale + replayed precedence maps to stale deny first (expected fail).
