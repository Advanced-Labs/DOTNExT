# Major Subsystem Integration Guide

**How to Add Major Systems Like the GC: Exact Integration Points**

This guide shows the precise files, interfaces, and types you need to work with when adding major subsystems to the .NET Runtime.

## Understanding Integration Boundaries

The .NET Runtime has **relatively well-defined** integration boundaries for major subsystems, but they require understanding multiple layers. Here's the complete picture:

## Case Study: Garbage Collector Integration Points

Let's use the GC as a concrete example to show **exactly** what you'd need to modify.

### GC Interface Layer (VM ↔ GC Boundary)

**Primary Interface Files:**

| File | Purpose | Key Types/Functions |
|------|---------|---------------------|
| **src/coreclr/gc/gcinterface.h** | Main GC interface contract | `IGCHeap`, `IGCHeapInternal` |
| **src/coreclr/vm/gcheaputilities.h** | VM's view of GC | `GCHeapUtilities`, `GCHeap` accessor |
| **src/coreclr/vm/gcheaputilities.cpp** | GC utility implementations | Allocation helpers |
| **src/coreclr/gc/gcenv.h** | GC's view of VM (Environment) | Callbacks GC needs from VM |
| **src/coreclr/gc/gcenv.ee.h** | Execution Engine interface | Thread suspension, stack walking |

**Exact Interface Signatures:**

```cpp
// src/coreclr/gc/gcinterface.h
class IGCHeap {
public:
    virtual Object* Alloc(size_t size, DWORD flags) = 0;
    virtual void GarbageCollect(int generation = -1, bool low_memory = false) = 0;
    virtual void SuspendEE(SUSPEND_REASON reason) = 0;
    virtual void RestartEE(bool bFinishedGC) = 0;
    // ~100 more methods
};
```

**To add a new GC, you'd implement this interface.**

### Object Layout Dependencies

**Files That Define Object Structure:**

| File | What It Defines | Why GC Cares |
|------|-----------------|--------------|
| **src/coreclr/vm/object.h** | Object header layout | GC reads sync block, marks objects |
| **src/coreclr/vm/object.cpp** | Object operations | Allocation, sizing |
| **src/coreclr/vm/methodtable.h** | Type information | GC needs to know object size, GC layout |
| **src/coreclr/vm/gcdesc.h** | GC descriptor format | Tells GC which fields are references |

**Critical Data Structures:**

```cpp
// src/coreclr/vm/object.h
class Object {
    MethodTable* m_pMethTab;  // Type info (GC reads this!)
    // Followed by instance fields
};

// Object header (preceding Object*)
struct ObjHeader {
    DWORD m_SyncBlockValue;  // Contains GC bits + sync block index
};
```

**If you add a new system that needs object metadata, you'd compete for bits in `m_SyncBlockValue` or need new header space.**

### JIT Integration Points

**Files Where GC Integrates with JIT:**

| File | Purpose | Exact Integration |
|------|---------|-------------------|
| **src/coreclr/jit/codegencommon.cpp** | Write barrier generation | Calls to GC write barrier helpers |
| **src/coreclr/jit/gcencode.cpp** | GC info encoding | Encodes which registers/stack slots have references |
| **src/coreclr/jit/emit.cpp** | Instruction emission | Inserts GC-safe points |
| **src/coreclr/vm/gcinfodecoder.cpp** | GC info decoding | VM reads JIT's GC info during stack walk |

**Write Barrier Example:**

```cpp
// src/coreclr/jit/codegenxarch.cpp (x64 code generation)
void CodeGen::genWriteBarrier(GenTree* tgt, GenTree* src) {
    // Generate:
    //   mov [tgt], src           ; Store reference
    //   cmp tgt, g_ephemeral_low ; Card table check
    //   jb  NoBarrier
    //   call JIT_WriteBarrier    ; Call GC helper
    // NoBarrier:
}
```

**If you add a system needing code generation hooks (like a memory profiler), you'd modify similar JIT codegen files.**

### Thread Coordination

