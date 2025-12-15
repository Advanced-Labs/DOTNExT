# WIP: Runtime Mutability Paradigms

> **Document Type:** Working Document - Knowledge Capture
> **Created:** 2025-12-15
> **Status:** DRAFT - New concepts requiring technical evaluation
> **Purpose:** Document the "Anytime" paradigm and runtime type mutation capabilities

---

## 1. Source Context

This document captures **NEW concepts** explained by Louis (2025-12-15) that were NOT adequately covered in previous documentation.

---

## 2. Louis's Verbatim Description (2025-12-15)

### 2.1 The "Anytime" Paradigm

> "One objective of the platform is "Anytime": Dev/design-time, build/compile-time, run/debug-time all still available as on traditional platform but more importantly all made possible at runtime. We want codes executed - and their AIs - to be able to modify themselves and other codes/types/etc, and be able to do so at their runtime as well as at runtime of other codes/types they decide to mutate."

### 2.2 Clone → Mutate → Options Pattern

> "1. Clone the Loaded and possibly Executing code/type/etc -> Work on Clone while the original is left to run as it is (unless paused is requested either at it's level, higher-level or the whole process, etc) ->
>
> Then
> Option A. Hotswap: Swap the Original with the modified Clone
> Option B. Simulation: Run the modified Clone in parallel to the Original: the Original outputs are effective and so it remains as if there's only just the Original running, but the Clone outputs/behaviors are observed and compared with the Original. Does the Clone performs well or does it needs reworking? Mutation can resume on this clone without re-cloning etc. Simulation would allow to switch between different configurations. For instance, only simulate the clone when a new instance of the original is created and therefore the clone type can be instantiated fresh before taking in any inputs and possibly data/states; or simulate with the clone any instances accessed/requested and either then either route all calls/accesses to both, or do so selectively based on signature differences or other criteria defined (first-class versioning at the type level as well as type-level clear dependencies etc could allow other types/instances to already be specifying they prefer or need this clone version; and the clone type could be able to define which types and versions can access it in whole or at specific member, etc. This would be made to fit in the Security models and systems of our platform). And config could allow the clone to take over the original in case the original fails."

### 2.3 Post-Simulation Options

> "Then from there, options/paths could be:
> - Option A again: If coming from Option B and ready to swap, then Hotswap (option A above).
> - Option C: stop simulating, don't swap anything, etc. Save type variant (the mutated clone) or don't.
> - Option D: Simulation-Swap: the clone replaces the original as the real executed type/instances - as in regular Hotswap - but the Original rather than be (possibly drained)/unloaded is going in simulation mode A&[B] -> B&[A] so that 1. comparison of behaviors/states/outputs can continue; 2. and possibly with config about the original taking over an instance - or the whole type back - whenever a failure is detected."

### 2.4 Instance-Level Version Management

> "In one variant on these are mode/config in which new version of a type doesn't necessarily replace - let alone force replacement - of existing instances of the older types (and possible even - configuration-dependently - allows new instances of the older type(s): instead the new type can be asked to be used only on new instances (i.e. of all versions, or of its specific version, or if its version fits the requested range etc), and/or to take in charges existing instances (i.e. again of all versions of the types or a range etc) but only when those are "hydrated" (i.e. from hibernation to reloaded/active), as opposed to take in charge instances of previous type versions which are loaded in memory. But then there could be the option to 'progressively or immediately drain to hibernation the instances of older type' so as to force 'natural-way rehydration by the newer type now "in service"."

### 2.5 Runtime Type Development

> "Then new types must obviously also be developable at runtime. What they references becomes dependencies for them at the type-level (yes, types are treated like we treat whole programs.. which makes sense in a distributed system platform like ours beyond our needs for mutability and runtime-development: security, persistence/engrams/etc, teleportations/duplications to other VM nodes, etc), and if those dependencies happens to be already dependencies of types which are already loaded in a process then those can be shared/reused as long as it's possible in terms of the versions requested (i.e. no version specified, or a specific one, or a range, etc). For those new types to be used once they are "put into service" (aka "published" I guess), existing types would need to be modified to reference and access those, instantiate over those etc."

---

## 3. The "Anytime" Paradigm

### 3.1 Traditional vs DOTNExT Development Phases

| Phase | Traditional | DOTNExT |
|-------|-------------|---------|
| **Design-time** | IDE only | IDE + Runtime |
| **Build/Compile-time** | Offline build | Offline + CIT at Runtime |
| **Run/Debug-time** | Fixed code executes | Code can mutate |
| **Runtime** | Read-only code | Full mutability |

