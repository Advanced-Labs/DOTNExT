# R&D Item Inventory - DOTNExT/VAYRON Platform

> **Document Type:** Systematic Inventory
> **Created:** 2025-12-12
> **Purpose:** Identify all discrete R&D Items that can be designed, implemented, tested, and integrated
> **Status:** DRAFT - Initial Extraction

---

## Classification Legend

### Granularity
- **ATOMIC**: Cannot be meaningfully subdivided; loses coherence if split
- **COMPOSITE**: Is whole by itself AND contains sub-items that are also whole by themselves
- **ASPECT**: Cross-cutting concern that touches multiple items

### Nature
- **CONCEPT**: Paradigm, model, principle, or design philosophy
- **TECH**: Implementable technology, code, system
- **HYBRID**: Has both conceptual and technological dimensions

### Status (to be determined through this process)
- **CANON**: Fixed part of the vision; not subject to experimentation
- **EXPERIMENTAL**: To be validated through implementation/testing
- **ALTERNATIVE**: One of multiple options being considered
- **TBD**: Status not yet determined

---

## PART 1: CONCEPTUAL R&D ITEMS

### 1.1 Foundational Paradigms

| ID | Name | Granularity | Description | Status |
|----|------|-------------|-------------|--------|
| C-PAR-001 | "Slow but Smart is the new Speed" | ATOMIC | AI inference bottleneck makes runtime overhead irrelevant | TBD |
| C-PAR-002 | VOS Architecture (Runtime = Kernel) | ATOMIC | CLR as kernel, services as userspace | TBD |
| C-PAR-003 | Semantic Inversion (sync is exception) | ATOMIC | Default yieldable, explicit sync | TBD |
| C-PAR-004 | Code-as-First-Class | ATOMIC | Source is primary artifact, binaries are cache | TBD |
| C-PAR-005 | Hybrid Development Path | ATOMIC | Managed-space first, selective lowering | TBD |
| C-PAR-006 | BEAM/Erlang Adaptation | COMPOSITE | Erlang patterns adapted to hosted CLR | TBD |
| C-PAR-007 | AI-Centrality | ATOMIC | AI as ground-line protocol, not add-on | TBD |

### 1.2 Architectural Models

| ID | Name | Granularity | Description | Status |
|----|------|-------------|-------------|--------|
| C-ARC-001 | Three-Layer Resolution (MAC/IP/DNS) | COMPOSITE | Grain/VCOM/VNS separation | TBD |
| C-ARC-002 | VARIA Virtues Model | COMPOSITE | The 8 platform virtues | TBD |
| C-ARC-003 | Process/Pathway Model | COMPOSITE | Execution hierarchy abstraction | TBD |
| C-ARC-004 | Engram Layers Model | COMPOSITE | 5-layer bounded extraction | TBD |
| C-ARC-005 | Security Driver Model | COMPOSITE | Pluggable security subsystem | TBD |
| C-ARC-006 | Universal Dynamic Types | COMPOSITE | Family of special dynamic types | TBD |
| C-ARC-007 | CMS/MOM/ORION Triad | COMPOSITE | Runtime memory architecture | TBD |

### 1.3 Vision/Goal Concepts

| ID | Name | Granularity | Description | Status |
|----|------|-------------|-------------|--------|
| C-VIS-001 | Cyberspace | COMPOSITE | Distributed object space vision | TBD |
| C-VIS-002 | Society of Minds | ATOMIC | AI-Objects as collaborative agents | TBD |
| C-VIS-003 | Self-Evolving Code | ATOMIC | Objects that modify their own types | TBD |
| C-VIS-004 | Internet of Objects | ATOMIC | VNS-navigable object network | TBD |

---

## PART 2: TECHNOLOGICAL R&D ITEMS