**Files for Thread Suspension/Coordination:**

| File | Purpose | Integration Point |
|------|---------|-------------------|
| **src/coreclr/vm/threads.h** | Thread state management | `Thread::m_fPreemptiveGCDisabled` |
| **src/coreclr/vm/threads.cpp** | Thread suspension | `Thread::SuspendRuntime()` |
| **src/coreclr/vm/threadsuspend.cpp** | Suspension logic | GC triggers suspension |
| **src/coreclr/vm/gcenv.ee.cpp** | GC ↔ EE glue | `GCToEEInterface::SuspendEE()` |

**Critical State:**

```cpp
// src/coreclr/vm/threads.h
class Thread {
    // GC mode: cooperative (can't GC) vs preemptive (can GC)
    bool m_fPreemptiveGCDisabled;

    // Transitions GC mode
    void DisablePreemptiveGC();
    void EnablePreemptiveGC();
};
```

**If you add a system needing global coordination (like a JIT recompilation engine), you'd use similar suspension mechanisms.**

### Configuration and Policy

**Files for GC Configuration:**

| File | Purpose | Variables |
|------|---------|-----------|
| **src/coreclr/gc/gcconfig.cpp** | GC config parsing | `GCConfig::Initialize()` |
| **src/coreclr/vm/eeconfig.h** | VM config | `EEConfig` - reads environment vars |
| **src/coreclr/vm/eeconfig.cpp** | Config parsing | `DOTNET_gcServer`, `DOTNET_GCHeapCount`, etc. |

**Configuration Pattern:**

```cpp
// src/coreclr/gc/gcconfig.cpp
void GCConfig::Initialize() {
    // Read DOTNET_GCHeapCount environment variable
    m_heapCount = GetConfigInteger("GCHeapCount", defaultHeapCount);

    // Read DOTNET_gcServer
    m_serverGC = GetConfigBool("gcServer", false);
}
```

**New systems would add similar config parsing.**

---

## Generalizing: Adding Any Major Subsystem

Based on the GC example, here's **exactly** what you need to know for ANY major subsystem:

### 1. Primary Interface Definition

**Location:** `src/coreclr/vm/` or `src/coreclr/gc/` (for GC-like systems)

**What to create:**
- Abstract interface class (like `IGCHeap`)
- Factory/accessor in VM (like `GCHeapUtilities`)
- Environment interface (what your system needs from VM)

**Example for hypothetical "Speculative Execution System":**

```cpp
// src/coreclr/vm/speculativeexec.h
class ISpeculativeExecutor {
public:
    virtual void BeginSpeculation(MethodDesc* method) = 0;
    virtual void CommitSpeculation() = 0;
    virtual void RollbackSpeculation() = 0;
    virtual bool IsSpeculating() = 0;
};

// Accessor
class SpecExecUtilities {
    static ISpeculativeExecutor* s_pSpecExec;
public:
    static ISpeculativeExecutor* GetSpecExec() { return s_pSpecExec; }
};
```

### 2. Object/Type System Integration

**Files to modify if you need per-object or per-type state:**

| Need | File to Modify | Exact Location |
|------|----------------|----------------|
| Per-object bits | `src/coreclr/vm/object.h` | `ObjHeader::m_SyncBlockValue` (limited bits!) |
| Per-type data | `src/coreclr/vm/methodtable.h` | Add field to `MethodTable` |
| Per-assembly data | `src/coreclr/vm/assembly.h` | Add field to `Assembly` |
| Per-method data | `src/coreclr/vm/method.hpp` | Add field to `MethodDesc` |

**Bit Budget Reality:**

```cpp
// src/coreclr/vm/syncblk.h
// Only 32 bits in object header!
#define BIT_SBLK_IS_HASH_OR_SYNCBLKINDEX 0x08000000  // 1 bit
#define BIT_SBLK_FINALIZER_RUN           0x40000000  // 1 bit
#define BIT_SBLK_GC_RESERVE              0x20000000  // 1 bit
// ... few bits left!
```

