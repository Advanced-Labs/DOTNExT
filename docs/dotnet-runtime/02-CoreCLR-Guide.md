# CoreCLR Runtime Guide

Deep dive into the CoreCLR runtime - the primary .NET execution engine.

## Overview

**CoreCLR** is the high-performance, production-ready .NET runtime that evolved from the .NET Framework CLR. It includes:
- **RyuJIT** - Advanced just-in-time compiler
- **Generational GC** - Concurrent garbage collector
- **Complete VM** - Type system, exception handling, interop
- **Full platform support** - Windows, Linux, macOS, FreeBSD

**Location:** `src/coreclr/`

## Architecture

```
Application Code (IL)
        ↓
┌───────────────────────────────────┐
│    Virtual Machine (VM)           │
│  - Type loading                   │
│  - Method dispatch                │
│  - Exception handling             │
│  - Coordination                   │
└───────┬───────────────────────────┘
        ↓
┌───────────────┬──────────────┬────────────┐
│   RyuJIT      │   GC         │  Metadata  │
│   Compiler    │   Memory Mgr │  Reader    │
└───────────────┴──────────────┴────────────┘
        ↓
┌───────────────────────────────────┐
│  Platform Abstraction Layer (PAL) │
└───────────────────────────────────┘
        ↓
    Operating System
```

## Major Components

### 1. Virtual Machine (`src/coreclr/vm/`)

The VM is the heart of CoreCLR - 340K+ lines of C++ code that manages execution.

#### Key Responsibilities

**Type System:**
- Load and validate types from metadata
- Create MethodTable structures (runtime type representation)
- Manage generic instantiations
- Handle type compatibility checks

**Method Execution:**
- Coordinate with JIT for first-time compilation
- Manage prestubs and method dispatch
- Handle virtual method calls (vtable, interface dispatch)
- Support tiered compilation

**Memory Management:**
- Coordinate with GC for allocations
- Manage object lifetime
- Handle synchronization (Monitor, lock)
- Object header management

**Exception Handling:**
- Catch and propagate exceptions
- Unwind stack frames
- Execute finally blocks
- Cross managed/native boundaries

**Interoperability:**
- P/Invoke marshaling
- COM interop (Windows)
- Reverse P/Invoke
- Type marshaling

#### Critical Files

| File | Purpose | Lines |
|------|---------|-------|
| **ceemain.cpp** | Runtime initialization, startup | ~5K |
| **methodtable.cpp** | Type representation (MethodTable) | ~10K |
| **class.cpp** | EEClass - class metadata | ~7K |
| **method.cpp** | MethodDesc - method representation | ~8K |
| **object.cpp** | Object layout and operations | ~2K |
| **typehandle.cpp** | Unified type handles | ~1K |
| **typedesc.cpp** | Type descriptors (arrays, pointers, etc.) | ~500 |
| **clsload.cpp** | Type loader - loads types from metadata | ~4K |
| **ceeload.cpp** | Assembly and module loading | ~10K |
| **assembly.cpp** | Assembly management | ~3K |
| **threads.cpp** | Thread management and TLS | ~7K |
| **excep.cpp** | Exception handling | ~10K |
| **dllimport.cpp** | P/Invoke implementation | ~2K |
| **comcallablewrapper.cpp** | COM CCW (managed → COM) | ~3K |
| **runtimecallablewrapper.cpp** | COM RCW (COM → managed) | ~2K |
| **prestub.cpp** | Method prestub generation | ~2K |
| **virtualstubdispatch.cpp** | Virtual method dispatch | ~3K |
| **jitinterface.cpp** | VM ↔ JIT interface | ~13K |
| **gcheaputilities.cpp** | VM ↔ GC interface | ~1K |
| **codeman.cpp** | Code manager (JIT code tracking) | ~2K |
| **eeconfig.cpp** | Configuration settings | ~4K |
| **proftoeetointerfaceimpl.cpp** | Profiler callback implementation | ~8K |
| **eventtrace.cpp** | ETW event tracing | ~5K |
| **interpreter.cpp** | MSIL interpreter (fallback) | ~10K |

#### Object Model

**Object Layout:**
```
┌─────────────────────┐
│  Object Header      │  (sync block index, hashcode)
├─────────────────────┤
│  MethodTable*       │  (pointer to type information)
├─────────────────────┤
│  Field 1            │
│  Field 2            │
│  ...                │
└─────────────────────┘
```

**MethodTable** (runtime type):
```
┌──────────────────────┐
│  EEClass*            │  → Metadata, field descriptions
│  Module*             │  → Containing assembly
│  Parent MethodTable* │  → Base class
│  Interface Map       │
│  VTable              │  → Virtual method pointers
│  Flags               │  → Type characteristics
└──────────────────────┘
```