### 2.1 Runtime Layer (DOTNExT Kernel)

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-RT-001 | DOTNExT Runtime Fork | COMPOSITE | TECH | Complete CLR fork | TBD |
| T-RT-002 | GC Integration | COMPOSITE | TECH | Leveraging/extending GC for VCOM | TBD |
| T-RT-003 | JIT Integration | COMPOSITE | TECH | JIT modifications for safe points, helpers | TBD |
| T-RT-004 | Type System Extension | COMPOSITE | TECH | Engram flags, UUID in MethodTable | TBD |
| T-RT-005 | Unified Safe Points | COMPOSITE | TECH | GC + preemption + checkpoint convergence | TBD |
| T-RT-006 | Unwinder Techniques | ATOMIC | TECH | Stack capture at any safe point | TBD |
| T-RT-007 | Tasklet Implementation | COMPOSITE | TECH | Captured stack frame structure | TBD |
| T-RT-008 | sync Keyword | COMPOSITE | TECH | Compiler + runtime support | TBD |
| T-RT-009 | Process Image Persistence | COMPOSITE | TECH | CRIU-like checkpoint/restore | TBD |

#### 2.1.1 CMS/MOM/ORION Subsystems (Runtime-Level)

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-RT-010 | CMS (Content Memory System) | COMPOSITE | TECH | Object content/state management wrapping GC | TBD |
| T-RT-011 | MOM (Managed Object Manager) | COMPOSITE | TECH | UUID identity, relationships, semantic metadata | TBD |
| T-RT-012 | ORION (Object Reference and Identity Observation Network) | COMPOSITE | TECH | Cross-node topology tracking | TBD |

### 2.2 VOS Services Layer (Userspace)

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-VOS-001 | VNS (Virtual Name System) | COMPOSITE | TECH | DNS for Objects | TBD |
| T-VOS-002 | VCOM (Component Object Model) | COMPOSITE | TECH | Object model with UUID identity | TBD |
| T-VOS-003 | VObject | ATOMIC | TECH | Universal base type | TBD |
| T-VOS-004 | VType System | COMPOSITE | TECH | Runtime type management | TBD |
| T-VOS-005 | Security Subsystem | COMPOSITE | TECH | Security driver coordination | TBD |
| T-VOS-006 | Persistence Subsystem | COMPOSITE | TECH | State persistence coordination | TBD |
| T-VOS-007 | Distribution Subsystem | COMPOSITE | TECH | Location transparency | TBD |

#### 2.2.1 VNS Sub-Items

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-VOS-001a | VNamespaceGrain | ATOMIC | TECH | Hierarchical namespace management | TBD |
| T-VOS-001b | VSearchGrain | ATOMIC | TECH | Semantic search (vectors) | TBD |
| T-VOS-001c | VAnchorGrain | ATOMIC | TECH | Named stable entry points | TBD |
| T-VOS-001d | VNS Address Formats | ATOMIC | TECH | URI scheme specification | TBD |
| T-VOS-001e | VNS Query Language | ATOMIC | TECH | Query/semantic addressing | TBD |

#### 2.2.2 VCOM Sub-Items

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-VOS-002a | UUID Generation/Management | ATOMIC | TECH | Identity assignment | TBD |
| T-VOS-002b | VCOM.Resolve() | ATOMIC | TECH | UUID to live object resolution | TBD |
| T-VOS-002c | VRelations | ATOMIC | TECH | Relationship management | TBD |
| T-VOS-002d | VCOM Proxy System | COMPOSITE | TECH | Transparent proxies | TBD |

#### 2.2.3 Security Sub-Items

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-VOS-005a | Security Driver Interface | ATOMIC | TECH | Driver contract definition | TBD |
| T-VOS-005b | CBS Driver | ATOMIC | TECH | Capability-based security | TBD |
| T-VOS-005c | RBAC Driver | ATOMIC | TECH | Role-based access control | TBD |
| T-VOS-005d | Crypto/ZK Driver | ATOMIC | TECH | Cryptographic verification | TBD |
| T-VOS-005e | Security Interception Points | COMPOSITE | TECH | Where security checks occur | TBD |

