# Mono Runtime Guide

Guide to the Mono runtime - .NET's lightweight, cross-platform runtime for mobile and WebAssembly.

## Overview

**Mono** is an alternative .NET runtime optimized for:
- **WebAssembly** - Browser and WASI environments
- **Mobile** - iOS and Android
- **Embedded** - Resource-constrained devices
- **Ahead-of-Time (AOT) compilation** - Platforms that don't allow JIT

**Location:** `src/mono/`

**Key Differences from CoreCLR:**
- Lighter weight, smaller footprint
- Better AOT support
- Interpreter mode available (no JIT required)
- WebAssembly first-class citizen
- Mobile platform expertise

## Architecture

```
Application (IL)
       ↓
┌──────────────────────────┐
│   Mono Runtime (mini)    │
│  - Type system           │
│  - Execution engine      │
│  - Interop               │
└──────────────────────────┘
       ↓
┌──────┬─────────┬─────────┐
│ JIT  │ Interp  │  AOT    │
└──────┴─────────┴─────────┘
       ↓
┌──────────────────────────┐
│   SGen GC (mono)         │
└──────────────────────────┘
       ↓
┌──────────────────────────┐
│   Platform (iOS/WASM)    │
└──────────────────────────┘
```

## Directory Structure

```
src/mono/
├── mono/                    # Mono runtime core
│   ├── mini/                # JIT compiler and execution engine
│   ├── metadata/            # Type system and metadata
│   ├── sgen/                # SGen garbage collector
│   ├── utils/               # Utilities
│   ├── eventpipe/           # EventPipe diagnostics
│   ├── arch/                # Architecture-specific code
│   └── eglib/               # Embedded GLib (utility library)
│
├── wasm/                    # WebAssembly support
│   ├── runtime/             # WASM runtime infrastructure
│   ├── build/               # Build configuration
│   └── debugger/            # WASM debugging support
│
├── browser/                 # Browser-specific code
│   └── runtime/             # Browser runtime integration
│
├── wasi/                    # WebAssembly System Interface
│   └── runtime/             # WASI runtime
│
├── System.Private.CoreLib/  # Mono's CoreLib implementation
│
├── sample/                  # Sample apps (WASM, mobile)
├── tools/                   # Mono tools
└── tests/                   # Mono tests
```

## Mono Runtime Core (`src/mono/mono/`)

### Mini - JIT and Execution Engine (`mono/mini/`)

**Purpose:** JIT compiler, interpreter, and method execution.

**Key Files:**
- **mini.c** - Main entry points, execution engine
- **method-to-ir.c** - Convert IL to Mono IR
- **decompose.c** - IR decomposition
- **linear-scan.c** - Register allocation
- **mini-{arch}.c** - Architecture-specific codegen (e.g., mini-arm64.c)
- **interp/interp.c** - Interpreter implementation
- **aot-compiler.c** - AOT compiler
- **aot-runtime.c** - AOT runtime support

**Compilation Modes:**
1. **JIT** - Just-in-time compilation (default on desktop)
2. **Interpreter** - Bytecode interpretation (iOS, platforms without JIT)
3. **AOT** - Ahead-of-time compilation (iOS, WASM)
4. **Hybrid** - AOT + Interpreter (common on iOS)

### Metadata System (`mono/metadata/`)

**Purpose:** Type system, assembly loading, reflection.

**Key Files:**
- **metadata.c** - Metadata reading
- **class.c** - Type loading and representation
- **loader.c** - Assembly and image loading
- **object.c** - Object model
- **icall.c** - Internal calls (VM-to-managed)
- **marshal.c** - P/Invoke marshaling
- **threads.c** - Threading support
- **domain.c** - AppDomain support
- **reflection.c** - Reflection implementation
- **gc.c** - GC interface

**Type Representation:**
- **MonoClass** - Runtime type representation
- **MonoMethod** - Method representation
- **MonoImage** - Loaded assembly
- **MonoDomain** - Application domain

### SGen Garbage Collector (`mono/sgen/`)

**Purpose:** Generational, concurrent garbage collector for Mono.

**Key Files:**
- **sgen-gc.c** - Main GC implementation
- **sgen-marksweep.c** - Mark-and-sweep algorithm
- **sgen-nursery-allocator.c** - Young generation allocation
- **sgen-los.c** - Large object space
- **sgen-cardtable.c** - Card table for write barriers
- **sgen-thread-pool.c** - GC thread pool

**Features:**
- Generational (nursery + old generation)
- Concurrent collection
- Multiple collectors (mark-sweep, copying)
- Configurable

**Configuration:**
```bash
# GC mode
export MONO_GC_PARAMS=nursery-size=4m,major=marksweep

# GC debugging
export MONO_GC_DEBUG=heap-dump
```