**If you need bits, you might need to add a sync block or indirection.**

### 3. JIT Integration (Code Generation)

**Files to modify if you need generated code changes:**

| Architecture | Codegen File | What to Modify |
|--------------|--------------|----------------|
| **x64** | `src/coreclr/jit/codegenxarch.cpp` | `genXXX()` methods |
| **ARM64** | `src/coreclr/jit/codegenarm64.cpp` | `genXXX()` methods |
| **All** | `src/coreclr/jit/codegencommon.cpp` | Architecture-agnostic logic |

**For instrumentation/checks:**
- `src/coreclr/jit/morph.cpp` - Add IR transformations
- `src/coreclr/jit/flowgraph.cpp` - Add basic blocks
- `src/coreclr/jit/lower.cpp` - Lowering phase

**Example: Adding bounds check instrumentation:**

```cpp
// src/coreclr/jit/morph.cpp
void Compiler::fgMorphArrayOps() {
    // Find: arr[i]
    // Insert: if (i >= arr.Length) RecordBoundsViolation();

    GenTree* boundsCheck = gtNewOperNode(GT_GE, TYP_INT, index, length);
    GenTree* call = gtNewHelperCallNode(CORINFO_HELP_RECORD_BOUNDS_VIOLATION);
    // Insert into IR...
}
```

### 4. Thread/Synchronization Integration

**Files to modify for coordination:**

| Need | File | Method |
|------|------|--------|
| Global coordination | `src/coreclr/vm/threadsuspend.cpp` | `ThreadSuspend::SuspendEE()` |
| Thread-local state | `src/coreclr/vm/threads.h` | Add field to `Thread` class |
| Fiber/coroutine support | `src/coreclr/vm/threads.cpp` | Thread switching hooks |

**Pattern:**

```cpp
// src/coreclr/vm/threads.h
class Thread {
    // Your system's per-thread state
    MySystemContext* m_pMySystemCtx;

    // Hooks
    void OnThreadSuspend() { /* cleanup */ }
    void OnThreadResume() { /* restore */ }
};
```

### 5. Diagnostics/Events Integration

**Files to add events:**

| Event System | File | How to Add |
|--------------|------|------------|
| **EventPipe** | `src/coreclr/vm/ClrEtwAll.man` | Add ETW manifest |
| **EventPipe** | `src/native/eventpipe/` | Add event definitions |
| **Profiler API** | `src/coreclr/inc/corprof.idl` | Add callbacks |

**Example:**

```xml
<!-- src/coreclr/vm/ClrEtwAll.man -->
<event value="1000"
       symbol="MySystem_OperationStart"
       task="MySystemTask"
       opcode="Start"
       level="Informational">
  <template tid="MySystemTemplate">
    <data name="OperationID" inType="win:UInt64"/>
  </template>
</event>
```

### 6. Configuration Integration

**Exact steps:**

1. **Define config in:** `src/coreclr/vm/eeconfig.h`
   ```cpp
   class EEConfig {
       bool m_enableMySystem;
       int m_mySystemThreshold;
   };
   ```

2. **Parse in:** `src/coreclr/vm/eeconfig.cpp`
   ```cpp
   void EEConfig::InitMySystem() {
       m_enableMySystem = GetConfigBool("MySystem_Enabled", false);
       m_mySystemThreshold = GetConfigInt("MySystem_Threshold", 1000);
   }
   ```

3. **Read environment variables:**
   ```bash
   export DOTNET_MySystem_Enabled=1
   export DOTNET_MySystem_Threshold=500
   ```

---

## Example: Adding a "Transactional Memory" System

Let's map out **exactly** what you'd need to modify:

### Phase 1: Core Interface (2-3 files)

**Create:**
- `src/coreclr/vm/transactionalmemory.h` - Interface definition
- `src/coreclr/vm/transactionalmemory.cpp` - Utility functions
- `src/coreclr/tm/tm.cpp` - Implementation (new directory)

