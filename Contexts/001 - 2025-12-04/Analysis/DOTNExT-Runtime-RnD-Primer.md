# DOTNExT Runtime R&D Primer

> **Document Type:** AI Onboarding & Priming for Runtime Research
> **Version:** 1.2
> **Date:** 2025-12-10
> **Purpose:** Single document that primes an AI to understand AND contribute to DOTNExT runtime R&D
> **Usage:** Read this FIRST (and possibly ONLY) for runtime-focused work
> **Key Update v1.2:** Clarifies that async was a doorway, not the destination. We study Unwinder techniques for a *universal execution model*, not async replacement. DOTNExT may develop its own constructs (not necessarily "Tasklets").
> **Key Update v1.1:** Distinguishes JIT vs Unwinder runtime-async approaches; DOTNExT stays on .NET 9 for Unwinder R&D

---

## How This Document Works

This isn't just facts to memorize. It contains:
1. **Context** - What we're building and why
2. **Derivations** - Reasoning chains that explain WHY, not just WHAT
3. **Challenges** - Questions to engage with, not skip over
4. **Generation Prompts** - Can you solve problems you haven't seen?

**If you can only recite facts but can't derive implications or generate solutions, you don't truly understand.**

---

## Part 1: The Context

### What is DOTNExT?

DOTNExT is a fork of the .NET VMR (Virtual Monolithic Repository) - the complete source for .NET runtime, Roslyn compilers, SDK, and frameworks.

**Location:** `D:\Dev\DOTNExT\src\runtime\` (runtime specifically)

**Goal:** Evolve the .NET runtime toward BEAM-like capabilities:
- Lightweight execution contexts (millions, not thousands)
- Preemptive scheduling without OS thread overhead
- Process checkpoint/restore (like Linux CRIU, but VM-internal)
- Foundation for distributed "Engrams" - portable computation units

### Why Fork .NET?

.NET is excellent but architecturally limited for our goals:
- Threads are heavy (~1MB each)
- GC is global (Stop-The-World affects all threads)
- No native execution suspend/resume primitive
- Distribution is library-level (Orleans), not runtime-level

We're not replacing .NET. We're extending it with capabilities that enable a new kind of platform (VAYRON) where objects are persistent, distributed, and AI-capable.

### DOTNExT's Trade-off Philosophy

**DOTNExT accepts different trade-offs than standard .NET.**

In standard .NET, speed and resource efficiency are paramount. In DOTNExT (especially for AI-first execution modes):

- **AI is the bottleneck** - by orders of magnitude. Runtime overhead becomes negligible.
- **We trade speed/resources for capabilities** - virtualization, introspection, control.
- **"Slow but Smart is the new Speed"** - when AI intelligence can leverage runtime capabilities, the value created far exceeds the cost.

This means we can afford:
- Virtualization overhead at every safe point
- Reification of every stack frame
- Checkpoints, forks, rollbacks, speculative execution
- Full runtime introspection from managed space

**The CPU cycles we "waste" buy capabilities that AI can use intelligently.**

### The Long-Term Vision

Imagine a "cyberspace" where:
- Code, execution state, and objects are all persistable and transferable
- A node can discover capabilities semantically, load them as "Engrams", execute locally
- The network forms an "Internet of Objects" navigable via VNS (Virtual Name System)
- AI-Objects collaborate in a Society of Minds

This is ambitious. The runtime R&D is the foundation that makes it possible.

---

## Part 2: The Key Discoveries

### Discovery 0: Async Was a Doorway, Not the Destination (Critical Clarification)

**The journey to the Unwinder:**

1. **Async+ (Roslyn-based)** - Started here. Roslyn generates state machines for async methods. We added interfaces to persist/restore these state machines. Prototype worked with Orleans storage backend.

2. **Limitations exposed** - The Roslyn approach only captures what the compiler decides to capture, only at await points, and can't handle byrefs. We researched .NET 10's JIT runtime-async...

3. **JIT Runtime-Async confusion** - Initially confused this with Tasklets/Unwinder. JIT runtime-async is "better async" but still async - only captures at await points, still can't handle byrefs.

4. **Unwinder discovery** - The `.NET 9 Unwinder experiment` captures **real stack frames at any safe point**. This is fundamentally different - not async replacement, but **universal execution state capture**.

**The key realization:**

> **We don't want the Unwinder's async-specific machinery. We want its techniques applied universally.**

The Unwinder proves that:
- Stack frames can become heap objects
- Capture can happen at any safe point (not just await)
- Byrefs survive (real stack semantics)
- Execution state is data that can be manipulated

**What we actually want:**

A **new execution model** where:
- **Everything runs as captured execution units** - not just async methods
- **Safe points become control points** - the runtime (or AI from managed space) can intervene anywhere
- **Execution becomes first-class data** - inspect, serialize, fork, migrate, compare, kill
- **"Threads" become Execution Pathways** - trackable, manipulable, distributable entities

**This is not "async with persistence". It's closer to:**
- Process calculi (π-calculus style mobility)
- Continuation-passing as universal execution model
- Software transactional memory extended to execution flow
- Speculative execution under intelligent control

**DOTNExT may develop its own constructs** - not necessarily called "Tasklets", potentially different in design. The Unwinder is studied for its **techniques**, not adopted wholesale.

### Discovery 0.5: The Semantic Inversion - `sync` is the New Exception

**Traditional .NET:**
- Default = Synchronous (blocking)
- Exception = `async` (marked explicitly)
- Mental model: "Most code blocks, async is special"

**DOTNExT Universal Execution Model:**
- Default = Everything can yield at any safe point (yieldable)
- This IS async-like behavior by nature
- The word "async" loses meaning (not distinguishing anything)
- **Exception = `sync`** (when you need guaranteed non-yielding)

**The Inversion:**

| Traditional .NET | DOTNExT |
|------------------|---------|
| `void Foo()` = synchronous | `void Foo()` = can yield at any safe point |
| `async Task Foo()` = may yield | Redundant - everything may yield |
| No keyword for sync | **`sync` keyword for guaranteed non-yielding** |
| `await` = explicit yield | Yields happen at any safe point anyway |

**The `sync` Keyword:**

```csharp
// Declaration-site: Method NEVER yields
sync void CriticalAtomicOperation()
{
    // Guaranteed: no yields, no preemption, no checkpoint
    // Always runs to completion atomically
}