## WebAssembly Support (`src/mono/wasm/`)

### WASM Runtime

**Purpose:** Run .NET applications in web browsers or WASI environments.

**Architecture:**
```
Browser (JavaScript)
       ↓
   dotnet.js (JavaScript runtime)
       ↓
   dotnet.wasm (Mono compiled to WASM)
       ↓
   Application assemblies (.dll)
```

**Key Components:**
- **runtime/driver.c** - WASM entry point
- **runtime/library_mono.js** - JavaScript/WASM interop
- **build/** - Build scripts for WASM

**Modes:**
1. **AOT** - Ahead-of-time compilation (faster execution)
2. **Interpreter** - IL interpretation (faster startup, smaller size)
3. **Hybrid** - AOT for hot code, interpreter for rest

### Browser Integration (`src/mono/browser/`)

**JavaScript Interop:**
```csharp
// Managed code
using System.Runtime.InteropServices.JavaScript;

[JSExport]
public static string MyMethod(string input)
{
    return "Hello " + input;
}

[JSImport("alert", "globalThis")]
public static partial void Alert(string message);
```

**Calling from JavaScript:**
```javascript
const { MyMethod } = await dotnet.runtime.getAssemblyExports("MyAssembly.dll");
const result = MyMethod("World");
```

**Location:** `src/libraries/System.Runtime.InteropServices.JavaScript/`

### WASI Support (`src/mono/wasi/`)

**WASI (WebAssembly System Interface):** Run .NET in non-browser WASM environments.

**Use cases:**
- Server-side WASM
- Containerized apps
- Edge computing

## Mobile Support

### iOS (`src/mono/`)

**Build for iOS:**
```bash
./build.sh -subset mono -os ios -arch arm64
```

**Execution Mode:**
- **AOT + Interpreter** (iOS doesn't allow JIT)
- All methods either AOT-compiled or interpreted
- No dynamic code generation

**Native Interop:**
```csharp
// Objective-C interop
[DllImport("__Internal")]
static extern void NSLog(string format, string message);
```

### Android (`src/mono/`)

**Build for Android:**
```bash
./build.sh -subset mono -os android -arch arm64
```

**Execution Mode:**
- **JIT** (default) or **AOT**
- Java interop via JNI
- Android platform APIs

**Java Interop:**
```csharp
// Call Java from C#
using Android.App;

Activity activity = ...;
activity.RunOnUiThread(() => {
    // UI code
});
```

## Execution Modes

### JIT Mode

**When:** Desktop, Android (default)

**How it works:**
1. Load IL assembly
2. On first method call, JIT compile to native code
3. Execute native code
4. Cache for future calls

**Configuration:**
```bash
# Standard JIT
mono MyApp.exe
```

### Interpreter Mode

**When:** iOS (no JIT allowed), debugging

**How it works:**
1. Load IL assembly
2. Interpret IL bytecode directly
3. No native code generation

**Configuration:**
```bash
# Force interpreter
mono --interpreter MyApp.exe
```

**Pros:**
- No JIT overhead
- Smaller memory footprint
- Works on platforms without JIT

**Cons:**
- Slower execution
- No optimizations

### AOT Mode

**When:** iOS, WebAssembly (for performance), embedded

**How it works:**
1. Compile all IL to native code ahead of time
2. Ship native code with app
3. No runtime compilation needed

**Compilation:**
```bash
# AOT compile
mono --aot MyApp.dll

# Generates: MyApp.dll.so (or .dylib on macOS)
```

**Configuration:**
```bash
# Use AOT at runtime
mono --full-aot MyApp.exe
```

**Pros:**
- Fast execution (no JIT overhead)
- Predictable performance
- Works on no-JIT platforms

**Cons:**
- Larger binary size
- Longer build time
- No runtime optimization

### Hybrid Mode (AOT + Interpreter)

**When:** iOS, WebAssembly

**How it works:**
1. AOT compile hot/critical methods
2. Interpret rarely-used methods
3. Balance size vs. performance

**Configuration:**
```bash
# Hybrid mode
mono --hybrid-aot MyApp.exe
```

## Building Mono

### Build Mono Runtime

```bash
# Full Mono + Libraries
./build.sh -subset mono+libs

# Mono runtime only
./build.sh -subset mono.runtime

# Mono tests
./build.sh -subset mono.tests
```

### Build for WebAssembly

```bash
# WASM runtime
./build.sh -subset mono.wasmruntime -os browser -arch wasm

# Sample WASM app
cd src/mono/sample/wasm
make build
make run
```

### Build for Mobile

```bash
# iOS
./build.sh -subset mono -os ios -arch arm64

# Android
./build.sh -subset mono -os android -arch arm64
```

## Testing Mono

### Run Mono Tests

```bash
# Build tests
./build.sh -subset mono.tests

# Run tests
cd src/mono/tests
make run-tests
```

### WASM Tests

```bash
# Build and run WASM tests
cd src/mono/wasm
make test
```

### Mobile Tests

**iOS:** Requires macOS and Xcode
```bash
# Build test app
./build.sh -subset mono.tests -os ios

# Deploy to device/simulator (manual)
```

**Android:** Requires Android SDK
```bash
# Build test app
./build.sh -subset mono.tests -os android

# Deploy to emulator/device (manual)
```

## Mono Configuration

### Environment Variables

```bash
# Execution mode
export MONO_ENV_OPTIONS=--interpreter    # Force interpreter
export MONO_ENV_OPTIONS=--aot            # Use AOT

# GC configuration
export MONO_GC_PARAMS=nursery-size=4m
export MONO_GC_DEBUG=heap-dump

# Logging
export MONO_LOG_LEVEL=debug
export MONO_LOG_MASK=asm,type

# WASM specific
export MONO_WASM_DEBUGGING=1
```

### Runtime Options

```bash
# Verbose logging
mono --verbose MyApp.exe

# Method timing
mono --profile=log:calls,nocalls MyApp.exe

# GC stress
mono --gc-debug=3 MyApp.exe
```

## Key Differences: Mono vs. CoreCLR

| Feature | Mono | CoreCLR |
|---------|------|---------|
| **Primary Use** | Mobile, WASM, embedded | Server, desktop |
| **Size** | Smaller (~3-5 MB) | Larger (~100+ MB) |
| **Startup** | Faster (especially AOT) | Slower (JIT) |
| **Steady-state Perf** | Good | Excellent (better JIT) |
| **JIT** | Simpler, smaller | Advanced (RyuJIT) |
| **AOT** | Excellent support | NativeAOT (separate) |
| **Interpreter** | Built-in | Not available |
| **Platforms** | WASM, iOS, Android | Windows, Linux, macOS |
| **GC** | SGen (simpler) | More sophisticated |

## Common Workflows

### Develop WASM App

1. **Create project:**
   ```bash
   dotnet new wasmbrowser -o MyWasmApp
   cd MyWasmApp
   ```

2. **Build:**
   ```bash
   dotnet build
   ```

3. **Run:**
   ```bash
   dotnet run
   # Opens browser at http://localhost:5000
   ```

4. **Publish (AOT):**
   ```bash
   dotnet publish -c Release
   # Creates optimized WASM bundle
   ```

### Develop iOS App (with .NET MAUI)

1. **Install workload:**
   ```bash
   dotnet workload install ios
   ```

2. **Create project:**
   ```bash
   dotnet new ios -o MyiOSApp
   ```

3. **Build:**
   ```bash
   dotnet build -f net8.0-ios
   ```

4. **Run (requires macOS):**
   ```bash
   dotnet run
   ```

## Debugging Mono

### WASM Debugging

**Browser DevTools:**
- Chrome/Edge: Built-in debugger
- Set breakpoints in C# code
- Inspect variables
- Call stack

**Enable debugging:**
```xml
<PropertyGroup>
  <WasmDebugLevel>1</WasmDebugLevel>
</PropertyGroup>
```

### Native Debugging

**GDB (Linux):**
```bash
gdb --args mono MyApp.exe
(gdb) break mono_method_to_ir
(gdb) run
```

**LLDB (macOS):**
```bash
lldb -- mono MyApp.exe
(lldb) breakpoint set --name mono_method_to_ir
(lldb) run
```

## Documentation

**Mono-specific docs:**
- `docs/design/mono/` - Mono design documents
- Mono project website: https://www.mono-project.com/

**WASM:**
- `docs/workflow/building/mono/wasm.md` - WASM build instructions

## Summary

Mono is optimized for:
- **Lightweight** - Smaller footprint than CoreCLR
- **Flexible execution** - JIT, Interpreter, AOT, or Hybrid
- **Cross-platform** - WASM, iOS, Android, embedded
- **AOT-first** - Excellent AOT compilation support

**Use Mono when:**
- Targeting WebAssembly
- Building mobile apps (iOS/Android)
- Need smaller runtime
- AOT is required
- Embedded systems

**Use CoreCLR when:**
- Server applications
- Desktop applications (Windows/Linux/macOS)
- Need maximum performance
- Advanced JIT optimizations needed

---

**Next:** See [08-Feature-Location-Reference.md](08-Feature-Location-Reference.md) for quick lookups of specific features.
