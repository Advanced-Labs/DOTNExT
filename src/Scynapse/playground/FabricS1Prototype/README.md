# Fabric S1/S2 Prototype

This playground executable is the conformance harness for Scynapse M0 slice work.

Current scope (W1-W3 baseline):

1. envelope/schema checks (L1)
2. message field checks (L2)
3. state-trace validation (L3)
4. deterministic deny mapping checks (L4)

Hardening additions in this pass:

1. explicit message-driven operation context state machine (`Resolve` and `Handshake`)
2. S1 mediated-only enforcement for direct-upgrade attempts
3. terminal-state message rejection
4. structured machine-checkable error IDs in harness output
5. slice-profile-aware route-upgrade behavior (`S1`/`S2`)

Default fixture input:

1. `Docs/Scynapse/Design/Fixtures/S1/TV-*.json`

Run from `src/Scynapse`:

```powershell
dotnet run --project playground/FabricS1Prototype/FabricS1Prototype.csproj
```

Run with explicit fixture directory:

```powershell
dotnet run --project playground/FabricS1Prototype/FabricS1Prototype.csproj -- D:\Dev\dotnext\Docs\Scynapse\Design\Fixtures\S1
```

Run S2 fixture pack:

```powershell
dotnet run --project playground/FabricS1Prototype/FabricS1Prototype.csproj -- D:\Dev\dotnext\Docs\Scynapse\Design\Fixtures\S2
```

Run S3 fixture pack:

```powershell
dotnet run --project playground/FabricS1Prototype/FabricS1Prototype.csproj -- D:\Dev\dotnext\Docs\Scynapse\Design\Fixtures\S3
```

Run S4 fixture pack:

```powershell
dotnet run --project playground/FabricS1Prototype/FabricS1Prototype.csproj -- D:\Dev\dotnext\Docs\Scynapse\Design\Fixtures\S4
```

Run S5 fixture pack:

```powershell
dotnet run --project playground/FabricS1Prototype/FabricS1Prototype.csproj -- D:\Dev\dotnext\Docs\Scynapse\Design\Fixtures\S5
```

Run M1-S1 fixture pack:

```powershell
dotnet run --project playground/FabricS1Prototype/FabricS1Prototype.csproj -- D:\Dev\dotnext\Docs\Scynapse\Design\Fixtures\M1-S1
```

Run M1-S2 fixture pack:

```powershell
dotnet run --project playground/FabricS1Prototype/FabricS1Prototype.csproj -- D:\Dev\dotnext\Docs\Scynapse\Design\Fixtures\M1-S2
```

Run M1-S3 fixture pack:

```powershell
dotnet run --project playground/FabricS1Prototype/FabricS1Prototype.csproj -- D:\Dev\dotnext\Docs\Scynapse\Design\Fixtures\M1-S3
```

Run M1-S4 fixture pack:

```powershell
dotnet run --project playground/FabricS1Prototype/FabricS1Prototype.csproj -- D:\Dev\dotnext\Docs\Scynapse\Design\Fixtures\M1-S4
```

Run M1-S5 fixture pack:

```powershell
dotnet run --project playground/FabricS1Prototype/FabricS1Prototype.csproj -- D:\Dev\dotnext\Docs\Scynapse\Design\Fixtures\M1-S5
```

Run M1-S6 fixture pack:

```powershell
dotnet run --project playground/FabricS1Prototype/FabricS1Prototype.csproj -- D:\Dev\dotnext\Docs\Scynapse\Design\Fixtures\M1-S6
```

Run M1-S7 fixture pack:

```powershell
dotnet run --project playground/FabricS1Prototype/FabricS1Prototype.csproj -- D:\Dev\dotnext\Docs\Scynapse\Design\Fixtures\M1-S7
```

Run M1-S8 fixture pack:

```powershell
dotnet run --project playground/FabricS1Prototype/FabricS1Prototype.csproj -- D:\Dev\dotnext\Docs\Scynapse\Design\Fixtures\M1-S8
```

Run M1-S9 fixture pack:

```powershell
dotnet run --project playground/FabricS1Prototype/FabricS1Prototype.csproj -- D:\Dev\dotnext\Docs\Scynapse\Design\Fixtures\M1-S9
```

Expected-fail fixture semantics:

1. `expected_error_ids` is the preferred exact failure oracle.
2. `expected_error_contains` remains supported for backward-compatible token matching.

Slice profile semantics:

1. `slice_profile` defaults to `S1` when omitted.
2. `slice_profile: "S2"` enables direct-upgrade gate evaluation:
   - `policy_allowed`
   - `disclosure_allowed`
   - `grant_status`
   - `trust_sufficient`
3. `slice_profile: "S3"` enables encrypted endpoint disclosure grant validation:
   - `endpoint_directory_mode`
   - `endpoint_grant_status`
   - `endpoint_disclosure_allowed`
   - `GrantPresent` proof-path checks before `ResolveResponse`
4. `slice_profile: "S4"` enables observation/replay lifecycle validation:
   - `ObserveOpen.scope_mode`
   - `ObserveOpen.follow_moves`
   - `ObserveGap.cause`
   - `ObserveResume.replay_available`
