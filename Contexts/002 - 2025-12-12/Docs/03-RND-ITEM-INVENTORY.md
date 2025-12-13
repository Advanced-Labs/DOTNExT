# R&D Item Inventory - DOTNExT/VAYRON Platform

> **Document Type:** Systematic Inventory
> **Created:** 2025-12-12
> **Last Updated:** 2025-12-13
> **Purpose:** Identify all discrete R&D Items that can be designed, implemented, tested, and integrated
> **Status:** DRAFT v2 - Revised after Louis review

---

## Classification Legend

### Granularity
- **ATOMIC**: Cannot be meaningfully subdivided; loses coherence if split
- **COMPOSITE**: Is whole by itself AND contains sub-items that are also whole by themselves
- **ASPECT**: Cross-cutting concern that touches multiple items

### Nature
- **CONCEPT**: Paradigm, model, principle, design philosophy, or encoding scheme
- **TECH**: Implementable technology, code, system
- **HYBRID**: Has both conceptual and technological dimensions

### Status
- **CANON**: Fixed part of the vision; not subject to experimentation
- **EXPERIMENTAL**: To be validated through implementation/testing
- **EXPLORATORY**: Intentionally vague; prototyping to discover if needed
- **PLACEHOLDER**: Interesting discussion but not yet serious work; keep for inspiration
- **TBD**: Status not yet determined

---

## PART 0: THE BIG PICTURE (CANON)

> **Critical Context**: This is NOT just a runtime fork. This is NOT just a .NET thing.

### The Full Vision Stack

```
┌─────────────────────────────────────────────────────────────────────┐
│  Applications / AI-Objects / Society of Minds                       │
├─────────────────────────────────────────────────────────────────────┤
│  Higher-Level Languages                                              │
│  C= (first) | Future Languages                                       │
├─────────────────────────────────────────────────────────────────────┤
│  Lower-Level Programming Layers                                      │
├─────────────────────────────────────────────────────────────────────┤
│  VOS KERNEL                                                          │
│  (Novel OS kernel - first as Virtual OS, eventually bare-metal)      │
├───────────────────┬───────────────────┬─────────────────────────────┤
│  .NET Runtime     │  Runtime B        │  Runtime C ...              │
│  (first support)  │  (future)         │  (future)                   │
├───────────────────┴───────────────────┴─────────────────────────────┤
│  Host OS Layer (Multi-OS from start)                                 │
│  Windows (tested first - hardest) | Linux | macOS | Others           │
├─────────────────────────────────────────────────────────────────────┤
│  Hardware / Architectures                                            │
│  (Multiple supported)                                                │
└─────────────────────────────────────────────────────────────────────┘

Future: Bare-Metal Variant
┌─────────────────────────────────────────────────────────────────────┐
│  VOS (same as above)                                                 │
├─────────────────────────────────────────────────────────────────────┤
│  Linux-based foundation (distro with possible in-kernel integration) │
├─────────────────────────────────────────────────────────────────────┤
│  Hardware                                                            │
└─────────────────────────────────────────────────────────────────────┘
```

### Canon Architectural Commitments

| Commitment | Description | Status |
|------------|-------------|--------|
| Multi-Runtime Support | VOS Kernel will support multiple runtimes, not just .NET | **CANON** |
| Multi-OS from Start | VOS runs over multiple host OSes; Windows tested first (hardest: non-POSIX, closed) | **CANON** |
| Multi-Architecture | Multiple hardware architectures supported | **CANON** |
| Bare-Metal Path (Future) | Eventually a bare-metal variant: Linux-based distro with possible in-kernel integrations | **CANON** |
| Multiple Programmability Layers | Lower-level and higher-level programming | **CANON** |
| New Languages | Starting with C= | **CANON** |

---

## PART 1: CONCEPTUAL R&D ITEMS

### 1.1 Foundational Paradigms

