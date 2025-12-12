# Erlang/BEAM: The Distributed VM Blueprint

> **Document Type:** Architectural Reference (Non-.NET)
> **Version:** 1.1
> **Date:** 2025-12-08 (Updated with CLR comparison)
> **Purpose:** Study BEAM architecture for DOTNExT's evolution into a distributed VM
> **Status:** STRATEGIC - Long-term architectural direction

---

## Executive Summary

Erlang/BEAM is the **only widely-used VM with built-in distribution** that operates transparently at scale. While DOTNExT currently builds distribution at the managed layer (NewOrleans/VCOM), the long-term vision is to **lower these capabilities into the runtime itself**.

BEAM got several things fundamentally right:
1. **Lightweight isolated processes** with per-process GC
2. **Location-transparent message passing**
3. **Preemptive scheduling** without OS thread overhead
4. **Fault tolerance** as a first-class concern (supervision trees)
5. **Hot code swapping** without system restart

DOTNExT's evolution should study and selectively adopt these patterns.

---

## 1. What is BEAM?

BEAM is the virtual machine that executes code in the Erlang Runtime System (ERTS). Key characteristics:

- **Register machine** - Instructions operate on named registers
- **One OS process** - Entire VM runs as single OS process
- **One thread per core** - Schedulers run on OS threads
- **Millions of processes** - Erlang processes are VM-level, not OS-level

