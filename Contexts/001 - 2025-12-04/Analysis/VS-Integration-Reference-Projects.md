# Visual Studio Integration Reference Projects

> **Document Type:** Research Reference
> **Version:** 1.0
> **Date:** 2025-12-07
> **Purpose:** Catalog of open-source projects demonstrating full-stack language/runtime VS integration
> **Status:** Reference material for VAYRON SDK development

---

## 1. Overview

This document catalogs open-source projects that demonstrate full-stack language/runtime integration with Visual Studio. These serve as **pattern-mines** for building VAYRON SDK's VS2022 integration.

**Key insight:** Almost all successful VS language integrations follow a common architectural pattern (see Section 4).

---

## 2. Tier 1: Microsoft "Data Science" Stack (PTVS/RTVS/NTVS)

These three were built by essentially the same team and provide three variants of "custom language + external runtime + rich tooling + Visual Studio integration". They're our most valuable reference set.

### 2.1 Python Tools for Visual Studio (PTVS)

**Repository:** `github.com/microsoft/PTVS`
**License:** Apache 2.0
**VS Support:** Up to 2015 as plugin, then became in-box Python workload in VS 2017+

**Features demonstrated:**
- IntelliSense, debugging (local & mixed C++/Python), profiling
- MPI cluster debugging, virtualenv/conda environments
- Test explorer, REPL, interactive windows

**Why it's gold as a model:**

| Subsystem | What you can learn |
|-----------|-------------------|
| VSIX + VSPackage | Core package registration: menus, project types, options, tool windows |
| Custom project system | Python environments (per-project + global), MSBuild integration |
| Editor & language service | Parser + AST + analyzer in background process, IntelliSense hooks |
| Debugging | Debug engine speaking Python debug protocol, mapping to VS `IDebug*` COM interfaces |
| Mixed-mode debug | Python and C++ debug engines cooperating |
| REPL & interactive | Interactive window with history, multi-line editing, "Send selection to REPL" |

**Best for:** "How to plug a non-.NET runtime into VS"

---

### 2.2 R Tools for Visual Studio (RTVS)

**Repository:** `github.com/microsoft/RTVS`
**License:** MIT
**VS Support:** Shipped with VS 2017 Data Science workload, support stopped before VS 2019

**Features demonstrated:**
- IntelliSense, debugging, plotting, variable explorer
- Remote execution, SQL integration, RMarkdown

**Why it's extra interesting (remote/cluster semantics):**

| Subsystem | What you can learn |
|-----------|-------------------|
| Remote execution model | Separate R host processes (local/remote), custom protocol |
| Session abstraction | Multi-session management, per-session workspace |
| Plot/workspace tooling | Custom tool windows for plots, variable explorer, data table viewer |
| RMarkdown integration | "Compile RMarkdown" toolchain wired into VS build/commands |
| Multi-process debugging | Debugging scripts on remote hosts with VS as front end |

**Best for:** "Runtime = remote service / cluster" pattern - **directly relevant for VAYRON Nodes**

---

### 2.3 Node.js Tools for Visual Studio (NTVS)

**Repository:** `github.com/microsoft/nodejstools`
**License:** Apache 2.0
**VS Support:** Evolved into built-in "Node.js development" workload in VS 2017/2019

**Features demonstrated:**
- Node project system, IntelliSense, npm integration
- Debugging, profiling, unit test discovery

**Why it matters:**

| Subsystem | What you can learn |
|-----------|-------------------|
| Project system + MSBuild | Node projects with build/debug hooked into MSBuild & VS config |
| npm integration | Tools window + commands for package management |
| Debug engine | Node debug adapter mapping to VS debugger interfaces |

**Best for:** External CLI toolchain integration

---

## 3. Tier 2: Non-Microsoft Compiled Language Integrations

These show "full compiler toolchain + project system + language service + debugger" - relevant for compiled language / custom runtime scenarios.

### 3.1 Visual D (D Programming Language)

**Repository:** `github.com/dlang/visuald`
**License:** BSL-1.0 (Boost Software License)
**VS Support:** **VS 2008-2022** (actively maintained!)

**Architectural highlights:**

| Subsystem | What you can learn |
|-----------|-------------------|
| Project system | Native D project types, MSBuild integration, multiple toolchains (DMD, LDC, GDC) |
| Language service | Syntax highlighting, navigation, completion via VS editor APIs |
| Debugger | Custom debug engine + MSVC debugger integration for D binaries |

**Best for:** One of the **few actively maintained, VS 2022-compatible, non-Microsoft full language integrations**

---

### 3.2 Visual Rust

**Repository:** `github.com/PistonDevelopers/VisualRust`
**License:** MIT/Apache 2.0 (same as Rust)
**VS Support:** Various versions

**Architectural highlights:**

| Subsystem | What you can learn |
|-----------|-------------------|
| Dual packaging | MSI for MSBuild integration + VSIX for IDE bits |
| Project system | Rust-specific `.rsproj`, templates, cargo bridging |
| Debugger | Wiring Rust compiler + GDB into VS debugging pipeline |

