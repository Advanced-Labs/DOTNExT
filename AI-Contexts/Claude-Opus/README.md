# Claude Opus Context Directory

**Purpose**: Working notes, analysis, and research that persists across context windows.

## Quick Start for New Sessions

**Read `CONTINUATION-PROTOCOL.md` first** - it explains how to orient yourself.

## Contents

| File | Purpose |
|------|---------|
| `CONTINUATION-PROTOCOL.md` | **READ FIRST** - How to continue across context resets |
| `DOTNExT-Vision.md` | The grand vision - Meta-OS, Memantics, C*, etc. |
| `SESSION-LOG.md` | What happened in each session |
| `AsyncDistributedComputing-Assessment.md` | Analysis of async/await for distribution |

## The Project in One Sentence

DOTNExT is evolving .NET from a runtime into a distributed, semantically-aware Meta-OS through staged modifications from lib/framework → Roslyn codegen → VM primitives.

## Key Concepts Quick Reference

- **Soft persistence**: Checkpoint state for recovery/transfer (async state machines)
- **Hard persistence**: Canonical application state (database-like commits)
- **Memantics**: Semantic memory - objects with embeddings, affinities, emergent behaviors
- **C***: C# superset as universal IL / transpilation target
- **Version drainage**: Old states complete on old code; new calls → new version