// Call-site: Execute call tree without yields
var result = sync SomeMethod();

// Creates a "sync scope" - transitive through all calls
// Until execution returns to this point
```

**When to use `sync`:**
- Lock-holding code
- Hardware interaction timing
- Interop with sync-expecting native code
- Performance-critical sections (opt-out of overhead)
- Atomic operations from caller's perspective

**Async/await compatibility:**
- `async`/`await` keywords kept for .NET compatibility
- `async` becomes documentation/hint ("yields expected here")
- `await` becomes explicit yield point hint
- But yields can happen elsewhere too - these aren't required

### Discovery 1: Two Runtime-Async Approaches (Historical Context)

**.NET's async/await today (Roslyn-generated):**
- Roslyn generates state machine structs
- Compiler decides what state to capture
- Resume point is implicit (state integer → switch case)

**The .NET team experimented with TWO runtime-async approaches:**

| Aspect | JIT Approach (.NET 10) | Unwinder Approach (.NET 9 experiment) |
|--------|------------------------|---------------------------------------|
| **What it captures** | State machine (like Roslyn, but JIT-generated) | Actual stack frames |
| **When it can capture** | Only at `await` points | At **any safe point** |
| **Byref support** | ❌ No (heap-allocated state) | ✅ Yes (real stack frames) |
| **Generics** | ✅ Yes | ❌ Not yet (engineering gap) |
| **Exception handling** | ✅ Yes | ❌ Not yet (engineering gap) |
| **Status** | Being productized for .NET 10 | Experiment concluded, not productized |
| **Location** | `dotnet/runtime` | `dotnet/runtimelab` branch `feature/async2-experiment` |

**Why the Unwinder matters more for DOTNExT:**

The JIT approach is designed for **async/await replacement** - it only captures at await points because that's all async needs. It's "better async" but still just async.

The Unwinder approach captures **real execution state at any safe point**. This enables:

```
JIT Approach:                    Unwinder Approach:

void Foo() {                     void Foo() {
    DoA();                           DoA();        ← Can capture here
    DoB();                           DoB();        ← Can capture here
    await X();  ← Capture here       await X();    ← Can capture here
    DoC();                           DoC();        ← Can capture here
}                                }
```

**For DOTNExT's goals:**

| Goal | JIT Runtime-Async | Unwinder |
|------|-------------------|----------|
| Checkpoint at await | ✅ Yes | ✅ Yes |
| Checkpoint anywhere (safe point) | ❌ No | ✅ Yes |
| BEAM-like preemption | ❌ No | ✅ Yes |
| Process image persistence | ❌ Partial | ✅ Full |
| Handle byrefs/Span | ❌ No | ✅ Yes |
| Execution Pathways | ❌ Limited | ✅ Full |

**The key insight:** The Unwinder's missing features (generics, EH) are **engineering gaps** that can be filled. The JIT approach's limitations (await-only, no byrefs) are **architectural** - no amount of engineering can change them.

**DOTNExT stays on .NET 9** because the Unwinder experiment is based on .NET 9, making it the better starting point for our R&D.

> **For detailed analysis:** See `DOTNExT-Unwinder-Async2-Analysis.md`

### Discovery 2: Unified Safe Points

**The insight:** Three different runtime concerns all need the same thing:

| Concern | What It Needs |
|---------|---------------|
| **Garbage Collection** | Know where all references are; consistent state |
| **Preemptive Scheduling** | Clean suspension point; resumable context |
| **Checkpointing** | Serializable state; reference locations known |

**The realization:** These ARE the same requirement. The JIT already computes this for GC (GC Info). We're not inventing new infrastructure - we're reusing what exists.

**GC safe points = Preemption points = Checkpoint points**

### Discovery 3: GC as the Secret Weapon

**The problem:** How do you serialize "any object" without requiring type annotations?

**The answer:** The GC already knows:
- Every managed object's location
- Every reference field (CGCDesc describes layout)
- The complete object graph from any root

**Engrams don't need special markers.** GC-powered extraction can capture ANY object graph. This bypasses the "everything must be VCOM" constraint.

### Discovery 4: Engrams Redefined

**Old definition:** "A memory package with UUID identity" (required type marking)

**New definition:** "A bounded extraction from a larger graph"

The key shift: **The boundary defines an Engram, not the content type.**

An Engram has **layers** (like maps over the same territory):
- **Code/Types layer** - Type definitions, source
- **Binaries layer** - Cached compiled code
- **Execution layer** - Tasklets, frames, registers
- **Objects layer** - Instance state, references
- **Topology layer** - Where in distributed space

**Process Image = Unbounded Engram** (boundary = everything)
**Async+ state = Bounded Engram** (boundary = reachable from state machine)

---

## Part 3: Derivation Chains (The WHY)

### Derivation A: Why Does BEAM Scale Better Than CLR?

**Chain:**
1. What limits concurrency? → OS threads cost ~1MB each
2. 10,000 threads = 10GB memory, plus kernel scheduling overhead
3. BEAM processes are VM-managed, ~2KB each → 1M processes = ~2GB
4. BEAM scheduling is VM-level, no kernel involvement
5. **Conclusion:** BEAM scales because processes are cheap. CLR doesn't because threads are expensive.

**For DOTNExT:** Tasklets could be our "lightweight execution context". But we still lack per-process GC (isolation).

### Derivation B: Why Per-Process GC Matters

**Chain:**
1. CLR GC pauses ALL managed threads (Stop-The-World)
2. In distributed system: Node A → Node B, if B is in 50ms GC pause...
3. Node A times out, thinks B is dead → cascading failure
4. BEAM: Each process has own small heap, GC runs per-process
5. Other processes unaffected, small heap = fast GC (~1ms)
6. **Conclusion:** Per-process GC eliminates global pauses, essential for distributed reliability.

**For DOTNExT:** Current CLR GC is a limitation. Long-term: per-region GC research. Short-term: accept and design around it.

### Derivation C: Why Safe Points Converge

**Chain:**
1. GC needs: known reference locations, consistent state, pause/resume
2. Preemption needs: clean suspension, resumable context, fair scheduling
3. Checkpointing needs: serializable state, reference locations, resume capability
4. **All three need the same thing!**
5. JIT already emits GC Info (reference locations at safe points)
6. **Conclusion:** We're not inventing - we're reusing JIT output for new purposes.

### Derivation D: The VCOM Problem → GC Solution

**Chain:**
1. Async+ state machine has reference fields that must survive restart
2. Original assumption: All objects must be VCOM (grain-backed with UUID)
3. Problem: System.String? List<T>? Third-party libs can't be VCOM
4. Insight: GC already tracks every managed object and reference field
5. Solution: Walk object graph from roots, serialize everything reachable
6. **Conclusion:** VCOM is enhancement (adds UUID identity), not requirement. GC enables serialization of ANY object.

### Derivation E: Why Cross-Node Composition Differs From RPC

**Chain:**
1. RPC: Node A calls Node B's API → B executes → B returns data
2. Code stays on B, only data moves
3. Engram loading: A discovers capability → loads bounding Engram → code/execution/objects all come to A
4. A executes locally with assembled pieces
5. **RPC = runtime dependency. Engram loading = assembly-time dependency.**
6. Once loaded, A is self-sufficient (can go offline)
7. **Conclusion:** We're composing computation from distributed sources, not calling remote services.

---

## Part 4: Challenging Questions

**Don't skip these.** If you can't answer with reasoning, you don't understand.

### Q1: Why can't we just serialize everything with standard serializers?

**Think about:**
- What can't be serialized? (file handles, network sockets, native pointers)
- What shouldn't be serialized? (references to singleton services)
- How do serializers know the object graph? (reflection, attributes)
- How does GC know the object graph? (CGCDesc, always complete)

### Q2: Why is "nodes as centroids" more than just a metaphor?

**Think about:**
- What does a node HAVE vs what does it KNOW ABOUT?
- How does this affect caching strategy?
- What happens when you load something from far away?
- Why can no node have "everything"?

### Q3: If safe points already exist for GC, what's the actual work for checkpointing?

**Think about:**
- What does GC Info describe? (reference locations)
- What else do you need to checkpoint? (register values, execution point)
- Does the JIT emit this? (Unwind Info gives frame layout)
- What's MISSING that we'd need to add?

### Q4: Why is Unwinder-based capture cleaner than Roslyn codegen or JIT runtime-async?

**Think about:**
- What does Roslyn's state machine capture? (compiler-selected fields)
- What does JIT runtime-async capture? (same as Roslyn, but JIT-generated)
- What does the Unwinder capture? (complete actual stack frame)
- Which can capture at any safe point? (only Unwinder)
- Which can handle byrefs? (only Unwinder)
- Why does this matter for BEAM-like preemption?

### Q5: What happens when an Engram references a VCOM object that no longer exists?

**Think about:**
- How are VCOM objects identified? (UUID)
- What does "load" mean for an external reference? (resolution)
- What are the options? (null, exception, lazy proxy, resurrection)
- Is this different from any distributed system's stale reference problem?

---

## Part 5: Generation Challenges

**Can you solve problems you haven't seen?** These test whether you can apply the concepts.

### Challenge 1: Design Lazy External Reference Loading

**Problem:** An Engram contains a reference to a VCOM object that isn't in the Engram (external reference). How do you handle this?

**Expected approach:**
- Proxy object holds UUID
- On first access, triggers VNS lookup
- VNS → VCOM resolution → grain activation if needed
- Replace proxy with real object
- Handle resolution failure gracefully

### Challenge 2: Type Version Mismatch

**Problem:** You load an Engram from another node. The type definition has different fields than your local version. How do you handle this?

**Think about:**
- What metadata is in the Engram? (type version/schema)
- What are your options? (field mapping, migration, rejection)
- How does Orleans handle this? (version vectors, schema evolution)
- What's the minimal safe approach?

### Challenge 3: AI-Object Forking

**Problem:** An AI-Object wants to create a copy of itself with divergent state. How?

**Expected approach:**
- Extract Engram with AI-Object as root
- Generate new UUID for copy
- Load Engram into same or different node
- Copy is now independent (shares type, different identity)
- Could share code layer, differ in objects/execution layer

### Challenge 4: Distributed Debugging

**Problem:** You're debugging an issue where objects came from 3 different nodes. How does this architecture help or hinder?

**Think about:**
- What does the topology layer record? (provenance)
- What does the execution layer contain? (where was it running?)
- What's lost if topology layer is incomplete?
- How do time-based issues manifest? (pieces from different times)

---

## Part 6: The Research Agenda

### Our Platform Decision: .NET 9 + Unwinder

**DOTNExT stays on .NET 9** for the following reasons:

1. The Unwinder experiment (`feature/async2-experiment`) is based on .NET 9
2. The Unwinder approach (any safe point capture, byref support) is what we need
3. The JIT runtime-async (.NET 10) doesn't provide what we need for BEAM-like goals
4. Porting Unwinder to .NET 10 would require rebasing work; starting from .NET 9 is easier
5. .NET 9 support continues until May 2026 - sufficient runway for R&D

### What We're Investigating

1. **Unwinder Mechanism** - How does `feature/async2-experiment` capture stack frames?
2. **Generics Support** - What's needed to add generics to the Unwinder? (engineering gap)
3. **Exception Handling** - What's needed for EH across Tasklet boundaries? (engineering gap)
4. **Safe Point Hooks** - Can we trigger checkpoint at any safe point (not just await)?
5. **Reduction Counting** - Can JIT insert decrement at safe points for preemption?
6. **GC-Powered Extraction** - Can we walk from arbitrary roots using GC primitives?
7. **Process Image Format** - What's the minimal format for checkpoint/restore?

### What We Need To Build

1. **Study Unwinder Code** - Understand the `feature/async2-experiment` implementation
2. **Port/Adapt Unwinder** - Integrate Unwinder mechanisms into DOTNExT fork
3. **Add Generics Support** - Capture/restore generic dictionary pointers in Tasklets
4. **Add EH Support** - Hybrid stack walker for real frames + Tasklet chains
5. **Checkpoint Prototype** - Single-threaded, minimal case using Unwinder capture
6. **Engram Extraction** - GC-based graph serialization
7. **Engram Loading** - Deserialization with address translation

### Open Architectural Questions

| Question | Options | Trade-offs |
|----------|---------|------------|
| Complete Unwinder? | Full implementation vs selective reuse | Effort vs capability |
| Generics approach | Implement fully vs restrict usage | Compatibility vs simplicity |
| EH approach | Full hybrid walker vs restrictions | Completeness vs complexity |
| Process Image composition | Monolithic vs Engram collection | Simplicity vs composability |
| VCOM in Engrams | UUID reference vs inline | Distribution vs completeness |
| Tasklet format | Native struct vs portable | Performance vs cross-platform |

> **For detailed Unwinder analysis:** See `DOTNExT-Unwinder-Async2-Analysis.md`

---

## Part 7: CLR Internals You Need To Know

### GC Infrastructure

- **CGCDesc** - Describes reference field layout per type (negative offset from MethodTable)
- **GC Info** - JIT-emitted data describing reference locations at each safe point
- **GC Heap Walk** - `GCHeapWalk` callback mechanism for enumerating all objects
- **GC Roots** - Static fields, stack locals, handles

### JIT Infrastructure

- **Safe Points** - Locations where GC can pause execution (call sites, back edges)
- **Unwind Info** - Frame layout for stack unwinding (register saves, frame size)
- **Method Tokens** - Stable identifiers for methods (survive JIT)

### Unwinder/Tasklet Specifics

- **Tasklet** - Captured stack frame (method token, IP, locals, registers)
- **Tasklet Chain** - Linked list representing suspended call stack
- **Unwinder** - Mechanism that walks stack, creates Tasklets from real frames
- **Suspension** - At safe points, unwind stack into Tasklet chain
- **Resumption** - Reconstruct stack from Tasklets, jump to saved IP
- **Byref Preservation** - Stack frames captured intact, byrefs remain valid
- **Source** - `dotnet/runtimelab` branch `feature/async2-experiment`

### Key Source Locations

```
src/runtime/src/coreclr/gc/           - GC implementation
src/runtime/src/coreclr/jit/          - JIT compiler
src/runtime/src/coreclr/vm/           - CLR VM (threads, execution engine)
src/runtime/src/libraries/System.Private.CoreLib/  - Tasklet types
```

---

## Part 8: The BEAM Blueprint

We study BEAM not to copy it, but to understand what's possible.

### What BEAM Got Right

| Feature | BEAM | CLR Current | DOTNExT Goal |
|---------|------|-------------|--------------|
| Process model | 2KB lightweight | 1MB threads | Tasklet-based |
| GC | Per-process | Global STW | Per-region (future) |
| Scheduling | Reduction counting | OS preemptive | JIT-inserted checks |
| Distribution | Native VM feature | Library (Orleans) | Evolving toward native |
| Hot code swap | Native | Not native | Via VCOM |

### Why BEAM Patterns at Managed Layer First

We can't rewrite CLR GC tomorrow. But we can:
1. Implement BEAM patterns at NewOrleans/VCOM layer (logical isolation)
2. Prove the patterns work at scale
3. Gradually lower proven patterns into runtime
4. DOTNExT becomes "distributed VM" over time

---

## Part 9: How To Know You Understand

### Level 1: Facts
Can you recall what a Tasklet is, what safe points are, what an Engram is?

### Level 2: Relationships
Can you explain how GC knowledge enables Engram extraction without type marking?

### Level 3: Implications
Can you predict what happens if CLR GC remains global (no per-process) for distributed systems?

### Level 4: Generation
Can you design a solution to "lazy external reference loading" that you haven't seen before?

**If you can do all four, you're ready to contribute to this research.**

---

## Part 10: Key Files To Read (If Needed)

**This document should be sufficient for most work.** But if you need deeper detail:

| Topic | Document | When To Read |
|-------|----------|--------------|
| **Unwinder analysis** | `DOTNExT-Unwinder-Async2-Analysis.md` | **ESSENTIAL** - JIT vs Unwinder, why Unwinder matters |
| Runtime-Async internals | `DOTNExT-Runtime-Async-Research.md` | Tasklet structure details |
| Safe point design | `DOTNExT-Unified-SafePoints.md` | Implementing checkpoint triggers |
| Process image format | `DOTNExT-Process-Image-Persistence.md` | Implementing checkpoint/restore |
| Execution Pathways | `DOTNExT-Execution-Pathways.md` | BEAM-like execution model on Tasklets |
| Engram structure | `DOTNExT-Engrams-Revised.md` | Implementing extraction |
| BEAM comparison | `Erlang-BEAM-Architecture-Reference.md` | Understanding long-term direction |
| Louis's vision | `Vision-Engrams-Cyberspace-Verbatim.md` | Understanding the WHY |

---

## Summary: The Core Ideas

1. **Async was the doorway, not the destination** - Async+ led us to Unwinder; now we see beyond async
2. **We study Unwinder for techniques, not adoption** - Extract principles for a universal execution model
3. **DOTNExT may create its own constructs** - Not necessarily "Tasklets"; our own design for our goals
4. **Universal execution capture is the goal** - Everything runs as capturable, inspectable, manipulable state
5. **Safe points become control points** - AI/runtime can intervene at any safe point, not just await
6. **The semantic inversion: `sync` is the new exception** - Everything yields by default; `sync` marks non-yielding code
7. **`async`/`await` become hints, not mechanisms** - Kept for compatibility; yields happen at any safe point anyway
8. **Trade speed for capabilities** - AI is the bottleneck; runtime overhead buys intelligent control
9. **DOTNExT stays on .NET 9** - Unwinder experiment is .NET 9 based, better starting point
10. **GC is the secret weapon** - It already knows the complete object graph
11. **Execution becomes first-class data** - Fork, migrate, compare, rollback, speculate
12. **"Slow but Smart is the new Speed"** - AI intelligence leveraging rich runtime capabilities creates value

**If you understand WHY each of these is true, you can contribute to the research.**

---

*This document primes an AI for DOTNExT runtime R&D. It combines context, derivations, challenges, and generation tests. Understanding is in the reasoning, not the facts.*

*Version 1.3 - 2025-12-10 - Added semantic inversion: `sync` is the new exception; async/await become hints; detailed sync keyword semantics*

*Version 1.2 - 2025-12-10 - Clarified: async was doorway not destination; we study Unwinder techniques for universal execution model, not async replacement; DOTNExT may develop its own constructs*

*Version 1.1 - 2025-12-10 - Updated to distinguish JIT vs Unwinder approaches and clarify .NET 9 direction*
