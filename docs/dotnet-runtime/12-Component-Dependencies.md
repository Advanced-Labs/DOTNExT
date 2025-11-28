# Component Dependencies & Interaction Matrix

**Understanding How Runtime Components Interconnect**

This guide maps the dependency relationships and interaction patterns between major runtime components.

## Dependency Hierarchy

```
┌────────────────────────────────────────────────────┐
│  Application Code (.NET IL)                        │
└──────────────────┬─────────────────────────────────┘
                   │
┌──────────────────▼─────────────────────────────────┐
│  Libraries (System.*, Microsoft.Extensions.*)      │
│  Location: src/libraries/                          │
└──────────────────┬─────────────────────────────────┘
                   │
┌──────────────────▼─────────────────────────────────┐
│  System.Private.CoreLib                            │
│  Location: src/coreclr/System.Private.CoreLib/     │
│  Special: Built with runtime, no dependencies      │
└──────────────────┬─────────────────────────────────┘
                   │
         ┌─────────┴──────────┬─────────────────┐
         │                    │                 │
┌────────▼────────┐  ┌───────▼────────┐  ┌────▼─────────┐
│   Virtual       │  │   JIT          │  │   Metadata   │
│   Machine (VM)  │  │   Compiler     │  │   System     │
│                 │  │                │  │              │
│ Location:       │  │ Location:      │  │ Location:    │
│ src/coreclr/vm/ │  │ src/coreclr/   │  │ src/coreclr/ │
│                 │  │ jit/           │  │ md/          │
└────────┬────────┘  └───────┬────────┘  └────┬─────────┘
         │                   │                 │
         └─────────┬─────────┴─────────────────┘
                   │
         ┌─────────┴──────────┬─────────────────┐
         │                    │                 │
┌────────▼────────┐  ┌───────▼────────┐  ┌────▼─────────┐
│   Garbage       │  │   Platform     │  │   Diagnostics│
│   Collector     │  │   Abstraction  │  │   (EventPipe)│
│                 │  │   (PAL)        │  │              │
│ Location:       │  │                │  │ Location:    │
│ src/coreclr/gc/ │  │ Location:      │  │ src/native/  │
│                 │  │ src/coreclr/   │  │ eventpipe/   │
│                 │  │ pal/           │  │              │
└─────────────────┘  └───────┬────────┘  └──────────────┘
                             │
                   ┌─────────▼──────────┐
                   │   Operating System │
                   │   (Windows/Linux/  │
                   │    macOS/etc.)     │
                   └────────────────────┘
```

## Component Interaction Matrix

### VM ↔ JIT Interface

**Direction: Bidirectional**

**VM → JIT (VM calls JIT):**

| File | Interface | Purpose |
|------|-----------|---------|
| `src/coreclr/vm/jitinterface.cpp` | `compileMethod()` | Request method compilation |
| `src/coreclr/vm/jitinterface.cpp` | `getMethodInfo()` | Provide method metadata to JIT |
| `src/coreclr/vm/jitinterface.cpp` | `resolveToken()` | Resolve metadata tokens |

**JIT → VM (JIT calls back to VM):**

| File | Interface | Purpose |
|------|-----------|---------|
| `src/coreclr/inc/corinfo.h` | `ICorJitInfo` interface | ~200 callback methods |
| `src/coreclr/jit/compiler.cpp` | `info.compCompHnd->getHelperFtn()` | Get runtime helper addresses |
| `src/coreclr/jit/compiler.cpp` | `info.compCompHnd->embedGenericHandle()` | Embed type/method handles |

**Example Interaction:**

```cpp
// VM wants to compile a method
// src/coreclr/vm/jitinterface.cpp
CORINFO_METHOD_HANDLE methodHandle = ...;
ICorJitCompiler* jit = getJit();
CorJitResult result = jit->compileMethod(
    this,           // ICorJitInfo* callbacks
    &methodInfo,    // Method metadata
    flags,          // Compilation flags
    &nativeCode     // [out] Compiled code
);

// Inside JIT, need to allocate code:
// src/coreclr/jit/compiler.cpp
void* codeAddr = info.compCompHnd->allocMem(codeSize, hotCodeSize, ...);

// Need type info:
CORINFO_CLASS_HANDLE clsHandle = info.compCompHnd->getMethodClass(methodHandle);
```

**What This Means for New Systems:**
- Adding JIT features requires extending `ICorJitInfo` interface
- Adding VM features that JIT needs requires callback additions
- Changes often require version synchronization

### VM ↔ GC Interface

**Direction: Bidirectional**

**VM → GC (VM calls GC):**

