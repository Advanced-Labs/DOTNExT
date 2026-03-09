# M1-S5 Fixture Pack (Relation Token Integrity)

Scope:

1. `slice_profile: "M1-S5"` extends M1-S4 with relation-token integrity checks.
2. inline token transport requires `relation_token_cid` to match `sha256(relation_token_blob)`.
3. deterministic mismatch rejection path is machine-checkable.

Vectors:

1. `TV-1001` inline token CID match (pass).
2. `TV-1002` inline token CID mismatch (expected fail).
3. `TV-1003` reference transport token boundary pass.
4. `TV-1004` reference transport with forbidden token blob (expected fail).
