# Nitra Research Plan

> **Purpose:** Deep analysis of JetBrains/Nitra for extractable patterns
> **Repository:** https://github.com/JetBrains/Nitra
> **Status:** Archived (Nov 2023) but code available
> **Language:** ~80% Nemerle, ~17% C#

---

## 1. The Core Hypothesis

Louis's intuition (paraphrased):

> Nemerle compiler-as-library can be used at runtime where the "hosted" Nemerle context can interact with the "host" context. Syntax/grammar/rules compile to IL types. Once compiled, these types can be loaded not just by the hosted context but **by the host itself**. The host could recompile parts of itself and load them - the behavior changes without the source changing, because the **syntax that interprets the source** changed.

This is not the DLR. It's something different:

| DLR | What Louis is describing |
|-----|--------------------------|
| Dynamic dispatch on objects | Dynamic dispatch on **syntax** |
| Types resolved at runtime | **Grammar rules** resolved at runtime |
| Call site caching | Grammar composition caching |
| Same language, dynamic types | **Dynamic language definition** |

The key insight: **If syntax compiles to types, and types can be hot-loaded, then syntax can be hot-loaded.**

---

## 2. What to Investigate

### 2.1 Nemerle Compiler-as-Library Usage

**Question:** How does ZenSharp (and similar projects) use Nemerle internally from C#?

**Where to look:**
- https://github.com/ulex/ZenSharp - C# project using Nemerle
- How does it invoke Nemerle compilation?
- What's the interface between C# host and Nemerle "scripting"?
- Is there context sharing between host and hosted?

**What we're looking for:**
- API for invoking Nemerle compiler at runtime
- How compiled types are loaded back into the host
- Whether host can "see" types compiled by hosted Nemerle
- Whether hosted can "see" host's types

### 2.2 Nitra's Grammar → Types Pipeline

**Question:** How do grammar definitions become IL types?

**Where to look:**
- `Nitra/Nitra.Compiler/Generation/` - The code generation pipeline
- `Nitra/Nitra.Compiler/Generation/Parser/ExtensionRuleParserEmitter.n`
- `Nitra/Nitra.Compiler/Generation/Ast/` - AST class generation

**What we're looking for:**
- How grammar rules become .NET types
- Whether these types are designed for runtime loading
- The assembly generation pattern
- Any use of `AssemblyLoadContext` or similar

### 2.3 Runtime Grammar Composition

**Question:** How does Nitra compose grammars at runtime?

**Where to look:**
- `Nitra/Nitra.Runtime/CompositeGrammar.n`
- `Nitra/Nitra.Runtime/ExtensibleRuleParser/`

**What we're looking for:**
- How multiple grammars are merged
- Whether new grammars can be added to a running parser
- The caching mechanism for composed parsers
- Any "reload" or "refresh" patterns

### 2.4 The Host/Hosted Context Boundary

**Question:** Is there a pattern for Nemerle code running "inside" other Nemerle/C# code with context sharing?

**Where to look:**
- `Nitra/Boot1/`, `Nitra/Boot2/` - Self-bootstrapping stages
- How does Nitra compile itself?
- Is there a "meta-circular" pattern?

**What we're looking for:**
- How Nitra uses Nemerle to compile Nitra
- Whether there's bidirectional context access
- The boundary between "compile-time Nemerle" and "runtime Nemerle"

### 2.5 PegGrammar's `[DynamicExpandable]`

**Question:** How do dynamically expandable rules actually work?

**Where to look:**
- Nemerle repo: `Nemerle.Peg.Macros`
- The `[DynamicExpandable]` attribute implementation
- How rules are "connected and disconnected during parsing"

**What we're looking for:**
- The mechanism for runtime rule modification
- Whether this is true hot-swap or pre-registration
- Performance implications

---

## 3. The Deeper Question

Louis is gesturing at something like:

```
┌─────────────────────────────────────────────────────────────────┐
│  Host Application (C#/Nemerle/F#)                               │
│                                                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  Hosted Nemerle Compiler (runtime)                       │   │
│  │                                                          │   │
│  │  source + grammar → compile → IL types                   │   │
│  │                                    │                     │   │
│  └────────────────────────────────────│─────────────────────┘   │
│                                       │                         │
│                                       ▼                         │
│  AssemblyLoadContext / hot-load ──────┘                         │
│                                                                 │
│  Host now has NEW TYPES from NEW GRAMMAR                        │
│  Host behavior changes without host code changing               │
│  Because the SYNTAX that interprets input changed               │
└─────────────────────────────────────────────────────────────────┘
```

This is **not** an interpreter. It's:
1. Compile syntax to types (IL)
2. Load types into running process
3. Types change how input is parsed/interpreted
4. Behavior evolves without code changing

The "Nemerle runtime" (even though it's really .NET) has a **meta-layer** - the macro system, the compiler-as-library - that enables this pattern.

---

## 4. Relevance to VAYRON

If this pattern exists and can be extracted:

| Pattern | VAYRON Application |
|---------|-------------------|
| Grammar → IL types | VCOM types defined by syntax, compiled at runtime |
| Hot-load grammar types | AI-Objects that evolve their own "language" |
| Host/hosted context sharing | VARIA transformations that can modify themselves |
| Dynamic syntax composition | VNS queries that understand new syntax |

**The "Anytime" vision:**
- No distinction between dev/build/runtime
- AI-Objects compile new syntax for themselves
- Load it into themselves
- Become something different without "changing code"

---

## 5. Files to Create During Research

```
Research/Nitra/
├── Research-Plan.md              ← This file
├── Compiler-as-Library.md        ← How Nemerle compiler is used at runtime
├── Grammar-to-IL.md              ← The compilation pipeline
├── Runtime-Composition.md        ← How grammars are composed at runtime
├── Context-Sharing.md            ← Host/hosted boundaries
├── DynamicExpandable-Rules.md    ← The PegGrammar mechanism
├── Extractable-Patterns.md       ← Summary of what we can use
└── VAYRON-Application.md         ← How to apply to VAYRON/C=
```

---

## 6. Priority Order

1. **ZenSharp analysis** - How C# uses Nemerle at runtime (easiest entry point)
2. **CompositeGrammar.n** - Runtime grammar composition
3. **Generation/Parser/** - Grammar → parser code → IL
4. **Boot1/Boot2** - Self-hosting patterns
5. **PegGrammar `[DynamicExpandable]`** - Dynamic rule mechanism

---

## 7. The Creative Leap

Louis's "creative folly à deux" suggestion:

What if the mechanism inside Nitra/Nemerle could be:
1. **Extracted** from the UI/workbench wrapper
2. **Generalized** into a "syntax runtime" (like DLR is a "type runtime")
3. **Integrated** with DLR for both dynamic types AND dynamic syntax
4. **Applied** to VCOM objects that can define and evolve their own language

This would be genuine **meta-meta-programming**:
- Code that writes code (macros) - Nemerle has this
- Code that writes the language that code is written in - Nitra attempted this
- Code that writes the system that writes languages - **not yet done**

For AI-Objects that evolve: they don't just change their code, they change **what code means**.

---

*Research plan created 2025-12-07. To be executed when diving into Nitra repository.*
