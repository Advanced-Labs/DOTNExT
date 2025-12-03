# .NET Runtime Repository Map & Guide

**Version:** 1.0
**Last Updated:** 2025-11-21
**Target Audience:** Microsoft engineers working on dotnet/runtime

## Purpose

This comprehensive guide maps the entire .NET Runtime repository, providing:
- Complete directory structure and component organization
- Technology and feature location mappings
- Quick reference for finding and modifying code
- Best practices for common development workflows

## Table of Contents

### Core Documentation

1. **[00-Repository-Overview.md](00-Repository-Overview.md)** - Executive summary and statistics
2. **[01-Directory-Structure.md](01-Directory-Structure.md)** - Complete top-level organization
3. **[02-CoreCLR-Guide.md](02-CoreCLR-Guide.md)** - CoreCLR runtime internals
4. **[03-Mono-Runtime-Guide.md](03-Mono-Runtime-Guide.md)** - Mono runtime guide
5. **[04-Libraries-Guide.md](04-Libraries-Guide.md)** - Class libraries organization (218+ packages)
6. **[05-Native-And-Hosting.md](05-Native-And-Hosting.md)** - Native code, hosting, and installation
7. **[06-Build-System.md](06-Build-System.md)** - Build infrastructure and configuration
8. **[07-Testing-Guide.md](07-Testing-Guide.md)** - Testing infrastructure and workflows
9. **[08-Feature-Location-Reference.md](08-Feature-Location-Reference.md)** - Quick lookup: technology → location
10. **[09-Contribution-Workflows.md](09-Contribution-Workflows.md)** - Common development tasks
11. **[10-Architecture-Concepts.md](10-Architecture-Concepts.md)** - Key architectural patterns and principles

### Advanced Integration Guides

12. **[11-Major-Subsystem-Integration.md](11-Major-Subsystem-Integration.md)** - How to add GC-scale systems: exact integration points
13. **[12-Component-Dependencies.md](12-Component-Dependencies.md)** - Component interaction matrix and dependency analysis
14. **[13-Modification-Impact-Zones.md](13-Modification-Impact-Zones.md)** - Understanding ripple effects of code changes
15. **[14-Extension-Points-Catalog.md](14-Extension-Points-Catalog.md)** - Where the runtime is designed to be extended

## Quick Start

### I want to modify...

| What | See Document | Quick Location |
|------|--------------|----------------|
| JIT compiler optimizations | [02-CoreCLR-Guide.md](02-CoreCLR-Guide.md) | `src/coreclr/jit/` |
| Garbage collector | [02-CoreCLR-Guide.md](02-CoreCLR-Guide.md) | `src/coreclr/gc/` |
| Type system / metadata | [02-CoreCLR-Guide.md](02-CoreCLR-Guide.md) | `src/coreclr/vm/`, `src/coreclr/md/` |
| Framework libraries (BCL) | [04-Libraries-Guide.md](04-Libraries-Guide.md) | `src/libraries/System.*/` |
| WebAssembly support | [03-Mono-Runtime-Guide.md](03-Mono-Runtime-Guide.md) | `src/mono/wasm/` |
| P/Invoke / interop | [05-Native-And-Hosting.md](05-Native-And-Hosting.md) | `src/coreclr/vm/interop*`, `src/native/libs/` |
| Host executable (dotnet) | [05-Native-And-Hosting.md](05-Native-And-Hosting.md) | `src/native/corehost/` |
| Build system | [06-Build-System.md](06-Build-System.md) | `eng/` |
| Tests | [07-Testing-Guide.md](07-Testing-Guide.md) | `src/tests/` |

### I want to understand...

| What | See Document |
|------|--------------|
| Overall architecture | [00-Repository-Overview.md](00-Repository-Overview.md) |
| How components fit together | [10-Architecture-Concepts.md](10-Architecture-Concepts.md) |
| Where to find specific technologies | [08-Feature-Location-Reference.md](08-Feature-Location-Reference.md) |
| How to build and test | [09-Contribution-Workflows.md](09-Contribution-Workflows.md) |
| Platform abstraction patterns | [10-Architecture-Concepts.md](10-Architecture-Concepts.md) |

### I need to...

| Task | See Document |
|------|--------------|
| Build a specific component | [06-Build-System.md](06-Build-System.md) |
| Run tests | [07-Testing-Guide.md](07-Testing-Guide.md) |
| Add a new library | [04-Libraries-Guide.md](04-Libraries-Guide.md) |
| Port to a new architecture | [02-CoreCLR-Guide.md](02-CoreCLR-Guide.md), [10-Architecture-Concepts.md](10-Architecture-Concepts.md) |
| Add diagnostic features | [08-Feature-Location-Reference.md](08-Feature-Location-Reference.md) |
| Add a major new subsystem | [11-Major-Subsystem-Integration.md](11-Major-Subsystem-Integration.md) |
| Understand component dependencies | [12-Component-Dependencies.md](12-Component-Dependencies.md) |
| Assess impact of my changes | [13-Modification-Impact-Zones.md](13-Modification-Impact-Zones.md) |
| Find where to hook/extend runtime | [14-Extension-Points-Catalog.md](14-Extension-Points-Catalog.md) |

