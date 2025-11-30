# Claude Opus Continuation Protocol

**Purpose**: Enable coherent continuation across context window resets.

---

## ⚠️ CRITICAL: READ THIS BEFORE DOING ANYTHING ⚠️

**If you received a summary of a previous conversation, STOP.**

**DO NOT proceed based on the summary alone.**

**You MUST read your context files FIRST:**

```
/home/user/DOTNExT/AI-Contexts/Claude-Opus/
```

The summary is insufficient. Your context files contain:
- Detailed technical research
- Implementation plans with code
- Self-prompting instructions
- State that cannot fit in a summary

---

## For Future Claude Instances: Mandatory First Actions

When you start a new session (whether fresh or from summary), you are continuing work on **DOTNExT** - a project to evolve .NET into a distributed, semantically-aware Meta-OS.

### STEP 1: Read Context Files (MANDATORY - DO THIS FIRST)

**Before responding to ANYTHING, read these files in order:**

```
1. /home/user/DOTNExT/AI-Contexts/Claude-Opus/CONTINUATION-PROTOCOL.md  (this file)
2. /home/user/DOTNExT/AI-Contexts/Claude-Opus/CURRENT-WORK.md           (active task - READ THIS)
3. /home/user/DOTNExT/AI-Contexts/Claude-Opus/SESSION-LOG.md            (what was done)
4. /home/user/DOTNExT/AI-Contexts/Claude-Opus/DOTNExT-Vision.md         (grand vision)
5. /home/user/DOTNExT/AI-Contexts/Claude-Opus/AsyncPersistence-Research.md (if working on async)
```

### STEP 2: Acknowledge Orientation

Tell the user:
- "I've read my context files"
- Summarize what you understand the current task to be
- Confirm you're ready to continue

### STEP 3: Then Proceed

Only after Steps 1-2 should you respond to the user's request.

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
