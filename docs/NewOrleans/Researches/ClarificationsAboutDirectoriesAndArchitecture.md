# Clarifications About Orleans Directories and Architecture

> **Purpose**: This document clarifies the relationships and differences between various "directory" systems in Orleans, and provides architecture analysis of the dynamic grain features. It exists to prevent confusion between similarly-named but fundamentally different systems.

**Research Date**: 2025-11-24
**Orleans Version**: 9.1.0

---

## Executive Summary

This document clarifies **multiple distinct systems** that are often confused:

1. **Grain Directory** (`IGrainDirectory`) - Tracks WHERE grain instances are located
2. **Grain Type Registry** (Manifest System) - Tracks WHAT grain types exist and HOW they behave
3. **Grain Type Directory** (`IGrainTypeDirectoryGrain`) - Tracks WHICH types are AVAILABLE and WHERE they're loaded
4. **Dynamic Grain Loading/Unloading** - Runtime assembly management

---

## The Three "Directories" Explained

Orleans and this fork have **three different systems** all called "directory":

### 1. Grain Directory (Location Tracking) - `IGrainDirectory`

**Purpose**: Track WHERE specific grain instances are located RIGHT NOW

**What it stores**:
```
┌──────────────────────────────┬─────────────────────────┐
│ GrainId (instance)           │ Current Location        │
├──────────────────────────────┼─────────────────────────┤
│ grain/IUserGrain/alice       │ Silo 10.0.0.5:11111     │
│ grain/IUserGrain/bob         │ Silo 10.0.0.7:11111     │
│ grain/IProductGrain/123      │ Silo 10.0.0.5:11111     │
└──────────────────────────────┴─────────────────────────┘
```

**Questions it answers**:
- "Where is alice's UserGrain instance right now?"
- "Which silo should I route this message to?"
- "Is bob's UserGrain activated anywhere?"

**Scale**: Millions of entries (one per active grain instance)

**Change frequency**: Very high (every activation/deactivation)

**Implementations**: `LocalGrainDirectory`, `DistributedGrainDirectory`, Redis, Azure, SQL

**Location**: Orleans Core - `src/Orleans.Runtime/GrainDirectory/`

### 2. Grain Type Registry (Type Metadata) - Manifest System

**Purpose**: Track WHAT grain types exist and HOW they should behave

**What it stores**:
```
┌────────────────┬────────────────┬──────────────────────────────┐
│ GrainType      │ CLR Type       │ Properties                    │
├────────────────┼────────────────┼──────────────────────────────┤
│ grain/IUserGrain│ UserGrain     │ PlacementStrategy: Random     │
│                │                │ Directory: Default            │
│                │                │ CollectionAge: 2 hours        │
├────────────────┼────────────────┼──────────────────────────────┤
│ grain/IProduct │ ProductGrain   │ PlacementStrategy: Stateless  │
│                │                │ MaxLocalWorkers: 10           │
└────────────────┴────────────────┴──────────────────────────────┘
```

**Questions it answers**:
- "What CLR type implements IUserGrain?"
- "What placement strategy should ProductGrain use?"
- "Which grain directory should OrderGrain use?"
- "What are the properties of grain type X?"

**Scale**: Hundreds to thousands of entries (one per grain type)

**Change frequency**: Low (only when assemblies load/unload)

**Components**: `GrainClassMap`, `SiloManifestProvider`, `ClusterManifestProvider`

**Location**: Orleans Core - `src/Orleans.Runtime/Manifest/`

### 3. Grain Type Directory (Type Discovery) - `IGrainTypeDirectoryGrain`

**Purpose**: Track WHICH grain types are AVAILABLE across the cluster and WHERE they're loaded

**What it stores**:
```
┌────────────────┬────────────────┬───────────────────┬─────────────────────┐
│ GrainType      │ Package        │ Load Status       │ Loaded On Silos     │
├────────────────┼────────────────┼───────────────────┼─────────────────────┤
│ IUserGrain     │ MyApp.Grains   │ LoadedOnAllSilos  │ [A, B, C]           │
│ IOrderGrain    │ Orders.Pkg     │ LoadedOnSomeSilos │ [A, C]              │
│ ICalculator    │ Math.Pkg       │ AvailableNotLoaded│ []                  │
└────────────────┴────────────────┴───────────────────┴─────────────────────┘
```

**Questions it answers**:
- "What grain types exist in the cluster?"
- "Is ICalculatorGrain loaded anywhere?"
- "Show me all grains in namespace 'Ecommerce.Orders'"
- "Which silos have IUserGrain loaded?"
- "What methods does IProductGrain have?" (with metadata feature)

