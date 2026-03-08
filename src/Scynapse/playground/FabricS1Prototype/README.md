# Fabric S1 Prototype

This playground executable is the first code slice for Scynapse M0 S1 conformance work.

Current scope (W1-W3 baseline):

1. envelope/schema checks (L1)
2. message field checks (L2)
3. state-trace validation (L3)
4. deterministic deny mapping checks (L4)

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

