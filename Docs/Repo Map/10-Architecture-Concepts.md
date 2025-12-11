# Architecture Concepts & Patterns

Key architectural patterns, design principles, and concepts used throughout the .NET Runtime.

## Design Principles

### 1. Performance is Paramount

The runtime is on the critical path for every .NET application. Performance considerations:

**JIT Compilation:**
- Tiered compilation balances startup (Tier 0) and throughput (Tier 1)
- Dynamic PGO provides profile-guided optimization without AOT
- OSR (On-Stack Replacement) optimizes long-running loops
- Inlining budget carefully tuned

**Garbage Collection:**
- Generational hypothesis: most objects die young
- Concurrent GC minimizes pause times
- Write barriers carefully optimized
- Different modes for workstation vs. server

**Memory Layout:**
- Cache-friendly data structures
- Hot/cold data separation
- Object header size minimized
- Vtables aligned for cache lines

**Zero-Overhead Abstractions:**
- Generics use code sharing where possible
- Struct types avoid heap allocation
- Span<T> provides safe zero-copy slicing
- Inlining removes abstraction cost

### 2. Cross-Platform by Design

**Platform Abstraction Layer (PAL):**
- Abstracts Windows vs. Unix differences
- Consistent API across platforms
- Platform-specific implementations isolated

**Architecture Independence:**
```
Platform-agnostic code (most of VM, JIT frontend)
        ↓
Architecture-specific (JIT backend, runtime stubs)
        ↓
src/coreclr/vm/{arch}/      # x64, ARM64, etc.
src/coreclr/jit/target*.cpp # Per-architecture codegen
```

**Build System:**
- CMake for native code (cross-platform)
- MSBuild for managed code
- Supports cross-compilation (e.g., Windows → Linux)

### 3. Modular Architecture

**Buildable Subsets:**
- CoreCLR can be built independently
- Mono can be built independently
- Libraries can be built independently
- Enables parallel development

**Clear Interfaces:**
- VM ↔ JIT interface (jitinterface.cpp)
- VM ↔ GC interface (gcinterface.h)
- Managed ↔ Native (System.Private.CoreLib)

**Component Isolation:**
```
Libraries depend on → CoreLib
CoreLib depends on → Runtime (VM)
VM depends on → JIT, GC, PAL
```

### 4. Backward Compatibility

**.NET is committed to compatibility:**
- API contracts must be maintained
- Breaking changes require extensive review
- Versioning strategy (major.minor.patch)
- AssemblyLoadContext for side-by-side loading

**Compatibility Techniques:**
- Reference assemblies (API surface)
- Implementation assemblies (hidden details)
- Runtime binding redirects
- Shims for deprecated APIs

### 5. Reliability and Correctness

**Testing:**
- 15+ test suites
- Stress testing (GCStress, JITStress)
- Cross-platform validation
- Performance regression testing

**Assertions:**
- Debug/Checked builds have extensive asserts
- Contracts (preconditions, postconditions)
- Handle validation

**Code Quality:**
- Static analysis (CodeAnalysis.globalconfig)
- Code reviews required
- Coding guidelines enforced

## Key Architectural Patterns

### Pattern 1: Two-Phase Compilation (Tiered Compilation)

**Problem:** JIT compilation adds startup latency, but optimization requires time.

**Solution:** Two tiers of compilation

```
Method first called
    ↓
Tier 0: Quick JIT
  - Minimal optimization
  - Fast compilation
  - Instrumented (collect profile data)
  - Methods compile in ~1-5ms
    ↓
Method called ~30 times
    ↓
Tier 1: Optimized JIT
  - Full optimizations
  - Uses profile data from Tier 0 (Dynamic PGO)
  - Methods compile in ~10-100ms
  - Better code quality
```

**Benefits:**
- Fast startup (Tier 0 is quick)
- Good steady-state performance (Tier 1)
- Profile-guided optimization without AOT

**Configuration:**
```bash
export DOTNET_TieredCompilation=1    # Enable (default)
export DOTNET_TieredPGO=1             # Enable Dynamic PGO
export DOTNET_TC_CallCounting=1      # Count calls to trigger Tier 1
```

