# Analysis Folder Index

> **Purpose:** Master index with categorization, tags, and reading curricula
> **Last Updated:** 2025-12-11
> **Key Update:** Added VOS architecture docs, Process Model, Security Model, Sync Semantics, Singularity/Midori research

---

## Quick Navigation

| Goal | Start Here |
|------|------------|
| **Runtime R&D** | `DOTNExT-Runtime-RnD-Primer.md` (self-contained) |
| **VAYRON Platform** | `BOOTUP.md` → `VAYRON-Architecture-Master.md` |
| **Quick Recovery** | `BOOTUP.md` |
| **Full Index** | This file |

---

## Document Categories & Tags

### Legend

| Tag | Meaning |
|-----|---------|
| `runtime` | DOTNExT runtime/CLR modification |
| `research` | Active research, not implementation |
| `vayron` | VAYRON platform layer |
| `vision` | Strategic vision document |
| `reference` | External reference material |
| `meta` | Navigation, context management |
| `neworleans` | Orleans fork specifics |
| `derivation` | Reasoning chains, WHY documents |

### Relevance Markers

| Marker | Meaning |
|--------|---------|
| **CORE** | Essential for the topic |
| **CONTEXT** | Helpful background |
| **OPTIONAL** | Read if time permits |
| **NOT NEEDED** | Not relevant for topic |

---

## Runtime R&D Curriculum

**For AI working on DOTNExT runtime modifications.**

### Minimal Curriculum (1 document)

| Document | Purpose |
|----------|---------|
| **DOTNExT-Runtime-RnD-Primer.md** | **START HERE** - Self-contained primer with context, derivations, challenges |

This single document synthesizes everything needed. It includes reasoning chains from Conceptual Derivations and engagement questions from Socratic FAQ.

### Extended Curriculum (if deeper detail needed)

| Document | Tags | When To Read |
|----------|------|--------------|
| `DOTNExT-Unwinder-Async2-Analysis.md` | `runtime`, `research` | **READ AFTER PRIMER** - JIT vs Unwinder, why Unwinder matters |
| `DOTNExT-Execution-Pathways.md` | `runtime`, `research` | BEAM-like execution model on Tasklets |
| `DOTNExT-Unified-SafePoints.md` | `runtime`, `research` | Safe point implementation |
| `DOTNExT-Process-Image-Persistence.md` | `runtime`, `research` | Checkpoint format design |
| `DOTNExT-Engrams-Revised.md` | `runtime`, `research` | Engram structure options |
| `Erlang-BEAM-Architecture-Reference.md` | `reference` | Understanding BEAM patterns |
| `Vision-Engrams-Cyberspace-Verbatim.md` | `vision` | Louis's distributed vision |

### NOT Needed for Runtime R&D

These are VAYRON platform layer, not runtime:

| Document | Why Not Needed |
|----------|----------------|
| All `VAYRON-*.md` files | Platform layer, not runtime |
| All `Vision-VAYRON-*.md` files | Platform vision |
| NewOrleans docs | Higher-level substrate |
| VS/SDK docs | Tooling layer |
| C=/Language docs | Language implementation |

---

## Complete Document List (Alphabetical)

### A-D

| Document | Tags | Runtime R&D? |
|----------|------|--------------|
| `About-Current-Memory-Systems.md` | `runtime`, `reference` | CONTEXT |
| `Async+.md` | `neworleans`, `feature` | NOT NEEDED |
| `BOOTUP.md` | `meta`, `context-recovery` | **READ FOR SESSION CONTEXT** |
| `BOTR-Index.md` | `runtime`, `reference` | OPTIONAL |
| `CoreCLR-Object-Layout.md` | `runtime`, `reference` | CONTEXT |
| `DLR-IronLanguages-Nemerle-Reference.md` | `reference`, `language` | NOT NEEDED |
| `DOTNExT-Conceptual-Derivations.md` | `derivation`, `runtime` | **CORE** (in Primer) |
| `DOTNExT-Distribution-Levels.md` | `runtime`, `vos`, `stub` | **NEW** - Distribution depth spectrum |
| `DOTNExT-Engrams-Revised.md` | `runtime`, `research` | **CORE** |
| `DOTNExT-Execution-Pathways.md` | `runtime`, `research` | **CORE** (v2.1 - sync semantics) |
| `DOTNExT-Persistence-Architecture-Options.md` | `runtime`, `research` | CONTEXT |
| `DOTNExT-Process-Image-Persistence.md` | `runtime`, `research` | **CORE** (v2.1) |
| `DOTNExT-Process-Model.md` | `runtime`, `vos`, `design` | **NEW CORE** - Process/Pathway model |
| `DOTNExT-Runtime-Async-Research.md` | `runtime`, `research` | CONTEXT |
| `DOTNExT-Runtime-RnD-Primer.md` | `runtime`, `derivation`, `meta` | **START HERE** (v1.3) |
| `DOTNExT-Scheduler-Design.md` | `runtime`, `vos`, `stub` | **NEW** - Scheduler questions |
| `DOTNExT-Security-Model.md` | `runtime`, `vos`, `stub` | **NEW** - VOS security subsystem |
| `DOTNExT-Singularity-Midori-Research.md` | `runtime`, `research`, `reference` | **NEW** - MS OS research (v2.0) |
| `DOTNExT-Socratic-FAQ.md` | `derivation`, `runtime` | **CORE** (in Primer) |
| `DOTNExT-Sync-Semantics.md` | `runtime`, `design` | **NEW CORE** - sync keyword spec |
| `DOTNExT-Understanding-Questionnaire.md` | `meta`, `assessment` | OPTIONAL |
| `DOTNExT-Unified-SafePoints.md` | `runtime`, `research` | **CORE** |
| `DOTNExT-Unwinder-Async2-Analysis.md` | `runtime`, `research` | **ESSENTIAL** - JIT vs Unwinder |
| `DOTNExT-VOS-Architecture.md` | `runtime`, `vos`, `stub` | **NEW** - VOS framing |
| `DynamicGrainAccess.md` | `neworleans`, `feature` | NOT NEEDED |

