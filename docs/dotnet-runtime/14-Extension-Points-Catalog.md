# Extension Points Catalog

**Where the Runtime is Designed to be Extended**

This guide catalogs all the official and de facto extension points in the .NET Runtime where new functionality can be added with minimal disruption.

## Official Extension Interfaces

### 1. GC Interface (Alternative Garbage Collectors)

**Interface:** `IGCHeap`
**Location:** `src/coreclr/gc/gcinterface.h`
**Purpose:** Plug in alternative garbage collectors

**Contract:**
```cpp
// src/coreclr/gc/gcinterface.h
class IGCHeap {
    // Allocation
    virtual Object* Alloc(size_t size, DWORD flags) = 0;
    virtual Object* AllocLHeap(size_t size, DWORD flags) = 0;

    // Collection
    virtual void GarbageCollect(int generation, bool low_memory, int mode) = 0;

    // Object operations
    virtual size_t GetObjectSize(Object* obj) = 0;
    virtual bool IsPromoted(Object* obj) = 0;

    // Thread coordination
    virtual void SuspendEE(SUSPEND_REASON reason) = 0;
    virtual void RestartEE(bool bFinishedGC) = 0;

    // ~100 more methods...
};
```

**How to Use:**
1. Implement `IGCHeap` interface
2. Set `g_pGCHeap` to your implementation
3. Coordinate with VM via `GCToEEInterface` callbacks

**Examples:**
- Default GC: `src/coreclr/gc/gc.cpp`
- (Hypothetical) Conservative GC
- (Hypothetical) Real-time GC
- (Hypothetical) Concurrent-only GC

**Effort:** 6-12 months for full implementation

---

### 2. JIT Interface (Alternative JIT Compilers)

**Interface:** `ICorJitCompiler`
**Location:** `src/coreclr/inc/corjit.h`
**Purpose:** Plug in alternative JIT compilers

**Contract:**
```cpp
// src/coreclr/inc/corjit.h
class ICorJitCompiler {
    virtual CorJitResult compileMethod(
        ICorJitInfo* comp,              // Callbacks to VM
        CORINFO_METHOD_INFO* info,      // Method to compile
        unsigned flags,                 // Compilation flags
        BYTE** nativeEntry,             // [out] Code pointer
        ULONG* nativeSizeOfCode         // [out] Code size
    ) = 0;

    // Additional methods...
};
```

**How to Use:**
1. Implement `ICorJitCompiler` interface
2. Export `getJit()` function from DLL
3. Runtime loads via `DOTNET_JitName` environment variable

**Examples:**
- RyuJIT: `src/coreclr/jit/`
- Mono JIT: `src/mono/mini/`
- (Historical) JIT32, JIT64
- (Experimental) LLILC (LLVM-based)

**Callbacks Available (ICorJitInfo):**
```cpp
// ~200 callbacks available to JIT
class ICorJitInfo {
    // Type information
    virtual CORINFO_CLASS_HANDLE getMethodClass(...) = 0;
    virtual DWORD getClassAttribs(...) = 0;

    // Method information
    virtual const char* getMethodName(...) = 0;
    virtual void getMethodSig(...) = 0;

    // Code generation
    virtual void* allocMem(ULONG size) = 0;
    virtual void reserveUnwindInfo(...) = 0;

    // Helper calls
    virtual void* getHelperFtn(...) = 0;

    // And ~195 more...
};
```

**Effort:** 12-24 months for production-quality JIT

---

### 3. Profiler API (Performance Profilers, APM tools)

**Interface:** `ICorProfilerCallback*` (multiple versions)
**Location:** `src/coreclr/inc/corprof.idl`
**Purpose:** Observe and instrument runtime behavior