**MethodDesc** (method representation):
- Metadata token
- JIT status (not compiled / tier 0 / tier 1)
- Code pointer (entry point)
- Flags (static, virtual, generic, etc.)

#### Type Loading Process

1. **Load metadata** - Read type information from assembly
2. **Create EEClass** - Metadata representation
3. **Create MethodTable** - Runtime type structure
4. **Resolve members** - Load fields, methods, base types
5. **Build vtable** - Virtual method table
6. **Resolve interfaces** - Interface map
7. **Run type initializer** - Static constructor

For generics, create instantiation (e.g., `List<int>`) by sharing code where possible.

### 2. JIT Compiler (`src/coreclr/jit/`)

RyuJIT is a modern, cross-platform JIT compiler with ~500K lines of code.

#### Compilation Pipeline

```
IL (bytecode)
    ↓
┌─────────────────────┐
│  1. Import          │  Convert IL to HIR (High-level IR)
│     (importer.cpp)  │
└─────────────────────┘
    ↓
┌─────────────────────┐
│  2. Morph           │  Transform and optimize HIR
│     (morph.cpp)     │  - Inlining
│                     │  - Constant folding
│                     │  - Dead code elimination
└─────────────────────┘
    ↓
┌─────────────────────┐
│  3. Optimize        │  Advanced optimizations
│     (optimizer.cpp) │  - CSE, assertion prop
│                     │  - Loop opts, range check elim
│                     │  - Value numbering
└─────────────────────┘
    ↓
┌─────────────────────┐
│  4. Rationalize     │  Prepare for lowering
│     (rationalize.*) │
└─────────────────────┘
    ↓
┌─────────────────────┐
│  5. Lower           │  HIR → LIR (Low-level IR)
│     (lower.cpp)     │  Target-specific lowering
└─────────────────────┘
    ↓
┌─────────────────────┐
│  6. Register Alloc  │  Linear Scan Register Allocation
│     (lsra.cpp)      │
└─────────────────────┘
    ↓
┌─────────────────────┐
│  7. Code Gen        │  Emit machine code
│     (codegen*.cpp)  │  - x64, ARM64, etc.
└─────────────────────┘
    ↓
Native Machine Code
```

#### Key JIT Files

| Component | Files | Purpose |
|-----------|-------|---------|
| **IR Definition** | gentree.h, gentree.cpp | GenTree - IR nodes |
| **Compiler State** | compiler.h, compiler.cpp | Main compiler data structure |
| **Import** | importer.cpp | IL → HIR |
| **Morph** | morph.cpp | HIR transformation |
| **Optimization** | optimizer.cpp, optcse.cpp, assertion.cpp, rangecheck.cpp | Various optimizations |
| **Inlining** | inlining.cpp, inline.cpp, inlinepolicy.cpp | Method inlining |
| **Lowering** | lower.cpp, lowerxarch.cpp, lowerarm64.cpp | HIR → LIR |
| **Register Alloc** | lsra.cpp, lsrabuild.cpp | LSRA algorithm |
| **Code Gen** | codegenxarch.cpp, codegenarm64.cpp | Emit instructions |
| **Flow Graph** | flowgraph.cpp, block.cpp | Control flow |
| **SSA** | ssabuilder.cpp | SSA construction |
| **Value Numbering** | valuenum.cpp | Value numbering for CSE |
| **Loop Opts** | loopcloning.cpp, inductionvariableopts.cpp | Loop optimization |

#### Intermediate Representation (IR)

**HIR (High-level IR):**
- Tree-based representation
- Platform-independent
- Example: `GT_ADD(GT_LCL_VAR, GT_CNS_INT)`

**LIR (Low-level IR):**
- Linear representation (list of nodes)
- Platform-specific
- Example: `GT_LCL_VAR → GT_CNS_INT → GT_ADD`

**GenTree Node:**
```cpp
struct GenTree {
    genTreeOps gtOper;      // Operation (ADD, MUL, CALL, etc.)
    var_types gtType;       // Type (int, long, ref, etc.)
    GenTree* gtNext;        // Next in execution order
    GenTree* gtPrev;        // Previous
    // ... many more fields
};
```

#### Optimization Passes