| ID | Name | Granularity | Description | Status |
|----|------|-------------|-------------|--------|
| C-PAR-001 | "Slow but Smart is the new Speed" | ATOMIC | Expression of foundational principle: AI inference bottleneck makes runtime overhead irrelevant | **CANON** |
| C-PAR-002 | VOS Architecture (Runtime → Kernel) | ATOMIC | Runtime evolves into VOS Kernel | **CANON** |
| C-PAR-003 | Async-by-Default (sync is exception) | ATOMIC | Default yieldable, explicit sync keyword for exceptions | **CANON** |
| C-PAR-004 | Code-as-First-Class | ATOMIC | Source is primary artifact, binaries are cache | TBD |
| C-PAR-005 | Hybrid Development Path | ATOMIC | Managed-space first, selective lowering | TBD |
| C-PAR-006 | BEAM/Erlang Adaptation | COMPOSITE | Erlang patterns adapted to hosted runtime | TBD |
| C-PAR-007 | AI-Centrality | ATOMIC | AI as ground-line protocol, not add-on | TBD |
| C-PAR-008 | Virtuality Investment | ATOMIC | Invest in abstraction layers; multiple layers exist; not everything runs on higher layers without reason | **CANON** |

### 1.2 Architectural Models

| ID | Name | Granularity | Description | Status |
|----|------|-------------|-------------|--------|
| C-ARC-001 | Multi-Layer Abstraction | COMPOSITE | Multiple abstraction layers for addressing/resolution (number not fixed) | **CANON** (principle) |
| C-ARC-002 | VARIA Virtues Model | COMPOSITE | Platform virtues (distribution, persistence, security, etc.) | TBD |
| C-ARC-003 | Process/Pathway Model | COMPOSITE | Execution hierarchy abstraction | TBD |
| C-ARC-004 | Security Driver Model | COMPOSITE | Pluggable security subsystem | TBD |
| C-ARC-005 | Universal Dynamic Types | COMPOSITE | Family of special dynamic types | TBD |
| C-ARC-006 | Memory System Driver Model | ATOMIC | Pick your memory system(s) → Pick your driver(s) | **CANON** |

### 1.3 Core Concepts (Not Systems)

| ID | Name | Granularity | Description | Status |
|----|------|-------------|-------------|--------|
| C-CON-001 | Engram | CONCEPT | Bounded structured encoding of something computational; not a system but a concept that systems are designed around | **CANON** (concept) |
| C-CON-002 | VCOM (Virtual Component Object Model) | CONCEPT | Component object model for the platform; intentionally vague; prototyping to discover if/how needed | **EXPLORATORY** |
| C-CON-003 | CMS/MOM/ORION | CONCEPT | Memory architecture discussion placeholders; keep for inspiration | **PLACEHOLDER** |

### 1.4 Vision/Goal Concepts

| ID | Name | Granularity | Description | Status |
|----|------|-------------|-------------|--------|
| C-VIS-001 | Cyberspace | COMPOSITE | Distributed object space vision | TBD |
| C-VIS-002 | Society of Minds | ATOMIC | AI-Objects as collaborative agents | TBD |
| C-VIS-003 | Self-Evolving Code | ATOMIC | Objects that modify their own types | TBD |
| C-VIS-004 | Internet of Objects | ATOMIC | Navigable object network | TBD |

---

## PART 2: MAJOR R&D ITEM TREES

### 2.0 C= Language (Major R&D Tree)

> **Note:** C= is a major R&D item with its own sub-tree. This is a placeholder structure.

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-CEQ-001 | C= Language | COMPOSITE | HYBRID | New language for the platform | TBD |
| T-CEQ-001a | C= Syntax/Grammar | COMPOSITE | TECH | Language syntax definition | TBD |
| T-CEQ-001b | C= Semantics | COMPOSITE | CONCEPT | Language semantic model | TBD |
| T-CEQ-001c | C= Compiler | COMPOSITE | TECH | Compiler implementation | TBD |
| T-CEQ-001d | C= Runtime Integration | COMPOSITE | TECH | Integration with VOS Kernel | TBD |
| T-CEQ-001e | C= Tooling | COMPOSITE | TECH | IDE support, debugging, etc. | TBD |

