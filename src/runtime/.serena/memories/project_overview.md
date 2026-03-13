# .NET Runtime Project Overview

## Purpose
The .NET Runtime repository contains the source code for:
- **.NET runtime** (CoreCLR and Mono implementations)
- **Base Class Libraries (BCL)** - System.* namespaces
- **Shared host** (dotnet executable) and installers

This is the foundational layer that all .NET applications run on.

## Tech Stack
- **Primary languages**: C# (libraries), C++ (native runtime components)
- **Build system**: MSBuild with Arcade SDK
- **SDK version**: 9.0.111
- **Target framework**: net9.0

## Repository Structure

```
src/
├── coreclr/     # CoreCLR runtime - JIT, GC, VM, type system
├── mono/        # Mono runtime - alternative runtime, WASM/mobile
├── libraries/   # BCL libraries (System.Collections, System.IO, etc.)
├── native/      # Native code and external dependencies
├── installer/   # Installation tooling
├── tests/       # Test suites
├── tools/       # Developer tools (illink, etc.)
├── tasks/       # MSBuild tasks
├── samples/     # Sample code
└── workloads/   # Workload definitions

eng/             # Build infrastructure (Arcade)
docs/            # Documentation
artifacts/       # Build outputs
```

## Key Components

### CoreCLR (`src/coreclr/`)
- JIT compiler (RyuJIT)
- Garbage Collector
- Virtual Machine (VM)
- Type system
- NativeAOT compiler

### Mono (`src/mono/`)
- Alternative runtime
- WebAssembly support
- Mobile platform support (iOS, Android)
- Interpreter

### Libraries (`src/libraries/`)
- System.Private.CoreLib - Core types (Object, String, etc.)
- System.Collections - Collection types
- System.IO - File/stream operations
- System.Net - Networking
- Microsoft.Extensions.* - Hosting, DI, Configuration, Logging
- And many more...
