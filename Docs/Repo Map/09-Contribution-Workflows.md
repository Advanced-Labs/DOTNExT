# Contribution Workflows

Step-by-step guides for common development tasks in the dotnet/runtime repository.

## Getting Started

### Initial Setup

```bash
# Clone the repository
git clone https://github.com/dotnet/runtime.git
cd runtime

# Check prerequisites
# - Windows: Visual Studio 2022 with C++ workload
# - Linux: clang/gcc, cmake, python
# - macOS: Xcode command line tools

# First build (this will take a while - 30-60 minutes)
./build.sh    # Linux/macOS
# or
build.cmd     # Windows
```

### Understanding Build Subsets

Build only what you need for faster iteration:

```bash
# CoreCLR runtime only
./build.sh -subset clr

# Libraries only
./build.sh -subset libs

# Both (common combination)
./build.sh -subset clr+libs

# Just the JIT
./build.sh -subset clr.jit

# Just tests
./build.sh -subset clr.tests

# Everything (full product)
./build.sh -subset clr+mono+libs+host+packs
```

### Build Configurations

```bash
# Debug - No optimization, asserts enabled, slow runtime
./build.sh -configuration Debug

# Checked - Optimized, asserts enabled (RECOMMENDED for development)
./build.sh -configuration Checked

# Release - Fully optimized, asserts disabled (production)
./build.sh -configuration Release
```

**Recommendation:** Use **Checked** builds for development - you get performance and diagnostics.

### Architecture and OS Selection

```bash
# Cross-platform build
./build.sh -subset clr -arch arm64 -os linux

# Common combinations
./build.sh -arch x64 -os windows
./build.sh -arch arm64 -os osx
./build.sh -arch wasm -os browser  # WebAssembly
```

## Workflow 1: Modifying the JIT Compiler

**Scenario:** You want to add an optimization or fix a JIT bug.

### Step 1: Build CoreCLR JIT Only

```bash
# First full build to get dependencies
./build.sh -subset clr+libs -configuration Checked

# For subsequent iterations, build just the JIT
./build.sh -subset clr.jit -configuration Checked
```

### Step 2: Make Your Changes

Edit files in `src/coreclr/jit/`:
- `morph.cpp` - IR transformations
- `optimizer.cpp` - Optimization passes
- `lower.cpp` - Lowering to LIR
- `codegenxarch.cpp` - x64 code generation
- etc.

### Step 3: Test Your Changes

```bash
# Build JIT tests
./build.sh -subset clr.jit.tests -configuration Checked

# Run specific test
cd src/tests/JIT/
./build.sh

# Run all JIT tests (time-consuming)
src/tests/run.sh JIT

# Or run a specific test
cd artifacts/tests/windows.x64.Checked/JIT/
./path/to/test.cmd

# Use CoreRun to run a specific app with your JIT
artifacts/bin/coreclr/windows.x64.Checked/corerun MyApp.dll
```

### Step 4: Debugging

```bash
# On Windows with Visual Studio:
# 1. Open runtime.sln
# 2. Set startup project to "corerun"
# 3. Set arguments to your test app
# 4. F5 to debug

# On Linux with GDB:
gdb --args artifacts/bin/coreclr/linux.x64.Checked/corerun MyApp.dll

# On Linux with LLDB:
lldb -- artifacts/bin/coreclr/linux.x64.Checked/corerun MyApp.dll
```

### Step 5: Advanced JIT Testing (SuperPMI)

SuperPMI lets you replay JIT compilations for regression testing:

```bash
# Collect traces
cd src/coreclr/scripts
python superpmi.py collect

# Replay with your JIT changes
python superpmi.py replay

# Compare before/after (diffs)
python superpmi.py asmdiffs
```

## Workflow 2: Modifying the Garbage Collector

**Scenario:** You want to modify GC behavior or fix a GC bug.

### Step 1: Build CoreCLR

```bash
./build.sh -subset clr -configuration Checked
```

### Step 2: Make Changes

Edit files in `src/coreclr/gc/`:
- `gc.cpp` - Main GC implementation
- `gcconfig.cpp` - Configuration
- `gcsvr.cpp` / `gcwks.cpp` - Server/Workstation modes