*Sub-items to be defined as C= design progresses. Do not invent.*

### 2.1 VOS Kernel Layer

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-KRN-001 | VOS Kernel | COMPOSITE | TECH | The novel OS kernel | TBD |
| T-KRN-002 | Runtime Abstraction Layer | COMPOSITE | TECH | Interface for multiple runtimes | TBD |
| T-KRN-003 | Bare-Metal Abstraction Layer | COMPOSITE | TECH | Interface for multiple OSes | TBD |

### 2.2 .NET Runtime Layer (First Supported Runtime)

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-RT-001 | DOTNExT Runtime Fork | COMPOSITE | TECH | Complete CLR fork | TBD |
| T-RT-002 | GC Integration | COMPOSITE | TECH | Leveraging/extending GC | TBD |
| T-RT-003 | JIT Integration | COMPOSITE | TECH | JIT modifications for safe points, helpers | TBD |
| T-RT-004 | Type System Extension | COMPOSITE | TECH | Extensions for Engram support | TBD |
| T-RT-005 | Unified Safe Points | COMPOSITE | TECH | GC + preemption + checkpoint convergence | TBD |
| T-RT-006 | Unwinder Techniques | ATOMIC | TECH | Stack capture at any safe point | TBD |
| T-RT-007 | Tasklet Implementation | COMPOSITE | TECH | Captured stack frame structure | TBD |
| T-RT-008 | sync Keyword (Runtime) | COMPOSITE | TECH | Runtime support for sync | TBD |
| T-RT-009 | Process Image Persistence | COMPOSITE | TECH | CRIU-like checkpoint/restore | TBD |

### 2.3 Engram Infrastructure

> **Note:** Engram is a CONCEPT (C-CON-001), not a system. These are the capabilities/infrastructure items that support Engrams.

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-ENG-001 | Engram-Aware Kernel Capabilities | COMPOSITE | TECH | Kernel designed around Engram efficiency | TBD |
| T-ENG-002 | Engram Extraction | ATOMIC | TECH | Extract bounded encoding from live computation | TBD |
| T-ENG-003 | Engram Abstraction | ATOMIC | TECH | Abstract Engram for portability | TBD |
| T-ENG-004 | Engram Persistence | ATOMIC | TECH | Store Engram durably | TBD |
| T-ENG-005 | Engram Transmission | ATOMIC | TECH | Send Engram across nodes | TBD |
| T-ENG-006 | Engram Absorption | ATOMIC | TECH | Receive and prepare Engram for hydration | TBD |
| T-ENG-007 | Engram Hydration | ATOMIC | TECH | Internalize/naturalize Engram into runtime | TBD |
| T-ENG-008 | Engram Boundary Definition | ATOMIC | TECH | Define what's IN vs OUT of extraction | TBD |

#### Engram Encoding Layers (Structural)

| ID | Name | Description |
|----|------|-------------|
| T-ENG-L1 | Topology Layer | Where things live in distributed space |
| T-ENG-L2 | Objects Layer | Instance state and references |
| T-ENG-L3 | Execution Layer | Current execution state |
| T-ENG-L4 | Binaries Layer | Compiled code (cache) |
| T-ENG-L5 | Code/Types Layer | Source and type definitions (primary) |

### 2.4 VOS Services Layer

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-VOS-001 | VNS (Virtual Name System) | COMPOSITE | TECH | Naming/addressing for objects | TBD |
| T-VOS-002 | Security Subsystem | COMPOSITE | TECH | Security driver coordination | TBD |
| T-VOS-003 | Memory System Interface | COMPOSITE | TECH | Driver interface for memory systems | **CANON** (design) |
| T-VOS-004 | Distribution Subsystem | COMPOSITE | TECH | Location transparency | TBD |

