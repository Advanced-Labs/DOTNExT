# DOTNExT Development Strategy: The Hybrid Path

> **Document Type:** Strategic Decision
> **Version:** 1.0
> **Date:** 2025-12-06
> **Status:** Adopted Direction

---

## 1. The Strategic Question

When building DOTNExT's next-generation memory architecture, we face a fundamental choice about how to proceed. This document captures the reasoning that led to our chosen path.

---

## 2. Paths Considered

### Path A: Reinvent From Scratch

**Approach:** Build entirely new runtime systems using .NET sources as reference/model only.

**Pros:**
- Complete control over everything
- No legacy constraints
- Clean architecture

**Cons:**
- Extremely slow to deliver value
- Massive effort
- Risk of never completing
- Would have been folly until recently (AI changes this calculus)

**Verdict:** Too risky despite AI assistance. Fruits take too long.

---

### Path B: Extend Existing Systems In-Place

**Approach:** Modify GC, EE, type system directly to add Engram capabilities.

**Pros:**
- Leverages proven code
- Faster initial progress

**Cons:**
- Unforeseen consequences from modifying battle-tested systems
- Risk of "we should have left those alone" regret
- Modifications compound; harder to reason about over time
- Breaking changes may be irreversible
- Could hit dead ends that require backtracking

**Verdict:** Lesser risk today, but risk increases with time. Potentially more dangerous than it appears.

---

### Path C: New Systems Replacing Old (Original Vision)

**Approach:** Build CMS, MOM, ORION as new systems that take over responsibilities from old systems.

**Pros:**
- Clean new architecture
- Full vision realized

**Cons:**
- Breaks ecosystem compatibility
- Big bang migration required
- Old systems become dead code or conflict with new

**Verdict:** Ambitious but risky. No fallback path.

---

### Path D: Hybrid Path (CHOSEN)

**Approach:** New systems live in parallel with old systems. Minimal modification to old systems. New systems draw from old, eventually absorbing responsibilities while old systems become compatibility facades.

**This is the chosen direction.** Details below.

---

## 3. The Hybrid Path Philosophy

### 3.1 Core Principles

**Principle 1: Minimize Modification of Existing Systems**

Old systems (GC, JIT, EE, Loader) are modified as little as possible. This:
- Avoids unforeseen consequences
- Preserves the ability to maintain "old dotnet" behavior
- Keeps ecosystem compatibility intact

**Principle 2: New Systems Live in Parallel**

CMS, MOM extensions, ORION, etc. are built alongside old systems, not replacing them. They:
- Observe and draw data from old systems
- Don't interfere with old system operation
- Can be disabled/removed without breaking runtime

**Principle 3: Gradual Absorption Over Time**

As new systems prove themselves:
- They take on more responsibility
- Old systems delegate to new systems
- Eventually old systems become facades/shims
- Interfaces preserved for compatibility

**Principle 4: Managed-Space Prototyping First**

