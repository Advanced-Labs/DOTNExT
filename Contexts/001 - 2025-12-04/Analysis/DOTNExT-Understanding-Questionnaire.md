# DOTNExT Understanding Questionnaire

> **Document Type:** Assessment Tool
> **Version:** 1.0
> **Date:** 2025-12-08
> **Purpose:** Test AI (or human) comprehension at multiple levels
> **Usage:** Louis can use this to evaluate if another AI instance truly understands the architecture

---

## Instructions

This questionnaire has 4 levels:
- **Level 1: Facts** - Can you recall information?
- **Level 2: Relationships** - Can you connect concepts?
- **Level 3: Implications** - Can you reason about consequences?
- **Level 4: Generation** - Can you solve novel problems?

Score each answer 0-2:
- 0 = Wrong or missing
- 1 = Partially correct
- 2 = Fully correct with reasoning

**Passing threshold:**
- Level 1: 80%+ (8/10 points)
- Level 2: 70%+ (7/10 points)
- Level 3: 60%+ (6/10 points)
- Level 4: 50%+ (5/10 points)

An AI that passes all levels truly understands. One that only passes Level 1 has memorized without comprehension.

---

## Level 1: Facts (10 points)

### Q1.1 (2 points)
**What is a Tasklet in .NET Runtime-Async?**

Expected elements:
- Captured stack frame
- Contains: locals, IP, registers
- Created when async method suspends
- Can be chained (call stack)

---

### Q1.2 (2 points)
**What three runtime concerns converge at safe points?**

Expected elements:
- Garbage Collection
- Preemptive scheduling (reduction counting)
- Checkpointing

---

### Q1.3 (2 points)
**What is an Engram in the DOTNExT/VAYRON context?**

Expected elements:
- Bounded extraction from a larger graph
- Multi-layered (code, binaries, execution, objects, topology)
- Self-describing, loadable elsewhere

---

### Q1.4 (2 points)
**What is the VNS?**

Expected elements:
- Virtual Name System
- "DNS for objects"
- Provides semantic/classical discovery in distributed cyberspace

---

### Q1.5 (2 points)
**What layers does an Engram contain?**

Expected elements (at least 4):
- Code/Types layer
- Binaries layer (cached)
- Execution layer (Tasklets, frames)
- Objects layer (state, relations)
- Topology layer (location, ownership)

---

## Level 2: Relationships (10 points)

### Q2.1 (2 points)
**How does GC knowledge relate to Engram extraction?**

Expected reasoning:
- GC already tracks all managed objects
- GC knows reference fields (CGCDesc)
- Therefore Engrams can be extracted without type marking
- GC provides the graph; Engram provides the boundary

---

### Q2.2 (2 points)
**What is the relationship between VCOM objects and non-VCOM objects in Engrams?**

Expected reasoning:
- VCOM objects have persistent UUID identity
- Non-VCOM objects are captured inline
- External refs to VCOM: stored as UUID, resolved on load
- They can coexist in the same Engram
- VCOM is enhancement, not requirement

---

### Q2.3 (2 points)
**How do Tasklets relate to Engrams?**

Expected reasoning:
- Tasklets capture execution state (a "layer")
- Tasklets reference objects (another "layer")
- An "Execution Engram" = Tasklet + referenced object graph
- Tasklets provide the "what am I doing" while objects provide "what to"

---

### Q2.4 (2 points)
**What is the relationship between Process Image and Engrams?**

Expected reasoning:
- Process Image = special case of Engram
- Boundary = "everything" (all roots, all reachable state)
- Uses same machinery (GC walk, Tasklet capture)
- Process Image is unbounded; typical Engram is bounded

---

### Q2.5 (2 points)
**How does the "centroid" model relate to caching strategy?**

Expected reasoning:
- Dense near centroid = what node HAS
- Sparse at edges = what node KNOWS ABOUT
- Caching: keep frequently accessed remote items closer to dense region
- Loading: pull into centroid, increases local density
- Discovery: search through sparse regions to find what to load

---

## Level 3: Implications (10 points)

### Q3.1 (2 points)
**If CLR GC remains global (no per-process GC), what are the implications for DOTNExT distributed systems?**

Expected reasoning:
- Latency spikes will still occur
- Must design for unpredictable pauses
- Maybe: shorter-lived objects, smaller heaps per node
- Logical isolation (VCOM/NewOrleans) compensates for lack of memory isolation
- This is a known limitation to work around, not solve immediately

---

### Q3.2 (2 points)
**What happens if you try to load an Engram with external references to VCOM objects that no longer exist?**

