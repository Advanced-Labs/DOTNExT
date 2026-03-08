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
