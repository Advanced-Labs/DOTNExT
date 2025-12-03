# Feature Location Reference

Quick lookup guide: **"I need to modify X, where do I go?"**

This document maps technologies, features, and concepts to their exact locations in the codebase.

## Quick Lookup Table

| What You're Looking For | Primary Location | Key Files |
|------------------------|------------------|-----------|
| **JIT Compiler** | src/coreclr/jit/ | compiler.cpp, importer.cpp, morph.cpp, lower.cpp |
| **Garbage Collector** | src/coreclr/gc/ | gc.cpp, gcpriv.h |
| **Type System** | src/coreclr/vm/ | methodtable.cpp, class.cpp, typehandle.cpp |
| **Assembly Loading** | src/coreclr/vm/ | ceeload.cpp, assembly.cpp, binder/ |
| **Exception Handling** | src/coreclr/vm/excep.cpp | excep.cpp, exceptionhandling.cpp |
| **P/Invoke** | src/coreclr/vm/ | dllimport.cpp, interoputil.cpp |
| **COM Interop** | src/coreclr/interop/ | comcallablewrapper.cpp, runtimecallablewrapper.cpp |
| **Profiler API** | src/coreclr/vm/ | proftoeetointerfaceimpl.cpp |
| **Diagnostics/EventPipe** | src/native/eventpipe/ | ep-*.cpp |
| **Metadata Reader** | src/coreclr/md/runtime/ | mdinternalro.cpp |
| **PAL (Platform Abstraction)** | src/coreclr/pal/ | src/, inc/pal.h |
| **Debugger (DAC)** | src/coreclr/debug/daccess/ | daccess.cpp |
| **Host (dotnet.exe)** | src/native/corehost/dotnet/ | dotnet.cpp |
| **Framework Resolver** | src/native/corehost/fxr/ | fx_resolver.cpp |
| **Base Class Library** | src/libraries/System.*/ | (see detailed breakdown below) |
| **WebAssembly** | src/mono/wasm/ | runtime/, driver.c |
| **NativeAOT** | src/coreclr/nativeaot/ | Runtime/, ILCompiler/ |

## By Technology Area

### Execution & Compilation

#### JIT Compilation (RyuJIT)
**Location:** `src/coreclr/jit/`

| Feature | File(s) | Description |
|---------|---------|-------------|
| IL Import | importer.cpp | Converts IL to HIR (High-level IR) |
| Optimization Framework | optimizer.cpp, morph.cpp | IR transformations |
| Inlining | inlining.cpp, inline.cpp, inlinepolicy.cpp | Method inlining decisions |
| Common Subexpression Elimination | optcse.cpp | CSE optimization |
| Loop Optimization | loopcloning.cpp, inductionvariableopts.cpp | Loop optimizations |
| Register Allocation | lsra.cpp, lsrabuild.cpp | Linear scan register allocator |
| Code Generation (x64) | codegenxarch.cpp | x64 code emission |
| Code Generation (ARM64) | codegenarm64.cpp | ARM64 code emission |
| SIMD/Intrinsics | simd.cpp, hwintrinsi*.cpp | Hardware intrinsics |
| Tiered Compilation | fgprofile.cpp | Profile-guided optimization |
| OSR (On-Stack Replacement) | compiler.cpp, flowgraph.cpp | Stack replacement for loops |
| Dynamic PGO | fgprofile.cpp | Runtime profile data |

#### Tiered Compilation
**Locations:**
- Runtime coordinator: `src/coreclr/vm/tieredcompilation.cpp`
- JIT integration: `src/coreclr/jit/fgprofile.cpp`
- Configuration: `src/coreclr/vm/eeconfig.cpp`

#### ReadyToRun (R2R)
**Locations:**
- R2R compiler: `src/coreclr/tools/aot/ILCompiler.ReadyToRun/`
- R2R format reader: `src/coreclr/vm/readytoruninfo.cpp`
- R2R dumper: `src/coreclr/tools/r2rdump/`

#### NativeAOT
**Location:** `src/coreclr/nativeaot/`

| Component | Location |
|-----------|----------|
| AOT Compiler | ILCompiler/ |
| AOT Runtime | Runtime/ |
| System.Private.CoreLib | System.Private.CoreLib/ |
| Reflection | System.Private.Reflection.Core/ |
| Type System | Common/TypeSystem/ |

#### Mono JIT
**Location:** `src/mono/mono/mini/`

| Component | File/Directory |
|-----------|----------------|
| JIT Core | mini.c, method-to-ir.c |
| Backend | mini-{arch}.c (e.g., mini-arm64.c) |
| Interpreter | interp/ |

### Memory Management

#### Garbage Collection
**Location:** `src/coreclr/gc/`

