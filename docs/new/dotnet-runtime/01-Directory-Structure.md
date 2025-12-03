# Directory Structure

Complete guide to the dotnet/runtime repository organization.

## Top-Level Directory Layout

```
dotnet-runtime/
├── src/                    # All source code
├── docs/                   # Documentation
├── eng/                    # Engineering/build infrastructure
├── .github/                # GitHub-specific configuration
├── .devcontainer/          # Dev container configurations
├── .config/                # Repository configuration
├── .azuredevops/          # Azure DevOps pipelines
├── Build.proj              # Root build orchestration
├── build.sh / build.cmd    # Build entry points
├── Directory.Build.props   # Global MSBuild properties
├── Directory.Build.targets # Global MSBuild targets
├── global.json             # .NET SDK version pinning
├── NuGet.config            # NuGet feed configuration
└── LICENSE.TXT             # MIT license
```

## Source Code (`src/`) - The Heart of the Repository

### Runtime Implementations

```
src/
├── coreclr/                # CoreCLR - Primary JIT-based runtime
│   ├── vm/                 # Virtual Machine (340K+ lines)
│   ├── jit/                # RyuJIT compiler (500K+ lines)
│   ├── gc/                 # Garbage collector
│   ├── md/                 # Metadata system
│   ├── pal/                # Platform Abstraction Layer
│   ├── debug/              # Debugging infrastructure
│   ├── interop/            # P/Invoke and COM interop
│   ├── nativeaot/          # NativeAOT compiler
│   ├── tools/              # Development tools
│   └── System.Private.CoreLib/  # Core managed library
│
├── mono/                   # Mono - Lightweight runtime
│   ├── mono/               # Mono runtime core
│   │   ├── mini/           # Mono JIT
│   │   ├── sgen/           # Garbage collector
│   │   ├── metadata/       # Type system
│   │   ├── arch/           # Architecture support
│   │   └── eventpipe/      # Diagnostics
│   ├── wasm/               # WebAssembly support
│   ├── browser/            # Browser interop
│   ├── wasi/               # WASI support
│   └── System.Private.CoreLib/  # Mono CoreLib
│
├── libraries/              # Class libraries (218+ packages)
│   ├── System.*/           # Core BCL types
│   ├── Microsoft.Extensions.*/  # Framework libraries
│   ├── shims/              # Compatibility shims
│   ├── pretest.targets     # Pre-test configuration
│   └── Directory.Build.props    # Library build config
│
├── native/                 # Native code
│   ├── corehost/           # Host executables
│   ├── libs/               # P/Invoke implementations
│   ├── eventpipe/          # EventPipe diagnostics
│   └── minipal/            # Minimal PAL
│
├── installer/              # Packaging & installers
│   ├── pkg/                # Package definitions
│   ├── managed/            # Managed installer components
│   └── tests/              # Installer tests
│
├── tests/                  # Test suites
│   ├── JIT/                # JIT compiler tests
│   ├── GC/                 # GC tests
│   ├── Loader/             # Assembly loading tests
│   ├── baseservices/       # Core runtime tests
│   └── ...                 # (15+ test categories)
│
└── tools/                  # Development tools
    ├── dotnet-pgo/         # PGO tooling
    ├── superpmi/           # JIT replay
    └── ...
```

## CoreCLR Detailed Structure (`src/coreclr/`)

### Virtual Machine (`src/coreclr/vm/`) - 340K+ Lines

The VM is the execution engine of .NET. It handles type loading, method invocation, exception handling, and coordinates with the JIT and GC.