**Best for:** Grafting non-MS toolchain into classic VS

---

### 3.3 X# (XSharpPublic)

**Repository:** `github.com/X-Sharp/XSharpPublic`
**License:** Open source

**What makes it unique:**
- **Full .NET language and runtime** with compiler, not just VS extension
- Compiler front-end + Roslyn-style integration
- VS project types, item templates, tooling

**Best for:** "Build a whole language stack + VS integration on top of .NET" - **closest to what VAYRON is doing**

---

## 4. Tier 3: Smaller but Instructive Integrations

Cleaner and more approachable - good for specific subsystem patterns.

### 4.1 Visual Studio Tools for Lua (VSLua)

**Repository:** `github.com/microsoft/VSLua`
**License:** Microsoft

**Why it's good model code:**
- Smaller surface area than PTVS/RTVS
- Custom language service for dynamic language
- Lua runtime and debug engine integration
- Some project system and tooling

**Best for:** Minimal full-stack example

---

### 4.2 AsmDude

**Repository:** `github.com/HJLebbink/asm-dude`
**License:** Open source

**Why it's useful:**
- Focused completely on **editor + language service**
- Classification, tagging, completion providers
- Parsing + semantics for multiple asm dialects

**Best for:** Minimal model of "custom language inside the VS editor" without project system overhead

---

### 4.3 VS SDK Language Integration Samples (Ook, etc.)

**Repository:** `github.com/microsoft/VSSDK-Extensibility-Samples`
**License:** Microsoft

**What it contains:**
- `Ook_Language_Integration` - toy language demonstrating:
  - Classification
  - IntelliSense
  - Outlining
  - Project system & build targets

**Best for:** *The* canonical minimal example of "how Visual Studio wants you to plug in a language"

---

## 5. Other Relevant Integrations

### 5.1 F# Visual Studio Integration

**Location:** `dotnet/fsharp` repo, `vsintegration` subtree

**Contains:**
- Editor, project system, language service & tooling
- Built around F# compiler service

**Best for:** Full .NET language integration in modern VS

### 5.2 Visual Studio Tools for Unity

**Notes:** Not fully OSS, but analyzers and some parts are public

**Best for:** Workflow integration (messages, asset references) in hybrid engine environment

---

## 6. Common Architectural Pattern

All successful VS language integrations follow this stack:

### 6.1 Layer 1: VSIX + Package

```
- VSIX manifest registration
- AsyncPackage or legacy Package implementing:
  - Service registration (language service, project factory, debugger, tool windows)
  - Menu commands and options
- MEF exports for editor components (classifiers, completion sources, etc.)
```

### 6.2 Layer 2: Project System / Build Integration

```
Options:
- CPS (Common Project System) - modern, MSBuild-backed (Visual D, X#, newer)
- Legacy MPF project system (older PTVS/RTVS/VisualRust)

Defines:
- Project type GUIDs and capabilities
- Item templates (file templates, project templates)
- Custom MSBuild targets that call compiler/interpreter

For script-first languages (Python/R/Lua):
- "Build" often means validation, lint, packaging, test runs
```

### 6.3 Layer 3: Language Service / Editor Integration

```
- Lex/parse language into AST
- Background analysis for symbols, references, diagnostics
- Hook into editor through:
  - Classification & colorization
  - Completion providers
  - Quick info / tooltips
  - Signature help
  - Go-to-definition / find all references

Modern trend: LSP-like server with process boundary between VS and language engine
```

### 6.4 Layer 4: Debug Engine & Runtime Bridge

```
Implement VS debug engine (IDebugEngine2, etc.):
- Map breakpoints, step, threads, stack frames, locals, watches, exceptions

For compiled languages with MSVC ABI (D, X#):
- Reuse MSVC debug engine where possible with translation layer

For remote/dynamic runtimes (Python/R/Node/Lua):
- Debug adapter / debug server inside the runtime
- Custom protocol (or DAP-like) over TCP/STDIO to VS debug engine
```

### 6.5 Layer 5: Interactive / REPL / Tool Windows

```
- Interactive console windows (PTVS/RTVS):
  - Session output
  - Execute snippets with history
  - "Send selection to REPL"

- Tool windows:
  - Variable explorer (RTVS)
  - Environment managers (PTVS)
  - Package managers (npm in NTVS, R packages in RTVS)
```

### 6.6 Layer 6: Runtime Orchestration

```
- Local: Start local interpreter/compiler on demand
- Remote: Spin up remote hosts, manage sessions, reconnect, handle versioning (RTVS)
- Multi-runtime: Multiple environments per solution
```

---

## 7. Mapping to VAYRON SDK

### 7.1 Which Blueprint Fits VAYRON?

**Primary: "Dynamic language over remote runtime" (RTVS + PTVS)**

Why:
- VAYRON Nodes are remote runtime hosts
- VNS provides dynamic discovery
- REPL / interactive exploration is core to VARIA experience
- Variable explorer maps to VCOM object inspector