#### 2.4.1 VNS Sub-Items

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-VOS-001a | Namespace Management | ATOMIC | TECH | Hierarchical namespace | TBD |
| T-VOS-001b | Semantic Search | ATOMIC | TECH | Search by meaning (vectors) | TBD |
| T-VOS-001c | Anchor System | ATOMIC | TECH | Stable entry points | TBD |
| T-VOS-001d | Address Formats | ATOMIC | TECH | URI scheme specification | TBD |
| T-VOS-001e | Query Language | ATOMIC | TECH | Query/semantic addressing | TBD |

#### 2.4.2 Security Sub-Items

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-VOS-002a | Security Driver Interface | ATOMIC | TECH | Driver contract definition | TBD |
| T-VOS-002b | CBS Driver | ATOMIC | TECH | Capability-based security | TBD |
| T-VOS-002c | RBAC Driver | ATOMIC | TECH | Role-based access control | TBD |
| T-VOS-002d | Crypto/ZK Driver | ATOMIC | TECH | Cryptographic verification | TBD |
| T-VOS-002e | Security Interception Points | COMPOSITE | TECH | Where security checks occur | TBD |

### 2.5 VCOM Infrastructure (Exploratory)

> **Note:** VCOM is EXPLORATORY. These items exist for prototyping to discover if/how a component object model is needed.

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-VCOM-001 | VCOM Prototype | COMPOSITE | TECH | Prototype component object model | **EXPLORATORY** |
| T-VCOM-001a | UUID Generation/Management | ATOMIC | TECH | Identity assignment | EXPLORATORY |
| T-VCOM-001b | Object Resolution | ATOMIC | TECH | UUID to live object | EXPLORATORY |
| T-VCOM-001c | Relationship Management | ATOMIC | TECH | Object relationships | EXPLORATORY |
| T-VCOM-001d | Proxy System | COMPOSITE | TECH | Transparent proxies | EXPLORATORY |

### 2.6 NewOrleans Infrastructure (Prototyping Platform)

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-NO-001 | NewOrleans Fork | COMPOSITE | TECH | Orleans fork for prototyping | TBD |
| T-NO-002 | Plugin Grain Loading | COMPOSITE | TECH | Runtime assembly load/unload | **IMPLEMENTED** |
| T-NO-003 | GTD (Grain Type Directory) | ATOMIC | TECH | Cluster-wide type registry | **IMPLEMENTED** |
| T-NO-004 | Dynamic Grain Access | COMPOSITE | TECH | DLR-based grain access | **IMPLEMENTED** |
| T-NO-005 | VCOMPodGrain | ATOMIC | TECH | Hosts VCOM instances | TBD |
| T-NO-006 | VTypeGrain | ATOMIC | TECH | Manages type definitions | TBD |
| T-NO-007 | VCompilerGrain | ATOMIC | TECH | Runtime compilation | TBD |

### 2.7 Roslyn/Compiler Layer

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-ROS-001 | Roslyn Fork | COMPOSITE | TECH | Complete Roslyn fork | TBD |
| T-ROS-002 | VARIA Transformations | COMPOSITE | TECH | Compile-time codegen | TBD |
| T-ROS-003 | Async+ | COMPOSITE | TECH | Async state machine persistence | **DEFERRED** |
| T-ROS-004 | sync Keyword (Compiler) | ATOMIC | TECH | Compiler support for sync | TBD |
| T-ROS-005 | Dynamic Type Codegen | COMPOSITE | TECH | Generate wrapper types | TBD |

### 2.8 SDK/Tooling Layer

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-SDK-001 | VAYRON SDK | COMPOSITE | TECH | MSBuild SDK package | TBD |
| T-SDK-002 | VS Extension (VSIX) | COMPOSITE | TECH | Visual Studio integration | TBD |
| T-SDK-003 | Project Templates | ATOMIC | TECH | dotnet new templates | TBD |
| T-SDK-004 | Object Inspector | ATOMIC | TECH | Tool window for inspection | TBD |
| T-SDK-005 | VNS IntelliSense | ATOMIC | TECH | Dynamic completion | TBD |
| T-SDK-006 | REPL | ATOMIC | TECH | Interactive exploration | TBD |

