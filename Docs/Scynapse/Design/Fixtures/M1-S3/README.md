# M1-S3 Fixture Pack (Security-Adapter Bridge)

Scope:

1. `slice_profile: "M1-S3"` security-adapter behavior on `HandshakeProof`.
2. fixture-selectable verification modes via `verification_mode`:
   - `mock`
   - `strict`
3. deterministic deny mapping for signature and nonce-replay failures.

Vectors:

1. `TV-801` strict verification success.
2. `TV-802` strict nonce replay detection (expected fail).
3. `TV-803` strict invalid signature detection (expected fail).
4. `TV-804` mock verification success.
5. `TV-805` mock replay detection (expected fail).
