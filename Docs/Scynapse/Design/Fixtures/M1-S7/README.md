# M1-S7 Fixture Pack (Reference Lookup Grant Guard)

Scope:

1. `slice_profile: "M1-S7"` extends M1-S6 with capability-gated reference lookup grant checks.
2. reference token transport requires deterministic grant status metadata.
3. active grant status requires typed `reference_grant_ref`.
4. missing/expired/revoked grant states deny deterministically before lookup resolution checks.

Vectors:

1. `TV-1201` active grant + resolved lookup CID match (pass).
2. `TV-1202` missing `reference_grant_status` schema check (expected fail).
3. `TV-1203` grant missing deterministic deny (expected fail).
4. `TV-1204` grant expired deterministic deny (expected fail).
5. `TV-1205` grant revoked deterministic deny (expected fail).
6. `TV-1206` active grant missing `reference_grant_ref` schema check (expected fail).
7. `TV-1207` active grant invalid `reference_grant_ref` schema check (expected fail).
