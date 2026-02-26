# Orleans Internals Documentation

Welcome to the comprehensive Orleans internals documentation. This guide provides deep insights into how Orleans works under the hood, its architecture, systems, and implementation details.

## Purpose

This documentation is designed for:
- Team members who need to understand Orleans internals
- Contributors working on Orleans core features
- Developers troubleshooting complex issues
- Anyone wanting to understand distributed actor systems

## Table of Contents

### Core Concepts
1. [**Paradigms and Core Concepts**](01-paradigms-and-concepts.md)
   - Virtual Actor Model
   - Design principles
   - Turn-based concurrency
   - Location transparency

### Architecture
2. [**Systems and Subsystems Map**](02-systems-and-subsystems.md)
   - Complete map of all major systems
   - How systems interact
   - Responsibilities and boundaries

3. [**Layer Architecture**](03-layer-architecture.md)
   - Architectural layers from Core to Providers
   - Dependencies between layers
   - Package organization

### Core Systems
4. [**Code Generation System**](04-codegen-system.md)
   - What codegen does and why
   - How it works (Roslyn Source Generators)
   - Generated code examples
   - Build integration

5. [**Runtime and Activation System**](05-runtime-activation.md)
   - Silo runtime
   - Grain activation lifecycle
   - Catalog and activation management
   - Scheduler and turn-based execution

6. [**Clustering and Membership**](06-clustering-membership.md)
   - Cluster membership protocol
   - Health monitoring
   - Failure detection
   - Membership gossip

7. [**Messaging and Networking**](07-messaging-networking.md)
   - Message routing
   - Connection management
   - Request/response patterns
   - Network protocols

8. [**Persistence and State Management**](08-persistence-state.md)
   - Storage abstractions
   - State lifecycle
   - Provider implementations
   - Transactional state

9. [**Serialization System**](09-serialization.md)
   - Codec-based architecture
   - Version tolerance
   - Performance optimizations
   - Custom serializers

### Reference
10. [**Additional Systems**](10-additional-systems.md)
    - Grain Directory
    - Placement System
    - Timers and Reminders
    - Streams
    - Transactions
    - Event Sourcing

11. [**Key Abstractions**](11-key-abstractions.md)
    - Core interfaces (IGrain, IGrainContext, etc.)
    - Identity types (GrainId, ActivationId, etc.)
    - Lifecycle abstractions
    - Storage abstractions

12. [**Directory Structure Guide**](12-directory-structure.md)
    - Source code organization
    - Package purposes
    - Where to find specific implementations

## Quick Navigation

### By Role
- **New Contributors**: Start with [Paradigms](01-paradigms-and-concepts.md) → [Layer Architecture](03-layer-architecture.md) → [Directory Structure](12-directory-structure.md)
- **Debugging Issues**: Check [Systems Map](02-systems-and-subsystems.md) → relevant system document
- **Understanding Features**: See [Additional Systems](10-additional-systems.md) and specific system docs

### By Topic
- **Performance**: [Serialization](09-serialization.md), [Scheduler](05-runtime-activation.md#scheduler-system), [Placement](10-additional-systems.md#placement-system)
- **Reliability**: [Clustering](06-clustering-membership.md), [Persistence](08-persistence-state.md)
- **Development**: [Code Generation](04-codegen-system.md), [Key Abstractions](11-key-abstractions.md)

## Contributing to This Documentation

This documentation is maintained alongside the codebase. When making changes to Orleans internals:

1. Update relevant documentation
2. Add examples where helpful
3. Document breaking changes
4. Keep architecture diagrams current

## Additional Resources

- Official Orleans Documentation: https://docs.microsoft.com/en-us/dotnet/orleans/
- Orleans GitHub: https://github.com/dotnet/orleans
- Orleans Blog: https://dotnet.github.io/orleans/blog/

---

**Last Updated**: 2025-11-21
**Orleans Version**: Main branch