| File | Interface | Method |
|------|-----------|--------|
| `src/coreclr/vm/gcheaputilities.cpp` | `IGCHeap::Alloc()` | Allocate object |
| `src/coreclr/vm/gcheaputilities.cpp` | `IGCHeap::GarbageCollect()` | Trigger collection |
| `src/coreclr/vm/gcheaputilities.cpp` | `IGCHeap::SuspendEE()` | Suspend for GC |

**GC → VM (GC calls back to VM):**

| File | Interface | Purpose |
|------|-----------|---------|
| `src/coreclr/gc/gcenv.ee.h` | `GCToEEInterface` | ~50 callbacks |
| `src/coreclr/vm/gcenv.ee.cpp` | `SuspendEE()` | Stop threads |
| `src/coreclr/vm/gcenv.ee.cpp` | `GcEnumAllocContexts()` | Find thread allocation contexts |

**Example Interaction:**

```cpp
// Application allocates object
// src/coreclr/vm/gcheaputilities.h
Object* obj = GCHeapUtilities::GetGCHeap()->Alloc(size, flags);

// GC needs to suspend threads for collection
// src/coreclr/gc/gc.cpp (inside GC)
int32_t GCHeap::GarbageCollectGeneration(...) {
    GCToEEInterface::SuspendEE(SUSPEND_FOR_GC);  // Call back to VM

    // Do collection...

    GCToEEInterface::RestartEE();  // Resume
}
```

**What This Means for New Systems:**
- Alternative GC must implement `IGCHeap` (large interface, ~100 methods)
- Systems needing global coordination can piggyback on GC suspension
- Object layout changes affect both GC and VM

### VM ↔ Metadata Interface

**Direction: VM reads Metadata**

**Files:**
| VM Side | Metadata Side | Purpose |
|---------|---------------|---------|
| `src/coreclr/vm/class.cpp` | `src/coreclr/md/runtime/mdinternalro.cpp` | Load type definitions |
| `src/coreclr/vm/method.cpp` | `src/coreclr/md/runtime/mdinternalro.cpp` | Load method signatures |
| `src/coreclr/vm/assembly.cpp` | `src/coreclr/md/runtime/mdinternalro.cpp` | Load assembly metadata |

**Example Interaction:**

```cpp
// VM loading a type
// src/coreclr/vm/class.cpp
IMDInternalImport* pMDImport = GetModule()->GetMDImport();
mdTypeDef tkTypeDef = ...;

// Read type name
LPCUTF8 szName, szNamespace;
pMDImport->GetNameOfTypeDef(tkTypeDef, &szName, &szNamespace);

// Read base type
mdToken tkExtends;
pMDImport->GetTypeDefProps(tkTypeDef, &flags, &tkExtends);

// Enumerate methods
HENUMInternal hEnum;
pMDImport->EnumInit(mdtMethodDef, tkTypeDef, &hEnum);
while (pMDImport->EnumNext(&hEnum, &tkMethod)) {
    // Load method...
}
```

**What This Means for New Systems:**
- Metadata format is stable (ECMA-335 standard)
- Adding new metadata requires format changes (rare, major)
- Most extensions use attributes instead

### JIT ↔ GC Interface

**Direction: JIT generates GC-aware code**

**Key Interactions:**

| JIT File | What It Generates | GC Dependency |
|----------|-------------------|---------------|
| `src/coreclr/jit/codegencommon.cpp` | Write barriers | GC needs to track stores |
| `src/coreclr/jit/gcencode.cpp` | GC info | GC needs to find references during stack walk |
| `src/coreclr/jit/gcinfo.cpp` | Live pointer tracking | GC needs to know what's live |

**Example: Write Barrier:**

```cpp
// JIT generates (conceptual):
// src/coreclr/jit/codegenxarch.cpp

// C# code: obj.field = value;
//
// Generated assembly:
mov [obj+offset], value         ; Store the reference

; Write barrier (if obj is old generation, value is young):
cmp obj, [g_ephemeral_low]      ; Check if obj is in old generation
jb  NoBarrier
mov rcx, obj
call JIT_WriteBarrier           ; Inform GC of cross-generational pointer
NoBarrier:
```

**GC Info Encoding:**

```cpp
// src/coreclr/jit/gcencode.cpp
void GCInfo::gcMakeRegPtrTable(...) {
    // For each instruction:
    // - Which registers contain object references?
    // - Which stack slots contain object references?
    // - Encoded as compressed bit vectors

    // GC reads this during stack walk to find all live objects
}
```

**What This Means for New Systems:**
- GC changes may require JIT changes (e.g., different write barrier)
- JIT optimizations must preserve GC correctness
- New pointer types require GC info updates

### Thread ↔ All Systems

**Threads are the coordination mechanism.**

**Thread State Affects:**

| System | Dependency | Why |
|--------|------------|-----|
| **GC** | Cooperative vs Preemptive mode | Can't GC while thread holds objects |
| **JIT** | Safe points | Can only suspend at safe points |
| **Profiler** | Stack walking | Need consistent stack |
| **Debugger** | Suspension | Need to stop all threads |