| Feature | File | Description |
|---------|------|-------------|
| Main GC Logic | gc.cpp | Core GC implementation (~2M lines!) |
| GC Configuration | gcconfig.cpp | Runtime GC configuration |
| Handle Table | gchandletable.cpp, handletable.cpp | Object lifetime management |
| Server GC | gcsvr.cpp | Multi-threaded server GC |
| Workstation GC | gcwks.cpp | Single-threaded workstation GC |
| Write Watch | softwarewritewatch.cpp | Write barrier support |
| GC Info Encoding | src/coreclr/jit/gcencode.cpp | JIT GC info generation |

**Configuration Options:**
- Environment variables: `gcconfig.cpp`
- Project settings: `.csproj` (ServerGarbageCollection, etc.)

#### Object Model
**Location:** `src/coreclr/vm/`

| Component | Files |
|-----------|-------|
| Object Layout | object.cpp, object.h |
| MethodTable | methodtable.cpp, methodtable.h, methodtable.inl |
| EEClass | class.cpp, class.h |
| Sync Blocks | syncblk.cpp, syncblk.h |
| Arrays | array.cpp |

### Type System & Metadata

#### Type System
**Location:** `src/coreclr/vm/`

| Feature | File | Description |
|---------|------|-------------|
| Type Handles | typehandle.cpp, typehandle.h | Unified type representation |
| Type Descriptors | typedesc.cpp, typedesc.h | Type description |
| Generic Types | genmeth.cpp, generics.cpp | Generic instantiation |
| Generic Dictionary | genericdict.cpp | Generic lookup |
| Type Loading | clsload.cpp, typeparse.cpp | Type resolution |
| Method Descriptors | method.cpp, method.hpp | Method representation |

#### Metadata
**Location:** `src/coreclr/md/`

| Component | Location | Purpose |
|-----------|----------|---------|
| Reading | runtime/ | Runtime metadata reading |
| Writing | compiler/ | Metadata emission |
| Internal Format | inc/ | Metadata tables and structures |
| Hot Data | hotdata/ | Hot/cold data separation |

#### Reflection
**Managed:**
- Core reflection: `src/libraries/System.Reflection/`
- Reflection emit: `src/libraries/System.Reflection.Emit/`
- Metadata: `src/libraries/System.Reflection.Metadata/`

**Native:**
- VM support: `src/coreclr/vm/reflectioninvocation.cpp`

### Interoperability

#### P/Invoke
**Locations:**
- Marshaling: `src/coreclr/vm/dllimport.cpp`, `ilmarshalers.cpp`
- IL stubs: `src/coreclr/vm/ilstubcache.cpp`
- Source generators: `src/libraries/System.Runtime.InteropServices/gen/`
- Native libs: `src/native/libs/`

#### COM Interop (Windows)
**Location:** `src/coreclr/interop/`, `src/coreclr/vm/`

| Feature | File |
|---------|------|
| COM Callable Wrapper (CCW) | comcallablewrapper.cpp |
| Runtime Callable Wrapper (RCW) | runtimecallablewrapper.cpp |
| COM Interop Utilities | interoputil.cpp |
| COM Host | src/native/corehost/comhost/ |

#### Reverse P/Invoke
**Location:** `src/coreclr/vm/`
- Entry point: `dllimportcallback.cpp`
- Marshaling: `ilmarshalers.cpp`

#### JavaScript Interop (WASM)
**Location:** `src/mono/wasm/`, `src/libraries/System.Runtime.InteropServices.JavaScript/`

### Assembly Loading & Binding

**Location:** `src/coreclr/vm/`, `src/coreclr/binder/`

| Feature | Location |
|---------|----------|
| Assembly Loading | ceeload.cpp, assembly.cpp |
| Binder | binder/ directory |
| AssemblyLoadContext | assemblyloadcontext.cpp |
| Fusion (legacy) | Historical, mostly removed |
| Module Loading | ceemain.cpp, domainfile.cpp |

### Diagnostics & Profiling

#### EventPipe
**Location:** `src/native/eventpipe/`

| Component | Files |
|-----------|-------|
| Core | ep.cpp, ep-provider.cpp |
| Events | ep-event.cpp, ep-event-instance.cpp |
| Sessions | ep-session.cpp |
| Buffers | ep-buffer.cpp, ep-buffer-manager.cpp |

#### ETW (Event Tracing for Windows)
**Location:** `src/coreclr/vm/`
- Manifest: `ClrEtwAll.man`
- Implementation: `eventtrace.cpp`

#### Profiler API
**Location:** `src/coreclr/vm/`

| Feature | File |
|---------|------|
| Profiler Callbacks | proftoeetointerfaceimpl.cpp |
| Profiler Interface | inc/corprof.idl |
| ELT Hooks | profilingenumerators.cpp |

