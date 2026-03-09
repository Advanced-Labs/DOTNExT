# M1-S4 Fixture Pack (Strict Failure Mapping)

Scope:

1. `slice_profile: "M1-S4"` strict security-adapter failure mapping.
2. deterministic strict failure IDs for:
   - expired assertion
   - revoked assertion
   - unresolvable proof chain
   - not-yet-valid assertion
3. strict failure-mode schema validation.

Vectors:

1. `TV-901` strict verification success.
2. `TV-902` strict expired assertion (expected fail).
3. `TV-903` strict revoked assertion (expected fail).
4. `TV-904` strict unresolvable proof chain (expected fail).
5. `TV-905` strict not-yet-valid assertion (expected fail).
6. `TV-906` invalid strict failure mode schema (expected fail).
