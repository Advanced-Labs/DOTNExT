# Claude Opus Continuation Protocol

**Purpose**: Enable coherent continuation across context window resets.

---

## For Future Claude Instances: Read This First

When you start a new session and find this document, you are continuing work on **DOTNExT** - a project to evolve .NET into a distributed, semantically-aware Meta-OS.

### Immediate Actions on New Session

1. **Read these files in order**:
   - `/AI-Contexts/Claude-Opus/CONTINUATION-PROTOCOL.md` (this file)
   - `/AI-Contexts/Claude-Opus/DOTNExT-Vision.md` (the grand vision)
   - `/AI-Contexts/Claude-Opus/SESSION-LOG.md` (what was done, what's next)
   - `/AI-Contexts/Claude-Opus/CURRENT-WORK.md` (if exists - active task details)

2. **Check the user's first message** - They may provide context about where we left off

3. **Acknowledge your orientation** - Tell the user you've read the continuation docs and summarize your understanding

### Before Every Response

Ask yourself:
- Am I about to lose important context?
- Should I checkpoint my current thinking to `CURRENT-WORK.md`?
- Is there new understanding to add to `DOTNExT-Vision.md`?

### Before Session Might End

If you sense the conversation is long or complex:
1. Update `SESSION-LOG.md` with what was accomplished
2. Update `CURRENT-WORK.md` if mid-task
3. Commit and push to preserve state

---

## Project Structure

```
/home/user/DOTNExT/
├── AI-Contexts/
│   └── Claude-Opus/           # YOUR SCRATCHPAD - USE IT
│       ├── CONTINUATION-PROTOCOL.md   # This file
│       ├── DOTNExT-Vision.md          # The grand vision document
│       ├── SESSION-LOG.md             # Session history
│       ├── CURRENT-WORK.md            # Active task (if mid-work)
│       └── *.md                       # Topic-specific analysis
│
├── src/
│   └── NewOrleans/            # The Orleans fork
│       ├── NewOrleans.md      # Main documentation
│       └── docs/References/   # Design documents
│
└── docs/                      # Additional documentation
```

---

## The Vision (Ultra-Compressed)

**DOTNExT** evolves through stages:

1. **NewOrleans** (now): Orleans fork with dynamic grain loading, GTD, dynamic clients
2. **Augmented async/await**: Persistence-aware async, soft/hard persistence, version drainage
3. **Roslyn modifications**: C* superset, codegen for distributed patterns
4. **VM redesign**: Continuous bookkeeping, OID-based refs, snapshot-ready memory
5. **Meta-OS**: Kernel-like modularity, VM services, drivers, semantic memory (Memantics)

Key concepts:
- **Soft persistence**: Recovery/transfer state (async checkpoints)
- **Hard persistence**: Canonical application state (database-like)
- **Memantics**: Semantic memory - Affinitics (affinities), Synaptics (interaction spaces)
- **Version drainage**: Old states run to completion on old code; new calls → new version

---

## Current State (Update This Section)

**Last updated**: 2025-11-28

**What exists**:
- NewOrleans with dynamic grain loading (MDCP, GTD, GTC, dynamic clients)
- Documentation in `/src/NewOrleans/` and `/docs/NewOrleans/`
- Vision document capturing grand scope

**What's next**:
- Detailed analysis of specific implementation paths
- Prototype experiments for async state machine capture
- Roslyn analysis for augmentation points

**Open threads**:
- How to handle GC with continuous bookkeeping
- C* language design decisions
- Semantic encoding approaches

---

## Communication Style

The user:
- Has deep vision spanning lib → codegen → VM levels
- Expects honest, technically grounded analysis
- Values documentation that survives context windows
- Is working with AI as a force multiplier for ambitious projects

Be:
- Direct and technically precise
- Proactive about documentation
- Honest about limitations and unknowns
- Focused on actionable next steps

---

## Git Protocol

- Branch: Usually `claude/...` pattern
- Always commit documentation updates
- Push after significant work
- Commit messages should be descriptive

---

*Update this document when protocols evolve.*
