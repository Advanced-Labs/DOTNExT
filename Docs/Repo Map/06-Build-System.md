# Build System Guide

Complete guide to the .NET Runtime build infrastructure.

## Overview

The .NET Runtime uses a sophisticated, modular build system:
- **MSBuild** for managed code and orchestration
- **CMake** for native code (C/C++)
- **Subset-based** architecture for building specific components
- **Cross-platform** support (Windows, Linux, macOS, etc.)
- **Cross-compilation** support (build for different architectures)

**Location:** `eng/`, root build files

## Entry Points

### Command-Line Build Scripts

**Linux/macOS:**
```bash
./build.sh [options]
```

**Windows:**
```cmd
build.cmd [options]
```

**Common Options:**
```bash
# Build specific subset
./build.sh -subset clr+libs

# Specific configuration
./build.sh -configuration Debug|Checked|Release

# Specific architecture
./build.sh -arch x64|x86|arm|arm64|wasm

# Specific OS
./build.sh -os windows|linux|osx|freebsd|ios|android|browser

# Cross-compilation
./build.sh -cross

# Run tests
./build.sh -test

# Clean before build
./build.sh -clean

# Binary log (for troubleshooting)
./build.sh -bl
```

### Root Build File

**Build.proj** - Main MSBuild orchestration file

```xml
<Project>
  <Import Project="eng/Subsets.props" />
  <Import Project="Directory.Build.props" />
  <!-- Build orchestration -->
</Project>
```

## Build Subsets

**Location:** `eng/Subsets.props`

Subsets allow building specific components independently.

### Core Subsets

```bash
# CoreCLR Runtime
clr                # Full CoreCLR (runtime + CoreLib)
clr.runtime        # Just the runtime (JIT, GC, VM)
clr.jit            # Just the JIT compiler
clr.gc             # Just the GC
clr.tools          # CoreCLR tools
clr.nativeaot      # NativeAOT compiler
clr.tests          # CoreCLR tests

# Mono Runtime
mono               # Full Mono runtime
mono.runtime       # Mono runtime only
mono.wasm          # WebAssembly support
mono.wasmruntime   # WASM runtime
mono.tests         # Mono tests

# Libraries
libs               # All class libraries
libs.ref           # Reference assemblies only
libs.src           # Implementation assemblies
libs.tests         # Library tests
libs.native        # Native P/Invoke implementations

# Hosting
host               # Host executables (dotnet, apphost, etc.)
host.native        # Native host components

# Packs (Distributable packages)
packs              # Runtime packs
packs.tests        # Pack tests

# Installer
installer          # Installer generation
```

### Subset Dependencies

Subsets have dependencies (defined in `eng/Subsets.props`):

```
libs → clr.corelib
     → mono.corelib (if building Mono)
     → libs.native

clr → clr.runtime
    → clr.corelib
    → clr.tools

host → libs
     → clr (or mono)
```

The build system automatically resolves dependencies.

### Common Subset Combinations

```bash
# CoreCLR development
./build.sh -subset clr+libs

# Mono + WebAssembly development
./build.sh -subset mono+libs

# Full product build
./build.sh -subset clr+mono+libs+host+packs

# Just tests
./build.sh -subset clr.tests+libs.tests
```

## Build Configurations

### Configuration Types

**Debug:**
- No optimization
- Assertions enabled
- Debug symbols
- Slow runtime, easy to debug
- Use for: Deep debugging

**Checked:**
- Optimized code
- Assertions enabled
- Debug symbols
- Fast runtime with diagnostics
- **Recommended for development**

**Release:**
- Fully optimized
- Assertions disabled
- Optimized for production
- Use for: Performance testing, production

### Configuration-Specific Build

```bash
# Debug build (default)
./build.sh -configuration Debug

# Checked build (recommended)
./build.sh -configuration Checked

# Release build
./build.sh -configuration Release

# Multiple configurations
./build.sh -configuration Debug+Release
```

## Architecture and OS Matrix

### Supported Platforms

| OS | Architectures | Notes |
|----|---------------|-------|
| **Windows** | x86, x64, arm, arm64 | Full support |
| **Linux** | x64, arm, arm64, riscv64, loongarch64 | Most distributions |
| **macOS** | x64, arm64 | Intel and Apple Silicon |
| **FreeBSD** | x64 | Community support |
| **iOS** | arm64, x64 (simulator) | Mono only |
| **Android** | arm, arm64, x64 | Mono only |
| **WebAssembly** | wasm | Mono only |

### Building for Specific Platforms

```bash
# Native build (build for current platform)
./build.sh -arch x64 -os linux

# Cross-compilation (Windows → Linux)
build.cmd -subset clr -arch x64 -os linux

# macOS ARM64 (Apple Silicon)
./build.sh -arch arm64 -os osx

# WebAssembly
./build.sh -subset mono.wasmruntime -arch wasm -os browser

# iOS
./build.sh -subset mono -os ios -arch arm64
```

### Cross-Compilation

