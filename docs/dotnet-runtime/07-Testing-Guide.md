# Testing Guide

Complete guide to testing in the .NET Runtime repository.

## Overview

The runtime has comprehensive test coverage with 15+ test suites:
- **CoreCLR tests** - Runtime and JIT tests (`src/tests/`)
- **Library tests** - Unit tests for each library (`src/libraries/*/tests/`)
- **Mono tests** - Mono-specific tests (`src/mono/tests/`)
- **Performance tests** - Benchmarks and performance validation

## Test Organization

```
src/
├── tests/                   # CoreCLR tests
│   ├── JIT/                 # JIT compiler tests
│   ├── GC/                  # Garbage collector tests
│   ├── Loader/              # Assembly loading tests
│   ├── Exceptions/          # Exception handling tests
│   ├── Interop/             # P/Invoke and COM tests
│   ├── baseservices/        # Core runtime tests
│   ├── readytorun/          # R2R tests
│   ├── profiler/            # Profiler API tests
│   └── ...
│
├── libraries/
│   └── */tests/             # Library-specific unit tests
│       └── *.Tests.csproj
│
└── mono/
    └── tests/               # Mono tests
```

## Building Tests

### Build CoreCLR Tests

```bash
# Build runtime first
./build.sh -subset clr+libs

# Build tests
./build.sh -subset clr.tests

# Outputs to: artifacts/tests/coreclr/
```

### Build Library Tests

```bash
# Build libraries with tests
./build.sh -subset libs -test

# Or build single library tests
cd src/libraries/System.IO.FileSystem/tests
dotnet build
```

### Build Mono Tests

```bash
./build.sh -subset mono.tests
```

## Running Tests

### Run CoreCLR Tests

**All tests (slow!):**
```bash
./build.sh -subset clr -test
```

**Specific test suite:**
```bash
cd src/tests
./build.sh JIT           # All JIT tests
./build.sh GC            # All GC tests
./build.sh Loader        # All loader tests
```

**Single test:**
```bash
cd artifacts/tests/coreclr/windows.x64.Checked/JIT/Regression/
./run.sh TestName
```

**Use CoreRun (test host):**
```bash
cd artifacts/bin/coreclr/windows.x64.Checked
./corerun.exe MyTest.dll
```

### Run Library Tests

**All library tests:**
```bash
./build.sh -subset libs -test
```

**Single library:**
```bash
cd src/libraries/System.IO.FileSystem/tests
dotnet test

# Or from root
dotnet test src/libraries/System.IO.FileSystem/tests/System.IO.FileSystem.Tests.csproj
```

**With filters:**
```bash
# Specific test method
dotnet test --filter "FullyQualifiedName~MyTestMethod"

# Specific class
dotnet test --filter "FullyQualifiedName~MyTestClass"

# By category
dotnet test --filter "Category=OuterLoop"

# By priority
dotnet test --filter "Priority=0"
```

### Run Mono Tests

```bash
cd src/mono/tests
make run-tests
```

## Test Categories

### CoreCLR Test Suites

**JIT Tests (`src/tests/JIT/`):**
- Optimization tests
- Regression tests (historical bugs)
- Directed tests (specific features)
- IL tests (unusual IL patterns)
- Performance tests

**GC Tests (`src/tests/GC/`):**
- Allocation patterns
- Finalization
- Weak references
- Large objects
- Concurrent collection
- Stress scenarios

**Loader Tests (`src/tests/Loader/`):**
- Assembly loading
- Type loading
- Generic instantiation
- Assembly binding
- Version resolution

**Interop Tests (`src/tests/Interop/`):**
- P/Invoke scenarios
- Marshaling
- COM interop (Windows)
- Reverse P/Invoke
- Struct marshaling

**Exception Tests (`src/tests/Exceptions/`):**
- Try/catch/finally
- Exception filters
- Stack overflow
- Cross-language exceptions

**ReadyToRun Tests (`src/tests/readytorun/`):**
- R2R compilation
- Version bubbles
- Cross-assembly inlining

**Profiler Tests (`src/tests/profiler/`):**
- Profiler API scenarios
- IL rewriting
- Enter/leave hooks

**Tracing Tests (`src/tests/tracing/`):**
- EventPipe scenarios
- ETW (Windows)
- Custom events

### Library Test Patterns

**xUnit Framework:**
```csharp
using Xunit;

public class MyTests
{
    [Fact]
    public void TestBasicFunctionality()
    {
        Assert.Equal(4, 2 + 2);
    }

    [Theory]
    [InlineData(1, 2, 3)]
    [InlineData(2, 3, 5)]
    public void TestAddition(int a, int b, int expected)
    {
        Assert.Equal(expected, a + b);
    }

    [ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsWindows))]
    public void WindowsOnlyTest()
    {
        // Only runs on Windows
    }
}
```

**Test Categories:**
```csharp
[Fact]
[OuterLoop]  // Slower, runs in outerloop CI
public void SlowTest() { }

[Fact]
[SkipOnPlatform(TestPlatforms.Browser, "Not supported in browser")]
public void NotForBrowser() { }

[Fact]
[ActiveIssue("https://github.com/dotnet/runtime/issues/12345")]
public void KnownIssue() { }
```

