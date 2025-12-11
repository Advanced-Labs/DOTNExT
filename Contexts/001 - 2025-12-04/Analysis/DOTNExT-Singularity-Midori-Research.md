# DOTNExT Singularity/Midori Research

> **Document Type:** Research Reference & Applicable Concepts
> **Version:** 2.0
> **Date:** 2025-12-11
> **Status:** RESEARCH - Revised after detailed analysis of applicability to hosted DOTNExT
> **Purpose:** Extract patterns, paradigms, and lessons from Singularity and Midori for DOTNExT's VOS design
> **Key Update v2.0:** Revised conclusions after recognizing DOTNExT is a hosted runtime (not bare-metal), values dynamism highly, and will implement security via VOS pluggable subsystems rather than compile-time enforcement.

---

## 1. Executive Summary

Microsoft Research developed two managed-code operating system projects:
- **Singularity** (2003-2015): Research OS with Software Isolated Processes (SIPs)
- **Midori** (evolved from Singularity): Advanced development OS with async-everywhere model

Both are highly relevant to DOTNExT's evolution toward a Virtual Operating System (VOS). This document extracts applicable concepts for DOTNExT's process model, scheduler, distribution, and security design.

**Key Sources:**
- [Microsoft Research - Singularity Project](https://www.microsoft.com/en-us/research/project/singularity/)
- [Joe Duffy's Midori Blog Series](https://joeduffyblog.com/2015/11/03/blogging-about-midori/)
- [Singularity: Rethinking the Software Stack (PDF)](https://courses.cs.washington.edu/courses/cse551/15sp/papers/singularity-osr07.pdf)

---

## 2. Singularity OS: Key Concepts

### 2.1 Software Isolated Processes (SIPs)

**What they are:** Processes whose boundaries are enforced by language safety (type system) rather than hardware (MMU/TLB).

**Key characteristics:**
- Each SIP has its own dedicated heap
- No shared writable memory between SIPs
- Statically verified type-safe code
- Run in ring 0 in kernel's address space (no context switch overhead)
- Cannot dynamically load code or modify their own structure
- Sealed at compile time - enables comprehensive static analysis

**Performance:**
- Process creation: ~388,000 CPU cycles (vs millions for traditional)
- Isolation overhead: <5% (vs 25-33% for hardware-based)
- No TLB flushes on process switch

**DOTNExT Applicability:**
| Singularity Concept | DOTNExT Application |
|---------------------|---------------------|
| SIP isolation via type safety | VCOM + type system provides logical isolation |
| Per-process heap | Consider per-pathway heap regions? |
| Sealed processes | Engrams are sealed extraction units |
| Cheap process creation | Lightweight Execution Pathways |

### 2.2 Exchange Heap & Zero-Copy IPC

**What it is:** A special heap for inter-process data transfer.

**Key characteristics:**
- All data passed between SIPs resides in exchange heap
- Only pointers are passed over channels (zero-copy)
- Linear types ensure single ownership at any time
- When process sends message, it loses pointer to that data
- Kernel garbage collects exchange heap for exited SIPs

**Invariants enforced:**
- Memory Independence: Process cannot hold reference into another process's heap
- State Isolation: Process cannot alter another process's state
- Ownership: Message ownership transfers from sender to receiver
- Exchange Heap: Contains no pointers into any process GC heap

**DOTNExT Applicability:**
| Singularity Concept | DOTNExT Application |
|---------------------|---------------------|
| Exchange heap | Shared region for inter-pathway communication |
| Zero-copy transfer | Engram transfer without serialization (same-node) |
| Linear types / ownership | Pathway ownership of captured state |
| Ownership transfer on send | Migration transfers pathway ownership |

### 2.3 Contract-Based Channels

**What they are:** Typed, bidirectional message conduits with exactly two endpoints.

**Key characteristics:**
- Channel contract = message declarations + protocol states
- Compiler statically verifies send/receive operations
- Prevents wrong-state communication at compile time
- Enables zero-copy data exchange

**Protocol states example:**
```
contract Calculator {
    message Add(int x, int y);
    message Result(int sum);

    state Ready {
        Add? -> Computing;
    }
    state Computing {
        Result! -> Ready;
    }
}
```

**DOTNExT Applicability:**
| Singularity Concept | DOTNExT Application |
|---------------------|---------------------|
| Typed channels | Inter-pathway communication contracts |
| Protocol states | Pathway interaction state machines |
| Static verification | Compile-time distributed protocol checking |
| Two-endpoint channels | Point-to-point pathway communication |

---

## 3. Midori OS: Key Concepts

### 3.1 Asynchronous Everything

**Core principle:** Synchronous blocking was flat-out disallowed. Everything was asynchronous.

**What this meant:**
- All file and network I/O
- All message passing
- All synchronization activities
- Even demand paging was disabled (kill program vs thrash)

**Implementation:**
- Promises as first-class values
- Async/await syntax (Midori adopted ~2009)
- Asynchrony explicit in type system
- Compiler knew which functions could block

**Process model:**
- Ultra-lightweight processes with single-threaded event loops
- Each process ran non-blocking "turns" until awaiting
- Linked stacks: 128 bytes initial, doubling to 8KB chunks
- Synchronous code ran on pooled kernel-managed stacks

**DOTNExT Applicability:**
| Midori Concept | DOTNExT Application |
|----------------|---------------------|
| Async everything | Our universal execution model (everything yields) |
| Sync disallowed | `sync` is the exception, not the rule |
| Lightweight processes | Execution Pathways |
| Event loop per process | Single-threaded pathway execution |
| Linked stacks | Captured execution frames (Tasklet-like) |

### 3.2 Capability-Based Security

**Core principle:** Objects as unforgeable capability tokens. No ambient authority.

**Key characteristics:**
- If software shouldn't perform operation, it never receives the token
- Type system prevents unauthorized operations at compile time
- No mutable static fields (they're ambient authority)
- Must explicitly receive capabilities (Clock, File, Network, etc.)

**Example transformation:**
```csharp
// Traditional (ambient authority):
var now = DateTime.Now;  // Global access

// Capability-based:
Clock clock;  // Must be passed in
var now = clock.Now;
```

**Capability patterns:**
- Revocation: Wrapper can invalidate after certain events
- Composition: Fine-grained capabilities combined into larger abstractions
- Remote capabilities: Most security capabilities were async (dispatched to remote processes)

**DOTNExT Applicability:**
| Midori Concept | DOTNExT Application |
|----------------|---------------------|
| Objects as capabilities | VCOM objects as capability tokens |
| No ambient authority | Pathways receive explicit capabilities |
| Compile-time security | Type system enforces capability constraints |
| Revocable capabilities | VCOM proxy invalidation |
| Remote capabilities | Distributed VCOM references |

### 3.3 Safe Concurrency

**Core principle:** No shared memory data races by construction.

**Key insight:** No two "threads" sharing address space could see the same object as mutable at the same time.

**Rules:**
- Many could read from same memory at once
- One could write
- Multiple could not write at once
- Isolated and ownership analysis in type system

**Ownership model:**
- All inputs to constructor that are isolated → isolated output
- Ownership transfer explicit in type system
- Higher-level frameworks for data partitioning

**DOTNExT Applicability:**
| Midori Concept | DOTNExT Application |
|----------------|---------------------|
| No data races by construction | VCOM actor model (single-threaded grains) |
| Ownership tracking | Pathway ownership of captured state |
| Isolated analysis | Engram extraction boundary analysis |
| Read-many/write-one | VCOM read replicas vs single writer |

### 3.4 The Error Model: Abandonment

**Two-pronged approach:**
1. **Abandonment** (fail-fast) for programming bugs
2. **Statically checked exceptions** for recoverable errors

**Abandonment characteristics:**
- Tears down entire process instantly
- No user code runs during abandonment
- Lightweight processes make this acceptable
- "Like abandoning a single thread, not a whole process"

**DOTNExT Applicability:**
| Midori Concept | DOTNExT Application |
|----------------|---------------------|
| Lightweight process abandonment | Pathway failure isolation |
| Fail-fast for bugs | Pathway termination on unrecoverable error |
| Cheap abandonment | Checkpoint/restore instead of full restart |
| Recoverable exceptions | Inter-pathway error propagation |

### 3.5 Ultra-Lightweight Processes

**Characteristics:**
- Many fine-grained processes per classical program
- Connected through strongly typed message passing
- Natural, safe, largely automatic parallelism
- Single-threaded event loops per process

**Benefits:**
- Parallelism without shared state complexity
- Isolation without hardware overhead
- Composition via message passing

**DOTNExT Applicability:**
| Midori Concept | DOTNExT Application |
|----------------|---------------------|
| Fine-grained processes | Execution Pathways |
| Message passing interfaces | VCOM grain interfaces |
| Single-threaded per process | Single-threaded pathway execution |
| Automatic parallelism | AI-controlled parallel pathway execution |

---

## 4. The Three Safeties

Midori was built on a foundation of three safeties that eliminated whole classes of bugs "by-construction":

### 4.1 Type Safety

- No invalid casts
- No type confusion attacks
- Objects are what they claim to be

### 4.2 Memory Safety

- No buffer overflows
- No use-after-free
- No dangling pointers
- GC-managed with exchange heap for IPC

### 4.3 Concurrency Safety

- No data races
- No deadlocks (no blocking!)
- Ownership model prevents aliasing hazards

**DOTNExT inherits all three from .NET**, plus adds:
- Execution safety (yieldable by default, `sync` for exceptions)
- Distribution safety (VCOM provides distributed consistency)
- Checkpoint safety (execution state always capturable)

---

## 5. Key Lessons for DOTNExT

### 5.1 Process Model Lessons

| Lesson | Application |
|--------|-------------|
| Software isolation works | Type safety + VCOM can replace hardware isolation |
| Processes should be cheap | Pathways must be lightweight (~thousands of cycles to create) |
| Per-process heap aids isolation | Consider per-pathway GC regions |
| Sealed processes enable analysis | Engrams are sealed, analyzable units |

### 5.2 IPC Lessons

| Lesson | Application |
|--------|-------------|
| Zero-copy is achievable | Exchange heap pattern for same-node communication |
| Typed channels prevent bugs | Contract-based pathway communication |
| Ownership transfer is key | Linear types / move semantics for pathway messages |
| Protocol states are valuable | State machines for distributed pathway protocols |

### 5.3 Async Lessons

| Lesson | Application |
|--------|-------------|
| Async everywhere is viable | DOTNExT's universal yieldable model |
| Type system should know about async | Already doing this |
| Sync should be explicit exception | `sync` keyword design |
| Linked stacks work | Frame capture mechanism |

### 5.4 Security Lessons

| Lesson | Application |
|--------|-------------|
| Capabilities beat ACLs | VCOM objects as capabilities |
| Eliminate ambient authority | Pathways receive explicit capabilities |
| Security in type system | Compile-time capability verification |
| Revocation matters | VCOM proxy invalidation |

### 5.5 Concurrency Lessons

| Lesson | Application |
|--------|-------------|
| Ownership prevents races | Pathway state ownership |
| Message passing for safety | VCOM grain communication |
| Single-threaded processes | Single-threaded pathway execution |
| Abandonment is acceptable | Pathway failure isolation |

---

## 6. REVISED: What Actually Applies to DOTNExT

### Critical Context: DOTNExT is NOT Bare-Metal

**Singularity and Midori were bare-metal OS projects.** They controlled the entire stack down to hardware. DOTNExT is a **hosted runtime** on Windows/Linux. This fundamentally changes what's applicable:

- We can't implement per-process heaps (GC is CLR-level)
- We can't do exchange heap (requires OS-level memory control)
- We benefit from underlying OS process isolation already
- Crash isolation is less critical (OS contains VM node crashes)

### DOTNExT Values Dynamism Highly

Singularity/Midori favored static verification and sealed processes. DOTNExT explicitly values:
- Dynamic capability granting (for AI adaptability)
- Runtime flexibility over compile-time guarantees
- Pluggable systems over baked-in mechanisms

### 6.1 From Singularity: Mostly Validation, Few Mechanisms

| Singularity Concept | First-Gen DOTNExT? | Reason |
|---------------------|-------------------|--------|
| SIPs (concept) | Yes (conceptually) | Validates lightweight process via type safety |
| Exchange Heap | **No** | Requires OS control we don't have |
| Contract Channels | **No** | Future R&D; generalized state machine question |
| Linear Types | **No** | Not clear it's our paradigm |
| Per-Process Heap | **No** | Major GC change, hosted runtime limitation |
| Manifest Capabilities | **No** | Too static for our dynamism goals |
| Sealed Processes | **No** | Conflicts with dynamism |

**What Singularity validates:** Software isolation via type safety works. Lightweight processes are achievable. The specific mechanisms don't transfer to hosted runtime.

### 6.2 From Midori: More Directly Applicable

| Midori Concept | First-Gen DOTNExT? | Reason |
|----------------|-------------------|--------|
| **Async everywhere / sync exception** | ✅ Already adopted | Core of our model |
| **Abandonment for bugs** | ✅ Yes (adapted) | Clean failure semantics, but OS gives us isolation |
| **Capabilities (dynamic)** | ✅ Via VOS security | Not compile-time; runtime pluggable system |
| **Lightweight processes** | ✅ Already adopted | Process/Pathway hierarchy |
| **Linked stacks** | Consider | Informs frame capture design |
| No mutable statics | Future | Can't enforce (.NET compat), awareness only |
| Ownership analysis | Future | Cross-Pathway sharing rules |

### 6.3 DOTNExT's Different Approach to Security

**Midori:** Capability-based security baked into type system. Compile-time enforcement. ~Zero runtime cost.

**DOTNExT:** Security as VOS pluggable subsystem. Multiple models available (CBS, RBAC, crypto, etc.). Runtime enforcement with variable cost. Optimization spectrum:

| Level | Example | Cost |
|-------|---------|------|
| Compile-time resolved | "Code X always has DateTime access" | Zero |
| Compile-time error | "Code Y lacks rights to DateTime" | Zero (prevented) |
| JIT-resolved once | "Predicate evaluated at JIT, baked in" | Near-zero |
| Runtime cached | "First check cached" | First call, then cheap |
| Runtime every check | "Dynamic predicate each time" | Full cost |

**Trade-off accepted:** 100-10000x more expensive per check than Midori. Acceptable because:
- AI is the bottleneck (security overhead negligible)
- Security can be dialed down when not needed
- Optimizations reduce many checks to zero cost

### 6.4 Crash Isolation: Different Problem Space

**Midori:** Bare-metal. Process corruption could crash the machine. Abandonment was survival.

**DOTNExT:** Hosted. OS process isolation protects us. VM Node crash is contained by OS.

**What DOTNExT needs instead:**
- Intra-node isolation (Pathway A crashes, Pathway B continues)
- Inter-node resilience (Node X dies, Node Y takes over)
- Cascading failure prevention

**Key design question:** Are we designing in ways that don't block future evolution toward powerful failure handling? Answer: Yes, because virtualized execution creates intervention points.

---

## 7. Open Questions for DOTNExT Design

Based on this research:

### Process Model
1. Is a Pathway equivalent to a Midori process, or finer-grained?
2. Should Pathways have per-pathway heap regions (like SIPs)?
3. How do explicit developer-created processes relate to implicit Pathways?

### IPC / Distribution
4. Should we implement an exchange heap for same-node zero-copy?
5. How do channel contracts translate to distributed VCOM interfaces?
6. What's the ownership model for cross-pathway data?

### Security
7. How do VCOM capabilities integrate with .NET's existing security?
8. What ambient authorities need elimination?
9. How are capabilities passed to Pathways?

### Scheduling
10. Is Midori's single-threaded event loop model right for Pathways?
11. How does this interact with AI-controlled execution?
12. What's the resource accounting model (reductions, gas, time)?

---

## 8. Related Documents

| Document | Relationship |
|----------|--------------|
| DOTNExT-Process-Model.md | Uses concepts from this research |
| DOTNExT-Scheduler-Design.md | Scheduling lessons from Midori |
| DOTNExT-Security-Model.md | Capability model from Midori |
| DOTNExT-Execution-Pathways.md | Process model implementation |

---

## 9. Sources

### Primary Sources
- [Microsoft Research - Singularity Project](https://www.microsoft.com/en-us/research/project/singularity/)
- [Singularity (operating system) - Wikipedia](https://en.wikipedia.org/wiki/Singularity_(operating_system))
- [Singularity: Rethinking the Software Stack (PDF)](https://courses.cs.washington.edu/courses/cse551/15sp/papers/singularity-osr07.pdf)
- [CS 261 Notes on Singularity](https://www.read.seas.harvard.edu/~kohler/class/cs261-f11/singularity.html)

### Joe Duffy's Midori Blog Series
- [Blogging about Midori](https://joeduffyblog.com/2015/11/03/blogging-about-midori/)
- [Asynchronous Everything](https://joeduffyblog.com/2015/11/19/asynchronous-everything/)
- [Objects as Secure Capabilities](https://joeduffyblog.com/2015/11/10/objects-as-secure-capabilities/)
- [The Error Model](https://joeduffyblog.com/2016/02/07/the-error-model/)
- [A Tale of Three Safeties](https://joeduffyblog.com/2015/11/03/a-tale-of-three-safeties/)
- [15 Years of Concurrency](https://joeduffyblog.com/2016/11/30/15-years-of-concurrency/)

### Additional Resources
- [Midori (operating system) - Wikipedia](https://en.wikipedia.org/wiki/Midori_(operating_system))
- [Language Support for Fast and Reliable Message-based Communication in Singularity (PDF)](https://www.microsoft.com/en-us/research/wp-content/uploads/2006/04/singsharp.pdf)

---

---

## 10. Key Terminology (Plain English)

### Capability

**Simple:** A key/token that lets you do something. Having the object IS the permission.

**Synonyms:** Access token, API key, file handle, ticket

```csharp
// Without capability (ambient):
var now = DateTime.Now;  // Anyone can call

// With capability:
void DoWork(IClock clock) { var now = clock.Now; }  // Must be given clock
```

### Ambient Authority

**Simple:** Stuff accessible just because you exist, without explicit permission.

**Synonyms:** Global variables, static methods with side effects, environment variables

**Examples in .NET:**
- `DateTime.Now` - time is ambient
- `File.ReadAllText(...)` - filesystem is ambient
- `Console.WriteLine(...)` - console is ambient

**Problem:** Can't control/sandbox code that has ambient access to everything.

### Abandonment

**Simple:** When a bug is detected, tear down the process instantly. No cleanup code runs. Don't try to recover.

**Why it works:** Lightweight processes make teardown cheap. Better to fail fast than corrupt state.

---

*This document researches Microsoft's Singularity and Midori OS projects for concepts applicable to DOTNExT's Virtual Operating System design. Revised to reflect DOTNExT's status as hosted runtime with dynamism priorities.*

*Version 2.0 - 2025-12-11 - Major revision: applicability analysis for hosted runtime, dynamism values, VOS security approach*

*Version 1.0 - 2025-12-10 - Initial research compilation*