| Optimization | Purpose | File |
|--------------|---------|------|
| **Inlining** | Replace method calls with body | inlining.cpp |
| **CSE** | Eliminate redundant computations | optcse.cpp |
| **Assertion Propagation** | Propagate known facts | assertion.cpp |
| **Range Check Elimination** | Remove bounds checks | rangecheck.cpp |
| **Dead Code Elimination** | Remove unreachable code | optimizer.cpp |
| **Constant Folding** | Evaluate constants at compile-time | morph.cpp |
| **Loop Cloning** | Optimize loops | loopcloning.cpp |
| **Copy Propagation** | Replace copies with original | copyprop.cpp |

#### Tiered Compilation

**Tier 0 (Quick JIT):**
- Minimal optimization
- Fast compilation
- Instrumented for profiling

**Tier 1 (Optimized):**
- Full optimizations
- Triggered after method runs ~30 times
- Uses profile data from Tier 0 (Dynamic PGO)

**Configuration:**
```bash
export DOTNET_TieredCompilation=1     # Enable (default)
export DOTNET_TieredPGO=1              # Enable Dynamic PGO
export DOTNET_TC_QuickJitForLoops=1   # Quick JIT even for loops
```

#### Dynamic PGO (Profile-Guided Optimization)

Tier 0 collects:
- Branch probabilities
- Class profiles (for devirtualization)
- Method call frequencies

Tier 1 uses this data for:
- Better inlining decisions
- Block reordering
- Guarded devirtualization

### 3. Garbage Collector (`src/coreclr/gc/`)

The GC is a generational, concurrent collector with ~2M lines in gc.cpp alone!

#### GC Fundamentals

**Generational Model:**
- **Gen 0** - New objects, collected frequently
- **Gen 1** - Survived one collection (medium-lived)
- **Gen 2** - Long-lived objects, collected rarely
- **LOH (Large Object Heap)** - Objects ≥ 85,000 bytes

**Modes:**
- **Workstation GC** - Single-threaded, low latency
- **Server GC** - Multi-threaded, high throughput

**Collection Types:**
- **Ephemeral** - Gen 0 + Gen 1 (fast)
- **Full** - All generations (slower)
- **Background** - Concurrent Gen 2 (low pause)

#### Key GC Files

| File | Purpose |
|------|---------|
| **gc.cpp** | Main GC implementation (~2M lines!) |
| **gcpriv.h** | Private GC data structures |
| **gcsvr.cpp** | Server GC |
| **gcwks.cpp** | Workstation GC |
| **gcconfig.cpp** | Configuration options |
| **gchandletable.cpp** | Object handles (weak, strong, pinned) |
| **handletable.cpp** | Handle table implementation |
| **softwarewritewatch.cpp** | Write watch for concurrent GC |

#### GC Algorithm (Simplified)

1. **Mark** - Trace from roots, mark reachable objects
2. **Plan** - Decide where to move objects (compaction)
3. **Relocate** - Update pointers
4. **Compact** - Move objects to compact heap

#### GC Configuration

Environment variables:
```bash
# Server vs Workstation
export DOTNET_gcServer=1

# GC heap count (server GC)
export DOTNET_GCHeapCount=4

# Concurrent GC
export DOTNET_gcConcurrent=1

# LOH compaction
export DOTNET_GCLOHCompact=1

# Heap limits
export DOTNET_GCHeapHardLimit=0x40000000  # 1GB
```

In project file:
```xml
<PropertyGroup>
  <ServerGarbageCollection>true</ServerGarbageCollection>
  <ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>
</PropertyGroup>
```

### 4. Metadata System (`src/coreclr/md/`)

Reads and manages IL metadata (types, methods, signatures, etc.).

#### Structure

```
src/coreclr/md/
├── compiler/        # Metadata writing (for compilers)
├── runtime/         # Metadata reading (for runtime)
├── enc/             # Edit and Continue support
└── inc/             # Headers
```

#### Metadata Tables

Metadata is stored in tables (ECMA-335):
- **TypeDef** - Type definitions
- **MethodDef** - Method definitions
- **FieldDef** - Field definitions
- **MemberRef** - Member references
- **TypeRef** - Type references
- **Assembly** - Assembly information
- **...** - Many more

#### Key Classes

- **IMDInternalImport** - Internal metadata reading interface
- **MetaDataRo** - Read-only metadata
- **MiniMd** - Minimal metadata model

### 5. Platform Abstraction Layer (`src/coreclr/pal/`)

Abstracts OS differences between Windows and Unix.

#### PAL Structure

```
src/coreclr/pal/
├── inc/
│   └── pal.h              # Main PAL interface
└── src/
    ├── thread/            # Thread APIs
    ├── sync/              # Synchronization
    ├── memory/            # Memory management
    ├── file/              # File I/O
    ├── exception/         # Exception handling
    ├── loader/            # Module loading
    └── arch/              # Architecture-specific
```