**Contract:**
```cpp
// Simplified (actual is COM IDL)
interface ICorProfilerCallback10 : ICorProfilerCallback9 {
    // Lifecycle
    HRESULT Initialize(IUnknown* pICorProfilerInfoUnk);
    HRESULT Shutdown();

    // Method events
    HRESULT JITCompilationStarted(FunctionID functionId, BOOL fIsSafeToBlock);
    HRESULT JITCompilationFinished(FunctionID functionId, HRESULT hrStatus, BOOL fIsSafeToBlock);
    HRESULT JITInlining(FunctionID callerId, FunctionID calleeId, BOOL* pfShouldInline);

    // GC events
    HRESULT GarbageCollectionStarted(...);
    HRESULT GarbageCollectionFinished();
    HRESULT ObjectAllocated(ObjectID objectId, ClassID classId);

    // Exception events
    HRESULT ExceptionThrown(ObjectID thrownObjectId);
    HRESULT ExceptionSearchFunctionEnter(FunctionID functionId);

    // Thread events
    HRESULT ThreadCreated(ThreadID threadId);
    HRESULT ThreadDestroyed(ThreadID threadId);

    // ~100 more callbacks...
};
```

**Capabilities:**
- **Observe:** All major runtime events
- **Modify IL:** Rewrite method IL before JIT
- **Insert instrumentation:** Add enter/leave probes
- **Stack walking:** Sample call stacks
- **Object tracking:** Track object allocations
- **Control:** Prevent inlining, force GC, etc.

**How to Use:**
1. Implement `ICorProfilerCallback*` COM interface
2. Register via environment variable or config
3. Runtime calls callbacks at event points

**Examples:**
- Application Performance Monitoring (APM) tools
- Memory profilers
- Coverage tools
- Tracing frameworks

**Effort:** 2-6 months for full-featured profiler

---

### 4. Hosting API (Embed .NET in Native Apps)

**Interface:** `hostfxr` + `nethost`
**Location:** `src/native/corehost/`
**Purpose:** Embed .NET runtime in native applications

**Contract:**
```c
// nethost.h - Find runtime
int get_hostfxr_path(
    char_t* buffer,
    size_t* buffer_size,
    const struct get_hostfxr_parameters* parameters
);

// hostfxr.h - Initialize runtime
int hostfxr_initialize_for_runtime_config(
    const char_t* runtime_config_path,
    const struct hostfxr_initialize_parameters* parameters,
    hostfxr_handle* host_context_handle
);

// Load assembly and call method
int hostfxr_get_runtime_delegate(
    const hostfxr_handle host_context_handle,
    enum hostfxr_delegate_type type,
    void** delegate
);
```

**How to Use:**
1. Find `hostfxr.dll` using `get_hostfxr_path()`
2. Load hostfxr
3. Initialize runtime
4. Get delegates to call managed code
5. Call managed methods from native

**Examples:**
- Game engines (Unity, Unreal)
- Native desktop apps with .NET plugins
- Browser engines (embedding .NET)

**Effort:** 1-2 weeks for basic integration

---

### 5. EventPipe (Custom Diagnostic Events)

**Interface:** EventPipe provider registration
**Location:** `src/native/eventpipe/`
**Purpose:** Add custom diagnostic events

**Contract:**
```cpp
// Register provider
EventPipeProvider* provider = EventPipe::CreateProvider(
    L"MyCompany-MyProduct-MyComponent",
    nullptr,  // Callback
    nullptr   // Context
);

// Define event
EventPipeEvent* event = provider->AddEvent(
    1,              // Event ID
    0x0,            // Keywords
    0,              // Event version
    EP_EVENT_LEVEL_INFORMATIONAL,
    true            // NeedStack
);

// Fire event
EventPipe::WriteEvent(
    event,
    payload,        // Event data
    payloadLength,
    activityId,
    relatedActivityId
);
```

**How to Use:**
1. Register EventPipe provider
2. Define events with schema
3. Fire events at interesting points
4. Tools (dotnet-trace) can collect

**Examples:**
- Custom component instrumentation
- Performance counters
- Diagnostic markers

**Effort:** Hours to days

---

## Semi-Official Extension Points

### 6. JIT Helpers (Runtime Support Functions)

**Pattern:** Add runtime helpers called by JIT-generated code
**Location:** `src/coreclr/inc/jithelpers.h`, `src/coreclr/vm/jithelpers.cpp`

**How to Add:**