**Linux ARM64 from x64:**
```bash
# Install cross-compilation tools
sudo apt-get install gcc-aarch64-linux-gnu g++-aarch64-linux-gnu

# Build rootfs (one-time setup)
./eng/common/cross/build-rootfs.sh arm64

# Cross-compile
./build.sh -subset clr -arch arm64 -cross
```

## Native Build System (CMake)

**Location:** `src/coreclr/` (288+ CMakeLists.txt files)

### CMake Organization

```
src/coreclr/
├── CMakeLists.txt              # Root CMake file
├── vm/
│   └── CMakeLists.txt          # VM components
├── jit/
│   └── CMakeLists.txt          # JIT compiler
├── gc/
│   └── CMakeLists.txt          # Garbage collector
└── pal/
    └── CMakeLists.txt          # Platform Abstraction Layer
```

### CMake Configuration

**Key CMake files in `eng/native/`:**
- `configure.cmake` - Platform detection and configuration
- `configuretools.cmake` - Compiler configuration
- `crossdag.cmake` - Cross-compilation support
- `functions.cmake` - Helper functions

**Generated files (artifacts/obj/):**
- `CMakeCache.txt` - CMake cache
- `Makefile` or `.vcxproj` - Build files

### CMake Variables

```cmake
# Target architecture
-DCLR_CMAKE_TARGET_ARCH=x64

# Configuration
-DCMAKE_BUILD_TYPE=Debug

# Cross-compilation
-DCLR_CMAKE_CROSS_COMPILE=ON
-DCLR_CMAKE_CROSS_ROOTFS=/path/to/rootfs
```

### Building Native Components Manually

```bash
# Configure
cmake -S src/coreclr -B artifacts/obj/coreclr \
  -DCMAKE_BUILD_TYPE=Debug \
  -DCLR_CMAKE_TARGET_ARCH=x64

# Build
cmake --build artifacts/obj/coreclr
```

## MSBuild Configuration

### Key MSBuild Files

**Root level:**
- `Directory.Build.props` - Global properties (imported first)
- `Directory.Build.targets` - Global targets (imported last)
- `global.json` - .NET SDK version pinning

**Engineering (`eng/`):**
```
eng/
├── Subsets.props              # Subset definitions
├── Versions.props             # Component versions
├── Version.Details.props      # Dependency versions
├── Directory.Build.props      # Build properties
├── Directory.Build.targets    # Build targets
├── SourceBuild.props          # Source build configuration
└── Signing.props              # Code signing
```

### Important Properties

**Version.props:**
```xml
<MajorVersion>9</MajorVersion>
<MinorVersion>0</MinorVersion>
<PatchVersion>0</PatchVersion>
```

**Subsets.props:**
```xml
<SubsetName Include="clr">
  <Description>CoreCLR runtime and System.Private.CoreLib</Description>
  <Dependencies>clr.runtime;clr.corelib</Dependencies>
</SubsetName>
```

## Build Outputs

### Output Directory Structure

```
artifacts/
├── bin/                       # Build outputs
│   ├── coreclr/
│   │   └── windows.x64.Debug/
│   │       ├── clrjit.dll     # JIT compiler
│   │       ├── coreclr.dll    # Runtime
│   │       ├── corerun.exe    # Test host
│   │       └── System.Private.CoreLib.dll
│   ├── mono/
│   ├── libraries/
│   │   └── System.IO.FileSystem/
│   └── tests/
├── obj/                       # Intermediate objects
│   ├── coreclr/
│   └── libraries/
├── packages/                  # NuGet packages
├── logs/                      # Build logs
└── tmp/                       # Temporary files
```

### Binary Locations

**CoreCLR:**
- Runtime DLLs: `artifacts/bin/coreclr/{os}.{arch}.{config}/`
- CoreLib: Same location as runtime
- Tools: `artifacts/bin/coreclr/tools/`

**Libraries:**
- Implementation: `artifacts/bin/libraries/{LibName}/{config}/`
- Reference: `artifacts/bin/ref/{LibName}/{config}/`
- Tests: `artifacts/bin/{LibName}.Tests/{config}/`

**Tests:**
- CoreCLR tests: `artifacts/tests/coreclr/`
- Library tests: Built in-place

## Incremental Builds

### How Incremental Builds Work

MSBuild tracks:
- Input files (source code)
- Output files (assemblies, executables)
- Timestamps

**Rebuilds when:**
- Source files change
- Dependencies change
- Build configuration changes

**Skips when:**
- Inputs unchanged
- Outputs up-to-date

### Forcing Rebuilds

```bash
# Clean everything
git clean -xdf

# Clean specific subset
./build.sh -subset clr -clean

# Rebuild without clean (incremental)
./build.sh -subset clr
```

### Incremental Build Issues

**Symptoms:**
- Build errors after switching branches
- Unexpected behavior

**Solutions:**
```bash
# Remove CMake cache
rm -rf artifacts/obj/coreclr/

# Clean and rebuild
./build.sh -subset clr -clean

# Nuclear option: clean everything
git clean -xdf
./build.sh
```

