# Repository Overview

## Executive Summary

The **dotnet/runtime** repository is the home of the complete .NET runtime implementation, including:
- Multiple runtime engines (CoreCLR, Mono, NativeAOT)
- The entire Base Class Library (BCL) with 218+ packages
- Native hosting infrastructure
- Comprehensive build and test systems
- Cross-platform support for 7+ operating systems and architectures

This is a production-grade, mission-critical codebase used by millions of developers worldwide.

## Repository Scope

### What's Included

**Runtime Implementations:**
- **CoreCLR** - The primary, high-performance JIT-based runtime
  - Evolved from .NET Framework
  - RyuJIT compiler with advanced optimizations
  - Generational garbage collector
  - ~2,511 C/C++ source files
  - ~340K lines in VM alone

- **Mono** - Lightweight runtime for embedded and mobile
  - WebAssembly support (browser and WASI)
  - iOS and Android platforms
  - Alternative JIT implementation

- **NativeAOT** - Ahead-of-time compilation
  - Self-contained native executables
  - No runtime dependency
  - Optimal startup time

**Class Libraries:**
- 218+ NuGet packages
- System.* namespace (BCL)
- Microsoft.Extensions.* framework
- Platform-specific implementations
- Reference assemblies

**Infrastructure:**
- Native hosting layer (dotnet executable)
- Build system (MSBuild-based)
- Test infrastructure
- Developer tools

### What's NOT Included

- **ASP.NET Core** - Separate repository (dotnet/aspnetcore)
- **Windows Forms / WPF** - Separate repositories (dotnet/winforms, dotnet/wpf)
- **.NET SDK** - Separate repository (dotnet/sdk)
- **NuGet client** - Separate repository (NuGet/NuGet.Client)
- **C# compiler** - Separate repository (dotnet/roslyn)

## Key Statistics

| Metric | Value |
|--------|-------|
| **Code Volume** | |
| CoreCLR C/C++ files | ~2,511 |
| VM component | ~340K lines |
| JIT compiler | ~500K lines |
| GC implementation | ~2M lines (gc.cpp) |
| Library packages | 218+ |
| Test categories | 15+ |
| | |
| **Platform Support** | |
| Operating Systems | Windows, Linux, macOS, FreeBSD, iOS, Android, WebAssembly |
| Architectures | x86, x64, ARM32, ARM64, RISC-V, LoongArch64, WASM |
| Build configurations | Debug, Checked, Release |
| | |
| **Repository Size** | |
| Full clone | 1-1.5 GB |
| Single build output | 10-20 GB |
| Documentation files | 100+ |
| CMake configurations | 288+ |

## Architecture Overview

### Component Layering

```
┌─────────────────────────────────────────────────────┐
│         User Applications (.NET apps)               │
└────────────────┬────────────────────────────────────┘
                 │
┌────────────────▼────────────────────────────────────┐
│    Class Libraries (src/libraries/)                 │
│    - System.* (BCL)                                 │
│    - Microsoft.Extensions.*                         │
│    - 218+ packages                                  │
└────────────────┬────────────────────────────────────┘
                 │
┌────────────────▼────────────────────────────────────┐
│    System.Private.CoreLib                           │
│    (Bridge between managed and native)              │
└────────────────┬────────────────────────────────────┘
                 │
         ┌───────┴────────┬──────────────┐
         │                │              │
┌────────▼─────┐  ┌──────▼─────┐  ┌────▼──────┐
│   CoreCLR    │  │    Mono    │  │ NativeAOT │
│              │  │            │  │           │
│ - RyuJIT     │  │ - Mono JIT │  │ - AOT     │
│ - GC         │  │ - SGen GC  │  │ - No JIT  │
│ - VM         │  │ - WASM     │  │ - Small   │
└──────┬───────┘  └──────┬─────┘  └─────┬─────┘
       │                 │              │
┌──────▼─────────────────▼──────────────▼──────┐
│    Platform Abstraction Layer (PAL)          │
└──────────────────┬───────────────────────────┘
                   │
┌──────────────────▼───────────────────────────┐
│    Operating System & Hardware               │
└──────────────────────────────────────────────┘
```

### Directory Organization

```
dotnet-runtime/
├── src/                    # All source code
│   ├── coreclr/           # CoreCLR runtime
│   ├── mono/              # Mono runtime
│   ├── libraries/         # Class libraries (BCL)
│   ├── native/            # Native hosting & P/Invoke implementations
│   ├── installer/         # Packaging and installation
│   ├── tests/             # Test suites
│   └── tools/             # Development tools
│
├── docs/                   # Documentation
│   ├── design/            # Design documents and BOTR
│   ├── coding-guidelines/ # Code standards
│   └── workflow/          # Build and test workflows
│
├── eng/                    # Build infrastructure
│   ├── common/            # Shared build components
│   ├── native/            # Native build (CMake)
│   └── *.props/*.targets  # MSBuild configuration
│
├── .github/               # GitHub CI/CD
├── .devcontainer/         # Dev container configs
└── Build.proj             # Root build project
```

## Core Technologies

### Execution & Compilation

| Technology | Purpose | Location |
|------------|---------|----------|
| **RyuJIT** | Primary JIT compiler | src/coreclr/jit/ |
| **Tiered Compilation** | Fast startup + optimized steady-state | src/coreclr/vm/ |
| **Dynamic PGO** | Profile-guided optimization | src/coreclr/jit/ |
| **ReadyToRun (R2R)** | Pre-compiled IL | src/coreclr/tools/ |
| **NativeAOT** | Ahead-of-time compilation | src/coreclr/nativeaot/ |
| **Mono JIT** | Lightweight JIT | src/mono/mini/ |

