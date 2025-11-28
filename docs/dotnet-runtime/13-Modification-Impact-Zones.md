# Modification Impact Zones

**Understanding the Ripple Effects of Code Changes**

This guide helps you understand what else you need to consider when modifying specific parts of the runtime.

## Impact Zone Categories

### 🔴 Critical Path (High Risk, Wide Impact)

Changes here affect almost everything. Extensive testing required.

### 🟡 Significant Impact (Medium Risk, Moderate Impact)

Changes affect multiple subsystems. Targeted testing required.

### 🟢 Localized Impact (Low Risk, Narrow Impact)

Changes are isolated. Basic testing sufficient.

---

## Core Data Structures

### Object Header (🔴 Critical)

**Files:** `src/coreclr/vm/object.h`, `syncblk.h`

**Current Layout:**
```cpp
struct ObjHeader {
    DWORD m_SyncBlockValue;  // 32 bits total!
    // Bits used:
    // [0-25]   - Sync block index OR hash code
    // [26]     - Hash code flag
    // [27]     - GC marking bit
    // [28]     - Pinning flag
    // [29]     - Finalizer flag
    // [30-31]  - Reserved
};

class Object {
    MethodTable* m_pMethTab;  // 8 bytes on 64-bit
    // Fields follow...
};
```

**Impact of Changes:**

| Change | What Breaks | Files to Update | Test Suites |
|--------|-------------|-----------------|-------------|
| **Add bit to header** | GC, sync, hash code | 20-30 files | All GC tests, all VM tests |
| **Change MethodTable ptr location** | Everything | 100+ files | Full test suite |
| **Add new header field** | Memory layout, GC | 50+ files | Full test suite |

**Affected Systems:**
- ✅ **GC** - Reads object header for marking, pinning
- ✅ **Synchronization** - Uses sync block bits
- ✅ **Hash codes** - Stored in header or sync block
- ✅ **Debugging (DAC)** - Reads object headers from dumps
- ✅ **JIT** - May inline object access assumptions
- ✅ **Profiler** - May read object layout

**Example Ripple:**
```
Add 1 bit to object header
    ↓
Reduce sync block index space (26→25 bits)
    ↓
Max sync blocks: 64M → 32M
    ↓
Large apps may run out of sync blocks
    ↓
Need to redesign sync block allocation strategy
    ↓
Affects Monitor.Enter() performance
```

**Recommendation:** Avoid if possible. Use indirection instead (pointer from MethodTable to side data).

---

### MethodTable (🔴 Critical)

**File:** `src/coreclr/vm/methodtable.h`

**Current Layout (Simplified):**
```cpp
class MethodTable {
    DWORD m_dwFlags;                    // Type characteristics
    DWORD m_BaseSize;                   // Base instance size
    WORD m_wNumInterfaces;              // Interface count
    WORD m_wNumVirtuals;                // Virtual method count
    MethodTable* m_pParentMethodTable;  // Base class
    EEClass* m_pEEClass;                // Metadata class info
    PTR_SLOT m_pSlots;                  // VTable slots
    // ... 20+ more fields
};
```

**Impact of Changes:**

| Change | Impact | Reason |
|--------|--------|--------|
| **Add field** | Medium | ~10K MethodTables × 8 bytes = 80KB (acceptable) |
| **Change vtable layout** | High | JIT embeds vtable offsets, crashes on mismatch |
| **Change flags** | High | Many components check flags |

**Affected Systems:**
- ✅ **Type loading** - Constructs MethodTable
- ✅ **JIT** - Embeds MethodTable offsets for calls
- ✅ **GC** - Reads MethodTable for object size, layout
- ✅ **Virtual dispatch** - Reads vtable
- ✅ **Interface dispatch** - Reads interface map
- ✅ **Reflection** - Enumerates methods
- ⚠️ **Debugger (DAC)** - Reads MethodTable from dumps (must update DAC)

