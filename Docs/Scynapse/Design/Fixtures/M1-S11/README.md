# M1-S11 Fixture Pack (Reference Grant Challenge-Session Nonce Binding)

Scope:

1. `slice_profile: "M1-S11"` extends M1-S10 with deterministic challenge-session nonce binding.
2. `HandshakeChallenge` must include non-empty `challenge_nonce`.
3. `HandshakeProof` must include non-empty `challenge_nonce` and match challenge nonce.
4. Active reference-grant `HandshakeAccept` must include non-empty `reference_grant_challenge_nonce` and match proof nonce.

Vectors:

1. `TV-1601` strict active challenge-binding pass.
2. `TV-1602` mock active challenge-binding pass.
3. `TV-1603` missing `HandshakeChallenge.challenge_nonce` (expected fail).
4. `TV-1604` invalid `HandshakeChallenge.challenge_nonce` (expected fail).
5. `TV-1605` missing `HandshakeProof.challenge_nonce` (expected fail).
6. `TV-1606` invalid `HandshakeProof.challenge_nonce` (expected fail).
7. `TV-1607` missing `HandshakeAccept.reference_grant_challenge_nonce` (expected fail).
8. `TV-1608` invalid `HandshakeAccept.reference_grant_challenge_nonce` (expected fail).
9. `TV-1609` forbidden `reference_grant_challenge_nonce` outside active reference grant path (expected fail).
10. `TV-1610` proof nonce mismatch runtime deny.
11. `TV-1611` accept nonce mismatch runtime deny.
12. `TV-1612` nonce mismatch precedence over lookup CID mismatch.