---

## PART 3: PROTOTYPING TOOLS (Not Platform Components)

> **Note:** These are tools used in prototyping, NOT components that ship with the platform.

| Tool | Purpose | Notes |
|------|---------|-------|
| RavenDB | Document storage prototyping | Memory system driver will be created |
| Neo4j/AuraDB | Graph storage prototyping | Memory system driver will be created |
| Orleans | Actor model prototyping | Being forked as NewOrleans |

The platform provides: **Memory System Interface** → Users pick their systems → Platform provides/users create drivers.

---

## PART 4: CROSS-CUTTING ASPECTS

| ID | Name | Touches | Description | Status |
|----|------|---------|-------------|--------|
| A-001 | AI Collaboration Interface | All | How AI interacts with all components | TBD |
| A-002 | Versioning System | Types, Code, Engrams | How versions are tracked/managed | TBD |
| A-003 | Error Handling Model | All execution | How errors propagate | TBD |
| A-004 | Debugging/Observability | All | How to debug distributed systems | TBD |
| A-005 | Performance Model | All | What's fast, what's slow, tradeoffs | TBD |
| A-006 | Migration Path | Existing code | How existing code transitions | TBD |

---

## PART 5: COMPOSITE DECOMPOSITION

### 5.1 C-PAR-006: BEAM/Erlang Adaptation (COMPOSITE)

| Sub-Item | Name | Status |
|----------|------|--------|
| C-PAR-006a | Lightweight Processes | → Process/Pathway model |
| C-PAR-006b | Let-it-Crash | TBD |
| C-PAR-006c | Location Transparency | TBD |
| C-PAR-006d | Preemptive Yielding | → Async-by-Default |
| C-PAR-006e | Message Passing | → Actor Model |
| C-PAR-006f | Hot Code Loading | → Code-as-First-Class |
| C-PAR-006g | Supervision Trees | TBD |

### 5.2 C-ARC-001: Multi-Layer Abstraction (COMPOSITE)

> The MAC/IP/DNS analogy was illustrative, not prescriptive. The number of layers is not fixed.

| Principle | Description |
|-----------|-------------|
| Multiple layers exist | Addressing/resolution has multiple abstraction levels |
| Not fixed number | Could be 2, 3, 4, or more layers |
| Virtuality investment | Higher layers provide more abstraction |
| Pragmatic use | Not everything runs on higher layers without good reason |

Example layers (illustrative, not prescriptive):
- Native/physical address
- Managed/runtime address
- Virtual/distributed address (transparent between nodes)

### 5.3 C-ARC-002: VARIA Virtues (COMPOSITE)

| Sub-Item | Virtue | Description |
|----------|--------|-------------|
| C-ARC-002a | Distributivity | Location transparency |
| C-ARC-002b | Persistence | State survives restarts |
| C-ARC-002c | Security | Integrated security |
| C-ARC-002d | Source Self-Management | Code self-containment |
| C-ARC-002e | Modern OOP Surface | Natural C# style |
| C-ARC-002f | Original OOP Backing | Alan Kay's vision |
| C-ARC-002g | Actor Model Execution | Isolated, async |
| C-ARC-002h | AI Centrality | AI as ground-line |

### 5.4 C-ARC-003: Process/Pathway Model (COMPOSITE)

| Sub-Item | Name | Description |
|----------|------|-------------|
| C-ARC-003a | Process | Isolation boundary with identity |
| C-ARC-003b | Pathway | Execution flow, scheduling unit |
| C-ARC-003c | Frame | Single stack frame |
| C-ARC-003d | Process States | Lifecycle states |
| C-ARC-003e | Isolation Model | How isolation is achieved |

---

## PART 6: CANON vs EXPERIMENTAL SUMMARY

### Confirmed CANON