**DAC Requirement:**
```cpp
// Changes to MethodTable require DAC updates
// src/coreclr/debug/daccess/daccess.cpp
VPTR_CLASS(MethodTable)
VPTR_FIELD(MethodTable, m_pEEClass)
VPTR_FIELD(MethodTable, m_pParentMethodTable)
// Must add new fields here!
```

**Recommendation:** Adding fields is OK if justified. Changing layout of existing fields requires coordination across 5+ teams.

---

### MethodDesc (🟡 Significant)

**File:** `src/coreclr/vm/method.hpp`

**Layout:**
```cpp
class MethodDesc {
    WORD m_wFlags;              // Method characteristics
    WORD m_wSlotNumber;         // VTable slot (if virtual)
    PTR_BYTE m_pCode;           // JIT-compiled code pointer
    // Followed by variable-sized data
};
```

**Impact of Changes:**

| Change | Impact | Affected Systems |
|--------|--------|------------------|
| **Add field** | Medium | ~100K methods × 8 bytes = 800KB |
| **Change flags** | Medium | JIT, reflection, type loader |
| **Change code pointer location** | High | JIT, prestub, tiering |

**Affected Systems:**
- ✅ **JIT** - Writes code pointer
- ✅ **Tiered compilation** - Updates code pointer for tier 1
- ✅ **Reflection** - Reads method info
- ✅ **Prestub** - Manages lazy JIT
- ⚠️ **Debugger** - Breakpoints

---

## Compilation Pipeline

### JIT IR Changes (🟡 Significant)

**Files:** `src/coreclr/jit/gentree.h`, `gentree.cpp`

**Impact of Adding New IR Node:**

| File | What to Update | Reason |
|------|----------------|--------|
| **gentree.h** | Add `GT_MY_NODE` enum | Define node type |
| **gentree.cpp** | Add to `gtDispNode()` | Debugging output |
| **flowgraph.cpp** | Handle in traversals | Don't break existing passes |
| **morph.cpp** | Morphing logic | Transformation rules |
| **lower.cpp** | Lowering to LIR | Platform-specific lowering |
| **codegenxarch.cpp** | Code generation (x64) | Emit instructions |
| **codegenarm64.cpp** | Code generation (ARM64) | Emit instructions |
| **gcinfo.cpp** | GC info (if node creates refs) | Stack walking |

**Effort Estimate:** 2-3 days for experienced developer

**Example: Adding `GT_INTRINSIC_NEW`:**

```cpp
// 1. gentree.h
enum genTreeOps : BYTE {
    GT_INTRINSIC_NEW = 300,
};

// 2. gentree.cpp
void Compiler::gtDispNode(GenTree* tree) {
    case GT_INTRINSIC_NEW:
        printf("intrinsic_new");
        break;
}

// 3. morph.cpp
GenTree* Compiler::fgMorphTree(GenTree* tree) {
    case GT_INTRINSIC_NEW:
        return MorphIntrinsicNew(tree);
}

// 4. lower.cpp
void Lowering::LowerNode(GenTree* node) {
    case GT_INTRINSIC_NEW:
        LowerIntrinsicNew(node);
}

// 5. codegenxarch.cpp
void CodeGen::genCodeForTreeNode(GenTree* tree) {
    case GT_INTRINSIC_NEW:
        genIntrinsicNew(tree);
}
```

**Testing Required:**
- JIT tests with new intrinsic
- All architectures (x64, ARM64, ARM, x86)
- SuperPMI replay (ensure no regressions)

---

### JIT Helper Functions (🟢 Localized)

**Adding a new JIT helper is relatively safe.**

**Steps:**

1. **Declare helper:** `src/coreclr/inc/jithelpers.h`
   ```cpp
   JITHELPER(CORINFO_HELP_MY_HELPER, JIT_MyHelper, CORINFO_HELP_SIG_REG_ONLY)
   ```