### Memory Management

| Technology | Purpose | Location |
|------------|---------|----------|
| **Generational GC** | Primary garbage collector | src/coreclr/gc/ |
| **Concurrent GC** | Low-latency collection | src/coreclr/gc/ |
| **Server vs Workstation** | GC mode selection | src/coreclr/gc/ |
| **SGen** | Mono garbage collector | src/mono/sgen/ |

### Interoperability

| Technology | Purpose | Location |
|------------|---------|----------|
| **P/Invoke** | Call native functions | src/coreclr/vm/dllimport.cpp |
| **COM Interop** | Windows COM integration | src/coreclr/interop/ |
| **Reverse P/Invoke** | Native calling managed | src/coreclr/vm/ |
| **JavaScript Interop** | WASM/browser integration | src/mono/wasm/ |

### Diagnostics

| Technology | Purpose | Location |
|------------|---------|----------|
| **EventPipe** | Event streaming | src/native/eventpipe/ |
| **ETW** | Event Tracing for Windows | src/coreclr/vm/ |
| **Profiler API** | Performance profiling | src/coreclr/vm/proftoeetointerfaceimpl.cpp |
| **DAC** | Debugger data access | src/coreclr/debug/daccess/ |

## Development Workflow Overview

### Build Process

```bash
# Build everything
./build.sh

# Build specific component
./build.sh -subset clr                    # CoreCLR only
./build.sh -subset libs                   # Libraries only
./build.sh -subset clr+libs               # Both

# Specific configuration
./build.sh -subset clr -configuration Debug
./build.sh -subset clr -arch arm64 -os linux
```

### Test Process

```bash
# Build tests
./build.sh -subset clr.tests

# Run tests
./build.sh -subset clr -test
dotnet test artifacts/bin/.../Tests.dll
```

### Common Patterns

1. **Clone and build**
   ```bash
   git clone https://github.com/dotnet/runtime.git
   cd runtime
   ./build.sh -subset clr+libs
   ```

2. **Modify and rebuild**
   ```bash
   # Edit code in src/coreclr/ or src/libraries/
   ./build.sh -subset clr    # Incremental rebuild
   ```

3. **Test changes**
   ```bash
   ./build.sh -subset clr.tests
   ./artifacts/bin/tests/.../run-tests.sh
   ```

## Team Organization

### Area Ownership

The repository is divided into areas, each owned by specific teams:

- **area-Infrastructure-coreclr** - CoreCLR infrastructure
- **area-Runtime-coreclr** - CoreCLR VM and runtime
- **area-Codegen-coreclr** - JIT compiler
- **area-GC-coreclr** - Garbage collector
- **area-Diagnostics-coreclr** - Diagnostics and profiling
- **area-System.Runtime** - Core BCL types
- **area-System.IO** - I/O libraries
- **area-System.Net** - Networking
- **area-Extensions-*** - Microsoft.Extensions framework
- **area-Infrastructure-mono** - Mono infrastructure
- **area-VM-mono** - Mono runtime

See `docs/area-owners.md` for complete ownership matrix.

## Design Principles

### 1. Performance First
- JIT optimization is critical
- GC latency and throughput matter
- Tiered compilation balances startup and steady-state

### 2. Cross-Platform
- Platform Abstraction Layer (PAL) hides OS differences
- Architecture-specific code isolated
- Build system handles platform matrix

### 3. Modularity
- Components can be built independently
- Clear dependency hierarchy
- Multiple runtime implementations supported

### 4. Backward Compatibility
- API contracts must be maintained
- Breaking changes require extensive review
- Versioning strategy enforced

### 5. Code Quality
- Extensive test coverage required
- Static analysis enforced
- Performance regression testing

## Getting Help

### Documentation Resources

- **Book of the Runtime (BOTR)** - `docs/design/coreclr/botr/` - Deep technical documentation
- **Design documents** - `docs/design/features/` - Feature specifications
- **Coding guidelines** - `docs/coding-guidelines/` - Code standards
- **Workflow guides** - `docs/workflow/` - Build and test procedures

### Common Questions

**Q: Which runtime should I work on?**
- CoreCLR for server, desktop, and general-purpose scenarios
- Mono for mobile (iOS/Android) and WebAssembly
- NativeAOT for self-contained, trimmed applications

**Q: Where do I add a new BCL API?**
- See [04-Libraries-Guide.md](04-Libraries-Guide.md)
- New types go in appropriate System.* library
- Follow API review process

**Q: How do I debug the runtime?**
- See `docs/workflow/debugging/`
- Use checked builds for debugging (optimized + asserts)
- DAC for managed debugging

**Q: How do I add support for a new architecture?**
- See [10-Architecture-Concepts.md](10-Architecture-Concepts.md)
- Implement PAL for platform
- Add architecture-specific code in runtime/{arch}/
- Port JIT backend

## Next Steps

- **Understand structure**: Read [01-Directory-Structure.md](01-Directory-Structure.md)
- **Dive into components**: Read guides 02-07 based on your area
- **Find features**: Use [08-Feature-Location-Reference.md](08-Feature-Location-Reference.md)
- **Start coding**: Follow [09-Contribution-Workflows.md](09-Contribution-Workflows.md)

---

This overview provides the foundation for understanding the massive .NET Runtime codebase. Use the detailed guides for specific areas of interest.