**Code Locations:**
- VM coordination: `src/coreclr/vm/tieredcompilation.cpp`
- JIT support: `src/coreclr/jit/fgprofile.cpp`

### Pattern 2: Handle Tables for Managed References

**Problem:** GC moves objects, so native code can't hold direct pointers.

**Solution:** Handle tables with indirection

```
Native Code
    ↓
Handle (stable address)
    ↓
Pointer to managed object (updated by GC)
    ↓
Managed Object (moves during GC)
```

**Handle Types:**
- **Strong Handle** - Keeps object alive
- **Weak Handle** - Doesn't prevent collection
- **Pinned Handle** - Prevents object from moving
- **Dependent Handle** - For ConditionalWeakTable
- **Async Pinned Handle** - For async I/O

**Code:** `src/coreclr/gc/gchandletable.cpp`, `handletable.cpp`

### Pattern 3: Virtual Stub Dispatch for Interface Calls

**Problem:** Interface calls require dynamic dispatch, but must be fast.

**Solution:** Polymorphic inline caches

```
First call: Interface method on type T1
    ↓
Create stub: if (type == T1) goto T1.Method else lookup
    ↓
Subsequent calls to T1: Fast path (direct jump)
    ↓
Call on type T2: Stub misses
    ↓
Expand stub: polymorphic (handles T1, T2, ...)
    ↓
Many types: Fall back to hash table
```

**Benefits:**
- Monomorphic calls: 1 comparison + 1 jump
- Polymorphic calls: N comparisons + 1 jump
- Better than full interface table lookup

**Code:** `src/coreclr/vm/virtualstubdispatch.cpp`

### Pattern 4: Lazy Type Loading

**Problem:** Loading all types at startup is slow.

**Solution:** Load types on-demand

```
Reference to type T
    ↓
Check if MethodTable exists
    ↓ (No)
Load metadata for T
    ↓
Create EEClass (metadata representation)
    ↓
Create MethodTable (runtime representation)
    ↓
Resolve base type (recursive)
    ↓
Resolve interfaces
    ↓
Build vtable
    ↓
Cache MethodTable for future use
```

**Benefits:**
- Only load types actually used
- Spreads startup cost over execution
- Reduces memory usage

**Code:** `src/coreclr/vm/clsload.cpp`, `typehandle.cpp`

### Pattern 5: Generic Code Sharing

**Problem:** Generics can cause code explosion (one copy per type).

**Solution:** Share code where possible

**Sharing Strategy:**
```
Reference types (class):
  - List<string>, List<object>, List<MyClass> → SHARE code
  - All references are same size (pointer)
  - Use generic dictionary for type-specific operations

Value types (struct):
  - List<int>, List<long>, List<MyStruct> → SEPARATE code
  - Different sizes, different layouts
  - Each instantiation gets its own code
```

**Generic Dictionary:**
```
List<T> needs to know:
  - Size of T
  - How to construct T
  - How to compare T
  - GC layout of T

Dictionary provides this at runtime
```

**Code:** `src/coreclr/vm/generics.cpp`, `genericdict.cpp`

### Pattern 6: IL Stubs for Marshaling

**Problem:** P/Invoke needs flexible marshaling, but must be fast.

**Solution:** Generate IL stubs at runtime

```
Declare: [LibraryImport("foo.dll")] void Bar(string s);
    ↓
First call:
  1. Generate IL stub
     - Marshal string → LPCWSTR
     - Call native function
     - Marshal return value
     - Handle exceptions
  2. JIT the stub
  3. Cache for future calls
    ↓
Subsequent calls: Direct call to cached stub
```

**Benefits:**
- Flexible marshaling logic
- JIT optimizes the stub
- No interpreter overhead

**Code:** `src/coreclr/vm/dllimport.cpp`, `ilstubcache.cpp`

### Pattern 7: Write Barriers for Concurrent GC

**Problem:** Concurrent GC runs while mutator (application) modifies objects.

**Solution:** Write barriers track modifications

```csharp
obj1.field = obj2;  // Simple assignment
    ↓
Compiler generates:
    obj1.field = obj2;
    if (obj1 in older generation && obj2 in younger generation)
        MarkCardTable(obj1);  // Record cross-generational pointer
```