### 3.2 What Can Mutate at Runtime

- Types (structure, members, behavior)
- Source code
- Instances (migration to new type versions)
- Dependencies
- Execution behavior

### 3.3 Who Can Mutate

- Executing code (self-mutation)
- AIs (embedded or external)
- Other types (with appropriate permissions)
- Platform services

---

## 4. Clone → Mutate → Options Pattern

### 4.1 Phase 1: Clone

```
┌─────────────────┐
│  Original Type  │  ─────────────────────────────────────────┐
│  (executing)    │                                           │
└─────────────────┘                                           │
                                                              │
         Clone Operation                                      │
              │                                               │
              ▼                                               │
┌─────────────────┐                                           │
│    Clone        │  (Original continues running unaffected) │
│  (for mutation) │                                           │
└─────────────────┘                                           │
```

**Key Point:** Original continues executing while clone is worked on.

### 4.2 Phase 2: Mutate Clone

Work on the clone:
- Modify structure
- Change behavior
- Add/remove members
- Update dependencies

Original remains untouched and continues serving requests.

### 4.3 Phase 3: Choose Option

```
                    ┌─────────────────────────────────────────┐
                    │                                         │
    ┌───────────────┴───────────────┐                        │
    │                               │                        │
    ▼                               ▼                        │
┌─────────┐                   ┌──────────────┐              │
│ Option A│                   │   Option B   │              │
│ Hotswap │                   │  Simulation  │              │
└─────────┘                   └──────────────┘              │
    │                               │                        │
    │                               │                        │
    │                         ┌─────┴─────┐                  │
    │                         │           │                  │
    │                         ▼           ▼                  │
    │                    ┌────────┐  ┌────────┐             │
    │                    │Option C│  │Option D│             │
    │                    │ Cancel │  │  Swap  │             │
    │                    │        │  │ Roles  │             │
    │                    └────────┘  └────────┘             │
    │                                                        │
    └────────────────────────────────────────────────────────┘
```

---

## 5. Option A: Hotswap

**Definition:** Replace the original type with the mutated clone.

**Behavior:**
- Clone becomes the "real" type
- Original is unloaded (possibly after draining)
- All future requests served by clone
- Existing instances can be:
  - Migrated to new type
  - Left as-is (old type version)
  - Drained to hibernation for rehydration

---

## 6. Option B: Simulation Mode

**Definition:** Run clone in parallel to original for comparison.

### 6.1 Simulation Behaviors

| Mode | Description |
|------|-------------|
| **Original is effective** | Original's outputs are used, clone's are observed |
| **Compare outputs** | Both run, outputs compared for validation |
| **Clone on new instances only** | New instances use clone, existing use original |
| **Selective routing** | Route based on signature differences, criteria |
| **Failover enabled** | Clone takes over if original fails |

### 6.2 Simulation Configuration

```
Simulation Config:
├── Which instances to simulate?
│   ├── All instances
│   ├── New instances only
│   └── Accessed instances only
├── Routing strategy
│   ├── Both (parallel execution)
│   ├── Selective by signature
│   └── Selective by criteria
├── Version preferences
│   ├── Types can specify preferred version
│   └── Types can specify required version
├── Member-level access control
│   ├── Clone defines who can access what
│   └── Fits with Security model
└── Failover behavior
    ├── Clone takes over on original failure
    └── Original takes over on clone failure
```

### 6.3 First-Class Versioning Integration

- Types can declare version preferences
- Types can declare version requirements
- Version routing is part of the type system
- Security model governs version access

---

## 7. Option C: Cancel Simulation

**Definition:** Stop simulating, don't swap.

**Behaviors:**
- Simulation stops
- Original continues unchanged
- Clone can be:
  - Discarded
  - Saved as type variant
  - Kept for future use

---

## 8. Option D: Simulation-Swap (Role Reversal)

**Definition:** Clone becomes real, original goes to simulation.

```
Before Swap:                    After Swap:
┌─────────────┐                ┌─────────────┐
│  Original   │ ◄─ effective   │   Clone     │ ◄─ effective
│  [A]        │                │   [B]       │
└─────────────┘                └─────────────┘
┌─────────────┐                ┌─────────────┐
│   Clone     │ ◄─ simulated   │  Original   │ ◄─ simulated
│   [B]       │                │   [A]       │
└─────────────┘                └─────────────┘

A&[B] ────────────────────────► B&[A]
```

**Benefits:**
1. Continuous behavior comparison
2. Rollback capability (original can retake control)
3. A/B testing at runtime
4. Safe deployment of changes

---

## 9. Instance-Level Version Management

### 9.1 Version Application Policies