From [The BEAM Book](https://blog.stenmans.org/theBeamBook/):
> "BEAM is just the virtual machine and it has no notion of processes, ports, ETS tables, and so on. It merely executes instructions."

### Historical Context

Erlang was developed at Ericsson in 1986 for telephone exchanges:
- Requirement: **Never fail** (99.9999999% reliability achieved - "nine nines")
- Handle hundreds of thousands of concurrent users
- Hot-swap code without downtime
- Distributed across physical machines

These requirements drove the architecture decisions that make BEAM unique.

---

## 2. The Process Model

### 2.1 Lightweight Processes

Erlang processes are **not** OS processes or threads:

| Aspect | OS Thread | Erlang Process |
|--------|-----------|----------------|
| Memory | ~1 MB stack | ~2 KB initial |
| Creation | Slow (syscall) | Fast (VM allocation) |
| Limit | Thousands | Millions |
| Scheduling | OS kernel | BEAM scheduler |
| Isolation | Shared memory | Fully isolated |

From [Erlang Solutions](https://www.erlang-solutions.com/blog/the-beam-erlangs-virtual-machine/):
> "Even if you are running an Erlang system of over one million processes, it is still only one OS process and one thread per core."

### 2.2 Process Isolation

Each process has **completely private memory**:
- Private heap
- Private stack
- Private mailbox
- Process Control Block (PCB)

**No shared memory between processes.** Communication only via message passing.

From [Medium](https://medium.com/flatiron-labs/elixir-and-the-beam-how-concurrency-really-works-3cc151cddd61):
> "They can only communicate via messages. Erlang programs do not need locking or protected sections."

### 2.3 Per-Process Garbage Collection

This is **critical for latency**:

- GC runs **per-process**, not globally
- No "stop the world" pauses
- Small processes = fast GC
- GC can run **in parallel** with other processes

From [HappiHacking](https://www.happihacking.com/blog/posts/2024/designing_concurrency/):
> "The BEAM never stops the world (STW). All threads of execution have fully separated memory pages. The garbage collector always runs on each process subset."

**Relevance to DOTNExT:** The CLR has a global GC with STW pauses. Per-object/per-region GC would be a major runtime enhancement.

---

## 3. Scheduling

### 3.1 Preemptive on Cooperative

BEAM uses **preemptive scheduling at the Erlang level** built on **cooperative scheduling at the C level**.

The mechanism: **Reduction counting**
- Each process has a "reduction" counter
- Increments with each function call
- After ~2000 reductions, scheduler preempts
- No process can monopolize a scheduler

From [BEAM Book Scheduling Chapter](https://github.com/happi/theBeamBook/blob/master/chapters/scheduling.asciidoc):
> "A process can only be suspended at certain points of the execution, such as at a receive or a function call."

### 3.2 Scheduler Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│ BEAM VM (Single OS Process)                                     │
├─────────────────────────────────────────────────────────────────┤
│  Scheduler 1      Scheduler 2      Scheduler 3      Scheduler N │
│  (OS Thread)      (OS Thread)      (OS Thread)      (OS Thread) │
│      │                │                │                │       │
│  ┌───┴───┐        ┌───┴───┐        ┌───┴───┐        ┌───┴───┐   │
│  │Run Q 1│        │Run Q 2│        │Run Q 3│        │Run Q N│   │
│  │P P P P│        │P P P P│        │P P P P│        │P P P P│   │
│  └───────┘        └───────┘        └───────┘        └───────┘   │
└─────────────────────────────────────────────────────────────────┘
```

- One scheduler per core (configurable)
- Each scheduler has its own run queue
- Work stealing between schedulers
- No kernel-level scheduling overhead

**Relevance to DOTNExT:** .NET's ThreadPool is similar but threads are heavier. Lowering to lightweight process model would be significant.

---

## 4. Distribution & Location Transparency

### 4.1 Transparent Message Passing

The killer feature for distributed systems:

```erlang
% Same code works locally or remotely
Pid ! Message

% Pid can be:
% - Local process on same node
% - Remote process on another node
% - Doesn't matter, same syntax
```

From [Erlang Documentation](https://www.erlang.org/doc/system/distributed.html):
> "Message passing between processes at different nodes, as well as links and monitors, are transparent when pids are used."

### 4.2 Node Clustering

```erlang
% Nodes connect automatically on first reference
spawn('other_node@host', Module, Function, Args)

% Or explicitly
net_adm:ping('other_node@host')
```

Erlang Port Mapper Daemon (epmd) handles node discovery automatically.

### 4.3 What Makes This Work

1. **Process IDs are location-encoded** - A PID carries node information
2. **Message semantics are preserved** - Remote messages work like local
3. **Failure detection is built-in** - Monitors/links work across nodes
4. **No shared state assumption** - Distribution is natural

**Relevance to DOTNExT:** This is what NewOrleans provides at the managed layer. The long-term goal is to lower this into the runtime - making .NET itself a distributed VM.

---

## 5. Fault Tolerance

### 5.1 "Let It Crash" Philosophy

Instead of defensive programming everywhere:
- Processes are expected to crash
- Supervisors restart them
- State is recovered from known-good state

### 5.2 Supervision Trees

```
          ┌─────────────┐
          │  Supervisor │
          └──────┬──────┘
       ┌─────────┼─────────┐
       │         │         │
   ┌───┴───┐ ┌───┴───┐ ┌───┴───┐
   │Worker │ │Worker │ │  Sub  │
   │   A   │ │   B   │ │  Sup  │
   └───────┘ └───────┘ └───┬───┘
                      ┌────┼────┐
                      │    │    │
                  ┌───┴┐ ┌─┴─┐ ┌┴───┐
                  │ W1 │ │W2 │ │ W3 │
                  └────┘ └───┘ └────┘
```

Restart strategies:
- **one_for_one**: Restart only crashed child
- **one_for_all**: Restart all children
- **rest_for_one**: Restart crashed child and all after it

**Relevance to DOTNExT:** VAYRON's supervision model should mirror this. NewOrleans grain activation is related but different.

### 5.3 Hot Code Swapping

```erlang
% Load new module version while system runs
code:load_file(my_module).

% Processes automatically use new code on next call
```

**Relevance to DOTNExT:** This aligns with VCOM's "code as first-class" - objects can evolve their code at runtime.

---

## 6. OTP: The Platform

OTP (Open Telecom Platform) is not just libraries - it's a **design methodology**:

### 6.1 Behaviours (Design Patterns)

| Behaviour | Purpose |
|-----------|---------|
| `gen_server` | Generic client-server |
| `gen_statem` | Finite state machine |
| `supervisor` | Fault tolerance tree |
| `application` | Application structure |
| `gen_event` | Event handling |

### 6.2 Applications as Units

OTP applications are:
- Self-contained units of functionality
- Have supervision trees
- Can be started/stopped independently
- Have defined dependencies

**Relevance to DOTNExT:** This maps to VCOM types + VAYRON Kernel services.

---

## 7. What BEAM Got Right (For Our Purposes)

| BEAM Feature | Why It Matters | DOTNExT Relevance |
|--------------|----------------|-------------------|
| **Lightweight processes** | Million+ concurrent entities | VCOM objects need this scale |
| **Per-process GC** | No global pauses | Critical for real-time AI systems |
| **Location transparency** | Same code local/remote | VNS resolution, VCOM references |
| **Preemptive scheduling** | Fair execution | AI-Objects shouldn't starve |
| **Supervision trees** | Fault tolerance | VAYRON reliability |
| **Hot code swapping** | Zero-downtime updates | Code-as-first-class in VCOM |
| **Message passing only** | No shared state bugs | Already Orleans model |
| **Built-in distribution** | Native clustering | Long-term DOTNExT goal |

---

## 8. Mapping to DOTNExT/VAYRON

### 8.1 Current State (Managed Layer)

```
VAYRON Today:
┌─────────────────────────────────────────────────────────────┐
│  VCOM Objects (C# objects with UUID)                        │
├─────────────────────────────────────────────────────────────┤
│  NewOrleans Grains (actor-like virtual actors)              │
├─────────────────────────────────────────────────────────────┤
│  .NET Runtime (CLR) - No native distribution                │
└─────────────────────────────────────────────────────────────┘
```

Distribution handled at NewOrleans layer, not runtime.

### 8.2 Long-Term Vision (Runtime Layer)

```
DOTNExT Future:
┌─────────────────────────────────────────────────────────────┐
│  VCOM Objects (native runtime objects)                      │
├─────────────────────────────────────────────────────────────┤
│  DOTNExT Runtime (CLR fork with BEAM-like features)         │
│  - Lightweight process model                                │
│  - Per-process/per-object GC                                │
│  - Native location-transparent messaging                    │
│  - Built-in distribution primitives                         │
│  - Preemptive scheduling at object level                    │
└─────────────────────────────────────────────────────────────┘
```

### 8.3 Evolution Path

1. **Phase 1 (Current):** Distribution at managed layer (NewOrleans)
2. **Phase 2:** Optimize hot paths, prove patterns
3. **Phase 3:** Lower proven patterns into DOTNExT runtime
4. **Phase 4:** DOTNExT becomes a "distributed VM" like BEAM

---

## 9. Specific BEAM Concepts to Study

### 9.1 For Immediate Value (NewOrleans/VCOM)

- **Supervision strategies** - Apply to VCOM lifecycle
- **Message passing patterns** - VNS/VCOM communication
- **Process registry** - Inspiration for VNS

### 9.2 For Medium-Term (Runtime Optimization)

- **Reduction counting** - Fair scheduling without OS overhead
- **Selective receive** - Pattern matching on message queues
- **Process links/monitors** - Failure propagation

### 9.3 For Long-Term (Runtime Lowering)

- **Process heap isolation** - Per-object GC regions
- **Distribution protocol** - Native cluster communication
- **Hot code loading** - Runtime code replacement

---

## 10. Resources

### Official Documentation
- [Erlang Documentation](https://www.erlang.org/doc/system/distributed.html)
- [A Brief BEAM Primer](https://www.erlang.org/blog/a-brief-beam-primer/)
- [Message Passing in Erlang](https://www.erlang.org/blog/message-passing/)

### Deep Dives
- [The BEAM Book](https://blog.stenmans.org/theBeamBook/) - Comprehensive VM internals
- [AOSA: The BEAM](https://aosabook.org/en/v2/erlang.html) - Architecture overview
- [Hitchhiker's Tour of the BEAM](http://www.erlang-factory.com/upload/presentations/708/HitchhikersTouroftheBEAM.pdf)

### Comparisons
- [BEAM vs JVM](https://www.erlang-solutions.com/blog/optimising-for-concurrency-comparing-and-contrasting-the-beam-and-jvm-virtual-machines/)
- [InfoQ: BEAM Resiliency](https://www.infoq.com/presentations/resilience-beam-erlang-otp/)

### Implementation Details
- [Stack Overflow: What kind of VM is BEAM?](https://stackoverflow.com/questions/16779162/what-kind-of-virtual-machine-is-beam-the-erlang-vm)
- [Process Isolation Explained](https://medium.com/flatiron-labs/elixir-and-the-beam-how-concurrency-really-works-3cc151cddd61)
- [Scheduling Chapter](https://github.com/happi/theBeamBook/blob/master/chapters/scheduling.asciidoc)

---

## 11. Key Takeaways

1. **BEAM proves it's possible** - A VM can be natively distributed at scale

2. **Process isolation is key** - Per-process memory + GC enables both distribution and latency guarantees

3. **Location transparency simplifies everything** - Same code works locally and remotely

4. **Fault tolerance must be built-in** - Not an afterthought, a design principle

5. **This is our long-term goal** - DOTNExT should evolve toward these capabilities

---

*This document captures BEAM architecture patterns for DOTNExT's evolution into a distributed VM. The immediate work is at the managed layer (NewOrleans/VCOM), but understanding BEAM guides the long-term runtime direction.*

*Version 1.1 - 2025-12-08 (Added BEAM vs CLR comparison, distribution ownership analysis)*

---

## 12. BEAM vs CLR: Detailed Comparison

> **Added:** 2025-12-08 research session

### 12.1 Where Do BEAM's Strengths Come From?

**Critical question:** Is it the Erlang language or the BEAM VM that provides distributed computing capabilities?

**Answer: Mostly BEAM (the VM).**

| Capability | Provided By | Language or VM? |
|------------|-------------|-----------------|
| Lightweight processes | BEAM | VM - any BEAM language gets this |
| Per-process GC | BEAM | VM - architectural decision |
| Preemptive scheduling | BEAM | VM - reduction counting in VM |
| Message passing | BEAM | VM - primitive operation |
| Location transparency | BEAM | VM - PIDs encode node info |
| Process isolation | BEAM | VM - memory model |
| Hot code swapping | BEAM | VM - module system |
| Supervision trees | OTP (Erlang libs) | Libraries - could be reimplemented |
| Fault tolerance patterns | OTP | Design patterns, not VM |

### 12.2 What Any BEAM Language Gets "For Free"

Elixir, Gleam, LFE (Lisp Flavored Erlang), Clojerl, etc. automatically inherit:

1. **Process model** - `spawn` creates a BEAM process regardless of language
2. **Message passing** - `send`/`receive` work identically
3. **Distribution** - Same node clustering, same transparent remote messaging
4. **Scheduling** - Same preemptive, fair scheduling
5. **GC characteristics** - Same per-process collection
6. **Fault isolation** - Process crashes don't propagate

**OTP behaviors** (gen_server, supervisor, etc.) are Erlang libraries that Elixir reimplemented as GenServer, Supervisor, etc. The underlying primitives are BEAM-provided.

### 12.3 BEAM Distribution: Language vs Runtime

**BEAM's distribution IS built into the VM:**

```erlang
Pid = spawn(fun() -> receive X -> X * 2 end end).  % Local
Pid ! 42.  % Send message

RemotePid = spawn('other@host', fun() -> ... end).  % Remote
RemotePid ! 42.  % IDENTICAL SYNTAX - VM handles it
```

PID structure encodes location:
```
<Node.ProcessId.Serial.Creation>
- Node: Which BEAM node (local = 0, remote = node identifier)
- ProcessId: Local process number
- Serial/Creation: Disambiguation
```

**The model is message-passing, but syntax is location-transparent.**

---

## 13. Why Each BEAM Feature Matters for Distribution

> **Added:** 2025-12-08 research session

### 13.1 Lightweight Processes

**The scaling argument:**

| Scenario | OS Threads | BEAM Processes |
|----------|------------|----------------|
| 10,000 concurrent entities | ~10GB memory | ~20MB memory |
| 100,000 entities | Impractical | ~200MB |
| 1,000,000 entities | Impossible | ~2GB |

**Why it matters:** Distributed systems want one entity per "thing" (connection, session, actor). Lightweight processes enable millions of entities.

**For DOTNExT:** If every VCOM object is backed by an entity, lightweight processes mean millions of live objects across a cluster.

**Is it required?** No - Orleans proves distribution works with heavier abstractions. It's about **efficiency and scale**, not capability.

### 13.2 Per-Process GC

**The latency argument:**

| GC Model | What Happens | Latency Impact |
|----------|--------------|----------------|
| Global GC (CLR) | All threads pause | 10-100ms+ spikes |
| Per-process GC (BEAM) | Only that process pauses | ~1ms isolated pauses |

**Why it matters:** Node A sends message to Node B. If Node B is in 50ms GC pause, that delay cascades through the system.

**For DOTNExT:** Per-object/per-region GC would be a major enhancement for real-time distributed systems.

### 13.3 Process Isolation

**The fault tolerance argument:**

- BEAM: Process A crashes → A's memory reclaimed → Process B unaffected
- CLR: Thread A corrupts shared memory → Thread B may read garbage → Cascade

**Why it matters:** "Let it crash" requires crashes to be **contained**.

**Would Copy-on-Write help?** Partially - COW helps message passing efficiency but doesn't prevent shared memory corruption.

### 13.4 Preemptive Scheduling (Reduction Counting)

**The problem:** Cooperative multitasking requires code to yield voluntarily. Infinite loops or long computations starve everything.

**BEAM's solution:**
```
Every process has "reduction counter"
Every function call increments it
After ~2000-4000 reductions, scheduler forcibly suspends
No process can monopolize
```

**Why it matters:** Without preemption, one misbehaving actor can freeze an entire node, triggering cascading distributed failures.

---

## 14. BEAM vs CLR Architecture Comparison

> **Added:** 2025-12-08 research session

### 14.1 Feature Comparison

| Feature | BEAM | CLR | DOTNExT Target |
|---------|------|-----|----------------|
| **Process model** | Lightweight (2KB) | OS threads (1MB) | Lightweight via unified safe points |
| **GC** | Per-process | Global (STW) | Per-object/region (future) |
| **Scheduling** | Reduction-based preemptive | OS preemptive | Reduction-based via JIT |
| **Isolation** | Full (no shared memory) | Partial (shared heap) | Full via VCOM |
| **Distribution** | Native VM feature | Library (Orleans) | Library → Runtime (evolution) |
| **Hot code swap** | Native | Not native | Via VCOM code-as-first-class |
| **Message passing** | Only mechanism | Optional | VCOM model |

### 14.2 What Would CLR Need for BEAM-Like Features?

| BEAM Feature | CLR Challenge | DOTNExT Approach |
|--------------|---------------|------------------|
| Lightweight processes | Heavy threads | Unified safe points + cooperative scheduling |
| Per-process GC | Global GC | Long-term: region-based GC |
| Process isolation | Shared memory | VCOM: no shared state |
| Location-transparent PIDs | No equivalent | VNS + VCOM UUID |
| Preemptive at reduction level | OS preemption only | JIT-inserted reduction checks |
| Hot code swap | Not native | VCOM code-as-first-class |

### 14.3 The DOTNExT Path

**Phase 1 (Current):** BEAM-like patterns at managed layer
- NewOrleans provides actor model
- VCOM provides isolation
- VNS provides naming

**Phase 2:** Runtime-Async + Unified Safe Points
- JIT inserts reduction checks
- Preemptive scheduling without OS threads
- Process checkpointing enabled

**Phase 3:** Lower patterns into runtime
- Distribution primitives in VM
- Per-region GC exploration
- Native location transparency

---

## 15. Related Documents (Updated)

| Document | Relationship |
|----------|--------------|
| DOTNExT-Runtime-Async-Research.md | **NEW** - Enables BEAM-like suspension |
| DOTNExT-Unified-SafePoints.md | **NEW** - GC + Preemption + Checkpoint convergence |
| DOTNExT-Process-Image-Persistence.md | **NEW** - CRIU-like capabilities |
| Vision-VAYRON-Platform.md | Uses BEAM patterns at managed layer |

---