### E-L

| Document | Tags | Runtime R&D? |
|----------|------|--------------|
| `Erlang-BEAM-Architecture-Reference.md` | `reference`, `runtime` | **CORE** |
| `Extension-Points-Summary.md` | `runtime`, `reference` | CONTEXT |
| `INDEX.md` | `meta` | N/A |
| `LETTER-TO-FUTURE-SELF.md` | `meta` | NOT NEEDED |

### M-R

| Document | Tags | Runtime R&D? |
|----------|------|--------------|
| `Modularity-Report.md` | `runtime`, `reference` | CONTEXT |
| `New Orleans.md` | `neworleans`, `reference` | NOT NEEDED |
| `OrleansAsync+.md` | `neworleans`, `feature` | NOT NEEDED |
| `PluginGrainArchitecture.md` | `neworleans`, `feature` | NOT NEEDED |
| `Runtime-Memory-Subsystems.md` | `runtime`, `reference` | CONTEXT |

### S-Z

| Document | Tags | Runtime R&D? |
|----------|------|--------------|
| `VAYRON-Architecture-Master.md` | `vayron`, `architecture` | NOT NEEDED |
| `VAYRON-Component-Specs.md` | `vayron`, `design` | NOT NEEDED |
| `VAYRON-Decision-Log.md` | `vayron`, `meta` | NOT NEEDED |
| `VAYRON-SDK-Design.md` | `vayron`, `tooling` | NOT NEEDED |
| `Vision-Async+-Solution.md` | `vayron`, `research` | CONTEXT |
| `Vision-Component-Details.md` | `vayron`, `vision` | NOT NEEDED |
| `Vision-DOTNExT-Memory-Architecture.md` | `runtime`, `vision` | CONTEXT |
| `Vision-Engrams-Cyberspace-Verbatim.md` | `vision`, `runtime` | **CORE** |
| `Vision-Glossary-and-Variants.md` | `meta`, `reference` | OPTIONAL |
| `Vision-VAYRON-DevExperience.md` | `vayron`, `vision` | NOT NEEDED |
| `Vision-VAYRON-Platform.md` | `vayron`, `vision` | NOT NEEDED |
| `Vision-VAYRON-Verbatim.md` | `vayron`, `vision` | NOT NEEDED |
| `VS-Integration-Reference-Projects.md` | `reference`, `tooling` | NOT NEEDED |

---

## Reading Paths by Goal

### Runtime R&D (Recommended)

```
DOTNExT-Runtime-RnD-Primer.md
    └── (Self-contained - includes derivations and challenges)
```

If deeper detail needed:
```
DOTNExT-Runtime-Async-Research.md
    └── DOTNExT-Unified-SafePoints.md
        └── DOTNExT-Process-Image-Persistence.md
            └── DOTNExT-Engrams-Revised.md
```

### VAYRON Platform Understanding

```
BOOTUP.md
    └── VAYRON-Architecture-Master.md
        └── VAYRON-Component-Specs.md
            └── VAYRON-Decision-Log.md
```

### Engrams & Distributed Vision

```
Vision-Engrams-Cyberspace-Verbatim.md
    └── DOTNExT-Engrams-Revised.md
        └── DOTNExT-Persistence-Architecture-Options.md
```

### BEAM-Like Evolution

```
Erlang-BEAM-Architecture-Reference.md
    └── DOTNExT-Unified-SafePoints.md
        └── DOTNExT-Execution-Pathways.md
```

### AI Understanding Transfer

```
DOTNExT-Conceptual-Derivations.md
    └── DOTNExT-Socratic-FAQ.md
        └── DOTNExT-Understanding-Questionnaire.md
```

(Note: All three are synthesized into Runtime-RnD-Primer.md for runtime work)