5. `slice_profile: "S5"` enables policy inheritance hard-lock validation:
   - `PolicyDelta.parent_hard_lock`
   - `PolicyDelta.child_weaken_attempt`
   - `PolicyDelta.override_granted`
   - `PolicyDeny.deny_code`
6. `slice_profile: "M1-S1"` enables deferred wire-closure validation:
   - typed identifiers (`<prefix>:<value>`) for id/ref fields
   - `expr_norm` requires `expr_norm_v=1`
   - policy-causal deny messages require `policy_ref`
   - `HandshakeAccept` relation token boundary:
     - `token_transport` (`reference|inline`)
     - `relation_token_ref`
     - `relation_token_cid` (`sha256:<hex>`)
     - `relation_token_blob` required only when `token_transport=inline`
7. `slice_profile: "M1-S2"` enables runtime-bridge data-path validation:
   - direct-upgrade gates from S2 remain active
   - `RouteData` messages validated against active session mode
   - mediated session requires `transport_path=mediated`
   - direct session requires `transport_path=direct`
   - bridge transit assertions available:
     - `bridge_transit_contains`
     - `bridge_transit_count_equals`
8. `slice_profile: "M1-S3"` enables security-adapter bridge validation:
   - `HandshakeProof` requires `verification_mode` (`mock|strict`)
   - `mock` mode uses fixture flags:
     - `mock_signature_valid`
     - `mock_replay_detected`
   - `strict` mode uses Scynapse.Security primitives:
     - `proof_ref`
     - optional `replay_probe`
     - optional `force_bad_signature`
   - deterministic deny mapping remains active for failure paths
9. `slice_profile: "M1-S4"` extends strict security-adapter validation with mapped strict failure modes:
   - optional `strict_failure_mode` (`none|expired|revoked|unresolvable_proof|not_yet_valid`)
   - strict-mode failures map to deterministic IDs (`E3081`..`E3084`)
10. `slice_profile: "M1-S5"` extends strict security-adapter and relation-token validation with integrity checks:
   - M1-S1 relation-token boundary fields are required on `HandshakeAccept`
   - inline token transport enforces `relation_token_cid == sha256(relation_token_blob)`
   - mismatched inline token CID maps to deterministic ID (`E3091_M1S5_TOKEN_CID_MISMATCH`)
11. `slice_profile: "M1-S6"` extends M1-S5 with reference-token resolution/rebinding guard:
   - reference transport requires `reference_lookup_status` (`resolved|missing|rebinding_detected`)
   - `resolved` status requires `reference_lookup_cid` and must match `relation_token_cid`
   - deterministic reference guard IDs:
     - `E3101_M1S6_REFERENCE_TOKEN_UNRESOLVED`
     - `E3102_M1S6_REFERENCE_TOKEN_CID_MISMATCH`
     - `E3103_M1S6_REFERENCE_TOKEN_REBIND_DETECTED`
12. `slice_profile: "M1-S7"` extends M1-S6 with capability-gated reference lookup grant checks:
   - reference transport requires `reference_grant_status` (`active|missing|expired|revoked|not_required`)
   - `active` status requires typed `reference_grant_ref`
   - deterministic reference grant IDs:
     - `E3111_M1S7_REFERENCE_GRANT_MISSING`
     - `E3112_M1S7_REFERENCE_GRANT_EXPIRED`
     - `E3113_M1S7_REFERENCE_GRANT_REVOKED`
13. `slice_profile: "M1-S8"` extends M1-S7 with reference grant proof binding checks:
   - `reference_grant_status=active` requires `reference_grant_verification_mode` (`mock|strict`)
   - strict mode requires typed `reference_grant_proof_ref`
   - mock mode requires boolean `reference_grant_mock_valid`
   - optional strict mode control: `reference_grant_strict_failure_mode` (`none|expired|revoked|unresolvable_proof|invalid_signature|not_yet_valid`)
   - deterministic grant-proof IDs:
     - `E3130_M1S8_REFERENCE_GRANT_PROOF_INVALID_SIGNATURE`
     - `E3131_M1S8_REFERENCE_GRANT_PROOF_CHAIN_UNRESOLVABLE`
     - `E3132_M1S8_REFERENCE_GRANT_PROOF_EXPIRED`
   - `E3133_M1S8_REFERENCE_GRANT_PROOF_REVOKED`
   - `E3134_M1S8_REFERENCE_GRANT_PROOF_NOT_YET_VALID`
   - `E3135_M1S8_REFERENCE_GRANT_PROOF_INVALID_MOCK`
14. `slice_profile: "M1-S9"` extends M1-S8 with reference grant proof freshness/replay checks:
   - active reference grant requires:
     - `reference_grant_proof_freshness_status` (`fresh|stale`)
     - `reference_grant_proof_replay_status` (`clear|replayed`)
   - non-active grant statuses must not include freshness/replay fields
   - deterministic freshness/replay IDs:
     - `E3150_M1S9_REFERENCE_GRANT_PROOF_FRESHNESS_STALE`
     - `E3151_M1S9_REFERENCE_GRANT_PROOF_REPLAY_DETECTED`