```
src/coreclr/vm/
├── ceemain.cpp             # Runtime initialization
├── method.cpp / method.hpp # Method representation
├── methodtable.cpp         # Type representation (MethodTable)
├── class.cpp / class.h     # EEClass - class metadata
├── object.cpp / object.h   # Object layout
├── typehandle.cpp          # Type handles and generics
├── typedesc.cpp            # Type descriptors
├── assembly.cpp            # Assembly management
├── appdomain.cpp           # AppDomain (historical)
├── threads.cpp / threads.h # Thread management
├── excep.cpp               # Exception handling
├── interoputil.cpp         # Interop utilities
├── dllimport.cpp           # P/Invoke implementation
├── comcallablewrapper.cpp  # COM interop (CCW)
├── runtimecallablewrapper.cpp  # COM interop (RCW)
├── gcheaputilities.cpp     # GC interface
├── genmeth.cpp             # Generic methods
├── instmethhash.cpp        # Generic instantiation cache
├── virtualstubdispatch.cpp # Virtual method dispatch
├── prestub.cpp             # Method pre-stub generation
├── precode.cpp             # Precodes (method stubs)
├── codeman.cpp             # Code manager
├── eeconfig.cpp            # Configuration
├── classhash.cpp           # Type hash tables
├── clsload.cpp             # Class loading
├── ceeload.cpp             # Assembly loading
├── domainfile.cpp          # File loading
├── securitydescriptor.cpp  # Security (legacy CAS)
├── syncblk.cpp / syncblk.h # Synchronization blocks
├── spinlock.cpp            # Spin locks
├── crst.cpp                # Critical sections
├── dbginterface.cpp        # Debugger interface
├── eedbginterfaceimpl.cpp  # Debugger implementation
├── proftoeetointerfaceimpl.cpp  # Profiler callbacks
├── eventtrace.cpp          # ETW tracing
├── interpreter.cpp         # MSIL interpreter
├── jitinterface.cpp        # JIT interface
├── genericdict.cpp         # Generic dictionaries
├── siginfo.cpp             # Signature parsing
├── ilmarshalers.cpp        # Marshaling
├── stackwalk.cpp           # Stack walking
├── frames.cpp / frames.h   # Stack frames
├── stubmgr.cpp             # Stub manager
└── ... (many more)
```

