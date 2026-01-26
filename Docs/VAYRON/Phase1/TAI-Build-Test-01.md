# TAI Build Test #1: T01+T02 Infrastructure Verification

> **Purpose:** Verify that T01 (Header Bit) and T02 (OpsRootTable) compile correctly
> **Date:** 2026-01-26
> **Prerequisites:** T01 and T02 implementation complete
> **Status:** Ready for TAI

---

## Build Command

Run from the runtime directory:

```bash
cd src/runtime
./build.sh -subset clr -c Debug
```

Or on Windows:
```cmd
cd src\runtime
build.cmd -subset clr -c Debug
```

**Note:** Using Debug configuration for faster builds and better error messages.

---

## Expected Outcome

### Success Criteria

1. **Build completes without errors**
2. **CrstTypes.def processed correctly** - CrstOpsRootTable should be recognized
3. **TDS source files compiled**:
   - `tds/opsroottable.cpp` compiles
   - `tds/opsroottable.h` included correctly
4. **ceemain.cpp compiles** with TDS initialization code

### Files That Should Compile

| File | Component | Notes |
|------|-----------|-------|
| `vm/syncblk.h` | Header | TDS bit constant and ObjHeader methods |
| `vm/object.h` | Header | Object::IsTDSNonDefault() |
| `vm/tds/opsroottable.h` | Header | OpsRootTable class |
| `vm/tds/opsroottable.cpp` | Source | OpsRootTable implementation |
| `vm/ceemain.cpp` | Source | TDS initialization |
| `inc/CrstTypes.def` | Build | CrstOpsRootTable type |

---

## Potential Issues to Watch For

### 1. Include Path Issues
```
Error: Cannot find include file 'tds/opsroottable.h'
```
**Fix:** Verify CMakeLists.txt adds the tds directory to include paths.

### 2. CrstTypes.def Parse Error
```
Error: Unknown Crst type: OpsRootTable
```
**Fix:** Run CrstTypeTool to regenerate crsttypes_generated.h (usually automatic).

### 3. Missing SHash Include
```
Error: 'SHash' is not defined
```
**Fix:** Ensure `#include "shash.h"` is in opsroottable.h.

### 4. CrstExplicitInit Issues
```
Error: 'CrstExplicitInit' has no member 'Init'
```
**Fix:** Verify the Crst initialization API matches CLR conventions.

### 5. Circular Include
```
Error: Redefinition of 'OpsRootEntry'
```
**Fix:** Check include guards and forward declarations.

---

## Verification Steps After Build

If build succeeds, verify these symbols exist in the output:

```bash
# Check for OpsRootTable symbols (Linux)
nm -C artifacts/bin/coreclr/Linux.x64.Debug/libcoreclr.so | grep OpsRootTable

# Expected output should include:
# g_OpsRootTable
# OpsRootTable::Initialize
# OpsRootTable::Get
# OpsRootTable::Set
# etc.
```

---

## Report Template for TAI

Please report back with:

```markdown
## TAI Build Test #1 Results

**Date:** [date]
**Platform:** [Linux/Windows] [x64/arm64]
**Configuration:** Debug

### Build Result
- [ ] SUCCESS - Build completed without errors
- [ ] FAILURE - Build failed (see errors below)

### Errors (if any)
```
[paste error output here]
```

### Warnings (notable)
```
[paste any TDS-related warnings]
```

### Symbol Verification (if build succeeded)
- [ ] g_OpsRootTable found
- [ ] OpsRootTable methods found
- [ ] CrstOpsRootTable type generated

### Notes
[any observations or issues encountered]
```

---

## Next Steps Based on Results

### If Build Succeeds
- Proceed with T03 (Device Interfaces)
- Consider running basic runtime smoke tests

### If Build Fails
- Analyze error messages
- Fix issues in T01/T02 implementation
- Re-test

---

## Reference: Key Implementation Files

```
src/runtime/src/coreclr/
├── inc/
│   └── CrstTypes.def          # Added CrstOpsRootTable
├── vm/
│   ├── syncblk.h              # BIT_SBLK_TDS_NONDEFAULT, ObjHeader methods
│   ├── object.h               # Object::IsTDSNonDefault()
│   ├── ceemain.cpp            # g_OpsRootTable.Initialize()/Destroy()
│   ├── CMakeLists.txt         # Added TDS sources
│   └── tds/
│       ├── opsroottable.h     # OpsRootTable class
│       ├── opsroottable.cpp   # Implementation
│       └── tds_tests.h        # Native test functions
```