### Step 3: Test

```bash
# Build GC tests
./build.sh -subset clr.tests -configuration Checked

# Run GC-specific tests
cd src/tests/GC/
# Run test suite

# Environment variables for GC debugging
export DOTNET_GCStress=3           # Enable GC stress
export DOTNET_HeapVerify=1         # Verify heap consistency
export DOTNET_GCgen0size=10000     # Small gen0 for more frequent GC
```

### Step 4: GC Logging

```bash
# Enable GC logging
export DOTNET_LogEnable=1
export DOTNET_LogFacility=0x00001000  # GC facility
export DOTNET_LogLevel=6
export DOTNET_LogToFile=1

# Run app - creates GC log
./artifacts/bin/coreclr/linux.x64.Checked/corerun MyApp.dll

# Analyze with GCLogParser (optional)
dotnet run --project src/coreclr/tools/GCLogParser/ -- gclog.txt
```

## Workflow 3: Adding/Modifying a Library API

**Scenario:** You want to add a new method to System.IO.File or modify an existing library.

### Step 1: Understand the Library Structure

```
src/libraries/System.IO.FileSystem/
├── ref/                            # Reference assembly (API contract)
│   └── System.IO.FileSystem.csproj
├── src/                            # Implementation
│   └── System.IO.FileSystem.csproj
└── tests/                          # Unit tests
    └── System.IO.FileSystem.Tests.csproj
```

### Step 2: Modify the API Surface (if adding new API)

Edit `src/libraries/System.IO.FileSystem/ref/System.IO.File.cs`:

```csharp
namespace System.IO
{
    public static partial class File
    {
        // Add your new method signature
        public static void MyNewMethod(string path) { throw null; }
    }
}
```

### Step 3: Implement the API

Edit `src/libraries/System.IO.FileSystem/src/System/IO/File.cs`:

```csharp
public static void MyNewMethod(string path)
{
    // Your implementation
}
```

For platform-specific implementations:
- `File.Windows.cs` - Windows-specific
- `File.Unix.cs` - Unix-specific

### Step 4: Add Tests

Edit `src/libraries/System.IO.FileSystem/tests/File/MyNewMethod.cs`:

```csharp
public class File_MyNewMethod
{
    [Fact]
    public void TestBasicFunctionality()
    {
        // Your test
    }

    [Theory]
    [InlineData("path1")]
    [InlineData("path2")]
    public void TestVariousInputs(string path)
    {
        // Parameterized test
    }
}
```

### Step 5: Build and Test

```bash
# Build the library
./build.sh -subset libs -projects src/libraries/System.IO.FileSystem/src/System.IO.FileSystem.csproj

# Build and run tests
cd src/libraries/System.IO.FileSystem
dotnet build
dotnet test

# Or from root
./build.sh -subset libs -test
```

### Step 6: API Review Process

For new public APIs:
1. Mark with `[EditorBrowsable(EditorBrowsableState.Never)]` initially
2. Create an API proposal issue
3. Present at API review meeting
4. Get approval before removing EditorBrowsable

## Workflow 4: Adding Native P/Invoke Functions

**Scenario:** You need to call a native OS API from managed code.

### Step 1: Add Native Implementation

Create/edit in `src/libraries/Native/Unix/System.Native/`:

**pal_mynewfunction.c**:
```c
#include "pal_config.h"
#include "pal_mynewfunction.h"

int32_t SystemNative_MyNewFunction(const char* path)
{
    // Native implementation
    return 0;
}
```

**pal_mynewfunction.h**:
```c
#pragma once
#include "pal_types.h"

PALEXPORT int32_t SystemNative_MyNewFunction(const char* path);
```

Update `CMakeLists.txt` to include your new file.

### Step 2: Add P/Invoke Declaration

In `src/libraries/Common/src/Interop/Unix/System.Native/Interop.MyNewFunction.cs`:

```csharp
internal static partial class Interop
{
    internal static partial class Sys
    {
        [LibraryImport(Libraries.SystemNative, EntryPoint = "SystemNative_MyNewFunction", StringMarshalling = StringMarshalling.Utf8)]
        internal static partial int MyNewFunction(string path);
    }
}
```

### Step 3: Use in Managed Code

In your library implementation:

```csharp
public static void MyMethod(string path)
{
    int result = Interop.Sys.MyNewFunction(path);
    if (result != 0)
        throw new IOException();
}
```

### Step 4: Build

```bash
# Rebuild native and managed
./build.sh -subset libs -configuration Checked
```

## Workflow 5: Debugging the Runtime

### Debugging Managed Code

```bash
# Just use regular debugging
dotnet run
# or
dotnet test
```

### Debugging Native Runtime (CoreCLR)

**Option 1: Visual Studio (Windows)**

1. Open `runtime.sln`
2. Set `corerun` as startup project
3. Project Properties → Debugging → Command Arguments: `path\to\YourApp.dll`
4. Set breakpoints in C++ code
5. F5

**Option 2: VS Code**

1. Open repository in VS Code
2. Use launch configurations in `.vscode/launch.json`
3. Select "CoreCLR (Debug)" configuration
4. Set breakpoints
5. F5

**Option 3: GDB (Linux)**

```bash
gdb --args artifacts/bin/coreclr/linux.x64.Checked/corerun MyApp.dll

# In GDB:
(gdb) break MethodTable::DoFullyLoad
(gdb) run
(gdb) backtrace
(gdb) print *this
```

**Option 4: LLDB (macOS/Linux)**

```bash
lldb -- artifacts/bin/coreclr/osx.arm64.Checked/corerun MyApp.dll

# In LLDB:
(lldb) breakpoint set --name MethodTable::DoFullyLoad
(lldb) run
(lldb) bt
(lldb) frame variable
```

### Useful Environment Variables for Debugging

```bash
# JIT
export DOTNET_JitDisasm=MethodName        # Disassemble specific method
export DOTNET_JitDump=MethodName          # Dump JIT IR for method
export DOTNET_JitStress=1                 # Enable JIT stress mode
export DOTNET_TieredCompilation=0         # Disable tiering for consistent behavior

# GC
export DOTNET_GCStress=3                  # GC stress
export DOTNET_HeapVerify=1                # Verify heap

# Type Loading
export DOTNET_LogEnable=1
export DOTNET_LogFacility=0x00000010      # Type loader
export DOTNET_LogLevel=10

# Assembly Loading
export DOTNET_LogFacility=0x00000040      # Loader

# General
export DOTNET_EnableEventLog=1            # Enable event logging
```

## Workflow 6: Cross-Platform Development

### Building for Different Platforms

**On Windows, targeting Linux:**

```bash
# Build for Linux (requires Docker or WSL)
build.cmd -subset clr -os linux -arch x64
```

**On Linux, targeting ARM64:**

```bash
# Install cross-compilation tools
sudo apt-get install gcc-aarch64-linux-gnu g++-aarch64-linux-gnu

# Build
./build.sh -subset clr -arch arm64 -cross
```

**On macOS, targeting iOS:**

```bash
./build.sh -subset mono -os ios -arch arm64
```

### Testing on Different Platforms

Use CI/CD or actual hardware:
1. Push to your fork
2. GitHub Actions will build and test across platforms
3. Or use physical devices for iOS/Android

## Workflow 7: Performance Investigation

### Step 1: Reproduce the Issue

```bash
# Build in Release mode for accurate perf numbers
./build.sh -subset clr+libs -configuration Release

# Run your benchmark
dotnet run -c Release -- MyBenchmark
```

### Step 2: Collect Traces

**On Windows (PerfView):**

```cmd
# Download PerfView from GitHub
PerfView.exe collect
# Run your app
# Stop collection
PerfView.exe analyze file.etl
```

**On Linux (perf):**

```bash
# Record
perf record -g dotnet run

# View report
perf report

# Or use dotnet-trace
dotnet-trace collect -- dotnet run
```

### Step 3: JIT Disassembly

