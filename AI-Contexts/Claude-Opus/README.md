# Claude Opus Context Directory

**Purpose**: Working notes, analysis, and research that persists across context windows.

## Quick Start for New Sessions

**Read `CONTINUATION-PROTOCOL.md` first** - it explains how to orient yourself.

---

## Contents

### Core Documentation

| File | Purpose |
|------|---------|
| `CONTINUATION-PROTOCOL.md` | **READ FIRST** - How to continue across context resets |
| `DOTNExT-Vision.md` | The grand vision - Meta-OS, Memantics, C*, etc. |
| `SESSION-LOG.md` | What happened in each session |
| `CURRENT-WORK.md` | Current task status and implementation details |

### Async Persistence (Roslyn+ / Orleans)

| File | Purpose |
|------|---------|
| `AsyncPersistence-Research.md` | Research on async state machine persistence |
| `AsyncDistributedComputing-Assessment.md` | Analysis of async/await for distribution |
| `RoslynModification-Design.md` | Design for Roslyn compiler modifications |
| `ROSLYN-BUILD-PROCEDURES.md` | How to build modified Roslyn compiler |
| `AsyncPlus-Scenarios.md` | Test scenarios (C1-C9, R1) with risk/value analysis |
| `AsyncPlus-SiloPatterns.md` | Orleans silo orchestration patterns |

### NewOrleans / Orleans Integration

| File | Purpose |
|------|---------|
| `NewOrleans-AsyncPlus-Integration.md` | Async+ integration with Orleans |
| `PluginGrainArchitecture.md` | Dynamic grain loading architecture |
| `DynamicGrainAccess.md` | Dynamic grain access patterns |

---

## The Project in One Sentence

DOTNExT is evolving .NET from a runtime into a distributed, semantically-aware Meta-OS through staged modifications from lib/framework → Roslyn codegen → VM primitives.

## Key Concepts Quick Reference

- **Soft persistence**: Checkpoint state for recovery/transfer (async state machines)
- **Hard persistence**: Canonical application state (database-like commits)
- **Memantics**: Semantic memory - objects with embeddings, affinities, emergent behaviors
- **C***: C# superset as universal IL / transpilation target
- **Version drainage**: Old states complete on old code; new calls → new version

## Current Focus (2025-12)

- **R1 Roslyn+ Cross-Session Persistence**: VERIFIED
- **C1 Hand-coded Cross-Session Persistence**: VERIFIED (legacy)
- **Next**: C2-C9 scenarios using Roslyn+ approach

See `CURRENT-WORK.md` for latest status.
