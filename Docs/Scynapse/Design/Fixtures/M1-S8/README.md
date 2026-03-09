# M1-S8 Fixture Pack (Reference Grant Proof Binding)

Scope:

1. `slice_profile: "M1-S8"` extends M1-S7 with deterministic grant-proof binding checks.
2. `HandshakeAccept` with `token_transport=reference` and `reference_grant_status=active` requires proof verification controls.
3. strict mode uses typed proof reference and optional strict failure mode.
4. mock mode uses required boolean grant-proof verdict.

Vectors:

1. `TV-1301` active grant + strict mode + no strict failure (pass).
2. `TV-1302` active grant + mock mode valid proof (pass).
3. `TV-1303` active grant missing verification mode (expected fail).
4. `TV-1304` strict mode missing proof ref (expected fail).
5. `TV-1305` strict mode invalid proof ref (expected fail).
6. `TV-1306` mock mode missing mock-valid flag (expected fail).
7. `TV-1307` grant-proof fields present when grant status is not active (expected fail).
8. `TV-1308` strict mode expired grant proof (expected fail).
9. `TV-1309` strict mode revoked grant proof (expected fail).
10. `TV-1310` strict mode invalid signature grant proof (expected fail).
11. `TV-1311` strict mode unresolvable grant proof chain (expected fail).
12. `TV-1312` strict mode not-yet-valid grant proof (expected fail).
13. `TV-1313` mock mode invalid grant proof (expected fail).