## Test Infrastructure

### CoreRun

**Purpose:** Lightweight test host for CoreCLR.

**Location:** `artifacts/bin/coreclr/{os}.{arch}.{config}/corerun`

**Usage:**
```bash
./corerun MyTest.dll
./corerun --help
```

**Environment variables:**
```bash
export DOTNET_GCStress=3         # GC stress
export DOTNET_JitStress=1        # JIT stress
./corerun MyTest.dll
```

### Test Harness

**Location:** `src/tests/Common/`

**CoreCLR test harness:**
- Discovers tests
- Executes tests
- Collects results
- Generates reports

**Library test harness:**
- xUnit runner
- Built into `dotnet test`

## Stress Testing

### GC Stress

**Purpose:** Find GC-related bugs by running GC frequently.

```bash
export DOTNET_GCStress=3      # GC before every allocation (very slow!)
export DOTNET_GCStress=4      # GC on transitions to preemptive mode
export DOTNET_GCStress=C      # GC on every tenth allocation

# Run test with GC stress
./corerun MyTest.dll
```

**Stress levels:**
- 0x3 (3) - GC on every allocation (extremely slow)
- 0x4 (4) - GC on GC mode transitions
- 0xC (12) - Mix of strategies

**Additional GC debugging:**
```bash
export DOTNET_HeapVerify=1    # Verify heap consistency
export DOTNET_GCgen0size=1000 # Tiny Gen0 for more frequent GC
```

### JIT Stress

**Purpose:** Test JIT with unusual configurations.

```bash
export DOTNET_JitStress=1     # Randomize JIT decisions
export DOTNET_JitStress=2     # Different stress mode
./corerun MyTest.dll
```

**JIT stress modes:**
- 1 - Random inlining decisions
- 2 - Force tail calls
- DOTNET_JitStressRegs=1 - Register stress

**JIT debugging:**
```bash
export DOTNET_JitDisasm=MethodName       # Disassemble method
export DOTNET_JitDump=MethodName         # Dump IR for method
export DOTNET_JitDiffableDasm=1          # Reproducible disasm
```

## Performance Testing

### Benchmarks

**Location:** Separate repository (dotnet/performance)

**But runtime has some perf tests:**
- `src/tests/JIT/Performance/` - JIT performance tests
- Library test projects with `[Benchmark]` attribute

### Microbenchmarks

**Using BenchmarkDotNet:**
```csharp
using BenchmarkDotNet.Attributes;

public class MyBenchmarks
{
    [Benchmark]
    public int CountStrings()
    {
        return "hello".Length;
    }
}
```

**Run:**
```bash
dotnet run -c Release -- --filter "*MyBenchmarks*"
```

### Profiling Tests

```bash
# Collect CPU profile (Linux)
perf record -g dotnet test
perf report

# Collect trace
dotnet-trace collect --process-id <pid>

# Analyze
dotnet-trace convert trace.nettrace --format speedscope
```

## Platform-Specific Testing

### Windows-Specific Tests

```csharp
[ConditionalFact(typeof(PlatformDetection), nameof(PlatformDetection.IsWindows))]
public void WindowsOnlyTest()
{
    // COM interop, Windows-specific APIs
}
```

### Unix-Specific Tests

```csharp
[ConditionalFact(nameof(PlatformDetection.IsUnix))]
public void UnixOnlyTest()
{
    // Unix-specific file permissions, signals, etc.
}
```

### WebAssembly Tests

```bash
# Build WASM tests
./build.sh -subset mono.wasmruntime -os browser

# Run WASM tests (requires browser)
cd src/mono/wasm
make test
```

## Debugging Test Failures

### Reproduce Locally

```bash
# Build same configuration as CI
./build.sh -subset clr+libs -configuration Checked

# Run specific test
cd src/libraries/System.IO.FileSystem/tests
dotnet test --filter "FullyQualifiedName~FailingTest"
```

### Attach Debugger

**Visual Studio:**
1. Set breakpoint in test code
2. Right-click test → Debug
3. Or attach to `dotnet.exe` or `corerun.exe`

**VS Code:**
1. Set breakpoint
2. Use launch configuration
3. F5

**Command line:**
```bash
# Linux
gdb --args dotnet test
(gdb) run
(gdb) bt  # backtrace on crash

# macOS
lldb -- dotnet test
(lldb) run
(lldb) bt
```

### Collect Crash Dumps

**Windows:**
```cmd
# Enable dumps
set DOTNET_DbgEnableMiniDump=1
set DOTNET_DbgMiniDumpName=C:\dumps\crash.dmp

# Run test
dotnet test
```

**Linux/macOS:**
```bash
# Enable dumps
export DOTNET_DbgEnableMiniDump=1
export DOTNET_DbgMiniDumpName=/tmp/crash.dmp

# Or use core dumps
ulimit -c unlimited
dotnet test

# Analyze dump
lldb -c core.12345
```

