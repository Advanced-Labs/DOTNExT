# Extension Points Relevant to Engram System

> Extracted from: D:\Dev\DOTNExT\Docs\Repo Map\14-Extension-Points-Catalog.md
> Purpose: Identify where Engram system could hook into CoreCLR

---

## High-Value Extension Points for Engram

### 1. Profiler API (ICorProfilerCallback)
**Location:** `src/coreclr/inc/corprof.idl`
**Effort:** 2-6 months
**Why Relevant:**
- `ObjectAllocated(ObjectID objectId, ClassID classId)` - Hook EVERY object creation
- Can observe GC events, object movement
- Can rewrite IL before JIT (for UUID injection?)
- Object tracking already exists

**Engram Use:**
- Intercept object creation for UUID assignment
- Track object relationships via reference field writes
- Observe object graph structure

### 2. GC Interface (IGCHeap)
**Location:** `src/coreclr/gc/gcinterface.h`
**Effort:** 6-12 months (full impl) but we can extend, not replace
**Why Relevant:**
- GC already tracks ALL object references (card tables, handle tables)
- Already walks object graphs for collection
- Handle types (weak, strong, pinned) already exist

**Engram Use:**
- Leverage existing reference tracking infrastructure
- Add new handle type for "engram-aware" references
- Hook into object finalization for lifecycle events

### 3. Type System Extensions
**Location:** `src/coreclr/vm/class.cpp`, `methodtable.cpp`
**Effort:** Months
**Why Relevant:**
- This is where MethodTable and EEClass live
- Special type treatment examples: Span<T>, Unsafe
- Object header layout decisions made here

**Engram Use:**
- Extend object header or MethodTable for UUID storage
- Add new type flags for "engram-enabled" types
- Special treatment for reference fields (relationship metadata)

### 4. JIT Helpers
**Location:** `src/coreclr/inc/jithelpers.h`, `src/coreclr/vm/jithelpers.cpp`
**Effort:** Days
**Why Relevant:**
- JIT calls these for complex operations
- Can intercept operations that should update relationship graph

**Engram Use:**
- `CORINFO_HELP_ENGRAM_FIELD_ASSIGN` - When setting a reference field, record relationship
- `CORINFO_HELP_ENGRAM_NEW` - Augmented object creation with UUID

### 5. VM Intrinsics
**Location:** `src/coreclr/vm/ecalllist.h`
**Effort:** Weeks
**Why Relevant:**
- `System.Runtime.CompilerServices.Unsafe` already exists
- Could add `System.Runtime.CompilerServices.Engram` namespace

**Engram Use:**
- `Engram.GetId(object)` - Get object UUID
- `Engram.GetRelations(object)` - Get relationship graph
- `Engram.Extract(object)` - Create engram from object graph

### 6. EventPipe (Diagnostics)
**Location:** `src/native/eventpipe/`
**Effort:** Hours
**Why Relevant:**
- Can emit diagnostic events for engram operations
- Debugging/observability of relationship tracking

**Engram Use:**
- Emit events for object creation, relationship changes
- Debug engram extraction/loading

---

## Anti-Patterns to Avoid

From the catalog, explicitly warned:

> ❌ Don't: Modify Object Layout Without Justification
> Why: Affects every object (billions in large apps)
> Alternative: Indirection via MethodTable or side table

**Implication for Engram:**
- Don't add UUID to every object header directly (too expensive)
- Use side table indexed by object address or MethodTable extension
- Or make it opt-in via attribute/marker interface

---

## Recommended Approach (Multi-pronged)

### Phase 1: Side Table + Profiler (Proof of Concept)
- Use Profiler API to intercept ObjectAllocated
- Maintain side table mapping ObjectID -> UUID
- Track relationships via write barrier interception
- **Minimal runtime modification**

### Phase 2: Type System Integration
- Add `[Engram]` attribute recognized by type loader
- Engram-attributed types get special treatment
- Optional UUID in MethodTable extension for these types

### Phase 3: JIT Helper Integration
- Reference field writes emit JIT helper calls
- Relationship graph updated automatically
- No manual tracking needed

### Phase 4: Native Support
- Object header extension (only for engram types)
- Built-in extraction/loading
- Cross-VM protocol

---

## Key Files to Investigate

| Purpose | File |
|---------|------|
| Object header structure | `src/coreclr/vm/object.h` |
| MethodTable definition | `src/coreclr/vm/methodtable.h` |
| EEClass definition | `src/coreclr/vm/class.h` |
| GC interface | `src/coreclr/gc/gcinterface.h` |
| Write barrier | `src/coreclr/gc/writebarrier*` |
| Profiler callbacks | `src/coreclr/inc/corprof.idl` |
| JIT helpers | `src/coreclr/inc/jithelpers.h` |
| Type loader | `src/coreclr/vm/clsload.cpp` |

---

*Last updated: 2025-12-05*
