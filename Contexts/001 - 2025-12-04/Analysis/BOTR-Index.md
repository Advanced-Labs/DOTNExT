# Book of the Runtime (BOTR) Index

> Reference: https://github.com/dotnet/runtime/tree/main/docs/design/coreclr/botr
> Generated: 2025-12-05

---

## All Documents

| Filename | Description | Relevance |
|----------|-------------|-----------|
| README.md | Main index for the Book of the Runtime documentation, provides overview and navigation to all BOTR chapters | LOW |
| botr-faq.md | Frequently asked questions about CoreCLR internals and design decisions | MEDIUM |
| clr-abi.md | CLR Application Binary Interface - calling conventions, register usage, stack frame layout | MEDIUM |
| corelib.md | System.Private.CoreLib and how managed code interacts with runtime internals via FCall/QCall | HIGH |
| dac-notes.md | Data Access Component for debugging - how debuggers read CLR data structures | MEDIUM |
| exceptions.md | Exception handling implementation, stack unwinding, and exception object layout | MEDIUM |
| garbage-collection.md | GC design, generations, segments, card tables, write barriers, object lifetime management | HIGH |
| guide-for-porting.md | Instructions for porting the runtime to new platforms | LOW |
| ilc-architecture.md | IL Compiler (NativeAOT) architecture and ahead-of-time compilation | LOW |
| intro-to-clr.md | High-level introduction to CLR concepts, execution model, and component overview | MEDIUM |
| logging.md | Runtime diagnostic logging infrastructure | LOW |
| managed-type-system.md | How types are represented in the managed type system, metadata reading | HIGH |
| method-descriptor.md | MethodDesc structure - how methods are represented, vtable slots, method metadata | HIGH |
| mixed-mode.md | Mixed-mode assemblies combining native and managed code (C++/CLI) | LOW |
| profilability.md | How the runtime supports profiling APIs and instrumentation hooks | LOW |
| profiling.md | Profiling API implementation and profiler interaction points | LOW |
| r2r-perfmap-format.md | Performance map format for Ready-to-Run images | LOW |
| readytorun-format.md | ReadyToRun binary format specification for precompiled assemblies | LOW |
| readytorun-overview.md | Overview of ReadyToRun technology for faster startup | LOW |
| readytorun-platform-native-envelope.md | Platform-specific native envelope for R2R images | LOW |
| runtime-async-codegen.md | How the runtime generates code for async/await state machines | MEDIUM |
| shared-generics.md | Shared generic instantiations, canonical forms, and generic dictionary layout | HIGH |
| stackwalking.md | Stack walking for GC, exceptions, and debugging - how frames are enumerated | MEDIUM |
| threading.md | Threading model, thread objects, synchronization primitives | MEDIUM |
| type-loader.md | How types are loaded, resolved, and initialized at runtime | HIGH |
| type-system.md | Core type system: MethodTable, EEClass, TypeHandle, object header layout, type identity | HIGH |
| vectors-and-intrinsics.md | SIMD vector types and hardware intrinsics support | LOW |
| virtual-stub-dispatch.md | Virtual method dispatch via stubs, vtables, interface dispatch | HIGH |
| xplat-minidump-generation.md | Cross-platform minidump generation for crash diagnostics | LOW |

---

## Priority Reading Order (for Engram Analysis)

1. **type-system.md** - Core document: MethodTable, EEClass, TypeHandle, object headers
2. **garbage-collection.md** - Object lifetime, reference tracking, memory layout implications
3. **type-loader.md** - How types materialize in memory
4. **method-descriptor.md** - MethodDesc and method slot layout
5. **corelib.md** - Managed/native boundary, intrinsic types
6. **shared-generics.md** - Generic type representation and dictionary layout
7. **virtual-stub-dispatch.md** - Vtable structure and interface maps

---

## Key Questions for Each Document

### type-system.md
- Object header structure - what's in it, can it be extended?
- Where does identity currently live?
- Relationship between MethodTable and actual object instances

### garbage-collection.md
- How are object references tracked?
- Card tables - could they inform relationship metadata?
- Handle tables - different handle types and their semantics

### type-loader.md
- Where could UUID generation be injected?
- Type initialization hooks

---

*Use SAGE subagent to fetch and analyze specific documents as needed*