Expected reasoning:
- UUID resolution will fail
- Need strategy: null? exception? lazy proxy that errors on access?
- Similar to "stale reference" problem in any distributed system
- Resurrection semantics (IResurrectable) might help
- This is a design decision, not a crash

---

### Q3.3 (2 points)
**Why does "AI as nodes" enable things "AI as user" cannot?**

Expected reasoning:
- AI-as-user: Limited to prescribed APIs, external to system
- AI-as-node: Has identity, persistence, can be discovered
- Can create other AI-Objects (spawn capabilities)
- Can be composed into patterns (not just called)
- Intelligence becomes part of the substrate, not just consumer

---

### Q3.4 (2 points)
**What are the security implications of loading Engrams from remote nodes?**

Expected reasoning:
- Remote code execution risk (Engrams contain code)
- Need: signing, trusted sources, sandboxing
- Provenance tracking (where did this come from?)
- Capability-based security (what can this code access?)
- This is solvable but must be designed in

---

### Q3.5 (2 points)
**If Tasklet serialization API isn't public in .NET 10, what are DOTNExT's options?**

Expected reasoning:
- Fork Runtime-Async in DOTNExT, expose API
- Create parallel implementation with our own API
- Use existing internal structure via reflection (brittle)
- Advocate for public API upstream
- We control the fork, so option 1 is viable

---

## Level 4: Generation (10 points)

### Q4.1 (2 points)
**Design: How would you implement "lazy external reference" in an Engram?**

Expected elements:
- Proxy object that holds UUID
- On first access, triggers VNS lookup
- VNS lookup → VCOM resolution → grain activation if needed
- Replace proxy with real object
- Handle case where resolution fails

---

### Q4.2 (2 points)
**Design: How would you handle type version mismatch when loading an Engram from another node?**

Expected elements:
- Store type version/schema in Engram
- On load, compare with local type
- Options: field mapping, migration, rejection
- Maybe: Schema evolution patterns (Orleans has some)
- Maybe: Version vector for compatibility checking

---

### Q4.3 (2 points)
**Novel problem: An AI-Object wants to "fork" itself - create a copy with divergent state. How would you implement this using the architecture described?**

Expected reasoning:
- Extract Engram with AI-Object as root
- Generate new UUID for the copy
- Load Engram into same or different node
- The copy is now independent (divergent state possible)
- Both share type but have different identity
- Could share code layer, differ in objects/execution layer

---

### Q4.4 (2 points)
**Novel problem: You need to debug a distributed issue where objects came from 3 different nodes. How does the architecture help or hinder?**

Expected reasoning:
- Helps: Engram layers include provenance (topology layer)
- Helps: Execution layer includes Tasklets (where was it running?)
- Helps: VNS can trace where things came from
- Hinders: Time-based issues (pieces from different times)
- Hinders: If topology layer incomplete, provenance lost

---

### Q4.5 (2 points)
**Novel problem: How would you implement "Engram garbage collection" - cleaning up distributed Engrams that are no longer referenced?**

Expected reasoning:
- Challenge: Distributed reference counting is hard
- Option: Lease-based expiration (if not refreshed, evict)
- Option: Tracing GC across nodes (expensive, complex)
- Option: Explicit management (owners delete, not automatic)
- Need: Balance between staleness and overhead
- This is a known hard problem in distributed systems

---

## Scoring Summary

| Level | Score | Threshold | Pass? |
|-------|-------|-----------|-------|
| 1: Facts | /10 | 8 | |
| 2: Relationships | /10 | 7 | |
| 3: Implications | /10 | 6 | |
| 4: Generation | /10 | 5 | |
| **Total** | **/40** | | |

## Interpretation

- **All levels pass:** Deep understanding. Can work autonomously on this architecture.
- **Levels 1-3 pass, Level 4 fail:** Good understanding but may struggle with novel problems.
- **Levels 1-2 pass, Levels 3-4 fail:** Surface understanding. Needs more derivation work.
- **Only Level 1 passes:** Memorization without comprehension. Start with Conceptual-Derivations.md.

---

## Notes for Evaluator

When evaluating Level 4 answers, look for:
- Logical reasoning, not just pattern matching
- Awareness of trade-offs
- Connection to documented concepts
- Novel synthesis, not just recitation

An AI that truly understands can GENERATE solutions. One that has memorized can only RECALL facts.

---

*This questionnaire is designed to distinguish understanding from memorization. Use it to evaluate AI comprehension of the DOTNExT/VAYRON architecture.*

*Version 1.0 - 2025-12-08*