```cpp
// src/coreclr/vm/transactionalmemory.h
class ITransactionalMemory {
    virtual void BeginTransaction() = 0;
    virtual void CommitTransaction() = 0;
    virtual void AbortTransaction() = 0;
    virtual bool IsInTransaction() = 0;
};
```

### Phase 2: Object Integration (1-2 files)

**Modify:**
- `src/coreclr/vm/object.h` - Add version counter? Read log?
- `src/coreclr/vm/methodtable.h` - Track transactional fields?

**Challenge:** Limited header space! Might need indirection.

```cpp
// Might add to MethodTable for field tracking
class MethodTable {
    PTR_TransactionalFieldInfo m_pTxFieldInfo; // NULL if not transactional
};
```

### Phase 3: JIT Integration (4-6 files)

**Modify:**
- `src/coreclr/jit/morph.cpp` - Transform field reads/writes
- `src/coreclr/jit/codegenxarch.cpp` - Generate transaction checks
- `src/coreclr/jit/codegenarm64.cpp` - ARM64 version
- `src/coreclr/jit/gcencode.cpp` - Encode transaction info

```cpp
// src/coreclr/jit/morph.cpp
GenTree* Compiler::fgMorphFieldAccess(GenTree* tree) {
    if (IsTransactionalField(fieldDesc)) {
        // Transform: obj.field
        // Into: TxReadField(obj, offset)
        return gtNewHelperCallNode(CORINFO_HELP_TX_READ_FIELD,
                                   obj, fieldOffset);
    }
}
```

### Phase 4: Thread Integration (2-3 files)

**Modify:**
- `src/coreclr/vm/threads.h` - Add transaction context
- `src/coreclr/vm/threads.cpp` - Manage context lifecycle
- `src/coreclr/vm/threadsuspend.cpp` - Abort on suspend?

```cpp
// src/coreclr/vm/threads.h
class Thread {
    TransactionContext* m_pTxContext; // NULL if not in transaction
};
```

### Phase 5: Configuration (2 files)

**Modify:**
- `src/coreclr/vm/eeconfig.h` - Add config fields
- `src/coreclr/vm/eeconfig.cpp` - Parse config

### Phase 6: Diagnostics (2-3 files)

**Modify:**
- `src/coreclr/vm/ClrEtwAll.man` - Add ETW events
- `src/native/eventpipe/` - Add EventPipe support
- `src/coreclr/inc/corprof.idl` - Add profiler callbacks

### Phase 7: Build Integration (3-4 files)

**Modify:**
- `src/coreclr/CMakeLists.txt` - Add new files
- `src/coreclr/vm/CMakeLists.txt` - Add compilation units
- `eng/Subsets.props` - Ensure buildable

---

## Key Integration Patterns

### Pattern 1: Hook Points Are Well-Defined

**Good news:** Major hook points are explicit:

| Hook Point | File | Method |
|------------|------|--------|
| Runtime startup | `src/coreclr/vm/ceemain.cpp` | `EEStartup()` |
| Method JIT | `src/coreclr/vm/jitinterface.cpp` | `compileMethod()` |
| Object allocation | `src/coreclr/vm/gcheaputilities.h` | `Alloc()` |
| Exception throw | `src/coreclr/vm/excep.cpp` | `RaiseTheExceptionInternalOnly()` |
| Thread start | `src/coreclr/vm/threads.cpp` | `Thread::SetupNewThread()` |

### Pattern 2: Data Structure Competition

**Challenge:** Adding fields to core types impacts memory.

**Solutions:**
1. **Indirection:** Add pointer to subsidiary structure (NULL if unused)
2. **Flags:** Use existing flag fields
3. **Sync Block:** Store in sync block (heavyweight)
4. **Side Table:** External hash table keyed by object address

**Example:**

```cpp
// BAD: Adds 8 bytes to EVERY object
class Object {
    void* m_pMySystemData; // ❌ Too expensive!
};

// GOOD: Indirection from MethodTable (per-type, not per-object)
class MethodTable {
    PTR_MySystemTypeData m_pMyData; // ✅ Only if type participates
};
```