**Thread States:**

```cpp
// src/coreclr/vm/threads.h
class Thread {
    // GC mode
    bool m_fPreemptiveGCDisabled;  // True = cooperative (can't GC)
                                   // False = preemptive (can GC)

    // Thread state
    ThreadState m_State;           // Running, Suspended, Stopped, etc.

    // Frame chain (for stack walking)
    Frame* m_pFrame;
};
```

**Example: GC Mode Transition:**

```cpp
// Managed → Native (enable GC)
// src/coreclr/vm/threads.h
void Thread::EnablePreemptiveGC() {
    m_fPreemptiveGCDisabled = FALSE;
    // GC can now run on this thread
}

// Native → Managed (disable GC)
void Thread::DisablePreemptiveGC() {
    m_fPreemptiveGCDisabled = TRUE;
    // Check if GC is pending
    if (g_TrapReturningThreads) {
        RareDisablePreemptiveGC();  // May trigger GC
    }
}
```

**What This Means for New Systems:**
- Systems needing global state must coordinate via thread suspension
- Per-thread state can piggyback on Thread object
- Must respect GC-safe points

## Cross-Cutting Concerns

### 1. Exception Handling

**Touches:**
- VM: `src/coreclr/vm/excep.cpp` - Exception dispatch
- JIT: `src/coreclr/jit/fgeh.cpp` - Exception handling region codegen
- PAL: `src/coreclr/pal/src/exception/` - SEH/signal handling
- Metadata: EH clauses in method headers
- GC: Stack unwinding during exception

**Data Flow:**

```
Exception thrown
    ↓
VM::RaiseTheExceptionInternalOnly() (src/coreclr/vm/excep.cpp)
    ↓
Stack unwinding (needs GC info from JIT)
    ↓
Find exception handler (from metadata)
    ↓
Execute finally blocks (JIT-generated code)
    ↓
Transfer control to catch handler
```

**What This Means for New Systems:**
- Exception handling is deeply integrated
- Adding new exception types requires VM changes
- Cleanup code must work with stack unwinding

### 2. Diagnostics (EventPipe/ETW)

**Touches:**
- EventPipe: `src/native/eventpipe/` - Event infrastructure
- VM: `src/coreclr/vm/eventtrace.cpp` - Event generation
- JIT: `src/coreclr/jit/` - JIT events
- GC: `src/coreclr/gc/` - GC events
- Profiler API: `src/coreclr/vm/proftoeetointerfaceimpl.cpp` - Callbacks

**Integration Pattern:**

```cpp
// Any component can fire events:
// src/coreclr/vm/method.cpp
void MethodDesc::JitCompiled() {
    // Fire ETW event
    ETW::MethodLog::MethodJitted(this);

    // Fire EventPipe event
    FireEtwMethodJitted(...);

    // Notify profiler
    if (CORProfilerPresent()) {
        g_profControlBlock.pProfInterface->JITCompilationFinished(this, ...);
    }
}
```

**What This Means for New Systems:**
- Easy to add events (add to manifest + call `FireEtw...()`)
- Events are opt-in (no overhead when not tracing)
- Multiple diagnostic systems (EventPipe, ETW, Profiler) - add to all

### 3. Configuration

**Sources:**
- Environment variables: `DOTNET_*`
- Config files: `runtimeconfig.json`
- Code: `AppContext.SetSwitch()`
- MSBuild properties

**Central Config:**

```cpp
// src/coreclr/vm/eeconfig.h
class EEConfig {
    // GC config
    int GetGCHeapCount();
    bool GetGCServer();

    // JIT config
    bool JitDisasm();
    bool JitStress();

    // VM config
    bool UseSpeculativeExec();
    // ...hundreds more
};

// Singleton
EEConfig* g_pConfig;
```

**Pattern:**

```cpp
// Add config value:
// 1. Add to EEConfig
bool m_myFeatureEnabled;

// 2. Read in EEConfig::GetConfigValue
m_myFeatureEnabled = CLRConfig::GetConfigValue(CLRConfig::EXTERNAL_MyFeature);

// 3. Use anywhere
if (g_pConfig->MyFeatureEnabled()) {
    // ...
}
```

**What This Means for New Systems:**
- Centralized config reading
- Environment variables are the primary mechanism
- Config is read once at startup (mostly)

### 4. Security

**Legacy CAS (Code Access Security) - Mostly removed**

**Modern Security:**
- **Type safety** - Verification (mostly obsolete with CoreCLR)
- **Memory safety** - GC prevents use-after-free
- **Sandboxing** - OS-level (no runtime sandboxing)

**Security-Relevant:**
| File | What It Does |
|------|--------------|
| `src/coreclr/vm/securitytransparent.cpp` | SecurityCritical attributes |
| `src/coreclr/vm/securitydescriptor.cpp` | Assembly trust (historical) |