---

## Document Relationships

```
                    DOTNExT-Runtime-RnD-Primer.md
                    (Synthesizes all runtime R&D)
                               │
        ┌──────────────────────┼──────────────────────┐
        │                      │                      │
        ▼                      ▼                      ▼
DOTNExT-Runtime-       DOTNExT-Unified-      DOTNExT-Engrams-
Async-Research.md      SafePoints.md          Revised.md
        │                      │                      │
        └──────────────────────┼──────────────────────┘
                               │
                               ▼
               DOTNExT-Process-Image-Persistence.md
                               │
                               ▼
               Vision-Engrams-Cyberspace-Verbatim.md
                               │
                               ▼
               Erlang-BEAM-Architecture-Reference.md


    VAYRON-Architecture-Master.md ──────────────────┐
               │                                     │
    ┌──────────┼──────────┐                         │
    ▼          ▼          ▼                         │
VAYRON-   VAYRON-SDK-  VAYRON-Decision-             │
Component- Design.md     Log.md                     │
Specs.md       │                                    │
               ▼                                    │
    Vision-Async+-Solution.md ──────────────────────┘
               │
               ▼
    DOTNExT-Runtime-Async-Research.md (connects both trees)
```

---

## File Statistics

| Category | Count | For Runtime R&D? |
|----------|-------|------------------|
| Runtime Research | 8 | **CORE** |
| VOS Architecture (NEW) | 6 | **CORE** (stubs to expand) |
| VAYRON Platform | 8 | NOT NEEDED |
| NewOrleans | 5 | NOT NEEDED |
| CLR Background | 6 | CONTEXT |
| External Reference | 4 | 2 CORE (BEAM, Singularity/Midori) |
| Meta/Navigation | 4 | 2 (Primer, BOOTUP) |
| Vision | 5 | 1 CORE (Cyberspace) |
| **Total Active** | 46 | 12-15 needed |

---

## Archived Documents

Located in `/Analysis/Archived/`:

| Document | Why Archived |
|----------|--------------|
| `Analysis-Plan.md` | Superseded by current research |
| `Current-Analysis-Context.md` | Superseded by BOOTUP |
| `Engram-Design-v0.1.md` | Revised in DOTNExT-Engrams-Revised.md |
| `Strategy-Hybrid-Development-Path.md` | Captured in Decision Log |

---

## Token Efficiency Guide

**Problem:** Reading all 39 documents consumes ~70%+ of context window.

**Solution:** Use targeted curricula:

| Task | Documents | Est. Tokens |
|------|-----------|-------------|
| Runtime R&D | Primer only | ~5k |
| Runtime R&D (extended) | Primer + 5 core | ~25k |
| VAYRON understanding | BOOTUP + 3 arch | ~20k |
| Full context | All 39 | ~120k |

**Recommendation:** Start with `DOTNExT-Runtime-RnD-Primer.md` for runtime work. It's designed to be sufficient on its own.

---

---

## VOS Architecture Curriculum (UPDATED 2025-12-11 Session 2)

**For AI working on DOTNExT VOS design.**

| Document | Status | Purpose |
|----------|--------|---------|
| `DOTNExT-VOS-Implementation-Strategy.md` | **START HERE** | **NEW** - Foundational strategy document (v1.0) |
| `DOTNExT-Runtime-RnD-Primer.md` | **CORE** | Runtime R&D foundation (v1.3) |
| `DOTNExT-Process-Model.md` | **CORE** | Process/Pathway abstraction |
| `DOTNExT-Sync-Semantics.md` | **CORE** | sync keyword specification |
| `DOTNExT-Singularity-Midori-Research.md` | **CORE** | MS OS lessons learned |
| `VAYRON-Architecture-Master.md` | **CORE** | Platform architecture |
| `DOTNExT-Security-Model.md` | Stub | VOS security subsystem |
| `DOTNExT-Scheduler-Design.md` | Stub | Scheduler questions |
| `DOTNExT-Distribution-Levels.md` | Stub | Distribution depth |
| `DOTNExT-VOS-Architecture.md` | Stub | VOS framing |

---

## Key Documents Summary

| Document | What It Covers |
|----------|----------------|
| `DOTNExT-VOS-Implementation-Strategy.md` | **Runtime as VOS kernel, universal dynamic types, security interception points, VARIA vs implementation, session verbatim quotes** |
| `DOTNExT-Runtime-RnD-Primer.md` | Async origins, Unwinder techniques, semantic inversion, sync keyword |
| `VAYRON-Architecture-Master.md` | Full platform stack, VNS, VCOM, NewOrleans substrate |
| `Vision-Engrams-Cyberspace-Verbatim.md` | Distributed cyberspace vision, Engram layers |

---

*Index updated 2025-12-11 (Session 2) with DOTNExT-VOS-Implementation-Strategy.md - foundational document on runtime as kernel, dynamic types strategy, security interception.*