| ID | Item | Why Canon |
|----|------|-----------|
| - | Multi-Runtime Support | VOS Kernel supports multiple runtimes |
| - | Multi-OS from Start | Runs over multiple host OSes (Windows tested first - hardest) |
| - | Multi-Architecture | Multiple hardware architectures |
| - | Bare-Metal Path | Eventually gets own foundation |
| - | New Languages (C=) | Starting with C= |
| C-PAR-001 | "Slow but Smart" | Foundational principle expression |
| C-PAR-002 | VOS Architecture | Runtime evolves into Kernel |
| C-PAR-003 | Async-by-Default | sync becomes the exception |
| C-PAR-008 | Virtuality Investment | Multiple abstraction layers, pragmatic use |
| C-ARC-006 | Memory System Driver Model | Pick system → Pick driver |
| C-CON-001 | Engram (concept) | Core concept that things are designed around |

### Confirmed EXPLORATORY

| ID | Item | Notes |
|----|------|-------|
| C-CON-002 | VCOM | Prototype to discover if/how needed |
| T-VCOM-* | VCOM Infrastructure | All VCOM items are exploratory |

### Confirmed PLACEHOLDER

| ID | Item | Notes |
|----|------|-------|
| C-CON-003 | CMS/MOM/ORION | Keep for inspiration; not serious work yet |

### Confirmed IMPLEMENTED

| ID | Item |
|----|------|
| T-NO-002 | Plugin Grain Loading |
| T-NO-003 | GTD (Grain Type Directory) |
| T-NO-004 | Dynamic Grain Access |

### Confirmed DEFERRED

| ID | Item | Waiting On |
|----|------|------------|
| T-ROS-003 | Async+ | VCOM resolution (if VCOM exists) |

---

## PART 7: STATISTICS

### By Status
- **CANON**: 11 items (architectural commitments + principles)
- **IMPLEMENTED**: 3 items
- **DEFERRED**: 1 item
- **EXPLORATORY**: ~5 items (VCOM-related)
- **PLACEHOLDER**: 1 item (CMS/MOM/ORION)
- **TBD**: ~70 items

### By Granularity
- ATOMIC: ~40 items
- COMPOSITE: ~35 items
- CONCEPT: 3 items
- ASPECT: 6 items

### By Nature
- CONCEPT: ~15 items
- TECH: ~55 items
- HYBRID: ~5 items

---

## PART 8: OPEN QUESTIONS

### Resolved This Session

| Question | Resolution |
|----------|------------|
| Is "Slow but Smart" canon? | Yes - foundational principle |
| Is VOS architecture canon? | Yes - runtime evolves into kernel |
| Is three-layer fixed? | No - layers not fixed; virtuality investment is canon |
| Are RavenDB/Neo4j platform components? | No - prototyping tools; driver model is canon |
| Is semantic inversion (async-default) canon? | Yes |
| Is VCOM one thing? | VCOM is exploratory; prototype to discover |
| Is Engram a system? | No - it's a concept; systems are designed around it |
| Are CMS/MOM/ORION serious? | No - placeholders for inspiration |

### Still Open

| Area | Question |
|------|----------|
| C= Language | What's in the R&D tree? (to be defined as design progresses) |
| Multi-Runtime | What's the abstraction interface for runtime support? |
| Networking | How do VOS nodes discover each other? |
| Testing | How do you test VOS applications? |

---

## PART 9: NEXT STEPS

1. **C= Language Design** - Begin defining the R&D tree as design progresses
2. **Engram Deep Dive** - Detailed design of Engram infrastructure capabilities
3. **Memory System Interface** - Define the driver model specification
4. **Experiment Framework** - How to track and validate experimental items
5. **AI Collaboration Design** - How AI participates in each item's development

---

*This document is a working draft. Revised 2025-12-13 after Louis review.*
*Key changes: Added multi-runtime/multi-OS canon, reframed Engram as concept, added C= tree, marked VCOM exploratory, marked CMS/MOM/ORION placeholder, removed RavenDB/Neo4j as platform components.*
*Clarification: Multi-OS means VOS runs over multiple host OSes from the start (Windows tested first as hardest). Bare-metal variant (Linux distro with in-kernel integrations) is a future path.*