### 2.3 NewOrleans Infrastructure

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-NO-001 | NewOrleans Fork | COMPOSITE | TECH | Complete Orleans fork | TBD |
| T-NO-002 | Plugin Grain Loading | COMPOSITE | TECH | Runtime assembly load/unload | IMPLEMENTED |
| T-NO-003 | GTD (Grain Type Directory) | ATOMIC | TECH | Cluster-wide type registry | IMPLEMENTED |
| T-NO-004 | Dynamic Grain Access | COMPOSITE | TECH | DLR-based grain access | IMPLEMENTED |
| T-NO-005 | VCOMPodGrain | ATOMIC | TECH | Hosts VCOM instances | TBD |
| T-NO-006 | VTypeGrain | ATOMIC | TECH | Manages type definitions | TBD |
| T-NO-007 | VCompilerGrain | ATOMIC | TECH | Runtime compilation | TBD |

### 2.4 Roslyn/Compiler Layer

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-ROS-001 | Roslyn Fork | COMPOSITE | TECH | Complete Roslyn fork | TBD |
| T-ROS-002 | VARIA Transformations | COMPOSITE | TECH | Compile-time codegen | TBD |
| T-ROS-003 | Async+ | COMPOSITE | TECH | Async state machine persistence | DEFERRED |
| T-ROS-004 | sync Keyword (Compiler) | ATOMIC | TECH | Compiler support for sync | TBD |
| T-ROS-005 | Dynamic Type Codegen | COMPOSITE | TECH | Generate wrapper types | TBD |

#### 2.4.1 VARIA Transformation Sub-Items

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-ROS-002a | new → VCOM Creation | ATOMIC | TECH | Transform instantiation | TBD |
| T-ROS-002b | Property → VCOM State | ATOMIC | TECH | Transform property access | TBD |
| T-ROS-002c | Method → Grain Invocation | ATOMIC | TECH | Transform method calls | TBD |
| T-ROS-002d | Reference → UUID | ATOMIC | TECH | Transform references | TBD |

### 2.5 Persistence Layer

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-PER-001 | RavenDB Integration | COMPOSITE | TECH | Document storage | TBD |
| T-PER-002 | Neo4j/AuraDB Integration | COMPOSITE | TECH | Graph storage | TBD |
| T-PER-003 | Engram Persistence | COMPOSITE | TECH | Bounded extraction storage | TBD |
| T-PER-004 | Code/Binary Storage | ATOMIC | TECH | Source + cached binaries | TBD |
| T-PER-005 | Orleans Storage Providers | COMPOSITE | TECH | RavenDB + Neo4j providers | TBD |

### 2.6 SDK/Tooling Layer

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-SDK-001 | VAYRON SDK | COMPOSITE | TECH | MSBuild SDK package | TBD |
| T-SDK-002 | VS Extension (VSIX) | COMPOSITE | TECH | Visual Studio integration | TBD |
| T-SDK-003 | Project Templates | ATOMIC | TECH | dotnet new templates | TBD |
| T-SDK-004 | VCOM Object Inspector | ATOMIC | TECH | Tool window for inspection | TBD |
| T-SDK-005 | VNS IntelliSense | ATOMIC | TECH | Dynamic completion | TBD |
| T-SDK-006 | VARIA REPL | ATOMIC | TECH | Interactive exploration | TBD |

### 2.7 Engram System

