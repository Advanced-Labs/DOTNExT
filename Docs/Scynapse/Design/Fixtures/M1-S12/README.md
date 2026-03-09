# M1-S12 Fixture Pack (Reference Grant Issuer Binding)

Scope:

1. `slice_profile: "M1-S12"` extends M1-S11 with deterministic reference-grant issuer-binding checks.
2. `HandshakeInit` requires typed `requested_grant_issuer_ref`.
3. Active reference-grant `HandshakeAccept` requires typed `reference_grant_claim_issuer_ref`.
4. Non-active grant statuses must not include `reference_grant_claim_issuer_ref`.
5. Runtime issuer mismatch between requested issuer and active grant issuer claim emits deterministic deny.

Vectors:

1. `TV-1701` strict active issuer-binding pass.
2. `TV-1702` mock active issuer-binding pass.
3. `TV-1703` missing `requested_grant_issuer_ref` (expected fail).
4. `TV-1704` invalid `requested_grant_issuer_ref` (expected fail).
5. `TV-1705` missing `reference_grant_claim_issuer_ref` (expected fail).
6. `TV-1706` invalid `reference_grant_claim_issuer_ref` (expected fail).
7. `TV-1707` issuer-claim field forbidden for non-active grant (expected fail).
8. `TV-1708` runtime issuer mismatch deny.
9. `TV-1709` issuer mismatch precedence over lookup CID mismatch.
10. `TV-1710` compatibility precedence uses M1-S10 subject mismatch first.
