# Vision Elements Extracted From Available Documents

## Documents Read
1. `/src/NewOrleans/NewOrleans.md` (39KB) - Read ✅
2. `/docs/NewOrleans/References/PluginGrainArchitecture.md` - Read ✅
3. `/docs/NewOrleans/References/DynamicGrainAccess.md` - Read ✅
4. `/docs/NewOrleans/Researches/ClarificationsAboutDirectoriesAndArchitecture.md` - Read ✅

## Documents NOT FOUND IN REPO (User must provide):
- `Docs/Async+/Async+.md` - NOT FOUND
- `Docs/New Orleans/New Orleans Features/OrleansAsync+.md` - NOT FOUND
- `Contexts/001 - 2025-12-04/Analysis/` folder - NOT FOUND

---

## Vision Elements From NewOrleans.md

### Core: Dynamic Grain Loading System
**Status**: IMPLEMENTED ✅

The NewOrleans fork implements a complete dynamic grain loading system allowing:
- Runtime loading/unloading of grain assemblies without restart
- Uses McMaster.NETCore.Plugins (MDCP) for assembly isolation
- Unloadable via collectible AssemblyLoadContext

### Key Components Implemented:
1. **Plugin Assembly Loader** - MDCP-based isolated loading
2. **Grain Type Directory (GTD)** - Cluster-wide registry of grain types
3. **Dynamic Grain Client** - Access grains without compile-time refs
4. **Package Cache System** - LRU/LFU eviction, file-system backed
5. **Package Store** - Multi-source (FileSystem, GrainStorage)
6. **DLR Integration** - C# `dynamic` keyword support

### Architecture Vision (from docs):
```
Orleans Cluster
├── Grain Type Directory (GTD) - Singleton grain
│   ├── Tracks which packages exist
│   ├── Which grain types are in each package
│   └── Which silos have them loaded
├── Package Store (Multi-Source)
│   ├── FileSystemPackageSource
│   └── GrainStoragePackageSource
├── Per-Silo Components
│   ├── Plugin Loader (MDCP)
│   ├── Manifest Provider
│   └── Package Cache (Local)
└── Dynamic Grain Client
    ├── Package loading/unloading
    ├── Dynamic grain access (DLR)
    └── GTD queries
```

### What's NOT Implemented:
- NuGet-based package system (HIGH priority)
- Interface hash compatibility
- Package dependency resolution
- Rolling upgrades
- Package signing/verification

---

## Vision Elements From PluginGrainArchitecture.md

**Last Updated**: 2025-11-27

### Plugin System Details:
- Uses `McMaster.NETCore.Plugins` 2.0.0
- `PluginLoader.CreateFromAssemblyFile()` with `IsUnloadable = true`
- Shared types configuration for Orleans type identity
- Split assembly support (Contracts/Implementation/Codegen)

### Key Insight: Naming Convention
All types use `Plugin*` prefix (not "Dynamic"):
- `IPluginGrainLoader`
- `PluginGrainLoaderService`
- `PluginAssemblyLoader`
- `PluginAssemblySet`

This avoids the misleading "Dynamic" prefix.

---

## Vision Elements From DynamicGrainAccess.md

**Implementation Status**: ALL 6 PHASES COMPLETE ✅

### Phases Completed:
1. Core Types (GrainPackage, GrainTypeMeta, GrainInterfaceMeta)
2. GTD Implementation
3. Factory Extensions (DynamicGrainReference DLR wrapper)
4. Package Storage & Distribution
5. Package Cache (LRU/LFU/FIFO/LargestFirst)
6. Client Integration (IDynamicGrainClient, GrainPackageHandle)

### Three-Tier Package Strategy:
1. **InterfacesOnly** - For clients (just interfaces + proxies)
2. **Full** - For silos (interfaces + implementations + codegen)
3. **ImplementationsOnly** - Requires separate interface package

---

## Vision Elements From ClarificationsAboutDirectoriesAndArchitecture.md

**Research Date**: 2025-11-24

### Critical Clarification: Three "Directory" Systems
| System | Purpose | Level |
|--------|---------|-------|
| Grain Directory (IGrainDirectory) | WHERE instances are | Instance |
| Type Registry (Manifest) | WHAT types exist | Type |
| Type Directory (IGrainTypeDirectoryGrain) | WHICH types available | Type |

**Key Insight**: These are DIFFERENT abstractions at DIFFERENT levels - should NOT be merged.

---

## MISSING: Higher-Level Vision Components

The documents I found cover **NewOrleans** (Orleans fork) implementation details.

**NOT FOUND** in current repo - likely in user's local files:
- **VCOM** (Component Object Model) - conceptual layer over runtime/VOS
- **VARIA** - Types/objects built over VCOM
- **VOS** (Virtual Operating System) - The larger vision
- **Async+** - Experimental async paradigm (may be deprecated)
- **Async2/Unwinder** - Runtime-integrated execution model
- **Runtime-as-Kernel** - Evolution of dotnet runtime to VOS kernel

These concepts were mentioned by the user but the documentation isn't in the repository yet.

---

## Current Understanding of Architecture Layers

Based on available docs + user hints:

```
[Higher Level - NOT DOCUMENTED YET]
┌─────────────────────────────────────────────┐
│              VARIA Types/Objects             │
│  (User-facing component types, like ActiveX) │
├─────────────────────────────────────────────┤
│                    VCOM                      │
│    (Component Object Model, like COM/DCOM)   │
├─────────────────────────────────────────────┤
│              VOS Services/Systems            │
│       (Virtual Operating System layer)       │
└─────────────────────────────────────────────┘

[Foundation - DOCUMENTED]
┌─────────────────────────────────────────────┐
│               NewOrleans                     │
│    (Dynamic grain loading, GTD, packages)    │
├─────────────────────────────────────────────┤
│              DOTNExT Runtime                 │
│   (Forked dotnet 9, evolving to "Kernel")    │
└─────────────────────────────────────────────┘
```

---

## Questions Needing Answers (from missing docs)

1. What is the exact relationship between VCOM and NewOrleans grains?
2. How do VARIAs map to underlying VCOM/grain types?
3. What happened to Async+ - is it replaced by Async2/Unwinder approach?
4. What execution model is planned for the runtime/kernel?
5. What services/systems will the VOS provide?