#### PAL Functions

Examples:
- `PAL_Initialize()` - Initialize PAL
- `PAL_VirtualAlloc()` - Allocate memory
- `PAL_CreateThread()` - Create thread
- `PAL_GetProcAddress()` - Get function pointer
- `PAL_TryExcept()` - Exception handling

### 6. Debugging & Diagnostics

#### DAC (Data Access Component) - `src/coreclr/debug/daccess/`

Allows debuggers to read CLR data structures from dumps or live processes.

**How it works:**
1. Debugger loads mscordaccore.dll
2. DAC reads memory from target process
3. Reconstructs CLR data structures
4. Exposes IXCLRData* interfaces

**DACized Code:**
```cpp
// Code that works both in-process and out-of-process
PTR_MethodTable pMT = obj->GetMethodTable();  // Works in DAC
```

#### EventPipe - `src/native/eventpipe/`

Cross-platform event streaming for diagnostics.

**Events:**
- GC events
- JIT events
- Exception events
- Custom events

**Tools:**
- `dotnet-trace` - Collect traces
- `dotnet-counters` - Performance counters
- `dotnet-dump` - Memory dumps
- PerfView (Windows)

### 7. Interop

#### P/Invoke - `src/coreclr/vm/dllimport.cpp`, `ilmarshalers.cpp`

**Process:**
1. Declare `[LibraryImport]` or `[DllImport]`
2. Runtime generates IL stub
3. Stub handles marshaling
4. Calls native function
5. Marshals return value

**Example:**
```csharp
[LibraryImport("kernel32.dll", SetLastError = true)]
[return: MarshalAs(UnmanagedType.Bool)]
static partial bool CloseHandle(IntPtr handle);
```

#### COM Interop - `src/coreclr/interop/`, `src/coreclr/vm/com*.cpp`

**CCW (COM Callable Wrapper):**
- Managed object → COM object
- Implements IUnknown, IDispatch
- Reference counting

**RCW (Runtime Callable Wrapper):**
- COM object → Managed object
- Wraps COM interface
- Manages COM lifetime

## Advanced Topics

### ReadyToRun (R2R)

Pre-compiled IL for faster startup.

**Location:** `src/coreclr/vm/readytoruninfo.cpp`, `src/coreclr/tools/aot/`

**Format:**
- Native code for methods
- Fixups for cross-assembly references
- Fallback to JIT if needed

### Reflection

**Managed:** `src/libraries/System.Reflection*/`
**Native VM support:** `src/coreclr/vm/reflectioninvocation.cpp`

### Threading

**Location:** `src/coreclr/vm/threads.cpp`

**Key concepts:**
- Thread-local storage (TLS)
- Synchronization (Monitor, lock)
- Managed thread pool
- Async state machine support

## Key Workflows

### Modifying the JIT

1. Edit files in `src/coreclr/jit/`
2. Build: `./build.sh -subset clr.jit`
3. Test: Use SuperPMI or specific test cases
4. Debug: Attach debugger to corerun

### Modifying the GC

1. Edit `src/coreclr/gc/gc.cpp` (or related)
2. Build: `./build.sh -subset clr`
3. Test: `src/tests/GC/` tests
4. Use GC stress: `export DOTNET_GCStress=3`

### Modifying the VM

1. Edit files in `src/coreclr/vm/`
2. Build: `./build.sh -subset clr`
3. Test: Appropriate test suite
4. Debug: Checked build for assertions

## Configuration

### JIT Configuration

```bash
export DOTNET_JitDisasm=MethodName        # Disassemble
export DOTNET_JitDump=MethodName          # Dump IR
export DOTNET_JitStress=1                 # Stress testing
export DOTNET_TieredCompilation=0         # Disable tiering
```

### GC Configuration

```bash
export DOTNET_gcServer=1                  # Server GC
export DOTNET_GCStress=3                  # GC stress
export DOTNET_HeapVerify=1                # Verify heap
```

## Documentation

**Book of the Runtime (BOTR):** `docs/design/coreclr/botr/`

Essential reading:
- **intro-to-clr.md** - CLR overview
- **ryujit-overview.md** - JIT architecture
- **garbage-collection.md** - GC design
- **type-system.md** - Type system
- **exceptions.md** - Exception handling
- **threading.md** - Threading model

---

**Next:** See [03-Mono-Runtime-Guide.md](03-Mono-Runtime-Guide.md) for the Mono runtime, or [08-Feature-Location-Reference.md](08-Feature-Location-Reference.md) for quick lookups.
