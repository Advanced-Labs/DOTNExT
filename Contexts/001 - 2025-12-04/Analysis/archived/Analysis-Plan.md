# Engram System Analysis Plan

> Task tracking for Engram feasibility analysis and design exploration.

---

## Phase 1: Understanding the Terrain

### 1.1 CoreCLR Object Model
- [ ] Object header structure (syncblock, methodtable ptr)
- [ ] How GC tracks object references (card tables, handle tables)
- [ ] Type metadata architecture (MethodTable, EEClass, TypeHandle)
- [ ] Memory layout: value types vs reference types
- [ ] Finalization and weak references

### 1.2 Existing Serialization Infrastructure
- [ ] BinaryFormatter internals (deprecated but instructive)
- [ ] System.Text.Json source generators
- [ ] How Orleans serializes grains
- [ ] MessagePack/Protobuf .NET implementations

### 1.3 Roslyn Capabilities
- [ ] Semantic model - what relationships it knows
- [ ] Source generators - extension point for UUID injection?
- [ ] IL generation patterns
- [ ] Async state machine structure (already touched for Async+)

### 1.4 Extension Points Catalog
- [ ] Where can we inject UUID generation?
- [ ] Where can we intercept object creation?
- [ ] Where can we hook reference tracking?
- [ ] What's modifiable without breaking compatibility?

---

## Phase 2: Design Exploration

### 2.1 UUID Identity System
- [ ] When/where to generate UUIDs
- [ ] Storage location (object header extension? separate table?)
- [ ] Performance implications
- [ ] Collision handling across nodes

### 2.2 Reference Abstraction Layer
- [ ] Internal reference representation
- [ ] External (engram) reference format
- [ ] Relationship metadata schema
- [ ] Graph reconstruction algorithms

### 2.3 Boundary Crossing Protocol
- [ ] Engram binary format design
- [ ] Missing reference resolution strategies
- [ ] Node identity and discovery
- [ ] Security considerations

### 2.4 Language Integration
- [ ] C# syntax extensions (if any)
- [ ] Attribute-based metadata
- [ ] Compiler-generated code patterns
- [ ] Exception/null handling for distributed references

---

## Phase 3: Proof of Concept Paths

### 3.1 Minimal Viable Engram
- [ ] Simplest possible implementation
- [ ] What subset demonstrates the concept?
- [ ] Integration with existing Orleans work

### 3.2 Incremental Adoption Path
- [ ] Opt-in mechanism
- [ ] Compatibility with standard .NET code
- [ ] Migration strategy for existing types

---

## Resources Identified

| Resource | Location | Status |
|----------|----------|--------|
| BOTR | https://github.com/dotnet/runtime/tree/main/docs/design/coreclr/botr | Need TOC |
| Repo Map | D:\Dev\DOTNExT\Docs\Repo Map\ | Available |
| docs_tree.md | D:\Dev\DOTNExT\docs_tree.md | Available |
| Feature Location Ref | Docs\Repo Map\08-Feature-Location-Reference.md | Check |
| Extension Points | Docs\Repo Map\14-Extension-Points-Catalog.md | Check |

---

## Completed Items

(Move items here when done)

---

*Last updated: 2025-12-05*