**Why:**
- GC needs to know about old → young pointers
- Card table is a bitmap marking modified regions
- GC scans card table to find roots in old generation

**Code:** `src/coreclr/gc/gc.cpp`, JIT emits barriers in codegen

### Pattern 8: Prestubs for Lazy JIT

**Problem:** Don't want to JIT every method at load time.

**Solution:** Prestubs provide indirection

```
Method first called
    ↓
Call goes to prestub (tiny stub)
    ↓
Prestub calls JIT compiler
    ↓
JIT compiles method
    ↓
Prestub replaced with pointer to compiled code
    ↓
Future calls: Direct call to compiled code (prestub bypassed)
```

**Benefits:**
- Only compile methods actually called
- Transparent to caller
- Supports tiered compilation

**Code:** `src/coreclr/vm/prestub.cpp`, `precode.cpp`

## Cross-Cutting Concerns

### Error Handling

**Managed Exceptions:**
- VM handles exception dispatch
- Stack unwinding with finally blocks
- Cross managed/native boundaries

**Native Exceptions:**
- SEH on Windows
- Signal handlers on Unix
- PAL abstracts differences

**Code:** `src/coreclr/vm/excep.cpp`, `exceptionhandling.cpp`

### Threading Model

**Managed Threads:**
- Map to OS threads (1:1)
- Thread-local storage (TLS) for per-thread state
- Synchronization primitives (Monitor, lock, etc.)

**Thread Pool:**
- Work-stealing queues
- Hill climbing algorithm for thread count
- Async I/O completion

**Code:** `src/coreclr/vm/threads.cpp`, `threadpool.cpp`

### Memory Management

**Stack:**
- Local variables
- Method arguments
- Return addresses
- Managed and native frames interleaved

**GC Heap:**
- Managed objects
- Generational (Gen 0, 1, 2, LOH)
- Compacting (moves objects)

**Unmanaged Heap:**
- P/Invoke buffers
- COM objects
- Native allocations

**Code Heap:**
- JIT-compiled code
- IL stubs
- Managed separately from GC heap

### Synchronization

**Monitor (lock statement):**
- Thin locks (sync block index in object header)
- Fat locks (full synchronization object)
- Automatic upgrade from thin to fat

**Reader-Writer Locks:**
- Optimized for read-heavy scenarios

**Interlocked Operations:**
- Atomic compare-and-swap
- Atomic increment/decrement

**Code:** `src/coreclr/vm/syncblk.cpp`, `threads.cpp`

## Platform-Specific Patterns

### Windows vs. Unix Abstraction

**Pattern:** PAL (Platform Abstraction Layer)

```cpp
// PAL function (works everywhere)
HANDLE PAL_CreateThread(...);

// Implementation (Windows)
HANDLE PAL_CreateThread(...) {
    return CreateThread(...);  // Windows API
}

// Implementation (Unix)
HANDLE PAL_CreateThread(...) {
    pthread_create(...);       // POSIX API
    return (HANDLE)thread_id;
}
```

**Key Areas:**
- File I/O: `PAL_fopen`, `PAL_ReadFile`
- Threading: `PAL_CreateThread`, `PAL_WaitForSingleObject`
- Memory: `PAL_VirtualAlloc`, `PAL_VirtualFree`
- Module loading: `PAL_LoadLibrary`, `PAL_GetProcAddress`
- Exceptions: `PAL_TryExcept`, `PAL_RaiseException`

**Code:** `src/coreclr/pal/`

### Architecture-Specific Code

**Pattern:** Subdirectories by architecture

```
src/coreclr/vm/
  amd64/         # x64-specific
  arm/           # ARM32-specific
  arm64/         # ARM64-specific
  i386/          # x86 32-bit
  loongarch64/   # LoongArch
  riscv64/       # RISC-V
```

**What's architecture-specific:**
- Calling conventions
- Register usage
- Assembly stubs (method entry, exception handling)
- Stack unwinding
- JIT backend (code generation)

**What's architecture-agnostic:**
- Type system
- GC algorithm (mostly)
- Most VM logic
- JIT frontend (optimization)

## Performance Patterns

### Hot/Cold Data Separation