| ID | Name | Granularity | Nature | Description | Status |
|----|------|-------------|--------|-------------|--------|
| T-ENG-001 | Engram Core | COMPOSITE | HYBRID | Bounded extraction system | TBD |
| T-ENG-002 | Topology Layer | ATOMIC | TECH | Where things live | TBD |
| T-ENG-003 | Objects Layer | ATOMIC | TECH | Instance state and references | TBD |
| T-ENG-004 | Execution Layer | ATOMIC | TECH | Current execution state | TBD |
| T-ENG-005 | Binaries Layer | ATOMIC | TECH | Compiled code cache | TBD |
| T-ENG-006 | Code/Types Layer | ATOMIC | TECH | Source and type definitions | TBD |
| T-ENG-007 | Engram Boundary Definition | ATOMIC | TECH | What's IN vs OUT | TBD |
| T-ENG-008 | Engram Operations | COMPOSITE | TECH | Extract/Persist/Transfer/Inject/Resume | TBD |

---

## PART 3: CROSS-CUTTING ASPECTS

| ID | Name | Touches | Description | Status |
|----|------|---------|-------------|--------|
| A-001 | AI Collaboration Interface | All | How AI interacts with all components | TBD |
| A-002 | Versioning System | Types, Code, Engrams | How versions are tracked/managed | TBD |
| A-003 | Error Handling Model | All execution | How errors propagate | TBD |
| A-004 | Debugging/Observability | All | How to debug distributed VARIA | TBD |
| A-005 | Performance Model | All | What's fast, what's slow, tradeoffs | TBD |
| A-006 | Migration Path | NewOrleans, VCOM | How existing code transitions | TBD |

---

## PART 4: COMPOSITE DECOMPOSITION

### 4.1 C-PAR-006: BEAM/Erlang Adaptation (COMPOSITE)

| Sub-Item | Name | Status |
|----------|------|--------|
| C-PAR-006a | Lightweight Processes | → Process/Pathway model |
| C-PAR-006b | Let-it-Crash | TBD |
| C-PAR-006c | Location Transparency | → VCOM |
| C-PAR-006d | Preemptive Yielding | → Semantic Inversion |
| C-PAR-006e | Message Passing | → Actor Model via Orleans |
| C-PAR-006f | Hot Code Loading | → Code-as-First-Class + VTypeGrain |
| C-PAR-006g | Supervision Trees | TBD |

### 4.2 C-ARC-001: Three-Layer Resolution (COMPOSITE)

| Sub-Item | Name | Maps To |
|----------|------|---------|
| C-ARC-001a | Grain Layer (MAC) | T-NO-* (NewOrleans) |
| C-ARC-001b | VCOM Layer (IP) | T-VOS-002 |
| C-ARC-001c | VNS Layer (DNS) | T-VOS-001 |

### 4.3 C-ARC-002: VARIA Virtues (COMPOSITE)

| Sub-Item | Virtue | Implementation Approach |
|----------|--------|------------------------|
| C-ARC-002a | Distributivity | Location transparency via VCOM |
| C-ARC-002b | Persistence | Automatic via Engrams |
| C-ARC-002c | Security | Security Driver System |
| C-ARC-002d | Source Self-Management | Code-as-First-Class |
| C-ARC-002e | Modern OOP Surface | VARIA transformations |
| C-ARC-002f | Original OOP Backing | Actor model |
| C-ARC-002g | Actor Model Execution | Orleans grains |
| C-ARC-002h | AI Centrality | AI as ground-line protocol |

### 4.4 C-ARC-003: Process/Pathway Model (COMPOSITE)

| Sub-Item | Name | Maps To |
|----------|------|---------|
| C-ARC-003a | Process (isolation boundary) | T-RT-009 (partial) |
| C-ARC-003b | Pathway (execution flow) | T-RT-007 (Tasklet) |
| C-ARC-003c | Frame (single stack frame) | T-RT-007 (Tasklet internals) |
| C-ARC-003d | Process States | Design work needed |
| C-ARC-003e | Isolation Model | Design work needed |

### 4.5 C-ARC-004: Engram Layers Model (COMPOSITE)

Maps directly to T-ENG-002 through T-ENG-006.

### 4.6 C-ARC-007: CMS/MOM/ORION Triad (COMPOSITE)