1. **Declare helper:**
```cpp
// src/coreclr/inc/jithelpers.h
JITHELPER(CORINFO_HELP_MY_HELPER, JIT_MyHelper, CORINFO_HELP_SIG_REG_ONLY)
```

2. **Implement helper:**
```cpp
// src/coreclr/vm/jithelpers.cpp
HCIMPL2(INT32, JIT_MyHelper, Object* obj, INT32 value) {
    FCALL_CONTRACT;

    // Implementation
    // (Can call VM functions, allocate objects, etc.)

    return result;
}
HCIMPLEND
```

3. **Call from JIT:**
```cpp
// src/coreclr/jit/morph.cpp
GenTree* call = gtNewHelperCallNode(CORINFO_HELP_MY_HELPER, TYP_INT, obj, value);
```

**Use Cases:**
- Complex operations needing VM support
- Operations that may allocate/GC
- Platform-specific operations
- Operations needing exception handling

**Examples:**
- Array bounds checking
- Type casting
- Null checking
- Monitor enter/exit
- String operations

**Effort:** Days

---

### 7. VM Intrinsics (Compiler-Recognized Methods)

**Pattern:** Methods with special compiler treatment
**Location:** `src/coreclr/vm/ecalllist.h`, JIT recognition

**Two Types:**

**A. FCall Intrinsics (Native implementation):**

```cpp
// src/coreclr/vm/ecalllist.h
FCFuncStart(gMathFuncs)
    QCFuncElement("Sin", COMDouble::Sin)
    QCFuncElement("Cos", COMDouble::Cos)
FCFuncEnd()

// Implementation: src/coreclr/classlibnative/bcltype/double.cpp
QCALLTYPE void COMDouble::Sin(double* result, double value) {
    *result = sin(value);  // Or platform-specific
}
```

**B. JIT Intrinsics (JIT generates inline code):**

```cpp
// src/coreclr/jit/importer.cpp
if (isIntrinsic(methodHandle)) {
    switch (intrinsicId) {
        case CORINFO_INTRINSIC_Sin:
            // JIT generates inline sin instruction
            return gtNewOperNode(GT_INTRINSIC, TYP_DOUBLE, arg);
    }
}
```

**How to Add:**
1. Declare in `ecalllist.h` (FCall) or recognize in JIT
2. Implement native version (FCall) or inline codegen (JIT)
3. Managed side declares as `[MethodImpl(InternalCall)]`

**Use Cases:**
- Math functions
- Hardware intrinsics (SIMD)
- Atomic operations
- Unsafe operations (Unsafe.As<T>)

**Effort:** Weeks

---

### 8. Runtime Configuration (New Settings)

**Pattern:** Add environment variable configuration
**Location:** `src/coreclr/vm/eeconfig.h`, `eeconfig.cpp`

**How to Add:**

1. **Declare in EEConfig:**
```cpp
// src/coreclr/vm/eeconfig.h
class EEConfig {
    bool m_myFeatureEnabled;
    int m_myFeatureThreshold;

public:
    bool MyFeatureEnabled() { return m_myFeatureEnabled; }
    int MyFeatureThreshold() { return m_myFeatureThreshold; }
};
```

2. **Parse in constructor:**
```cpp
// src/coreclr/vm/eeconfig.cpp
EEConfig::EEConfig() {
    m_myFeatureEnabled = CLRConfig::GetConfigValue(
        CLRConfig::EXTERNAL_MyFeature_Enabled
    ) != 0;

    m_myFeatureThreshold = CLRConfig::GetConfigValue(
        CLRConfig::EXTERNAL_MyFeature_Threshold
    );
}
```

3. **Use anywhere:**
```cpp
if (g_pConfig->MyFeatureEnabled()) {
    // ...
}
```

4. **Users set:**
```bash
export DOTNET_MyFeature_Enabled=1
export DOTNET_MyFeature_Threshold=100
```

**Effort:** Hours

---

### 9. Diagnostic Commands (Hidden Env Vars)

**Pattern:** Environment variables that enable diagnostic features
**Location:** Throughout codebase, documented in `eeconfig.h`