2. **Implement helper:** `src/coreclr/vm/jithelpers.cpp`
   ```cpp
   HCIMPL2(INT32, JIT_MyHelper, Object* obj, INT32 value) {
       // Implementation
       return result;
   }
   HCIMPLEND
   ```

3. **Call from JIT:** `src/coreclr/jit/morph.cpp`
   ```cpp
   GenTree* call = gtNewHelperCallNode(CORINFO_HELP_MY_HELPER, TYP_INT, obj, value);
   ```

**Impact:** Low - helpers are designed to be extensible

**Testing:** Helper-specific tests

---

## Memory Management

### GC Algorithm Changes (🔴 Critical)

**File:** `src/coreclr/gc/gc.cpp` (2M lines!)

**Impact of Changes:**

| Change Type | Risk | Testing Required |
|-------------|------|------------------|
| **Collection algorithm** | Extreme | Full GC test suite + stress |
| **Heap layout** | High | All GC tests |
| **Write barrier** | High | All tests (affects correctness) |
| **Allocation strategy** | Medium | Allocation tests + stress |
| **Configuration** | Low | Config tests |

**Example Ripple: Change Write Barrier**

```
Modify write barrier algorithm in gc.cpp
    ↓
Update JIT write barrier generation (src/coreclr/jit/codegenxarch.cpp)
    ↓
Update all architectures (x64, ARM64, ARM, x86)
    ↓
Update assembly stubs (src/coreclr/vm/amd64/JitHelpers_*.asm)
    ↓
Update GC stress modes
    ↓
Test on all platforms × all configurations
    ↓
6-8 weeks of work
```

**Affected Files (Write Barrier Change):**
- `src/coreclr/gc/gc.cpp` - Algorithm
- `src/coreclr/jit/codegenxarch.cpp` - x64 codegen
- `src/coreclr/jit/codegenarm64.cpp` - ARM64 codegen
- `src/coreclr/vm/amd64/JitHelpers_*.asm` - Assembly stubs
- `src/coreclr/vm/arm64/stubs.cpp` - ARM64 stubs
- Tests: All GC tests

**Recommendation:** GC changes require deep expertise. Prototype thoroughly.

---

### GC Configuration (🟢 Localized)

**File:** `src/coreclr/gc/gcconfig.cpp`

**Adding config is safe:**

```cpp
// src/coreclr/gc/gcconfig.cpp
void GCConfig::Initialize() {
    m_myNewSetting = GetConfigInteger("GC_MyNewSetting", defaultValue);
}

// Usage in gc.cpp
if (GCConfig::GetMyNewSetting() > threshold) {
    // Apply optimization
}
```

**Impact:** Low - configuration doesn't affect correctness

**Testing:** Config tests only

---

## Threading and Synchronization

### Thread State Changes (🔴 Critical)

**File:** `src/coreclr/vm/threads.h`

**Thread object is central to VM:**

```cpp
class Thread {
    // ~200 fields!
    ThreadState m_State;
    Frame* m_pFrame;
    bool m_fPreemptiveGCDisabled;
    AppDomain* m_pDomain;  // Historical
    Context* m_pContext;
    // ...
};
```

**Impact of Changes:**

| Change | Risk | Reason |
|--------|------|--------|
| **Add field** | Low | Thread count is small (~10-100) |
| **Change state transitions** | High | Affects GC, debugger, profiler |
| **Change GC mode** | Extreme | Can cause crashes, corruption |

**Affected Systems:**
- ✅ **GC** - Thread suspension, GC mode
- ✅ **Debugger** - Thread control
- ✅ **Profiler** - Thread notifications
- ✅ **Exception handling** - Stack walking
- ✅ **Synchronization** - Monitor, locks

---

### Monitor/Lock Changes (🟡 Significant)

**Files:** `src/coreclr/vm/syncblk.cpp`, `syncblk.h`