| Sub-Item | Name | Maps To |
|----------|------|---------|
| C-ARC-007a | CMS (Content) | T-RT-010 |
| C-ARC-007b | MOM (Identity) | T-RT-011 |
| C-ARC-007c | ORION (Topology) | T-RT-012 |

---

## PART 5: DEPENDENCY SKETCH (Initial)

```
                         ┌─────────────────┐
                         │   C-VIS-001     │
                         │   Cyberspace    │
                         └────────┬────────┘
                                  │ requires
         ┌────────────────────────┼────────────────────────┐
         │                        │                        │
         ▼                        ▼                        ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   T-ENG-001     │    │   T-VOS-001     │    │   C-VIS-002     │
│   Engram Core   │    │   VNS           │    │   Society of    │
│                 │    │                 │    │   Minds         │
└────────┬────────┘    └────────┬────────┘    └────────┬────────┘
         │                      │                      │
         │ requires             │ requires             │ requires
         ▼                      ▼                      ▼
┌─────────────────┐    ┌─────────────────┐    ┌─────────────────┐
│   T-VOS-002     │    │   T-NO-001      │    │   C-PAR-007     │
│   VCOM          │    │   NewOrleans    │    │   AI-Centrality │
└────────┬────────┘    └────────┬────────┘    └─────────────────┘
         │                      │
         │ requires             │ requires
         ▼                      ▼
┌─────────────────┐    ┌─────────────────┐
│   T-RT-001      │    │   Orleans       │
│   DOTNExT       │    │   (upstream)    │
│   Runtime       │    │                 │
└─────────────────┘    └─────────────────┘
```

---

## PART 6: ITEMS REQUIRING CLARIFICATION

These items need Louis's input to properly classify:

### 6.1 Canon vs Experimental?

| Item | Question |
|------|----------|
| C-PAR-001 | Is "Slow but Smart" a fixed principle or hypothesis? |
| C-PAR-002 | Is VOS architecture canon or could alternatives exist? |
| C-PAR-003 | Is semantic inversion mandatory or optional? |
| C-ARC-001 | Is three-layer resolution fixed or could be two/four layers? |
| T-PER-001/002 | Are RavenDB + Neo4j fixed choices or placeholders? |

### 6.2 Granularity Questions

| Item | Question |
|------|----------|
| T-VOS-002 | Is VCOM one thing or should UUID, proxies, relations be separate items? |
| T-ENG-001 | Is Engram one system or five independent layer systems? |
| C-ARC-007 | Are CMS/MOM/ORION truly separate or one unified memory system? |

### 6.3 Missing Items?

| Area | Potential Missing Items |
|------|------------------------|
| Networking | How do VM nodes discover each other? |
| Cluster Management | Orleans clustering is hidden - what replaces it conceptually? |
| Schema Evolution | How do VCOM types evolve over time? |
| Garbage Collection | What happens to orphaned Engrams? |
| Testing Framework | How do you test VARIA code? |
| C= Language | Is this a separate R&D item or part of Roslyn work? |

---

## PART 7: STATISTICS

### By Granularity
- ATOMIC: ~45 items
- COMPOSITE: ~35 items
- ASPECT: 6 items

### By Nature
- CONCEPT: ~20 items
- TECH: ~60 items
- HYBRID: ~5 items

### By Status (Current)
- TBD: ~80 items
- IMPLEMENTED: 3 items (T-NO-002, T-NO-003, T-NO-004)
- DEFERRED: 1 item (T-ROS-003 Async+)
- CANON/EXPERIMENTAL: Not yet determined

---

## Next Steps

1. **Louis Review**: Validate this inventory - what's missing? What's miscategorized?
2. **Canon Determination**: Which items are fixed vs experimental?
3. **Dependency Mapping**: Full dependency graph between items
4. **Experimentation Framework**: How to test/validate experimental items
5. **AI Collaboration Design**: How AI participates in each item's development

---

*This document is a working draft. It represents the first systematic extraction of R&D items from the consolidated vision.*