**Examples:**
```cpp
// JIT diagnostics
DOTNET_JitDisasm=MethodName         // Disassemble method
DOTNET_JitDump=MethodName           // Dump IR
DOTNET_JitStress=1                  // Enable stress mode

// GC diagnostics
DOTNET_GCStress=3                   // GC stress
DOTNET_HeapVerify=1                 // Verify heap

// Type loader diagnostics
DOTNET_LogEnable=1                  // Enable logging
DOTNET_LogFacility=0x00000010       // Type loader
DOTNET_LogLevel=10                  // Verbose
```

**How to Add:**
Follow configuration pattern above, but:
- No external documentation (internal/diagnostic use)
- Can have complex behaviors
- May break things if misused

**Effort:** Hours

---

### 10. Hardware Intrinsics (CPU Instructions)

**Pattern:** Expose CPU instructions as managed APIs
**Location:** `src/coreclr/jit/hwintrinsic*.cpp`, `src/libraries/System.Runtime.Intrinsics/`

**How to Add:**

1. **Define in JIT:**
```cpp
// src/coreclr/jit/hwintrinsicxarch.cpp
case NI_AVX2_Add:
    ins = INS_vpaddd;
    simdSize = 32;
    break;
```

2. **Managed API:**
```csharp
// System.Runtime.Intrinsics
namespace System.Runtime.Intrinsics.X86 {
    public abstract class Avx2 {
        [Intrinsic]
        public static Vector256<int> Add(Vector256<int> left, Vector256<int> right);
    }
}
```

3. **JIT recognizes and generates code:**
```cpp
// JIT sees call to Avx2.Add
// Generates: vpaddd ymm0, ymm1, ymm2
```

**CPU Instruction Sets Supported:**
- **x86/x64:** SSE, SSE2, SSE3, SSSE3, SSE4.1, SSE4.2, AVX, AVX2, AVX-512, BMI1, BMI2, FMA, LZCNT, POPCNT
- **ARM64:** NEON, AES, CRC32, CRYPTO, DP, RDM

**Effort:** Weeks per instruction set

---

## Unofficial/Internal Extension Points

### 11. Specialized Allocators

**Pattern:** Custom memory allocation strategies
**Location:** Scattered throughout VM

**Examples:**
```cpp
// src/coreclr/vm/loaderallocator.hpp
class LoaderAllocator {
    // Allocate memory tied to assembly lifetime
    void* GetLowFrequencyHeap()->AllocMem(size);
    void* GetHighFrequencyHeap()->AllocMem(size);
    void* GetStubHeap()->AllocMem(size);
};
```

**Use Cases:**
- Assembly-lifetime allocations
- Executable code
- Read-only data
- Temporary buffers

**How to Add:**
Reuse existing patterns, or add new heap type.

**Effort:** Days to weeks

---

### 12. Managed/Native Transitions

**Pattern:** Hooks during P/Invoke or reverse P/Invoke
**Location:** `src/coreclr/vm/dllimport.cpp`, IL stub generation

**Extension Points:**
- Pre-call marshaling
- Post-call marshaling
- Exception translation
- Thread mode transitions

**How to Extend:**
Modify IL stub generation in `dllimport.cpp`

**Effort:** Weeks

---

### 13. Type System Extensions

**Pattern:** Special type treatment
**Location:** `src/coreclr/vm/class.cpp`, `methodtable.cpp`

**Examples:**
- `System.Span<T>` - Byref-like types (stack-only)
- `System.Runtime.CompilerServices.Unsafe` - Unsafe operations
- `System.Tuple<...>` - Auto-implemented equality

**How to Add:**
1. Add special case in type loader
2. Add JIT recognition if needed
3. Add runtime behavior

**Effort:** Months (requires deep understanding)

---

### 14. Virtual Dispatch Extensions

**Pattern:** Custom method dispatch mechanisms
**Location:** `src/coreclr/vm/virtualstubdispatch.cpp`

**Current Mechanisms:**
- VTable dispatch (virtual methods)
- Interface dispatch (polymorphic inline caches)
- Delegate dispatch (special-cased)

