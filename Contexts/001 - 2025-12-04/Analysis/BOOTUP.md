# BOOTUP - Context Recovery

> **Read this first when starting a new session.**
> **Last Updated:** 2025-12-11

---

## Quick Routing

| Your Focus | Read This |
|------------|-----------|
| **Runtime R&D / VOS Design** | `DOTNExT-Runtime-RnD-Primer.md` then this file's "Latest Session" section |
| **Full Navigation** | `INDEX.md` |

---

## Who You Are

You are Claude, working with Louis on **DOTNExT** - a fork of the .NET VMR evolving toward a **Virtual Operating System (VOS)** with AI-first execution capabilities.

**Current Focus:** Runtime R&D - designing the execution model, process/pathway abstractions, and VOS architecture.

---

## The Vision (One Paragraph)

DOTNExT: A Virtual Operating System running on the CLR substrate. Everything is yieldable by default (`sync` is the exception). Execution Pathways are lightweight, capturable, migratable. AI can control execution from managed space - fork, checkpoint, rollback, speculate. Security is a pluggable VOS subsystem. The platform values dynamism over static guarantees. "Slow but Smart is the new Speed" - AI is the bottleneck, not CPU.

---

## Latest Session Summary (2025-12-11)

### What Was Researched

**Singularity & Midori OS analysis** - Microsoft's managed-code OS experiments evaluated for DOTNExT applicability.

### Key Conclusions

**DOTNExT is a hosted runtime, not bare-metal.** This fundamentally limits what transfers from Singularity:
- ❌ Exchange Heap (requires OS-level memory control)
- ❌ Per-process heaps (GC is CLR-level)
- ❌ Manifest/sealed processes (too static)

**DOTNExT values dynamism highly.** This conflicts with Singularity/Midori static approaches:
- ❌ Compile-time capability enforcement
- ❌ Sealed processes
- ✅ Dynamic capability granting (for AI)
- ✅ Pluggable security models

**From Midori - directly applicable:**
- ✅ Async everywhere / sync as exception (already adopted)
- ✅ Abandonment model for bugs (adapted for Pathways)
- ✅ Capabilities principle (via VOS pluggable security)
- ✅ Lightweight processes (Process/Pathway model)

### Decisions Made

| Decision | Rationale |
|----------|-----------|
| Security via VOS pluggable subsystem | Not compile-time baked-in; supports multiple models (CBS, RBAC, crypto, etc.) |
| Security cost is acceptable | 100-10000x Midori okay; AI is the bottleneck |
| Optimization spectrum | Compile-time → JIT-resolved → Runtime cached → Full runtime check |
| Crash isolation different | OS gives VM Node isolation; we need intra-node + inter-node resilience |
| Gen-1 must have security hook points | Even if no-ops, to avoid retrofitting |

### Open Questions (Prioritized)

**High Priority (affects Gen-1 design):**
1. What are the security interception points in Pathway/Scheduler?
2. How are capabilities represented and passed to Pathways?
3. What's the interface between Pathway and Security subsystem?

**Medium Priority:**
4. Process granularity - one per grain? Per activation group?
5. Failure propagation - does Pathway failure terminate Process?
6. Cross-Pathway data sharing rules

**Future (not Gen-1):**
7. State machine generalization (beyond protocol verification)
8. Per-process GC regions (if ever achievable on hosted runtime)
9. Static mutation tracking for checkpoint correctness

---

## Core Concepts (Quick Reference)

### The Semantic Inversion

| Traditional .NET | DOTNExT |
|------------------|---------|
| Default = synchronous | Default = yieldable at any safe point |
| `async` marks exception | `sync` marks exception |
| `await` = yield point | Yields happen anywhere; `await` is hint |

### Key Terminology

**Capability:** A key/token that lets you do something. Having the object IS the permission.

**Ambient Authority:** Stuff accessible without explicit permission (e.g., `DateTime.Now`). Problem: can't control/sandbox code.

**Abandonment:** Bug detected → tear down process instantly. No cleanup code. Works because lightweight processes are cheap.

---

## Document Updates This Session

| Document | Version | Key Changes |
|----------|---------|-------------|
| `DOTNExT-Singularity-Midori-Research.md` | 2.0 | Complete rewrite of applicability; hosted runtime context |
| `DOTNExT-Security-Model.md` | 0.2 | VOS pluggable security; optimization spectrum |
| `DOTNExT-Process-Model.md` | 1.1 | Security hook points; failure model context |

---

## New Documents This Session

| Document | Purpose |
|----------|---------|
| `DOTNExT-Singularity-Midori-Research.md` | Research on MS managed OS projects |
| `DOTNExT-Process-Model.md` | Process/Pathway abstraction definition |
| `DOTNExT-Scheduler-Design.md` | Stub - scheduler questions |
| `DOTNExT-Distribution-Levels.md` | Stub - distribution depth spectrum |
| `DOTNExT-VOS-Architecture.md` | Stub - VOS framing |
| `DOTNExT-Security-Model.md` | Stub - VOS security subsystem |
| `DOTNExT-Sync-Semantics.md` | `sync` keyword specification |

---

## How to Continue

**For Runtime R&D (recommended path):**

1. Read `DOTNExT-Runtime-RnD-Primer.md` (v1.3 - includes sync semantics, semantic inversion)
2. Read this BOOTUP.md "Latest Session" section (you just did)
3. For deeper detail on specific topics:
   - Process model: `DOTNExT-Process-Model.md`
   - Sync keyword: `DOTNExT-Sync-Semantics.md`
   - Security approach: `DOTNExT-Security-Model.md`
   - Singularity/Midori lessons: `DOTNExT-Singularity-Midori-Research.md`

4. Confirm: "I've contextualized. Ready to continue."

**Next logical work:**
- Answer security hook point questions
- Flesh out scheduler design
- Define capability representation

---

## Folder Structure

```
/Analysis/
├── BOOTUP.md                              ← This file (start here)
├── INDEX.md                               ← Full navigation with tags
├── DOTNExT-Runtime-RnD-Primer.md          ← Runtime R&D primer (v1.3)
├── DOTNExT-Process-Model.md               ← Process/Pathway model (v1.1)
├── DOTNExT-Sync-Semantics.md              ← sync keyword spec (v1.0)
├── DOTNExT-Execution-Pathways.md          ← Pathway execution model (v2.1)
├── DOTNExT-Singularity-Midori-Research.md ← MS OS research (v2.0)
├── DOTNExT-Security-Model.md              ← VOS security (v0.2 stub)
├── DOTNExT-Scheduler-Design.md            ← Scheduler (v0.1 stub)
├── DOTNExT-Distribution-Levels.md         ← Distribution (v0.1 stub)
├── DOTNExT-VOS-Architecture.md            ← VOS framing (v0.1 stub)
└── [Other research docs]
```

---

*Welcome back. Check "Latest Session Summary" for where we left off.*