**What This Means for New Systems:**
- No built-in sandboxing to work around
- Type/memory safety enforced by VM/GC
- Security is mostly about correct API design

### 5. Versioning

**Assembly Versioning:**
- Strong names: `src/coreclr/vm/assembly.cpp`
- Version binding: `src/coreclr/binder/`
- Type forwarding: Compiler-generated

**API Versioning:**
- Reference assemblies define contract
- Implementation can change
- Breaking changes require major version bump

**What This Means for New Systems:**
- Internal changes OK (not visible to apps)
- Public API changes need careful review
- Use `[EditorBrowsable]` for work-in-progress

## Dependency Impact Analysis

**"If I modify X, what else needs to change?"**

### Scenario 1: Change Object Header Layout

**File Changed:** `src/coreclr/vm/object.h`

**Direct Impact:**
- ✅ **GC** (`src/coreclr/gc/`) - Reads object header
- ✅ **VM** (`src/coreclr/vm/`) - All object operations
- ✅ **JIT** (`src/coreclr/jit/`) - May embed offsets
- ✅ **Debugger** (DAC) - Reads objects from dumps

**Build Impact:**
- Full rebuild required
- ~30 minutes

**Testing Required:**
- All GC tests
- All VM tests
- DAC tests

### Scenario 2: Add New JIT Optimization

**File Changed:** `src/coreclr/jit/optimizer.cpp`

**Direct Impact:**
- ✅ **JIT** only (usually)
- ⚠️ **VM** if optimization requires new helper calls

**Build Impact:**
- Rebuild JIT only (~2 minutes)
- Can use `./build.sh -subset clr.jit`

**Testing Required:**
- JIT tests
- SuperPMI (JIT regression tests)

### Scenario 3: Add New EventPipe Event

**Files Changed:**
- `src/coreclr/vm/ClrEtwAll.man`
- Call site (e.g., `src/coreclr/vm/method.cpp`)

**Direct Impact:**
- ⚠️ Minimal (just adding event)
- Manifest changes regenerate event headers

**Build Impact:**
- Rebuild VM (~10 minutes)

**Testing Required:**
- Ensure event fires
- Check EventPipe tests

### Scenario 4: Change MethodTable Layout

**File Changed:** `src/coreclr/vm/methodtable.h`

**Direct Impact:**
- ✅ **VM** - Everything that uses types
- ✅ **GC** - Reads MethodTable for object size/layout
- ✅ **JIT** - May embed MethodTable offsets
- ✅ **Debugger** (DAC) - Reads MethodTable from dumps
- ✅ **Profiler** - May read MethodTable

**Build Impact:**
- Full runtime rebuild (~20 minutes)

**Testing Required:**
- Extensive - this is core data structure
- All test suites

## Quantifying Integration Complexity

**For any major subsystem, count:**

### Integration Points

| Integration Type | Typical Count | Effort |
|------------------|---------------|--------|
| **Interfaces to implement** | 1-3 large interfaces | High (weeks) |
| **VM files to modify** | 10-20 files | Medium (days) |
| **JIT integration** | 4-6 files per arch | Medium (days) |
| **Configuration** | 2-3 files | Low (hours) |
| **Diagnostics** | 3-5 files | Low (hours) |
| **Build system** | 2-4 files | Low (hours) |
| **Tests** | New test suite | Medium (days) |

### Data Structure Additions

| Addition Type | Space Cost | Effort |
|---------------|------------|--------|
| **Per-object field** | 8 bytes × billion objects = 8GB! | High (consider carefully) |
| **Per-type field** | 8 bytes × 10K types = 80KB | Low (usually OK) |
| **Per-method field** | 8 bytes × 100K methods = 800KB | Medium |
| **Global state** | Fixed size | Low |

### Code Generation Hooks

| Hook Type | Files to Modify | Architectures |
|-----------|-----------------|---------------|
| **New IR node** | 5-10 files | All (x64, ARM64, ARM, x86) |
| **New helper call** | 3-5 files | All |
| **New optimization** | 2-5 files | Arch-independent |

## Conclusion

**Integration well-defined: ~80%**
- Major interfaces explicit (`IGCHeap`, `ICorJitInfo`, etc.)
- Hook points documented
- Patterns established

**Integration requires discovery: ~20%**
- Bit layouts
- Performance implications
- Edge case interactions
- Memory ordering assumptions

**For a major subsystem (GC-scale):**
- **Files to create:** ~10-20 new files
- **Files to modify:** ~30-50 existing files
- **Interfaces to implement:** 1-3 large interfaces
- **Effort:** 3-6 months for experienced team

The dependency structure is relatively clean, but deep integration requires understanding multiple layers of the stack.