**Secondary: "Compiled language on MS toolchain" (Visual D + X#)**

Why:
- We use Roslyn fork for compilation
- VARIA transformation is compile-time
- Strong typing at build time (with dynamic fallback)

### 7.2 Specific Pattern Mapping

| VAYRON Component | Reference Project | Pattern to Extract |
|------------------|-------------------|-------------------|
| VAYRON.Sdk (MSBuild) | Visual D, X# | Custom SDK props/targets |
| VAYRON.VisualStudio (VSIX) | PTVS, RTVS | AsyncPackage, tool windows |
| VNS IntelliSense | RTVS | Dynamic completion from runtime |
| VCOM Object Inspector | RTVS Variable Explorer | Tool window with live updates |
| VAYRON Node Management | RTVS Remote Sessions | Session abstraction, reconnection |
| VARIA REPL | PTVS Interactive | REPL with object exploration |
| Debug Engine | PTVS Mixed-Mode | Managed + native debugging |

### 7.3 VS 2022 Specific Considerations

- 64-bit only process; old 32-bit assumptions may break
- Microsoft pushing "Modern Visual Studio Extensibility" but classic VSIX + VSPackage + CPS still works
- Can mix: Classic VSIX for editor & project system + off-process language servers for heavy lifting

---

## 8. Curated Corpus for AI/Training

If using these repos to train/guide AI coding agents:

### 8.1 Full Repository List

```
github.com/microsoft/PTVS
github.com/microsoft/RTVS
github.com/microsoft/nodejstools
github.com/dlang/visuald
github.com/PistonDevelopers/VisualRust
github.com/microsoft/VSLua
github.com/HJLebbink/asm-dude
github.com/X-Sharp/XSharpPublic
github.com/microsoft/VSSDK-Extensibility-Samples
github.com/dotnet/fsharp  (vsintegration sub-tree)
```

### 8.2 Partitioned by Concern

| Concern | Best References |
|---------|-----------------|
| Editor / Language Service | AsmDude, VSLua, Ook sample, PTVS/RTVS language service |
| Project System & MSBuild | Visual D, Visual Rust, X#, NTVS, PTVS/RTVS project code |
| Debug & Runtime Integration | PTVS, RTVS, NTVS, Visual D, Visual Rust |
| Interactive Tooling | PTVS, RTVS (REPL, plots, variable explorers) |

### 8.3 Key Patterns to Annotate

For AI to reproduce patterns effectively:

- How a **language service** is wired (lexer/parser location, IClassifier/ICompletionSource hooks)
- How a **debug engine** maps runtime protocol ↔ VS interfaces
- How a **project system** maps MSBuild targets to compiler/interpreter invocations
- How **remote sessions** are modeled (RTVS) and client/server protocol shape
- How projects handle **multi-version VS support** and 32→64-bit shifts

---

## 9. Licensing Notes

| Project | License | Usage Notes |
|---------|---------|-------------|
| PTVS | Apache 2.0 | Free to use as reference |
| RTVS | MIT | Free to use as reference |
| NTVS | Apache 2.0 | Free to use as reference |
| Visual D | BSL-1.0 | Free to use as reference |
| Visual Rust | MIT/Apache 2.0 | Free to use as reference |
| X# | Open source | Check specific terms |
| VSLua | Microsoft | Check specific terms |
| AsmDude | Open source | Check specific terms |

**General guidance:**
- Using as **reference** or for training AI is generally fine
- Don't blindly clone or lightly obfuscate their code
- If copying non-trivial pieces, track provenance and obey attribution requirements

---

## 10. Concrete Blueprints

### Blueprint 1: "Dynamic language over remote runtime"

**Base on:** RTVS + PTVS

```
- External runtime host processes (VAYRON Nodes)
- JSON/RPC-style control protocol
- VS package + editor + REPL + debugger
```

**Good fit for:** VAYRON's runtime model with Nodes, VNS exploration, object inspector

### Blueprint 2: "Compiled language on MS toolchain"

**Base on:** Visual D + X#

```
- MSBuild-backed project types
- CLI compiler integrated with MS toolchain
- Debug engine wrapping MSVC debug infrastructure
```

**Good fit for:** VARIA compilation, strong typing, Roslyn integration

### Blueprint 3: "Embedded DSL / bridge to external engine"

**Base on:** VSLua + AsmDude + Ook sample

```
- Minimal or no project system
- Rich editor integration
- Thin run/debug pipeline that shells out to engine
```

**Good fit for:** Specialized DSLs, configuration languages

---

## 11. Next Steps

1. **Clone key repos locally** for exploration:
   - RTVS (remote runtime model)
   - Visual D (VS 2022 compatibility)
   - X# (full .NET language stack)

2. **Extract specific patterns** for:
   - AsyncPackage initialization
   - Tool window registration
   - Custom completion source
   - Session management

3. **Prototype VAYRON.VisualStudio** using RTVS as structural template

---

*This document provides reference material for VAYRON SDK VS integration. The listed projects have solved the problems we need to solve - we should learn from them rather than reinvent.*

*Version 1.0 - 2025-12-07*