Before committing to native implementation:
- Build experiments in managed code (C#)
- Use pipes/commodities from runtime to managed space
- Prove features work at higher level
- Only lower to native what's proven necessary

### 3.2 The Layered Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    MANAGED SPACE                             │
│                                                             │
│   ┌─────────────────────────────────────────────────────┐   │
│   │              Experiments & Features                  │   │
│   │     (Engram prototype, ORION queries, drivers)      │   │
│   └─────────────────────────────────────────────────────┘   │
│                            ↑↓                               │
│   ┌─────────────────────────────────────────────────────┐   │
│   │           Pipes / Commodities Interface              │   │
│   │      (Events, queries, controlled native access)     │   │
│   └─────────────────────────────────────────────────────┘   │
│                            ↑↓                               │
├─────────────────────────────────────────────────────────────┤
│                    NEW RUNTIME SYSTEMS                       │
│                                                             │
│   ┌─────────────┐  ┌─────────────┐  ┌─────────────┐        │
│   │    CMS      │  │ MOM hooks   │  │   ORION     │        │
│   │  (minimal)  │  │  (minimal)  │  │  (native)   │        │
│   └─────────────┘  └─────────────┘  └─────────────┘        │
│                            ↑↓                               │
│              [Data/Knowledge Access - Read Mostly]          │
│                            ↑↓                               │
├─────────────────────────────────────────────────────────────┤
│                    OLD RUNTIME SYSTEMS                       │
│                    (Minimally Modified)                      │
│                                                             │
│   ┌─────────────┐  ┌─────────────┐  ┌─────────────┐        │
│   │     GC      │  │     JIT     │  │   Loader    │        │
│   │             │  │             │  │             │        │
│   └─────────────┘  └─────────────┘  └─────────────┘        │
│                                                             │
│   ┌─────────────┐  ┌─────────────┐  ┌─────────────┐        │
│   │     EE      │  │   Handles   │  │   Stacks    │        │
│   │             │  │             │  │             │        │
│   └─────────────┘  └─────────────┘  └─────────────┘        │
│                                                             │
│              [Interfaces Preserved for Compatibility]        │
└─────────────────────────────────────────────────────────────┘
```

### 3.3 The Evolution Pattern

**Stage 1: Observation**
- New systems observe old systems
- Read data, receive events
- No modification of old system behavior

**Stage 2: Augmentation**
- New systems add capabilities
- Old systems continue unchanged
- Features are additive, not replacing

**Stage 3: Delegation**
- Old systems begin delegating to new systems
- New systems do the actual work
- Old systems become pass-through

**Stage 4: Absorption**
- New systems fully responsible
- Old systems are compatibility shims
- Interfaces preserved, implementation moved

**Stage 5: (Optional) Removal**
- If compatibility no longer needed
- Old systems can be removed
- New systems stand alone

---

## 4. Benefits of This Path

### 4.1 Risk Mitigation Through Reversibility

If an experiment fails or has unforeseen consequences:
- Old systems remain intact
- New systems can be disabled
- No corruption of battle-tested code
- Always possible to fall back to "old dotnet" behavior

### 4.2 Ecosystem Compatibility as Asset

The .NET ecosystem is enormous:
- Millions of NuGet packages
- Countless existing codebases
- Tooling expects standard behavior

Keeping old systems as working facades means:
- Existing code just works
- Engram features are opt-in
- No forced migration
- Gradual adoption possible

### 4.3 Managed-Space Prototyping is Fast

C# development advantages over C++ runtime hacking:
- Better tooling and debugging
- Faster iteration cycles
- More contributors can participate
- AI assistance is more effective for managed code
- Easier to experiment and throw away

### 4.4 The "Absorption" Pattern is Proven

This evolution model has succeeded before:
- Windows NT over DOS (DOS became compatibility layer)
- Git over older VCS (facades for SVN, etc.)
- Containers over VMs (for many workloads)
- LINQ over manual iteration (compiler transforms)

New system does real work; old system becomes shim.

### 4.5 Preserves Optionality

At any point we can:
- Go deeper into native (if performance requires)
- Stay in managed (if overhead acceptable)
- Abandon an experiment (without damage)
- Ship partial features (value early)

### 4.6 Delivers Value Early

Managed experiments can ship before native optimization:
- Prove concepts work
- Get user feedback
- Refine design
- Then optimize

---

## 5. Addressing Concerns

### 5.1 Performance Overhead of Pipes

**Concern:** Crossing managed/native boundary has cost. Hot paths (every allocation, every write barrier) could be prohibitive.

**Mitigation:**
- Pipes for infrequent operations (extraction, persistence, queries)
- For hot paths: profiler callbacks or minimal native hooks
- Batch updates rather than per-operation callbacks
- Measure before assuming - some overhead may be acceptable

### 5.2 Two Codebases Syndrome

**Concern:** Features implemented twice - managed for experimentation, native for performance.

**Mitigation:**
- Managed experiments are explicitly *prototypes*, not products
- Clear understanding that lowering to native is end goal for proven features
- Accept some throwaway code as cost of reduced risk
- Not everything needs to be native - many features can stay managed

### 5.3 Complexity of Parallel Systems

**Concern:** Old and new systems running in parallel means more state, potential inconsistencies.

**Mitigation:**
- Clear ownership rules at all times
- Old systems are authoritative until explicitly handed off
- New systems are *observers first, actors second*
- Document which system owns what at each stage

---

## 6. Concrete Implementation Phases

### Phase 1: Define Commodities/Pipes

Build the interface between runtime and managed space.

**What managed space needs access to:**

| Commodity | Source | Data/Event |
|-----------|--------|------------|
| Object birth | GC/Allocator | Address, MethodTable, size |
| Object death | GC | Address (before collection) |
| Reference write | Write barrier | Source, field offset, target |
| Type loaded | Loader | MethodTable pointer, type info |
| GC occurred | GC | Generation, timing, stats |
| Object moved | GC compaction | Old address, new address |

**Implementation options:**
- EventPipe (exists, low overhead, diagnostic focused)
- New QCall/FCall surface for managed queries
- Profiler API (exists but heavy)
- Custom lightweight callback mechanism

### Phase 2: Managed-Space Engram Prototype

Build the Engram system entirely in managed code:

```csharp
public class EngramRuntime
{
    // Side table for UUID tracking (managed)
    private ConditionalWeakTable<object, EngramIdentity> _identities;

    // Graph of known relationships (managed)
    private EngramGraph _graph;

    // Subscribe to runtime events via pipes
    public void Initialize()
    {
        RuntimePipes.OnObjectCreated += HandleObjectCreated;
        RuntimePipes.OnReferenceWritten += HandleReferenceWritten;
        RuntimePipes.OnObjectCollected += HandleObjectCollected;
    }

    // Extraction - walks object graph in managed code
    public Engram Extract(object root, ExtractionOptions options)
    {
        // Use reflection + CGCDesc-equivalent info from pipes
        // Build Engram structure
        // Return serializable result
    }

    // Loading - creates objects, wires references
    public T Load<T>(Engram engram, LoadOptions options)
    {
        // Create objects via RuntimeHelpers
        // Assign UUIDs
        // Wire references
        // Return root
    }
}
```

### Phase 3: Integration Testing

- Integrate managed Engram prototype with Orleans
- Test with Async+ scenarios
- Measure overhead
- Identify bottlenecks

### Phase 4: Selective Lowering

Based on Phase 3 results, lower to native only what's necessary:

| Feature | Stay Managed? | Lower to Native? | Reasoning |
|---------|--------------|------------------|-----------|
| UUID assignment | Maybe | Maybe | Depends on overhead measurement |
| Graph storage (ORION) | Probably | If perf requires | Query engine can be managed |
| Write barrier extension | No | Yes | Too hot, must be native |
| Extraction logic | Yes | No | Infrequent, complex logic |
| Persistence drivers | Yes | No | I/O bound anyway |
| Object birth events | - | Minimal hook | Just emit event, minimal code |

### Phase 5: Gradual Absorption

As new systems prove stable:
- Move more responsibility from old to new
- Old systems delegate rather than implement
- Maintain interfaces for compatibility
- Document ownership transitions

---

## 7. Success Criteria

### Short Term (3-6 months)
- [ ] Pipes/commodities interface defined and implemented
- [ ] Managed Engram prototype functional
- [ ] Basic extraction/loading working
- [ ] Overhead measured and documented

### Medium Term (6-12 months)
- [ ] Orleans integration via managed prototype
- [ ] Selective lowering complete for hot paths
- [ ] ORION graph queries functional
- [ ] Memory drivers interface defined

### Long Term (12+ months)
- [ ] New systems handling majority of Engram operations
- [ ] Old systems serving as compatibility layer
- [ ] Memantics development begun
- [ ] Distributed features prototyped

---

## 8. Relationship to Vision Documents

This strategy document describes *how* we build. The vision documents describe *what* we build.

| Document | Describes |
|----------|-----------|
| Vision-DOTNExT-Memory-Architecture.md | The end-state architecture |
| Vision-Component-Details.md | Component specifications |
| Vision-Glossary-and-Variants.md | Terminology and design decisions |
| **This document** | How we get there safely |

The vision remains the north star. This strategy is the path that gets us there through **accretion rather than replacement**.

---

## 9. Key Decisions Captured

1. **Minimize modification of old systems** - Risk mitigation
2. **New systems parallel, not replacing** - Reversibility
3. **Managed-space prototyping first** - Fast iteration
4. **Lower to native selectively** - Only what's proven necessary
5. **Preserve interfaces for compatibility** - Ecosystem value
6. **Gradual absorption pattern** - Proven evolution model

---

## 10. Next Immediate Action

**Define the Pipes/Commodities Interface**

This is the foundation. What events, what queries, what data, what overhead is acceptable. Everything downstream depends on this interface being right.

Specification to be drafted in: `Spec-Runtime-Managed-Pipes.md`

---

*This document captures the strategic reasoning for DOTNExT's development path. It should be referenced when making architectural decisions to ensure alignment with the chosen approach.*

*Version 1.0 - 2025-12-06*
*Decision made by: Louis*
*Documented by: Claude Opus 4.5*