#### Debugger
**Location:** `src/coreclr/debug/`

| Component | Location |
|-----------|----------|
| DAC (Data Access) | daccess/ |
| Debug Interface | di/ (ICorDebug implementation) |
| EE Debug Support | ee/ |
| Dump Generation | createdump/ |

### Platform Abstraction

#### Platform Abstraction Layer (PAL)
**Location:** `src/coreclr/pal/`

| Feature | Location |
|---------|----------|
| Thread APIs | src/thread/ |
| Sync Primitives | src/sync/ |
| Memory Management | src/memory/ |
| File I/O | src/file/ |
| Exception Handling | src/exception/ |
| Module Loading | src/loader/ |
| Process Management | src/process/ |

#### Architecture-Specific Code
**Location:** `src/coreclr/`

| Architecture | Locations |
|--------------|-----------|
| x64 (amd64) | vm/amd64/, jit/targetamd64.cpp |
| ARM32 | vm/arm/, jit/targetarm.cpp |
| ARM64 | vm/arm64/, jit/targetarm64.cpp |
| x86 (i386) | vm/i386/, jit/targeti386.cpp |
| RISC-V | vm/riscv64/, jit/targetriscv64.cpp |
| LoongArch | vm/loongarch64/, jit/targetloongarch64.cpp |

### Hosting & Installation

#### Host Executables
**Location:** `src/native/corehost/`

| Component | Directory | Binary Output |
|-----------|-----------|---------------|
| dotnet CLI | dotnet/ | dotnet.exe |
| App Host | apphost/ | yourapp.exe |
| Framework Resolver | fxr/ | hostfxr.dll |
| Host Policy | hostpolicy/ | hostpolicy.dll |
| .NET Host API | nethost/ | nethost.dll |
| COM Host | comhost/ | comhost.dll |

#### Installers
**Location:** `src/installer/`

| Type | Location |
|------|----------|
| Package Definitions | pkg/ |
| Shared Framework | managed/Microsoft.NETCore.App/ |
| Host Pack | managed/Microsoft.NETCore.DotNetHost/ |

## By Library/Framework Feature

### Core Types

| Type/Namespace | Location |
|----------------|----------|
| System.Object, String, Array | src/libraries/System.Private.CoreLib/src/System/ |
| ValueType, Enum | src/libraries/System.Private.CoreLib/src/System/ |
| Span<T>, Memory<T> | src/libraries/System.Memory/ or CoreLib |
| Nullable<T> | src/libraries/System.Private.CoreLib/src/System/ |

### Collections

| Collection Type | Location |
|-----------------|----------|
| List, Dictionary, etc. | src/libraries/System.Collections/src/System/Collections/Generic/ |
| Concurrent collections | src/libraries/System.Collections.Concurrent/ |
| Immutable collections | src/libraries/System.Collections.Immutable/ |

### I/O

| Feature | Location |
|---------|----------|
| File I/O | src/libraries/System.IO.FileSystem/ |
| Streams | src/libraries/System.IO/ or System.Private.CoreLib |
| Compression | src/libraries/System.IO.Compression/ |
| Pipes | src/libraries/System.IO.Pipes/ |
| Memory-mapped files | src/libraries/System.IO.MemoryMappedFiles/ |

### Networking

| Feature | Location |
|---------|----------|
| HttpClient | src/libraries/System.Net.Http/ |
| Sockets | src/libraries/System.Net.Sockets/ |
| DNS | src/libraries/System.Net.NameResolution/ |
| SSL/TLS | src/libraries/System.Net.Security/ |
| WebSockets | src/libraries/System.Net.WebSockets/ |
| NetworkInformation | src/libraries/System.Net.NetworkInformation/ |

**Native implementations:**
- Unix: `src/libraries/Native/Unix/System.Net.Security.Native/`
- Windows: Built-in (Schannel, WinHTTP)

### Threading & Async

| Feature | Location |
|---------|----------|
| Thread, ThreadPool | src/libraries/System.Private.CoreLib/ (native: coreclr/vm/threads.cpp) |
| Task, Task<T> | src/libraries/System.Private.CoreLib/src/System/Threading/Tasks/ |
| async/await machinery | Compiler magic + System.Private.CoreLib |
| Channels | src/libraries/System.Threading.Channels/ |
| Synchronization | src/libraries/System.Private.CoreLib/src/System/Threading/ |

### Data & Serialization

| Feature | Location |
|---------|----------|
| JSON (System.Text.Json) | src/libraries/System.Text.Json/ |
| XML | src/libraries/System.Xml.*/ |
| Regex | src/libraries/System.Text.RegularExpressions/ |
| LINQ | src/libraries/System.Linq/ |
| Expression Trees | src/libraries/System.Linq.Expressions/ |

### Security & Cryptography

