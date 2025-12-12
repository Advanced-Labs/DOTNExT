# BOOTUP - Context Recovery

> **Read this first when starting a new session.**
> **Last Updated:** 2025-12-11 (Session 2)

---

## Quick Routing

| Your Focus | Read This |
|------------|-----------|
| **VOS Implementation Strategy** | `DOTNExT-VOS-Implementation-Strategy.md` ← **NEW: Comprehensive session record** |
| **Runtime R&D / VOS Design** | `DOTNExT-Runtime-RnD-Primer.md` then this file's "Latest Session" section |
| **Full Navigation** | `INDEX.md` |

---

## Who You Are

You are Claude, working with Louis on **DOTNExT** - a fork of the .NET VMR evolving toward a **Virtual Operating System (VOS)** with AI-first execution capabilities.

**Current Focus:** VOS architecture - the runtime as kernel, dynamic types as VARIA implementation, pluggable security drivers.

---

## The Vision (One Paragraph)

DOTNExT: A Virtual Operating System where the **CLR runtime IS the kernel**. VOS services (VNS, persistence, security) run in "userspace" built on NewOrleans. VARIA types embody platform virtues (distribution, persistence, security, AI-centrality) initially via "special dynamic types" + Roslyn codegen, later potentially native to the kernel. Everything is yieldable by default (`sync` is the exception). Security is a pluggable driver system. "Slow but Smart is the new Speed" - AI is the bottleneck, not CPU.

---

## Latest Session Summary (2025-12-11 - Session 2)

### What Was Researched

**Security interception points in .NET** - comprehensive analysis of where security enforcement could hook in: compile-time (Roslyn), assembly loading, JIT, vtable dispatch, object operations, reflection, dynamic types.

**VOS architecture framing** - the runtime as VOS kernel, VOS services in userspace, NewOrleans as infrastructure.

**Implementation strategy** - universal dynamic types as initial VARIA implementation, progressive lowering into kernel.

### Key Realizations

**The DOTNExT runtime IS the VOS kernel:**
- Lowest layer - everything runs on it
- Provides fundamental primitives (GC, JIT, types, execution)
- Progressive lowering targets this layer
- Clear boundary: managed = userspace, runtime internals = kernel

**VOS services are "userspace" but still "part of the OS":**
- VNS, persistence, security are VOS services
- Built on NewOrleans (VOS infrastructure)
- Like DNS in Unix - userspace but "part of the OS"
- Can be lowered into kernel later if needed

**Universal dynamic types as VARIA implementation:**
- One family of types handles all cross-cutting concerns
- Compile-time codegen wraps user types
- Drivers for each concern (security, persistence, VNS, etc.)
- Runtime agnostic initially - all managed-space
- Progressive lowering when beneficial

### Decisions Made

| Decision | Rationale |
|----------|-----------|
| Runtime = VOS Kernel | Clear architectural framing; progressive lowering target |
| VOS services in userspace first | Faster iteration; matches traditional OS design |
| Universal dynamic types | One abstraction for all concerns; runtime agnostic |
| Security as pluggable drivers | Supports multiple models (CBS, RBAC, crypto, etc.) |
| VARIA = concept, not implementation | Dynamic types are first impl; kernel-native possible later |

### Questions RESOLVED This Session

The original questions from previous session are now answered:

1. **Security interception points** → Comprehensive list: compile-time, load-time, JIT-time, runtime (see `DOTNExT-VOS-Implementation-Strategy.md` Section 3)

2. **"How are capabilities represented?"** → **Question was malformed.** Capability representation is driver-specific. Runtime provides hooks; drivers implement models.

3. **"Interface between Pathway and Security?"** → **Question was malformed.** Pathways have identity; security drivers are queried at interception points: "Can X do Y to Z?"

### Open Questions (Updated)

**High Priority (affects Gen-1 design):**
1. Dynamic types family design - base types, interfaces, generics
2. Driver interface definitions (Security, Persistence, VNS, Distribution)
3. Codegen transformation rules

**Medium Priority:**
4. Process granularity - one per grain? Per activation group?
5. Failure propagation - does Pathway failure terminate Process?
6. VNS anchor point management

**Future:**
7. Kernel lowering criteria and interface
8. Native VARIA recognition in runtime

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

## Document Updates This Session (Session 2)

| Document | Version | Key Changes |
|----------|---------|-------------|
| `BOOTUP.md` | Session 2 | Updated vision, resolved questions, new focus |

---

## New Documents This Session (Session 2)

| Document | Purpose |
|----------|---------|
| `DOTNExT-VOS-Implementation-Strategy.md` | **FOUNDATIONAL** - Runtime as kernel, dynamic types strategy, security interception points, session record |

---

## Previous Session Documents (Session 1)

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

**For VOS Implementation (current focus):**

1. Read `DOTNExT-VOS-Implementation-Strategy.md` - comprehensive session record
2. Read this BOOTUP.md "Latest Session" section (you just did)
3. For deeper detail on specific topics:
   - Runtime R&D foundation: `DOTNExT-Runtime-RnD-Primer.md`
   - Process model: `DOTNExT-Process-Model.md`
   - Security model: `DOTNExT-Security-Model.md`
   - VAYRON architecture: `VAYRON-Architecture-Master.md`

4. Confirm: "I've contextualized. Ready to continue."

**Next logical work:**
- Design the dynamic types family (base types, interfaces)
- Define driver interfaces (Security, Persistence, VNS, Distribution)
- Specify codegen transformation rules

---

## Folder Structure

```
/Analysis/
├── BOOTUP.md                              ← This file (start here)
├── INDEX.md                               ← Full navigation with tags
├── DOTNExT-VOS-Implementation-Strategy.md ← **NEW** VOS impl strategy (v1.0)
├── DOTNExT-Runtime-RnD-Primer.md          ← Runtime R&D primer (v1.3)
├── DOTNExT-Process-Model.md               ← Process/Pathway model (v1.1)
├── DOTNExT-Sync-Semantics.md              ← sync keyword spec (v1.0)
├── DOTNExT-Execution-Pathways.md          ← Pathway execution model (v2.1)
├── DOTNExT-Singularity-Midori-Research.md ← MS OS research (v2.0)
├── DOTNExT-Security-Model.md              ← VOS security (v0.2 stub)
├── DOTNExT-Scheduler-Design.md            ← Scheduler (v0.1 stub)
├── DOTNExT-Distribution-Levels.md         ← Distribution (v0.1 stub)
├── DOTNExT-VOS-Architecture.md            ← VOS framing (v0.1 stub)
├── VAYRON-Architecture-Master.md          ← Platform architecture (v1.0)
└── [Other research docs]
```

---

*Welcome back. Check "Latest Session Summary" for where we left off.*