| Policy | Description |
|--------|-------------|
| **New instances only** | New type used only for newly created instances |
| **On rehydration** | New type used when instances come out of hibernation |
| **All instances** | Migrate all instances to new type |
| **Version range** | Apply based on version compatibility |

### 9.2 Draining Strategies

To force migration to new type version:

```
Progressive Drain:
┌────────────────────────────────────────────────────────────┐
│                                                            │
│  Active Instances (Old Type)                               │
│  ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐                           │
│  │ 1 │ │ 2 │ │ 3 │ │ 4 │ │ 5 │                           │
│  └───┘ └───┘ └───┘ └───┘ └───┘                           │
│    │     │     │     │     │                              │
│    ▼     │     │     │     │    (progressive hibernation) │
│  ┌───┐   │     │     │     │                              │
│  │ H │   ▼     │     │     │                              │
│  └───┘ ┌───┐   │     │     │                              │
│    │   │ H │   ▼     │     │                              │
│    │   └───┘ ┌───┐   │     │                              │
│    │     │   │ H │   ▼     │                              │
│    │     │   └───┘ ┌───┐   │                              │
│    │     │     │   │ H │   ▼                              │
│    │     │     │   └───┘ ┌───┐                            │
│    │     │     │     │   │ H │                            │
│    │     │     │     │   └───┘                            │
│    │     │     │     │     │                              │
│    ▼     ▼     ▼     ▼     ▼                              │
│                                                            │
│  Rehydrated Instances (New Type)                          │
│  ┌───┐ ┌───┐ ┌───┐ ┌───┐ ┌───┐                           │
│  │ 1'│ │ 2'│ │ 3'│ │ 4'│ │ 5'│                           │
│  └───┘ └───┘ └───┘ └───┘ └───┘                           │
│                                                            │
└────────────────────────────────────────────────────────────┘
```

### 9.3 Version Coexistence

Multiple versions of a type can coexist:
- Configuration determines which version serves which requests
- Instances of different versions can interact (with version awareness)
- Gradual migration without big-bang deployment

---

## 10. Runtime Type Development

### 10.1 Types as First-Class Programs

> "types are treated like we treat whole programs"

This enables:
- Security at type level
- Persistence at type level
- Teleportation of types
- Duplication to other nodes
- Independent type lifecycle

### 10.2 Dependency Management

When developing a new type at runtime:

```
New Type Being Developed
        │
        ├── References Type A:v1.2  ← Already loaded? Reuse.
        ├── References Type B:any   ← Version flexible
        └── References Type C:>=2.0 ← Version range

Dependency Resolution:
1. Check if dependency already loaded in process
2. Check version compatibility
3. Share if compatible, load if not
4. Track as type-level dependency
```

### 10.3 Publishing New Types

To make a new type usable:
1. Type is "put into service" / "published"
2. Existing types that need it must be modified
3. Those types can then reference, access, instantiate

---

## 11. Technical Evaluation Needed

### 11.1 Runtime Infrastructure Required

- [ ] Type cloning mechanism
- [ ] Parallel execution infrastructure
- [ ] Output comparison framework
- [ ] Version routing system
- [ ] Instance migration mechanism
- [ ] Hibernation/rehydration integration
- [ ] Dependency resolution at runtime
- [ ] Type publication mechanism

### 11.2 Questions to Answer

1. **How does type cloning work at IL level?**
2. **How are parallel executions isolated?**
3. **What's the overhead of simulation mode?**
4. **How does version routing integrate with VNS?**
5. **How does this interact with JIT compilation?**
6. **What state needs to migrate with instances?**
7. **How do type-level dependencies interact with process boundaries?**

### 11.3 Integration Points

- **Memantics**: Type versions stored in Memantics
- **Engrams**: Instance state for hibernation/rehydration
- **VNS**: Version-aware type resolution
- **Security**: Access control for mutation operations
- **Execution Model**: Parallel execution in Pathways

---

## 12. Relationship to Unwinder

The Unwinder techniques enable:
- Execution state capture (for simulation)
- Instance migration (state preservation)
- Checkpoint/restore (for version transitions)

This is why "async by default" and the Unwinder are foundational.

---

## 13. Summary: The Vision

**Traditional Platform:**
- Code is written → compiled → deployed → runs → fixed
- Changes require redeploy

**DOTNExT Platform:**
- Code is always mutable
- Types evolve at runtime
- Versions coexist safely
- Changes are tested in simulation
- Deployment is gradual and reversible
- AIs can improve code while it runs

---

*This is a working document capturing new concepts. Technical evaluation and design work needed.*
