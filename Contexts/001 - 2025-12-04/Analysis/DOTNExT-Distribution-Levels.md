# DOTNExT Distribution Levels

> **Document Type:** Architecture Design (Stub)
> **Version:** 0.1
> **Date:** 2025-12-10
> **Status:** STUB - Questions and outline for distribution architecture
> **Prerequisite Reading:** DOTNExT-Process-Model.md, DOTNExT-Security-Model.md

---

## 1. Purpose

This document will define the distribution architecture for DOTNExT - how VM nodes interconnect at various depths of integration based on trust relationships and deployment topology.

---

## 2. Distribution Depth Spectrum

```
Level 0: Application Layer (current Orleans)
├── Distribution is library code
├── Runtime unaware of distribution
└── No runtime optimization

Level 1: Runtime-Aware Distribution
├── Runtime knows about remote references
├── Scheduler considers locality
└── Still separate heaps, separate VMs

Level 2: Coordinated VMs (Federation)
├── Schedulers communicate
├── Coordinated checkpointing
├── Distributed GC protocols
└── Process migration supported

Level 3: Logical Single VM (same machine)
├── Shared memory regions
├── Direct reference passing
├── Single scheduler with affinity hints
└── Near-zero overhead same-machine calls

Level 4: Virtual Single VM (cross-machine)
├── Transparent distributed heap
├── Distributed pointers
├── Single logical scheduler
└── Latency-tolerant (AI-first)
```

---

## 3. Key Questions to Answer

### 3.1 Trust Model
- [ ] What trust levels? (Domain, Federation, Confederation, Public)
- [ ] How is trust established?
- [ ] How does trust level map to integration depth?
- [ ] Can trust change dynamically?

### 3.2 Same-Machine Optimization
- [ ] Can VMs on same machine share memory?
- [ ] How is shared memory region managed?
- [ ] What's the IPC mechanism between same-machine VMs?
- [ ] Can same-machine VMs operate as logical single VM?

### 3.3 Cross-Machine Communication
- [ ] What protocols for cross-machine?
- [ ] How is latency tolerated?
- [ ] What consistency model?
- [ ] How are distributed pointers implemented?

### 3.4 Scheduler Coordination
- [ ] Do schedulers on different nodes coordinate?
- [ ] Independent, cooperative, or hierarchical?
- [ ] How is load balancing achieved?
- [ ] Cross-node process migration triggers?

### 3.5 Distributed GC
- [ ] How are cross-node references tracked?
- [ ] Distributed GC protocols?
- [ ] What happens when node fails?
- [ ] How does trust affect GC coordination?

### 3.6 Distributed Pointers/References
- [ ] What is a "distributed pointer"?
- [ ] How does resolution work?
- [ ] Caching strategy?
- [ ] Consistency guarantees?

### 3.7 Process Migration
- [ ] What triggers migration?
- [ ] What's the migration protocol?
- [ ] How are references to migrated process updated?
- [ ] Rollback on failed migration?

---

## 4. Design Considerations from Research

### From Singularity
- Exchange heap: shared region for IPC
- Zero-copy: only pointers transferred
- Linear types: ownership tracking

### From Midori
- Message passing: no shared state
- Async interfaces: distribution-friendly
- Capabilities: security boundary matches isolation

### DOTNExT Specific
- AI-first: latency tolerance for intelligence
- VCOM: already provides distributed abstraction
- Engrams: portable computation units for migration

---

## 5. Outline (To Be Developed)

1. Trust Model and Levels
2. Integration Depth by Trust
3. Same-Machine VM Integration
4. Cross-Machine Communication
5. Distributed Pointers/References
6. Scheduler Coordination
7. Distributed GC
8. Process Migration Protocol
9. Security at Each Level
10. Implementation Phases

---

## 6. Related Documents

| Document | Relationship |
|----------|--------------|
| DOTNExT-Process-Model.md | What migrates between nodes |
| DOTNExT-Scheduler-Design.md | Scheduler coordination |
| DOTNExT-Security-Model.md | Trust model |
| VAYRON-Architecture-Master.md | Overall distributed architecture |

---

*Stub document - to be expanded with distribution architecture details.*

*Version 0.1 - 2025-12-10 - Initial stub with questions*