## Repository Statistics

- **CoreCLR Source Files:** ~2,511 C/C++ files
- **VM Code:** ~340K lines
- **JIT Code:** ~500K lines
- **Library Packages:** 218+
- **Platform Targets:** 7+ (Windows, Linux, macOS, FreeBSD, WebAssembly, iOS, Android)
- **Architecture Support:** 7+ (x86, x64, ARM32, ARM64, RISC-V, LoongArch64, WASM)

## Major Components

### Three Runtime Implementations

1. **CoreCLR** (`src/coreclr/`) - Primary JIT-based runtime, evolved from .NET Framework
2. **Mono** (`src/mono/`) - Lightweight runtime for embedded, mobile, and WebAssembly
3. **NativeAOT** (`src/coreclr/nativeaot/`) - Ahead-of-time compilation to native code

### Framework Libraries

- **218+ packages** in `src/libraries/`
- Core BCL (System.Runtime, System.IO, System.Net, etc.)
- Microsoft.Extensions framework (DI, Logging, Configuration, etc.)
- Platform-specific implementations

### Supporting Infrastructure

- **Native hosting** (`src/native/corehost/`) - The `dotnet` command and app hosting
- **Build system** (`eng/`) - MSBuild-based modular build infrastructure
- **Tests** (`src/tests/`) - Comprehensive test suites mirroring runtime components
- **Tools** (`src/tools/`) - Development and diagnostic utilities

## Navigation Tips

1. **Start with the overview** - [00-Repository-Overview.md](00-Repository-Overview.md) provides context
2. **Use the feature reference** - [08-Feature-Location-Reference.md](08-Feature-Location-Reference.md) for quick lookups
3. **Deep dive by component** - Use guides 02-07 for detailed information
4. **Follow workflows** - [09-Contribution-Workflows.md](09-Contribution-Workflows.md) for practical tasks
5. **For major changes** - Use advanced guides (11-14) to understand integration points and impact

## When to Use Advanced Guides

### Use Basic Guides (01-10) When:
- Learning the repository structure
- Finding where features are implemented
- Making localized changes (bug fixes, optimizations)
- Adding standard features (new library, new API)
- Following established patterns

### Use Advanced Guides (11-14) When:
- **Adding major subsystems** (like a new GC, JIT, or memory manager)
  → See [11-Major-Subsystem-Integration.md](11-Major-Subsystem-Integration.md)

- **Understanding cross-component interactions** (how VM, JIT, GC coordinate)
  → See [12-Component-Dependencies.md](12-Component-Dependencies.md)

- **Assessing change impact** (will my change break other components?)
  → See [13-Modification-Impact-Zones.md](13-Modification-Impact-Zones.md)

- **Finding extension points** (where can I hook into the runtime?)
  → See [14-Extension-Points-Catalog.md](14-Extension-Points-Catalog.md)

**Key Insight from Advanced Guides:**

The advanced guides answer the question: *"If we wanted to add systems like the GC but having other concerns in how those augment the platform, how much would we know in which files and systems and types exactly to work with/around/into?"*

**Answer:** About **80-90% of integration points can be enumerated precisely**:
- ✅ Exact interfaces to implement (~5-10 files)
- ✅ Exact VM integration points (~10-20 files)
- ✅ Exact JIT codegen files (~4-6 per architecture)
- ✅ Configuration and diagnostics files (~5-10 files)
- ⚠️ Some discovery needed for: bit layouts, performance implications, edge cases

For a GC-scale subsystem: expect to touch **~30-50 files** total (out of 10,000+ in repo).

## Additional Resources

### Official Documentation
- **docs/design/coreclr/botr/** - Book of the Runtime (deep technical documentation)
- **docs/coding-guidelines/** - Coding standards and conventions
- **docs/workflow/** - Build, test, and debug workflows
- **docs/design/features/** - Feature design documents

### Key Entry Points
- **Build script:** `eng/build.sh` / `eng/build.cmd`
- **Root build:** `Build.proj`
- **Subset definitions:** `eng/Subsets.props`
- **Global configuration:** `Directory.Build.props`

## Document Maintenance

This guide is a living document. When making significant architectural changes:
1. Update the relevant guide document
2. Update the feature location reference if needed
3. Keep the overview statistics current

## Feedback

For questions or corrections to this guide, contact the repository maintainers or create an issue in the dotnet/runtime repository.

---

**Happy coding!** This guide should help you navigate the vast .NET Runtime codebase efficiently.