### Verbose Test Logging

**Library tests:**
```bash
dotnet test -v:detailed
```

**CoreCLR tests:**
```bash
export DOTNET_LogEnable=1
export DOTNET_LogLevel=10
./corerun MyTest.dll
```

## Test Best Practices

### Writing Good Tests

**Do:**
- Test one thing per test
- Use descriptive names
- Clean up resources (use `using`, `IDisposable`)
- Make tests deterministic (no random, no timing dependencies)
- Test edge cases and error conditions

**Don't:**
- Depend on test execution order
- Use hard-coded paths (use `GetTestFilePath()`)
- Make tests flaky (timing-dependent)
- Test internal implementation details

### Test Naming

**Pattern:** `MethodName_Scenario_ExpectedBehavior`

```csharp
public void ReadAllText_FileExists_ReturnsContent() { }
public void ReadAllText_FileNotFound_ThrowsException() { }
public void ReadAllText_EmptyFile_ReturnsEmptyString() { }
```

### Test Helpers

**Common helpers:**
```csharp
// Temporary file management
string path = GetTestFilePath();

// Skip tests conditionally
if (!PlatformDetection.IsWindows)
    return;

// Remote executor (for process isolation)
RemoteExecutor.Invoke(() => {
    // Code runs in separate process
});
```

## Continuous Integration

### PR Validation

When you create a PR:
1. **Smoke tests** run first (~30 minutes)
   - Critical scenarios
   - Multiple platforms

2. **Full CI** runs in parallel
   - All test suites
   - All platforms
   - ~6-12 hours

### Outerloop Tests

**Purpose:** Slower, more comprehensive tests.

**Marked with:**
```csharp
[Fact]
[OuterLoop]
public void ExpensiveTest() { }
```

**Run in CI:**
- Daily builds
- Before releases
- Not on every PR

**Run locally:**
```bash
dotnet test --filter "Category=OuterLoop"
```

### Helix

**Helix:** Microsoft's distributed test execution system.

**What it does:**
- Distributes tests across machines
- Runs tests on multiple platforms
- Collects results

**CI uses Helix automatically.**

## Common Test Failures

### Flaky Tests

**Symptoms:** Test passes sometimes, fails sometimes.

**Common causes:**
- Timing dependencies
- Race conditions
- External dependencies (network, disk)
- Undisposed resources

**Solutions:**
- Add retries for flaky infrastructure
- Use deterministic test data
- Isolate tests (RemoteExecutor)
- Fix race conditions

### Platform-Specific Failures

**Symptoms:** Test passes on one platform, fails on others.

**Common causes:**
- Platform-specific behavior (file paths, line endings)
- Missing platform support
- Platform-specific bugs

**Solutions:**
- Skip test on unsupported platforms
- Use platform detection
- Fix platform-specific code

### Performance Regressions

**Detection:**
- Performance benchmarks in CI
- Compare against baseline

**Investigation:**
```bash
# Profile before and after change
dotnet-trace collect --process-id <pid>

# Compare traces
```

## Quick Reference

### Build and Test Commands

```bash
# CoreCLR development
./build.sh -subset clr+libs              # Build runtime and libraries
./build.sh -subset clr.tests             # Build tests
./build.sh -subset clr -test             # Build and run tests

# Library development
cd src/libraries/System.IO.FileSystem
dotnet build                             # Build library
dotnet test                              # Run tests
dotnet test --filter "FullyQualifiedName~MyTest"  # Specific test

# Stress testing
export DOTNET_GCStress=3
export DOTNET_JitStress=1
./corerun MyTest.dll

# Performance
dotnet run -c Release -- --filter "*MyBenchmark*"
```

### Environment Variables

```bash
# JIT
export DOTNET_JitDisasm=MethodName       # Disassemble
export DOTNET_JitDump=MethodName         # Dump IR
export DOTNET_JitStress=1                # Stress mode

# GC
export DOTNET_GCStress=3                 # GC stress
export DOTNET_HeapVerify=1               # Verify heap

# Logging
export DOTNET_LogEnable=1
export DOTNET_LogLevel=10

# Dumps
export DOTNET_DbgEnableMiniDump=1
export DOTNET_DbgMiniDumpName=/tmp/crash.dmp
```

## Summary

**Test organization:**
- CoreCLR tests: `src/tests/` (JIT, GC, Loader, etc.)
- Library tests: `src/libraries/*/tests/`
- Mono tests: `src/mono/tests/`

**Running tests:**
- CoreCLR: `./build.sh -subset clr -test`
- Libraries: `dotnet test`
- Filters: `--filter "FullyQualifiedName~MyTest"`

**Stress testing:**
- GC stress: `DOTNET_GCStress=3`
- JIT stress: `DOTNET_JitStress=1`

**Debugging:**
- Attach debugger
- Collect dumps
- Verbose logging

**Best practices:**
- One thing per test
- Descriptive names
- Clean up resources
- Make tests deterministic

---

**Next:** See [09-Contribution-Workflows.md](09-Contribution-Workflows.md) for step-by-step development workflows.
