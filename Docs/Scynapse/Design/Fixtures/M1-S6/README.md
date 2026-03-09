# M1-S6 Fixture Pack (Reference Token Resolution/Rebinding Guard)

Scope:

1. `slice_profile: "M1-S6"` extends M1-S5 with reference-token guard semantics.
2. reference token transport requires a deterministic lookup status.
3. resolved reference lookup must return CID equal to `relation_token_cid`.
4. unresolved and rebinding paths deny deterministically.

Vectors:

1. `TV-1101` reference lookup resolved with matching CID (pass).
2. `TV-1102` reference lookup missing (expected fail).
3. `TV-1103` reference lookup CID mismatch (expected fail).
4. `TV-1104` reference rebinding detected (expected fail).
5. `TV-1105` resolved lookup missing `reference_lookup_cid` schema check (expected fail).