**Key Subdirectories:**
- **amd64/** - x64-specific assembly stubs
- **arm/** - ARM32-specific code
- **arm64/** - ARM64-specific code
- **i386/** - x86 32-bit code
- **wasm/** - WebAssembly support

### JIT Compiler (`src/coreclr/jit/`) - 500K+ Lines

RyuJIT is the Just-In-Time compiler that converts IL to native machine code.

```
src/coreclr/jit/
├── compiler.cpp / compiler.h  # Main compiler data structure
├── flowgraph.cpp              # Control flow graph construction
├── block.cpp                  # Basic blocks
├── gentree.cpp / gentree.h    # GenTree - IR nodes
├── importer.cpp               # IL import to HIR
├── morph.cpp                  # IR morphing/transformation
├── rationalize.cpp            # HIR to LIR lowering prep
├── lower.cpp                  # HIR to LIR lowering
├── lsra.cpp                   # Linear Scan Register Allocation
├── codegencommon.cpp          # Code generation framework
├── codegenxarch.cpp           # x86/x64 codegen
├── codegenarm.cpp             # ARM32 codegen
├── codegenarm64.cpp           # ARM64 codegen
├── gcencode.cpp               # GC info encoding
├── unwind.cpp                 # Unwind info generation
├── optimizer.cpp              # Optimization framework
├── optcse.cpp                 # Common Subexpression Elimination
├── assertion.cpp              # Assertion propagation
├── rangecheck.cpp             # Range check elimination
├── loopcloning.cpp            # Loop optimization
├── valuenum.cpp               # Value numbering
├── ssabuilder.cpp             # SSA construction
├── copyprop.cpp               # Copy propagation
├── redundantbranchopts.cpp    # Branch optimization
├── inductionvariableopts.cpp  # IV optimization
├── inlining.cpp               # Method inlining
├── inline.cpp                 # Inline policy
├── inlinepolicy.cpp           # Inlining heuristics
├── earlyprop.cpp              # Early propagation
├── fgprofile.cpp              # Profile data handling
├── fgopt.cpp                  # Flow graph optimization
├── debuginfo.cpp              # Debug info generation
├── simd.cpp / simdhwinstininsic*.cpp  # SIMD/intrinsics
├── gschecks.cpp               # Security checks
├── emit.cpp / emit.h          # Instruction emission
├── instr.cpp                  # Instruction encoding
└── ... (many more)
```

**Subdirectories:**
- **hwi/** - Hardware intrinsics
- **CodeGenInterface/** - JIT-VM interface

### Garbage Collector (`src/coreclr/gc/`)

The generational, concurrent garbage collector.

```
src/coreclr/gc/
├── gc.cpp                  # Main GC implementation (~2M lines!)
├── gcpriv.h                # Private GC definitions
├── gcsvr.cpp               # Server GC
├── gcwks.cpp               # Workstation GC
├── gcconfig.cpp            # GC configuration
├── gchandletable.cpp       # Handle table implementation
├── gchandletableimpl.h     # Handle table details
├── gcload.cpp              # GC initialization
├── gccommon.cpp            # Common GC code
├── gceewks.cpp             # WKS GC EE interface
├── gceesvr.cpp             # SVR GC EE interface
├── handletable.cpp         # Object handle management
├── objecthandle.cpp        # Handle operations
├── softwarewritewatch.cpp  # Write watch support
├── vxsort/                 # Sorting for GC
└── env/                    # Environment abstraction
```

### Metadata (`src/coreclr/md/`)

Reads and manages IL metadata (type information, method signatures, etc.).

```
src/coreclr/md/
├── compiler/               # Metadata writing (compiler)
│   ├── mdutil.cpp          # Metadata utilities
│   ├── regmeta_emit.cpp    # Metadata emission
│   └── ...
├── runtime/                # Metadata reading (runtime)
│   ├── mdinternalro.cpp    # Read-only metadata
│   ├── metamodelro.cpp     # Metadata tables
│   └── ...
├── enc/                    # Edit and Continue
├── hotdata/                # Hot/cold data separation
├── databuffer.cpp          # Data buffer handling
└── ...
```

### Platform Abstraction Layer (`src/coreclr/pal/`)

Abstracts OS differences to support Windows and Unix.

```
src/coreclr/pal/
├── inc/                    # PAL headers
│   ├── pal.h               # Main PAL interface
│   └── ...
├── src/                    # PAL implementation
│   ├── thread/             # Thread abstraction
│   ├── sync/               # Synchronization primitives
│   ├── memory/             # Memory management
│   ├── file/               # File I/O
│   ├── exception/          # Exception handling
│   ├── loader/             # Module loading
│   ├── arch/               # Architecture-specific
│   └── ...
└── tests/                  # PAL tests
```

### Debugging (`src/coreclr/debug/`)

Debugging infrastructure including DAC (Data Access Component).

```
src/coreclr/debug/
├── daccess/                # Data Access Component
│   ├── daccess.cpp         # DAC implementation
│   └── ...
├── di/                     # Debug Interface (ICorDebug)
├── ee/                     # Execution Engine debug support
├── debug-pal/              # Debug PAL
├── createdump/             # Dump file creation
├── dbgutil/                # Debug utilities
└── inc/                    # Debug headers
```

### Tools (`src/coreclr/tools/`)

Development and diagnostic tools.

```
src/coreclr/tools/
├── aot/                    # AOT compilation tools
│   └── ILCompiler/         # IL compiler
├── dotnet-pgo/             # PGO tooling
├── superpmi/               # JIT replay infrastructure
│   ├── superpmi/           # Replay tool
│   ├── superpmi-shim-collector/  # Collection shim
│   └── mcs/                # MC (Method Context) store
├── r2rdump/                # ReadyToRun dumper
├── ilverify/               # IL verification
├── illink/                 # Trimming/linking
├── aotcatalogmanager/      # AOT catalog management
├── cdac-build-tool/        # Compact DAC builder
├── StressLogAnalyzer/      # Stress log analysis
└── ...
```

## Libraries Detailed Structure (`src/libraries/`)

### Organization Pattern

Each library follows a consistent structure:

```
src/libraries/System.Example/
├── src/                    # Source code
│   ├── System.Example.csproj
│   └── System/
│       └── Example/
│           └── *.cs        # Implementation
├── ref/                    # Reference assembly (API surface)
│   └── System.Example.csproj
├── tests/                  # Unit tests
│   ├── System.Example.Tests.csproj
│   └── ...
└── pkg/                    # NuGet package definition (if needed)
```

### Major Library Categories

**Core Types:**
```
System.Runtime/             # Object, String, Array, etc.
System.Runtime.InteropServices/  # P/Invoke, marshaling
System.Runtime.CompilerServices/  # Compiler services
System.Private.CoreLib/     # Special - built with runtime
```

**Collections:**
```
System.Collections/
System.Collections.Concurrent/
System.Collections.Immutable/
System.Collections.Specialized/
System.Collections.NonGeneric/
```

**I/O and Networking:**
```
System.IO.FileSystem/
System.IO.Compression/
System.IO.Pipes/
System.Net.Http/
System.Net.Sockets/
System.Net.Security/
System.Net.WebSockets/
```

**Data and Serialization:**
```
System.Text.Json/
System.Text.RegularExpressions/
System.Xml.*/
System.Data.*/
System.Linq/
System.Linq.Expressions/
```

**Threading and Async:**
```
System.Threading/
System.Threading.Tasks/
System.Threading.Channels/
System.Threading.RateLimiting/
```

**Diagnostics and Reflection:**
```
System.Diagnostics.*/
System.Reflection/
System.Reflection.Emit/
System.Reflection.Metadata/
```

**Security:**
```
System.Security.Cryptography/
System.Security.Cryptography.Algorithms/
System.Security.Cryptography.Cng/
System.Security.Cryptography.OpenSsl/
System.Security.Claims/
System.Security.Principal/
```

**Microsoft.Extensions Framework:**
```
Microsoft.Extensions.DependencyInjection/
Microsoft.Extensions.Configuration/
Microsoft.Extensions.Logging/
Microsoft.Extensions.Hosting/
Microsoft.Extensions.Options/
Microsoft.Extensions.Caching/
Microsoft.Extensions.Http/
Microsoft.Extensions.FileProviders/
```

### Native Library Implementations (`src/libraries/Native/`)

P/Invoke layers for platform-specific functionality:

```
src/libraries/Native/
├── Unix/
│   ├── System.Globalization.Native/    # ICU bindings
│   ├── System.IO.Compression.Native/   # zlib/brotli
│   ├── System.Native/                  # OS APIs
│   ├── System.Net.Security.Native/     # SSL/TLS
│   ├── System.Security.Cryptography.Native.*/  # Crypto
│   └── ...
└── Windows/
    └── ... (Windows-specific implementations)