**Lock mechanism:**
```
obj.GetHashCode() or lock(obj)
    ↓
Check object header sync block bits
    ↓
If thin lock: Try atomic compare-exchange
    ↓
If fails: Inflate to sync block (fat lock)
    ↓
Allocate SyncBlock structure
    ↓
Use mutex/event for contention
```

**Impact of Changes:**

| Change | Risk | Reason |
|--------|------|--------|
| **Thin lock algorithm** | Medium | High-performance path |
| **Sync block allocation** | Medium | Memory management |
| **Inflation threshold** | Low | Performance tuning |

**Testing:** Lock tests, stress tests with high contention

---

## Diagnostics and Profiling

### ETW Event Changes (🟢 Localized)

**Files:** `src/coreclr/vm/ClrEtwAll.man`, event call sites

**Adding events is safe:**

```xml
<!-- ClrEtwAll.man -->
<event value="1000"
       symbol="MyFeature_OperationStart"
       task="MyFeatureTask"
       opcode="Start"
       level="Informational">
  <template tid="MyFeatureTemplate">
    <data name="OperationID" inType="win:UInt64"/>
    <data name="Details" inType="win:UnicodeString"/>
  </template>
</event>
```

```cpp
// Call site (e.g., src/coreclr/vm/myfeature.cpp)
void MyFeature::StartOperation(UINT64 opId, LPCWSTR details) {
    FireEtwMyFeature_OperationStart(opId, details);
}
```

**Impact:** Very low - events are opt-in

**Testing:** Ensure event fires correctly

---

### Profiler API Changes (🟡 Significant)

**File:** `src/coreclr/inc/corprof.idl`

**Adding profiler callback:**

```cpp
// corprof.idl
interface ICorProfilerCallback10 : ICorProfilerCallback9 {
    HRESULT MyNewCallback([in] FunctionID functionId,
                          [in] ObjectID objectId);
};

// Implementation: src/coreclr/vm/proftoeetointerfaceimpl.cpp
void EEToProfInterfaceImpl::MyNewCallback(FunctionID funcId, ObjectID objId) {
    if (m_pCallback10) {
        m_pCallback10->MyNewCallback(funcId, objId);
    }
}
```

**Impact:** Medium - changes COM interface

**Versioning:** Must add to new interface version (ICorProfilerCallback11, etc.)

**Testing:** Profiler tests

---

## Platform Abstraction

### PAL Changes (🟡 Significant)

**Files:** `src/coreclr/pal/inc/pal.h`, `src/coreclr/pal/src/`

**Adding PAL function:**

```cpp
// pal.h
PALIMPORT
DWORD
PALAPI
PAL_MyNewFunction(
    IN LPCWSTR lpFilename,
    OUT LPVOID lpBuffer);

// Windows implementation: pal/src/file/myfunction.cpp
DWORD PAL_MyNewFunction(LPCWSTR lpFilename, LPVOID lpBuffer) {
    return GetFileAttributesW(lpFilename);  // Use Windows API
}

// Unix implementation: pal/src/file/myfunction.cpp
DWORD PAL_MyNewFunction(LPCWSTR lpFilename, LPVOID lpBuffer) {
    // Convert LPCWSTR to char*
    char path[MAX_PATH];
    WideCharToMultiByte(lpFilename, path, ...);
    // Use POSIX API
    struct stat st;
    if (stat(path, &st) == 0) {
        // Convert to Windows format
        return ConvertPosixToWin32Attributes(&st);
    }
}
```

**Impact:** Medium - must test on all platforms

**Testing:**
- Windows (x64, x86, ARM64)
- Linux (x64, ARM64)
- macOS (x64, ARM64)
- FreeBSD

---

### Architecture-Specific Code (🟡 Significant)

**Locations:**
- `src/coreclr/vm/amd64/` - x64 assembly stubs
- `src/coreclr/vm/arm64/` - ARM64 stubs
- `src/coreclr/jit/targetamd64.cpp` - x64 JIT backend
- `src/coreclr/jit/targetarm64.cpp` - ARM64 JIT backend

