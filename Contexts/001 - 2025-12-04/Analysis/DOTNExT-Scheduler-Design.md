# DOTNExT Scheduler Design

> **Document Type:** Architecture Design (Stub)
> **Version:** 0.1
> **Date:** 2025-12-10
> **Status:** STUB - Questions and outline for scheduler design
> **Prerequisite Reading:** DOTNExT-Process-Model.md, DOTNExT-Execution-Pathways.md

---

## 1. Purpose

This document will define the DOTNExT scheduler - the component responsible for deciding which Pathways execute, when, and for how long.

---

## 2. Key Questions to Answer

### 2.1 Scheduling Unit
- [ ] What is scheduled: Processes or Pathways?
- [ ] If Pathways, does Process priority affect all its Pathways?
- [ ] Are there different schedulers for different levels?

### 2.2 Scheduling Algorithm
- [ ] Reduction counting (BEAM-style)?
- [ ] Time-slice based?
- [ ] Priority-based?
- [ ] Gas/budget-based?
- [ ] Pluggable algorithms?
- [ ] Default algorithm?

### 2.3 Priority Model
- [ ] How many priority levels?
- [ ] Static vs dynamic priority?
- [ ] Priority inheritance (for dependencies)?
- [ ] Per-process vs per-pathway priority?
- [ ] Can code adjust its own priority?

### 2.4 Preemption
- [ ] Only at safe points?
- [ ] Configurable preemption frequency?
- [ ] JIT-inserted vs cooperative?
- [ ] What triggers preemption?

### 2.5 Resource Accounting
- [ ] What resources tracked? (CPU, memory, I/O, "gas")
- [ ] Per-process or per-pathway accounting?
- [ ] How are limits enforced?
- [ ] What happens when limit exceeded?

### 2.6 Fairness
- [ ] Starvation prevention?
- [ ] QoS tiers?
- [ ] Guaranteed minimum?

### 2.7 AI Integration
- [ ] How does AI influence scheduling?
- [ ] Can AI override scheduler decisions?
- [ ] AI-driven adaptive scheduling?

### 2.8 Distribution
- [ ] Per-node scheduler coordination?
- [ ] Cross-node scheduling?
- [ ] Load balancing between nodes?

---

## 3. Design Considerations from Research

### From BEAM
- Reduction counting: decrement counter at safe points, yield when zero
- Per-process scheduling: each process has own budget
- Preemptive but cooperative: yield at safe points

### From Midori
- Single-threaded per process: no internal scheduling needed
- Event loop model: process runs until awaiting
- No blocking: async-first eliminates blocking waits

### From Singularity
- Cheap context switch: software isolation means no TLB flush
- Fine-grained processes: many small things to schedule

---

## 4. Outline (To Be Developed)

1. Scheduler Architecture
2. Scheduling Unit and Hierarchy
3. Scheduling Algorithms (pluggable)
4. Priority Model
5. Preemption Mechanism
6. Resource Accounting
7. Fairness Guarantees
8. AI Integration Points
9. Distributed Scheduling
10. Implementation Phases

---

## 5. Related Documents

| Document | Relationship |
|----------|--------------|
| DOTNExT-Process-Model.md | What we're scheduling |
| DOTNExT-Execution-Pathways.md | Pathway execution model |
| DOTNExT-Sync-Semantics.md | Sync scopes affect scheduling |
| DOTNExT-Distribution-Levels.md | Cross-node scheduling |
| Erlang-BEAM-Architecture-Reference.md | BEAM scheduler reference |

---

*Stub document - to be expanded with scheduler design details.*

*Version 0.1 - 2025-12-10 - Initial stub with questions*