```

## Native Hosting (`src/native/`)

### Core Host (`src/native/corehost/`)

The native executables that launch .NET applications:

```
src/native/corehost/
├── apphost/                # Application host (yourapp.exe)
├── dotnet/                 # dotnet CLI executable
├── fxr/                    # Framework resolver
├── hostfxr/                # Host FX Resolver library
├── hostpolicy/             # Host policy implementation
├── hostcommon/             # Shared host utilities
├── nethost/                # Minimal host for embedding
├── ijwhost/                # IJW (C++/CLI) hosting
└── comhost/                # COM activation host
```

## Tests (`src/tests/`)

### Test Organization

```
src/tests/
├── JIT/                    # JIT compiler tests
│   ├── Regression/         # Regression tests
│   ├── Directed/           # Directed scenarios
│   ├── opt/                # Optimization tests
│   ├── Performance/        # Perf tests
│   └── ...
├── GC/                     # Garbage collector tests
│   ├── Scenarios/
│   ├── Stress/
│   └── ...
├── Loader/                 # Assembly loading tests
├── Exceptions/             # Exception handling
├── Interop/                # P/Invoke and COM
├── Reflection/             # Reflection APIs
├── baseservices/           # Core runtime services
├── readytorun/             # R2R tests
├── profiler/               # Profiler API tests
├── tracing/                # EventPipe/diagnostics
├── async/                  # Async/await tests
├── nativeaot/              # NativeAOT tests
└── Common/                 # Shared test infrastructure
```

## Documentation (`docs/`)

```
docs/
├── design/
│   ├── coreclr/
│   │   ├── botr/           # Book of the Runtime
│   │   └── jit/            # JIT documentation
│   ├── mono/               # Mono design docs
│   └── features/           # Feature design documents
├── coding-guidelines/      # Code style and standards
├── workflow/               # Build, test, debug guides
│   ├── building/
│   ├── testing/
│   └── debugging/
├── project/                # Project management
└── area-owners.md          # Area ownership matrix
```

## Engineering Infrastructure (`eng/`)

```
eng/
├── common/                 # Shared engineering
│   ├── native/             # CMake infrastructure
│   └── tools/              # Build tools
├── native/                 # Native build configuration
│   └── *.cmake             # CMake files
├── pipelines/              # CI/CD pipelines
├── Subsets.props           # Build subset definitions
├── Version.Details.props   # Dependency versions
├── Versions.props          # Component versions
├── signing/                # Code signing
├── CodeAnalysis.*.globalconfig  # Analyzer rules
└── *.props / *.targets     # MSBuild configuration
```

## Configuration Files

**Root-level configuration:**
```
Directory.Build.props       # Global MSBuild properties
Directory.Build.targets     # Global MSBuild targets
global.json                 # SDK version
NuGet.config                # NuGet sources
.editorconfig               # Editor settings
.gitignore / .gitattributes # Git configuration
```

## Build Outputs (`artifacts/`)

Generated during build (not in source control):

```
artifacts/
├── bin/                    # Build outputs
│   ├── coreclr/            # CoreCLR binaries
│   ├── mono/               # Mono binaries
│   ├── libraries/          # Library assemblies
│   └── tests/              # Test binaries
├── obj/                    # Intermediate objects
├── packages/               # NuGet packages
├── logs/                   # Build logs
└── ...
```

## Summary

The repository is organized with clear separation of concerns:
- **src/** contains all source code, organized by component
- **docs/** contains comprehensive documentation
- **eng/** contains build infrastructure
- **artifacts/** contains build outputs (generated)

Each major component (CoreCLR, Mono, Libraries, Native) has its own subtree with consistent organization patterns, making it easier to navigate once you understand the structure.

---

**Next:** See [02-CoreCLR-Guide.md](02-CoreCLR-Guide.md) for deep dive into CoreCLR internals.