**Pattern:** Separate frequently accessed data from rarely accessed data.

```cpp
class MethodTable {
    // HOT: Accessed on every method call
    DWORD m_dwFlags;
    WORD m_wNumInterfaces;
    MethodTable* m_pParentMethodTable;

    // COLD: Accessed rarely (in EEClass)
    // - Debug information
    // - Metadata tokens
    // - Field layout (static fields)
};
```

**Benefits:**
- Better cache utilization
- Smaller working set

### Inline Caching

**Pattern:** Cache lookup results inline with call site.

```
First call: obj.InterfaceMethod()
    ↓
Cache miss → Full lookup → Cache result
    ↓
Subsequent calls: Check cache, use if match
```

**Applied to:**
- Virtual calls
- Interface calls
- Generic dictionary lookups

### Generational Hypothesis

**Pattern:** Most objects die young.

```
Allocate in Gen 0
    ↓
Gen 0 collection (frequent, fast)
    ↓
Survivors → Gen 1
    ↓
Gen 1 collection (less frequent)
    ↓
Survivors → Gen 2
    ↓
Gen 2 collection (rare, slower)
```

**Benefits:**
- Most collections are fast (Gen 0 only)
- Long-lived objects rarely scanned
- Compaction keeps Gen 0 compact

## Debugging Patterns

### DAC (Data Access Component)

**Pattern:** Separate library for reading CLR data structures from dumps.

```
Debugger Process
    ↓
Load mscordaccore.dll
    ↓
DAC reads memory from target process/dump
    ↓
Reconstructs CLR data structures
    ↓
Exposes IXCLRData* COM interfaces
    ↓
Debugger (WinDbg, LLDB, etc.) uses interfaces
```

**Benefits:**
- Debug live process or dump
- No runtime impact (separate process)
- Consistent debugging experience

**Code:** `src/coreclr/debug/daccess/`

### Stress Modes

**Pattern:** Intentionally stress the system to find bugs.

**GCStress:**
```bash
export DOTNET_GCStress=3  # GC before every allocation
```
- Finds GC bugs
- Finds handle leaks
- Finds missing GC reporting

**JITStress:**
```bash
export DOTNET_JitStress=1  # Random JIT variations
```
- Finds JIT bugs
- Tests rare code paths
- Finds optimizer bugs

## Extension Points

### Profiler API

**Pattern:** COM-based callback API for profilers.

```
Profiler DLL
    ↓
Implements ICorProfilerCallback
    ↓
Runtime calls profiler on events:
  - Method JIT'd
  - Object allocated
  - GC started/finished
  - Thread created
  - Exception thrown
    ↓
Profiler can request stack walks, modify IL, etc.
```

**Code:** `src/coreclr/vm/proftoeetointerfaceimpl.cpp`, `inc/corprof.idl`

### EventPipe

**Pattern:** Event streaming for diagnostics.

```
Application
    ↓
Runtime fires events (GC, JIT, exceptions, custom)
    ↓
EventPipe buffers events
    ↓
Tools (dotnet-trace) read events via IPC
    ↓
Analysis (PerfView, SpeedScope, etc.)
```

**Benefits:**
- Cross-platform (vs. ETW on Windows only)
- Low overhead
- Rich event schema

**Code:** `src/native/eventpipe/`

## Summary

The .NET Runtime architecture is built on:

1. **Performance** - Every design decision considers performance
2. **Cross-platform** - PAL + architecture abstraction
3. **Modularity** - Clear component boundaries
4. **Compatibility** - Versioning and API contracts
5. **Reliability** - Extensive testing and validation

Key patterns:
- **Tiered compilation** - Fast startup + optimized steady-state
- **Handle tables** - Indirection for GC-movable objects
- **Virtual stub dispatch** - Fast polymorphic calls
- **Lazy loading** - Load types/methods on-demand
- **Code sharing** - Share generic code where possible
- **IL stubs** - Generate marshaling code dynamically
- **Write barriers** - Enable concurrent GC
- **Prestubs** - Enable lazy JIT

These patterns and principles guide development across the entire codebase.

---

For specific implementation details, see the component-specific guides (02-07) and the Book of the Runtime in `docs/design/coreclr/botr/`.