**Scale**: Hundreds to thousands of entries (one per grain type)

**Change frequency**: Low (only when assemblies load/unload)

**Components**: `IGrainTypeDirectoryGrain` (singleton grain), `GrainTypeMeta`, `GrainPackage`

**Location**: This Fork - `src/Orleans.Runtime/DynamicGrains/`

### Why They're All Different

| Aspect | Grain Directory (#1) | Type Registry (#2) | Type Directory (#3) |
|--------|---------------------|-------------------|---------------------|
| **Level** | Instance-level | Type-level | Type-level |
| **Question** | WHERE is instance X? | WHAT is type X? | WHICH types exist? |
| **Scale** | 1,000,000 entries | 1,000 entries | 1,000 entries |
| **Changes** | Every second | Rarely (load/unload) | Rarely (load/unload) |
| **Critical Path** | Message routing | Grain activation | Discovery/tooling |
| **Performance** | Must be sub-ms | Can be ms | Can be tens of ms |
| **Analogy** | Table index | Table schema | Schema catalog |

**Critical Insight**: These are **different abstractions at different levels**. They SHOULD NOT be merged.

---

## Following a Single Grain Call

Let's trace what happens when a client calls a grain method, showing how all three systems are involved:

```csharp
// Client code
var user = grainFactory.GetGrain<IUserGrain>("alice");
await user.UpdateProfile("new bio");
```

### Step 1: Type Registry Lookup (Happens Once Per Grain Type)

**System**: Manifest System (GrainClassMap, GrainPropertiesResolver)

**Question**: "What CLR type implements IUserGrain and how should it behave?"

**Lookup**:
```csharp
// GrainClassMap
GrainType("grain/IUserGrain") -> typeof(UserGrain)

// GrainPropertiesResolver
GrainType("grain/IUserGrain") -> {
    PlacementStrategy: RandomPlacement,
    GrainDirectory: "Default",
    CollectionAge: 2 hours
}
```

**Result**: "Use `UserGrain` class, use `DefaultGrainDirectory`, use `RandomPlacement`"

**Cached**: Yes - this lookup happens once and is cached per grain type

### Step 2: Grain Directory Lookup (Happens For Every Call)

**System**: Grain Directory (`IGrainDirectory` - location tracking)

**Question**: "Where is alice's UserGrain instance right now?"

**Lookup**:
```csharp
// DefaultGrainDirectory (or DistributedGrainDirectory, Redis, etc.)
GrainId("grain/IUserGrain/alice") -> SiloAddress("10.0.0.5:11111")
```

**Result**: "Route message to silo 10.0.0.5"

**Cached**: Yes - with TTL/invalidation, but checked frequently

### Step 3: Grain Type Directory (Not Involved in Call Path)

**System**: Grain Type Directory (`IGrainTypeDirectoryGrain` - discovery)

**This system is NOT involved in the call path at all.**

It's only used for **discovery and exploration**:

```csharp
// Example discovery queries (not during grain calls)
var gtd = grainFactory.GetGrain<IGrainTypeDirectoryGrain>("gtd");

// Query 1: What grain types exist?
var allTypes = await gtd.GetAllGrainTypesAsync();
// Returns: [IUserGrain, IProductGrain, IOrderGrain, ...]

// Query 2: Search for calculator grains
var calculators = await gtd.FindGrainTypesAsync(namePattern: "*Calculator*");
// Returns: [ICalculatorGrain, IScientificCalculatorGrain, ...]
```

**When is it used?**
- IDE/tooling integration (showing available grain types)
- Dynamic client code (discovering what grains exist)
- Administration dashboards
- Debugging/diagnostics

### Visual Comparison

```
┌─────────────────────────────────────────────────────────────────┐
│                     Grain Call Flow                              │
└─────────────────────────────────────────────────────────────────┘

Client: grainFactory.GetGrain<IUserGrain>("alice").UpdateProfile(...)
    │
    ├──> Step 1: Type Registry (GrainClassMap)
    │    Question: "What implements IUserGrain?"
    │    Answer: typeof(UserGrain), RandomPlacement, DefaultDirectory
    │    Used: Once per grain type (cached)
    │
    └──> Step 2: Grain Directory (IGrainDirectory)
         Question: "Where is alice's UserGrain instance?"
         Answer: Silo 10.0.0.5:11111
         Used: Every call (with caching/TTL)


┌─────────────────────────────────────────────────────────────────┐
│                  Discovery/Exploration Flow                      │
└─────────────────────────────────────────────────────────────────┘

Developer/Tool: "What grain types exist in the cluster?"
    │
    └──> Grain Type Directory (IGrainTypeDirectoryGrain)
         Question: "Show me all grain types"
         Answer: List of GrainTypeMeta with load status, silos, metadata
         Used: On demand (tooling, admin, dynamic clients)
```

---

## Common Questions Answered

### Q1: "Could we have built the dynamic features directly into Orleans grain directory?"

**Answer**: **NO** - You're confusing three different systems:

1. **Grain Directory** (`IGrainDirectory`) tracks WHERE instances are (location)
2. **Grain Type Registry** (Manifest) tracks WHAT types exist (metadata)
3. **Grain Type Directory** (`IGrainTypeDirectoryGrain`) tracks WHICH types are available (discovery)

Dynamic grain loading modifies #2 (Type Registry), not #1 (Grain Directory).

The Grain Directory (location tracking) **already works automatically** with dynamic grains - no integration needed.

### Q2: "Shouldn't we build everything into a single unified directory?"

**Answer**: **NO** - They're at different abstraction levels:

| System | Scale | Changes | Critical Path | Analogy |
|--------|-------|---------|---------------|---------|
| Grain Directory | 1M entries | Every second | Message routing | Table index |
| Type Registry | 1K entries | Rarely | Grain activation | Table schema |
| Type Directory | 1K entries | Rarely | Discovery/tooling | Schema catalog |

Merging them would be like storing table schemas in the same data structure as table rows - wrong level of abstraction.

### Q3: "The AI said there's a 'distributed grain directory' - is that related to dynamic loading?"

**Answer**: **NO** - "Distributed Grain Directory" is Orleans' experimental implementation of **location tracking** (system #1), NOT related to dynamic loading.

It's an alternative to `LocalGrainDirectory`, using 30 partitions per silo with consistent hashing.

**Naming is unfortunate** - it has nothing to do with our "Grain Type Directory" (discovery system).

### Q4: "Can a silo have multiple grain directories of different types?"

**Answer**: **YES** - For location tracking:

```csharp
siloBuilder.AddRedisGrainDirectory("FastDirectory");
siloBuilder.AddAzureTableGrainDirectory("PersistentDirectory");
siloBuilder.AddDistributedGrainDirectory(); // Default

[GrainDirectory("FastDirectory")]
public class RealtimeGrain : Grain, IRealtimeGrain { }

[GrainDirectory("PersistentDirectory")]
public class ArchiveGrain : Grain, IArchiveGrain { }
```

**Key constraints**:
- Each grain type maps to exactly ONE directory
- One directory is designated as "default"
- All instances of a grain type use the same directory

### Q5: "What's the difference between manifest system and type directory?"

**Answer**:

**Manifest System** (Type Registry):
- **Question**: "What IS grain type X and how should it behave?"
- **Data**: CLR type, placement strategy, directory to use, properties
- **When**: Accessed during grain activation
- **Scope**: Local silo + cluster aggregation

**Type Directory** (Discovery):
- **Question**: "WHICH grain types exist and WHERE are they available?"
- **Data**: Type names, load status, which silos have them, metadata
- **When**: Accessed by tooling, dynamic clients, administrators
- **Scope**: Cluster-wide registry (singleton grain)

**Analogy**:
- Manifest = Dictionary with definitions of words
- Type Directory = Library catalog of available books

### Q6: "Why not use IGrainDirectory for type discovery?"

**Answer**: **Wrong abstraction level**

`IGrainDirectory` is for **routing** (performance-critical, millions of lookups):
```csharp
interface IGrainDirectory {
    Task<SiloAddress?> Lookup(GrainId); // Find instance location
}
```

Type discovery is for **exploration** (infrequent, metadata-rich):
```csharp
interface IGrainTypeDirectoryGrain {
    Task<ImmutableList<GrainTypeMeta>> FindGrainTypesAsync(...); // Find types matching pattern
}
```

**Combining them would**:
- Slow down routing (wrong data mixed in)
- Complicate both use cases
- Create confused API surface

---

## Summary

| Criterion | Type Registry | Grain Directory | Type Directory |
|-----------|--------------|-----------------|----------------|
| **In Call Path?** | YES (once per type) | YES (every call) | NO (discovery only) |
| **Performance Critical?** | Medium | HIGH | LOW |
| **Scale** | ~1,000 entries | ~1,000,000 entries | ~1,000 entries |
| **Storage** | In-memory, replicated | Distributed/external | Grain-based |
| **Purpose** | Configuration | Routing | Discovery |
| **Analogy** | Table schema | Table index | Schema catalog |

**They solve different problems at different levels** - merging them would create unnecessary coupling and performance issues.

---

**Document Version**: 2.0
**Last Updated**: 2025-11-27