### Pattern 3: JIT Helper Pattern

**For runtime calls from JIT-generated code:**

1. **Declare in:** `src/coreclr/inc/jithelpers.h`
   ```cpp
   JITHELPER(CORINFO_HELP_MY_OPERATION, MyOperationHelper, CORINFO_HELP_SIG_...)
   ```

2. **Implement in:** `src/coreclr/vm/myhelpers.cpp`
   ```cpp
   HCIMPL2(void, MyOperationHelper, Object* obj, INT32 value) {
       // Implementation
   }
   HCIMPLEND
   ```

3. **Call from JIT:** `src/coreclr/jit/`
   ```cpp
   GenTree* call = gtNewHelperCallNode(CORINFO_HELP_MY_OPERATION, obj, value);
   ```

### Pattern 4: Policy Objects

**For configurable behavior:**

```cpp
// src/coreclr/vm/mypolicy.h
class MySystemPolicy {
    static bool ShouldApplyToMethod(MethodDesc* pMD);
    static int GetThresholdForType(MethodTable* pMT);
};
```

Used throughout VM for decisions.

---

## Reality Check: How Much Is Well-Defined?

### ✅ Well-Defined (Clear Integration Points):

1. **GC Interface** - `IGCHeap` is explicit contract
2. **JIT Helpers** - Pattern is clear
3. **ETW Events** - Manifest-based, explicit
4. **Configuration** - Environment variable pattern
5. **Profiler API** - COM interface, explicit callbacks
6. **PAL** - Platform abstraction is explicit

### ⚠️ Moderately Defined (Requires Understanding):

1. **Object Layout** - Need to understand header, MethodTable
2. **Thread Coordination** - Need to understand GC-safe points
3. **JIT Integration** - Need to understand IR phases
4. **Type System** - Need to understand type loading

### ❌ Poorly Defined (Requires Deep Investigation):

1. **Bit budgets** - No central registry of used bits
2. **Memory ordering** - Implicit assumptions throughout
3. **Performance implications** - Not documented per-component
4. **Cross-component interactions** - Need to discover

---

## Recommendation: Integration Checklist

When adding a major subsystem:

### Phase 1: Design (Weeks 1-2)
- [ ] Define interface contract (like `IGCHeap`)
- [ ] Identify data structure needs
- [ ] Map JIT integration points
- [ ] Plan configuration strategy
- [ ] Design diagnostics events

### Phase 2: Prototype (Weeks 3-4)
- [ ] Implement minimal interface
- [ ] Add to one architecture (x64)
- [ ] Test basic functionality
- [ ] Measure overhead

### Phase 3: Integration (Weeks 5-8)
- [ ] Add to all architectures
- [ ] Thread coordination
- [ ] Configuration system
- [ ] Diagnostics/events
- [ ] Documentation

### Phase 4: Refinement (Weeks 9-12)
- [ ] Performance tuning
- [ ] Edge cases
- [ ] Stress testing
- [ ] Cross-platform validation

---

## Answer to Your Question

> "How much would we know in which files and systems and types exactly to work with/around/into?"

**Answer: About 80-90% can be enumerated precisely.**

**You'd know exactly:**
- ✅ Interface files to create/modify (~5-10 files)
- ✅ JIT codegen files (~4-6 files per architecture)
- ✅ Configuration files (~2-3 files)
- ✅ Diagnostic integration (~3-5 files)
- ✅ Core VM integration points (~10-15 files)

**You'd need to discover:**
- ⚠️ Exact bit layouts (trial and error or deep reading)
- ⚠️ Memory ordering requirements (implicit in code)
- ⚠️ Performance critical paths (profiling needed)
- ⚠️ Edge cases and interactions (emerge during implementation)

**Total files to touch: ~30-50 files for a major subsystem** (compared to 10,000+ files in repo)

The boundaries are reasonably well-defined, especially compared to many large codebases, but require architectural understanding rather than just following interfaces.