**How to Extend:**
Modify dispatch stub generation.

**Effort:** Months (highly complex)

---

## Extension Point Selection Guide

**"I want to add X, which extension point should I use?"**

### For Observability:

| Goal | Extension Point | Files |
|------|----------------|-------|
| Monitor runtime events | Profiler API | corprof.idl |
| Custom diagnostic events | EventPipe | eventpipe/ |
| ETW events | ClrEtwAll.man | ClrEtwAll.man |
| Performance counters | PerfCounters | perfcounters.cpp |

### For Performance:

| Goal | Extension Point | Files |
|------|----------------|-------|
| CPU-specific instructions | Hardware Intrinsics | hwintrinsic*.cpp |
| Inline operations | VM Intrinsics | ecalllist.h |
| Custom JIT optimization | JIT Interface | jit/ |
| Custom allocation | Specialized Allocators | loaderallocator.hpp |

### For Customization:

| Goal | Extension Point | Files |
|------|----------------|-------|
| Alternative GC | GC Interface | gcinterface.h |
| Alternative JIT | JIT Interface | corjit.h |
| Embed .NET | Hosting API | corehost/ |
| Configuration | Runtime Config | eeconfig.h |

### For Language Features:

| Goal | Extension Point | Files |
|------|----------------|-------|
| New operators | JIT Helpers | jithelpers.h |
| Type behaviors | Type System | class.cpp |
| Calling conventions | Dispatch | virtualstubdispatch.cpp |

---

## Anti-Patterns (Don't Extend Here)

### ❌ Don't: Modify Object Layout Without Justification

**Why:** Affects every object (billions in large apps)

**Alternative:** Indirection via MethodTable or side table

### ❌ Don't: Add Fields to Thread Object Lightly

**Why:** Thread count is small but Thread object is central

**Alternative:** Thread-local storage (TLS) separate from Thread object

### ❌ Don't: Change Metadata Format

**Why:** ECMA-335 standard, affects interop

**Alternative:** Use attributes for metadata extensions

### ❌ Don't: Modify JIT IR Without Considering All Phases

**Why:** Many JIT phases assume IR invariants

**Alternative:** Add new node type properly, update all phases

### ❌ Don't: Add Global State Without Synchronization

**Why:** Race conditions, crashes

**Alternative:** Use proper locking or thread-local storage

---

## Checklist: Adding an Extension

### Planning Phase:
- [ ] Identify appropriate extension point
- [ ] Review existing similar extensions
- [ ] Estimate effort (hours/days/weeks/months)
- [ ] Design interface contract
- [ ] Identify affected components

### Implementation Phase:
- [ ] Implement interface or pattern
- [ ] Add tests for new functionality
- [ ] Add configuration if needed
- [ ] Add diagnostics/events
- [ ] Update documentation

### Integration Phase:
- [ ] Test on all platforms
- [ ] Test on all architectures
- [ ] Stress testing if needed
- [ ] Performance validation
- [ ] API review if public

### Maintenance Phase:
- [ ] Monitor for issues
- [ ] Respond to feedback
- [ ] Maintain compatibility

---

## Summary: Best Extension Points by Effort

| Effort | Extension Point | Impact | Typical Use |
|--------|----------------|--------|-------------|
| **Hours** | EventPipe events | Low | Diagnostics |
| **Hours** | Runtime configuration | Low | Feature flags |
| **Days** | JIT helpers | Low-Med | New operations |
| **Weeks** | VM intrinsics | Medium | Performance |
| **Weeks** | Hardware intrinsics | Medium | CPU features |
| **Months** | Profiler | Medium | APM tools |
| **Months** | Type system extensions | High | Language features |
| **6-12mo** | Alternative GC | High | Research |
| **12-24mo** | Alternative JIT | High | Research/ports |

**Recommendation:** Start with small extensions (EventPipe, config) to learn the codebase, then progress to larger extensions.

**Most Accessible:**
- EventPipe events
- Runtime configuration
- JIT helpers

**Most Powerful:**
- Profiler API
- GC Interface
- JIT Interface

Choose based on your goals and available effort!