**Adding architecture support for existing feature:**

| Architecture | Effort | Availability |
|--------------|--------|--------------|
| **x64** | 1x | High (most tested) |
| **ARM64** | 1.2x | High (Apple Silicon, servers) |
| **ARM32** | 1.5x | Medium (embedded, mobile) |
| **x86** | 1.3x | Low (legacy) |
| **RISC-V** | 2x | Emerging |

**Pattern:**
```cpp
// src/coreclr/vm/callhelpers.cpp
void CallDescrWorker(...) {
#ifdef TARGET_AMD64
    CallDescrWorkerInternal_AMD64(...);
#elif defined(TARGET_ARM64)
    CallDescrWorkerInternal_ARM64(...);
#elif defined(TARGET_ARM)
    CallDescrWorkerInternal_ARM(...);
#else
    #error Unsupported architecture
#endif
}
```

---

## Build System

### Adding New Component (🟢 Localized)

**Steps:**

1. **Create directory:** `src/coreclr/mycomponent/`

2. **Add CMakeLists.txt:**
   ```cmake
   set(MYCOMPONENT_SOURCES
       myfile1.cpp
       myfile2.cpp
   )

   add_library(mycomponent STATIC ${MYCOMPONENT_SOURCES})
   ```

3. **Update parent CMakeLists.txt:**
   ```cmake
   add_subdirectory(mycomponent)
   ```

4. **Update Subsets.props (if new buildable subset):**
   ```xml
   <SubsetName Include="mycomponent">
     <Description>My new component</Description>
   </SubsetName>
   ```

**Impact:** Low - additive change

---

## Decision Matrix: Should I Make This Change?

### Questions to Ask:

1. **Does it change core data structures?** (Object, MethodTable, MethodDesc)
   - Yes → 🔴 High risk, needs RFC
   - No → Continue

2. **Does it change GC behavior?**
   - Yes → 🔴 High risk, needs GC expert review
   - No → Continue

3. **Does it change JIT output?**
   - Yes → 🟡 Medium risk, needs JIT team review
   - No → Continue

4. **Does it add new functionality?**
   - Yes → 🟢 Low risk, follow patterns
   - No → Continue

5. **Is it a bug fix?**
   - Yes → 🟢 Low risk, targeted fix
   - No → Continue

6. **Is it a configuration/diagnostic change?**
   - Yes → 🟢 Very low risk
   - No → Continue

### RFC (Request for Comments) Required:

- 🔴 Any critical path changes
- Changes affecting >20 files
- New major subsystems
- Breaking changes
- Performance-critical changes

---

## Summary: Impact Zone Quick Reference

| Area | Risk Level | Key Files | Test Scope |
|------|------------|-----------|------------|
| **Object header** | 🔴 Critical | object.h, syncblk.h | Full suite |
| **MethodTable** | 🔴 Critical | methodtable.h | Full suite |
| **MethodDesc** | 🟡 Significant | method.hpp | VM + JIT tests |
| **GC algorithm** | 🔴 Critical | gc/gc.cpp | Full GC suite + stress |
| **GC config** | 🟢 Localized | gc/gcconfig.cpp | Config tests |
| **JIT IR node** | 🟡 Significant | jit/gentree.h | JIT tests, SuperPMI |
| **JIT helper** | 🟢 Localized | jithelpers.h | Helper tests |
| **ETW events** | 🟢 Localized | ClrEtwAll.man | Event tests |
| **PAL function** | 🟡 Significant | pal/ | Cross-platform tests |
| **Thread state** | 🔴 Critical | threads.h | Full suite |
| **Build system** | 🟢 Localized | CMakeLists.txt | Build tests |

**Rule of Thumb:**
- 🔴 Critical → 4-12 weeks, multiple reviewers, extensive testing
- 🟡 Significant → 1-4 weeks, team review, targeted testing
- 🟢 Localized → Hours-days, peer review, basic testing
