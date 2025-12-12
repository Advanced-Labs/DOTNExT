# .NET Runtime Modularity Report

> Generated: 2025-12-05
> Purpose: Assess extension points for Engram integration

---

## Executive Summary

| Component | Modularity | Interface Quality | Can Replace? |
|-----------|------------|------------------|--------------|
| **GC** | EXCELLENT | IGCHeap (v5.3), IGCToCLR (v2) | YES - proven |
| **JIT** | GOOD | ICorJitCompiler | YES - proven |
| **Type System** | POOR | None | NO - fork required |
| **VES/Threading** | POOR | None | NO - fork required |
| **Profiler** | EXCELLENT | ICorProfilerCallback | YES - standard |
| **Hosting** | GOOD | hostfxr API | YES - standard |

**Recommendation:** GC extension is the most viable path for Engram integration.

---

## GC Modularity - EXCELLENT

### Key Interfaces

**IGCHeap** (`src/coreclr/gc/gcinterface.h`)
- ~100 methods
- Version: `GC_INTERFACE_MAJOR_VERSION = 5`, `MINOR = 3`
- Covers: allocation, collection, generations, profiling

**IGCToCLR** (`src/coreclr/gc/gcinterface.ee.h`)
- Callbacks from GC to VM
- Thread control, root scanning, diagnostics

### Standalone GC Build

The runtime already builds standalone GC DLLs:
```cmake
# From src/coreclr/gc/CMakeLists.txt
add_library_clr(clrgc SHARED ${GC_SOURCES})      # Segments
add_library_clr(clrgcexp SHARED ${GC_SOURCES})   # Regions
```

### Loading Custom GC

```bash
set DOTNET_GCName=path\to\custom\gc.dll
```

**Initialization function:**
```cpp
GC_EXPORT HRESULT GC_Initialize(
    IGCToCLR* clrToGC,           // VM callbacks
    IGCHeap** gcHeap,            // OUT: GC heap
    IGCHandleManager** gcHandles, // OUT: Handle manager
    GcDacVars* gcDacVars         // OUT: Debugging support
);
```

### Sample GC

Complete sample at `src/coreclr/gc/sample/GCSample.cpp` demonstrating standalone usage.

### Proven Modularity

- Workstation vs Server GC (two implementations)
- Segments vs Regions (two memory models)
- NativeAOT uses same GC with different binding
- Sample demonstrates standalone embedding

---

## JIT Modularity - GOOD

### Interface

**ICorJitCompiler** (`src/coreclr/inc/corjit.h`)
```cpp
virtual CorJitResult compileMethod(
    ICorJitInfo* comp,               // ~200 VM callbacks
    CORINFO_METHOD_INFO* info,       // Method to compile
    unsigned flags,                  // Compilation flags
    uint8_t** nativeEntry,           // OUT: Code
    uint32_t* nativeSizeOfCode       // OUT: Size
);
```

### Loading Custom JIT

```bash
set DOTNET_JitName=path\to\custom\jit.dll
set DOTNET_AltJit=*                # Use for all methods
```

### Proven by
- RyuJIT
- Multiple cross-compilers
- Historical: JIT32, JIT64, LLILC (LLVM-based)
- SuperPMI replay testing

---

## VMR Build Modularity

### Independent Component Builds

```bash
build.cmd -subset clr         # CoreCLR only
build.cmd -subset libs        # Libraries only
build.cmd -subset mono        # Mono only
build.cmd -subset clr.jit     # JIT only
build.cmd -subset clr.corelib # CoreLib only
```

Defined in `eng/Subsets.props` (lines 64-101).

---

## Engram Integration Options

### Option A: Custom GC (RECOMMENDED)

**Approach:** Implement IGCHeap with Engram-awareness

**Why:**
- Clean, versioned interface
- Proven extension point
- Sample code exists
- Dynamic loading via env var
- No VM fork required

**Engram-specific additions:**
- Object UUID tracking in allocation
- Relationship recording on reference writes
- Engram extraction via heap walking
- Persistence/recovery hooks

**Effort:** 6-12 months for production quality

### Option B: GC + Profiler Hybrid

Use Profiler for object tracking, custom GC for memory management.

**Trade-off:** More complexity, but leverages well-documented Profiler API.

### Option C: Fork VM (NOT RECOMMENDED)

Deep modification of type system. Massive maintenance burden, hard to track upstream.

---

## Key Files

| Purpose | Path |
|---------|------|
| GC interface | `src/coreclr/gc/gcinterface.h` |
| GC-VM callbacks | `src/coreclr/gc/gcinterface.ee.h` |
| GC loader | `src/coreclr/gc/gcload.cpp` |
| GC sample | `src/coreclr/gc/sample/GCSample.cpp` |
| JIT interface | `src/coreclr/inc/corjit.h` |
| JIT callbacks | `src/coreclr/inc/corinfo.h` |
| Build subsets | `eng/Subsets.props` |

---

## Conclusion

The GC's IGCHeap interface is the **sanctioned and proven extension point** for deep runtime integration. Implementing an Engram-aware GC is technically feasible and aligns with the runtime's architectural design.

The type system and VES are not modular - they would require forking. This should be avoided.

*Next step: Design Engram-aware GC that implements IGCHeap while leveraging CGCDesc for relationship tracking.*