| Feature | Location |
|---------|----------|
| Core crypto types | src/libraries/System.Security.Cryptography/ |
| Algorithms | src/libraries/System.Security.Cryptography.Algorithms/ |
| CNG (Windows) | src/libraries/System.Security.Cryptography.Cng/ |
| OpenSSL (Unix) | src/libraries/System.Security.Cryptography.OpenSsl/ |
| X.509 Certificates | src/libraries/System.Security.Cryptography.X509Certificates/ |

**Native implementations:**
- Unix: `src/libraries/Native/Unix/System.Security.Cryptography.Native.*/`
- macOS: Additional Apple-specific code

### Microsoft.Extensions Framework

| Feature | Location |
|---------|----------|
| Dependency Injection | src/libraries/Microsoft.Extensions.DependencyInjection/ |
| Configuration | src/libraries/Microsoft.Extensions.Configuration/ |
| Logging | src/libraries/Microsoft.Extensions.Logging/ |
| Options | src/libraries/Microsoft.Extensions.Options/ |
| Hosting | src/libraries/Microsoft.Extensions.Hosting/ |
| Caching | src/libraries/Microsoft.Extensions.Caching.*/ |
| HTTP Factory | src/libraries/Microsoft.Extensions.Http/ |

## By Common Task

### "I want to add..."

| Addition | Primary Location | See Also |
|----------|------------------|----------|
| JIT optimization | src/coreclr/jit/ | morph.cpp, optimizer.cpp |
| New intrinsic | src/coreclr/jit/hwintrinsic*.cpp | Also libraries for managed API |
| GC feature | src/coreclr/gc/gc.cpp | gcconfig.cpp for config |
| New BCL type | src/libraries/System.*/ | Choose appropriate library |
| Diagnostic event | src/native/eventpipe/ | Also ClrEtwAll.man for ETW |
| P/Invoke function | src/native/libs/System.Native/ | Platform-specific |
| Marshaling support | src/coreclr/vm/ilmarshalers.cpp | |
| Platform support | src/coreclr/pal/, src/coreclr/vm/{arch}/ | Multi-file effort |

### "I want to fix..."

| Bug Area | Start Looking In |
|----------|------------------|
| Crash on startup | src/coreclr/vm/ceemain.cpp, src/native/corehost/ |
| Type loading error | src/coreclr/vm/clsload.cpp, typehandle.cpp |
| JIT miscompilation | src/coreclr/jit/ (narrow down phase) |
| GC issue | src/coreclr/gc/ |
| Stack overflow | src/coreclr/vm/threads.cpp, stackwalk.cpp |
| P/Invoke failure | src/coreclr/vm/dllimport.cpp, ilmarshalers.cpp |
| Assembly load failure | src/coreclr/binder/, ceeload.cpp |
| Performance regression | Profile first, then appropriate component |

### "I want to understand..."

| Topic | Primary Documentation | Code Entry Point |
|-------|----------------------|------------------|
| How JIT works | docs/design/coreclr/botr/ryujit-overview.md | src/coreclr/jit/compiler.cpp |
| How GC works | docs/design/coreclr/botr/garbage-collection.md | src/coreclr/gc/gc.cpp |
| Type system | docs/design/coreclr/botr/type-system.md | src/coreclr/vm/methodtable.cpp |
| Exception handling | docs/design/coreclr/botr/exceptions.md | src/coreclr/vm/excep.cpp |
| Threading model | docs/design/coreclr/botr/threading.md | src/coreclr/vm/threads.cpp |
| Debugging | docs/design/coreclr/botr/dac-notes.md | src/coreclr/debug/ |

## Build System Locations

| Need to Modify | File |
|----------------|------|
| Build subsets | eng/Subsets.props |
| Component versions | eng/Versions.props |
| Dependency versions | eng/Version.Details.props |
| Global properties | Directory.Build.props |
| Global targets | Directory.Build.targets |
| Native build (CMake) | CMakeLists.txt files (288+) |
| CI/CD pipelines | .github/workflows/, eng/pipelines/ |

## Test Locations

| Testing | Location |
|---------|----------|
| JIT tests | src/tests/JIT/ |
| GC tests | src/tests/GC/ |
| Library tests | src/libraries/{LibraryName}/tests/ |
| Interop tests | src/tests/Interop/ |
| Performance tests | src/tests/JIT/Performance/, library test projects |

## Summary

This reference provides quick navigation to specific features and technologies. For deeper understanding of each area, refer to the detailed component guides (02-07) and the Book of the Runtime in `docs/design/coreclr/botr/`.

**Pro tip:** Use your IDE's "Go to Symbol" or grep to find specific function/class names within these locations.

---

**Next:** [09-Contribution-Workflows.md](09-Contribution-Workflows.md) for step-by-step workflows for common tasks.