## Code Analysis

**Location:** `eng/CodeAnalysis.*.globalconfig`

### Analyzer Configuration

**CodeAnalysis.src.globalconfig** - Source code analysis (~64K lines!)
- Coding style rules
- Performance rules
- Security rules
- Naming conventions

**CodeAnalysis.test.globalconfig** - Test code analysis
- Relaxed rules for tests
- Test-specific patterns

### Running Analyzers

Analyzers run automatically during build:
```bash
./build.sh -subset libs  # Analyzers run
```

Treating warnings as errors:
```bash
./build.sh -warnaserror
```

## Binary Logs

### Generating Binary Logs

```bash
# Create binary log
./build.sh -bl

# Output: artifacts/log/Debug/Build.binlog
```

### Viewing Binary Logs

**MSBuild Binary Log Viewer:**
1. Download: https://msbuildlog.com/
2. Open .binlog file
3. Analyze build:
   - Task execution times
   - Project dependencies
   - Build order
   - Errors and warnings

**Command line:**
```bash
# View log summary
dotnet build -bl:summary.binlog -v:detailed
```

## Performance Optimization

### Parallel Builds

```bash
# Maximum parallelism (default)
./build.sh -m

# Specific CPU count
./build.sh -m:4
```

### Build Caching

**Incremental build:**
- MSBuild tracks dependencies
- Only rebuilds changed projects

**Distributed builds (not officially supported):**
- Can use ccache for C/C++ (unofficial)

### Build Performance Tips

1. **Use Checked instead of Debug** - Faster runtime, still has asserts
2. **Build only what you need** - Use subsets
3. **Incremental builds** - Don't clean unless necessary
4. **SSD storage** - Build is I/O intensive
5. **Sufficient RAM** - 16GB+ recommended

## Troubleshooting

### Common Build Errors

**Error: "SDK version not found"**
```bash
# Check global.json
cat global.json

# Install required SDK
# Download from https://dotnet.microsoft.com/download
```

**Error: "CMake not found"**
```bash
# Install CMake
# Windows: choco install cmake
# Linux: sudo apt-get install cmake
# macOS: brew install cmake
```

**Error: "Cannot find Windows SDK"** (Windows)
```bash
# Install Windows SDK via Visual Studio Installer
# Workload: "Desktop development with C++"
```

**Error: Cross-compilation rootfs not found**
```bash
# Build rootfs
./eng/common/cross/build-rootfs.sh arm64
```

### Build Logging

**Verbose logging:**
```bash
./build.sh -v:detailed

# Or
./build.sh -v:diagnostic  # Very verbose
```

**Specific project logging:**
```bash
dotnet build src/libraries/System.IO.FileSystem/src/System.IO.FileSystem.csproj -v:detailed
```

### Clean Builds

**Levels of cleaning:**

1. **Soft clean** - MSBuild clean
   ```bash
   ./build.sh -clean
   ```

2. **Medium clean** - Remove artifacts
   ```bash
   rm -rf artifacts/
   ```

3. **Nuclear clean** - Remove everything not in source control
   ```bash
   git clean -xdf  # CAREFUL: Removes ALL untracked files
   ```

## Advanced Scenarios

### Source Build

Build .NET from source without prebuilt binaries:

```bash
./build.sh -subset clr+libs -sourcebuild
```

**Why:** Linux distributions require building from source.

### Official Builds

Microsoft's official builds use additional:
- Code signing
- Compliance validation
- Security scanning

**Configuration:** `eng/pipelines/` Azure DevOps YAML files

### Docker Builds

Build inside Docker container:

```bash
# Use .devcontainer configuration
docker build -f .devcontainer/Dockerfile .

# Or manual
docker run -v $(pwd):/runtime -it mcr.microsoft.com/dotnet/sdk:8.0
cd /runtime
./build.sh
```

## CI/CD Integration

**Location:** `.github/workflows/`, `eng/pipelines/`

### GitHub Actions

**.github/workflows/runtime.yml** - Main CI workflow
- Builds all configurations
- Runs tests
- Creates artifacts

### Azure DevOps

**eng/pipelines/** - Official build pipelines
- Longer-running validations
- Platform matrix
- Signed builds

## Summary

Build system key points:

**Entry:** `build.sh` / `build.cmd`

**Subsets:** Modular components (clr, mono, libs, host, packs)

**Configurations:** Debug, Checked (recommended), Release

**Platforms:** Windows, Linux, macOS, iOS, Android, WebAssembly

**Technologies:**
- MSBuild - Managed code orchestration
- CMake - Native code compilation

**Outputs:** `artifacts/bin/`, `artifacts/packages/`

**Tips:**
- Use Checked builds for development
- Build only needed subsets
- Use binary logs (-bl) for troubleshooting
- Don't clean unless necessary

---

**Next:** See [07-Testing-Guide.md](07-Testing-Guide.md) for running and writing tests.