```bash
# See what code the JIT generated
export DOTNET_JitDisasm=MyHotMethod
dotnet run

# Or use dotnet-dump for live process
dotnet-dump collect -p <pid>
dotnet-dump analyze dump.dmp
> dumpil MyMethod
> dumpasm MyMethod
```

### Step 4: Profile-Guided Optimization

```bash
# Collect profile data
export DOTNET_TieredPGO=1
export DOTNET_TC_QuickJitForLoops=1
dotnet run

# PGO data is used automatically in tiered compilation
```

## Workflow 8: Running Tests

### Run All Tests

```bash
# Build and run all tests (very time-consuming!)
./build.sh -subset clr+libs -test
```

### Run Subset of Tests

```bash
# Just CoreCLR tests
./build.sh -subset clr.tests
src/tests/run.sh

# Just library tests
./build.sh -subset libs.tests
# Tests run automatically
```

### Run Specific Test

```bash
# Specific library test project
cd src/libraries/System.IO.FileSystem/tests
dotnet test

# Specific test method
dotnet test --filter "FullyQualifiedName~MyTestMethod"

# CoreCLR test
cd src/tests/JIT/opt/
./run.sh TestName
```

### Test Filtering

```bash
# By category
dotnet test --filter "Category=OuterLoop"

# By priority
dotnet test --filter "Priority=0"

# Multiple filters
dotnet test --filter "Category=CoreCLR&Priority=0"
```

## Workflow 9: Making a Pull Request

### Step 1: Create a Branch

```bash
git checkout -b myfeature
```

### Step 2: Make Changes and Test

```bash
# Edit files
# Build
./build.sh -subset clr+libs

# Test
./build.sh -subset clr.tests+libs.tests
```

### Step 3: Commit

```bash
git add .
git commit -m "Add XYZ feature

- Implement core functionality
- Add tests
- Update documentation

Fixes #12345"
```

### Step 4: Push and Create PR

```bash
git push origin myfeature

# Go to GitHub and create PR
# Fill out the PR template
# Wait for CI validation
```

### Step 5: Address Feedback

```bash
# Make changes based on review
git add .
git commit -m "Address PR feedback"
git push origin myfeature

# CI will re-run automatically
```

## Workflow 10: Build Troubleshooting

### Clean Build

```bash
# Clean everything
git clean -xdf

# Or selective clean
./build.sh -clean
```

### Incremental Build Issues

```bash
# If incremental build is broken, rebuild from scratch
rm -rf artifacts/
./build.sh -subset clr+libs
```

### CMake Cache Issues

```bash
# Remove CMake cache
rm -rf artifacts/obj/coreclr/
./build.sh -subset clr
```

### Common Errors

**Error: "SDK not found"**
```bash
# Check global.json for required SDK version
cat global.json
# Install matching SDK from https://dotnet.microsoft.com/download
```

**Error: "ROOTFS_DIR not set" (cross-compilation)**
```bash
# Set up rootfs for cross-compilation
./eng/common/cross/build-rootfs.sh arm64
```

## Pro Tips

### Faster Iteration

1. **Build only what changed:**
   ```bash
   ./build.sh -subset clr.jit  # Just JIT
   ```

2. **Use Checked builds** for development (optimized + asserts)

3. **Parallel builds:**
   ```bash
   ./build.sh -bl -m  # Binary log + max parallelism
   ```

### IDE Setup

**Visual Studio:**
- Open `runtime.sln`
- Set startup project based on what you're working on
- Use filters to show only relevant projects

**VS Code:**
- Install C# and C++ extensions
- Use provided launch configurations
- Use tasks for building specific subsets

**JetBrains Rider:**
- Open runtime.sln
- Configure debugger to use artifacts/bin/coreclr/.../corerun

### Documentation

Always check `docs/workflow/` for official guides:
- `docs/workflow/building/` - Detailed build instructions
- `docs/workflow/testing/` - Testing guides
- `docs/workflow/debugging/` - Debugging guides

---

These workflows cover the most common development scenarios. For advanced scenarios, consult the BOTR (Book of the Runtime) in `docs/design/coreclr/botr/`.
