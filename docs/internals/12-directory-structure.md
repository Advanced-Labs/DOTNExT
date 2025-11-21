# Directory Structure Guide

## Overview

This guide maps Orleans' source code organization to help you find specific implementations.

**Repository Root**: `/src/`

## Core Libraries

### Orleans.Core.Abstractions

**Path**: `src/Orleans.Core.Abstractions/`

**Purpose**: Platform-agnostic abstractions for grain programming model.

```
Orleans.Core.Abstractions/
├── Core/
│   ├── IGrain.cs                    # Base grain interface
│   ├── IGrainBase.cs                # Grain implementation interface
│   └── IGrainWithKey.cs             # Typed key interfaces
├── IDs/
│   ├── GrainId.cs                   # Grain identity
│   ├── GrainType.cs                 # Grain type identifier
│   ├── ActivationId.cs              # Activation instance ID
│   ├── SiloAddress.cs               # Silo endpoint + generation
│   └── GrainAddress.cs              # Complete grain location
├── Placement/
│   ├── PlacementStrategy.cs         # Base placement class
│   └── PlacementAttributes.cs       # Placement strategy attributes
├── Runtime/
│   ├── IGrainContext.cs             # Runtime grain context
│   ├── IGrainActivator.cs           # Grain instance factory
│   └── IInvokable.cs                # Method invocation payload
├── Timers/
│   ├── IRemindable.cs               # Reminder callback interface
│   └── IGrainTimer.cs               # Timer interfaces
├── Lifecycle/
│   └── IGrainLifecycle.cs           # Lifecycle management
├── Concurrency/
│   └── ReentrantAttribute.cs        # Reentrancy control
├── Versions/
│   └── GrainVersionAttribute.cs     # Grain versioning
└── SystemTargetInterfaces/
    └── ISystemTarget.cs             # Internal system grains
```

### Orleans.Serialization.Abstractions

**Path**: `src/Orleans.Serialization.Abstractions/`

**Purpose**: Serialization framework interfaces.

```
Orleans.Serialization.Abstractions/
├── IFieldCodec.cs                   # Type serializer interface
├── IDeepCopier.cs                   # Deep copy interface
├── IActivator.cs                    # Object factory interface
├── GenerateSerializerAttribute.cs   # Mark types for codegen
└── IdAttribute.cs                   # Field ID for versioning
```

### Orleans.Serialization

**Path**: `src/Orleans.Serialization/`

**Purpose**: High-performance serialization implementation.

```
Orleans.Serialization/
├── Serializer.cs                    # Main entry point
├── Session/
│   └── Session.cs                   # Serialization context
├── Codecs/
│   ├── IFieldCodec.cs               # Codec interface
│   ├── PrimitiveCodecs.cs           # Built-in type codecs
│   ├── CollectionCodecs.cs          # List, Array, Dictionary
│   └── ReferenceCodec.cs            # Object reference handling
├── Cloning/
│   └── DeepCopier.cs                # Deep copy implementation
├── Activators/
│   └── DefaultActivator.cs          # Object creation
├── Buffers/
│   ├── Writer.cs                    # Serialization writer
│   └── Reader.cs                    # Deserialization reader
├── TypeSystem/
│   └── TypeResolver.cs              # Type resolution
└── Invocation/
    └── InvokableCodec.cs            # RPC invocation codec
```

### Orleans.Core

**Path**: `src/Orleans.Core/`

**Purpose**: Client-side implementation and shared runtime components.

```
Orleans.Core/
├── Core/
│   ├── GrainReference.cs            # Base class for proxies
│   ├── GrainFactory.cs              # Grain reference factory
│   └── ClientGrainContext.cs        # Client-side context
├── Configuration/
│   └── ClientConfiguration.cs       # Client config
├── Diagnostics/
│   └── Metrics.cs                   # Telemetry
├── Messaging/
│   └── Message.cs                   # Message type
├── Networking/
│   ├── Connection.cs                # Network connection base
│   └── ConnectionContext.cs         # Connection state
├── Runtime/
│   ├── RequestContext.cs            # Per-request context
│   └── GrainReferenceRuntime.cs     # Reference runtime support
└── Serialization/
    └── SerializationManager.cs      # Serialization integration
```

### Orleans.Runtime

**Path**: `src/Orleans.Runtime/`

**Purpose**: Server-side silo implementation - the heart of Orleans.

```
Orleans.Runtime/
├── Silo/
│   ├── Silo.cs                      # Main silo host
│   └── SiloBuilder.cs               # Silo configuration
├── Catalog/
│   ├── Catalog.cs                   # Activation registry
│   ├── ActivationData.cs            # Grain activation metadata
│   └── ActivationCollector.cs       # Idle activation GC
├── Activation/
│   ├── IGrainActivator.cs           # Activation factory
│   └── GrainCreator.cs              # Grain instance creator
├── Scheduler/
│   ├── OrleansTaskScheduler.cs      # Global scheduler
│   ├── WorkItemGroup.cs             # Per-activation queue
│   └── ActivationTaskScheduler.cs   # Activation scheduler
├── Messaging/
│   ├── MessageCenter.cs             # Message router
│   └── InboundMessageQueue.cs       # Incoming messages
├── Networking/
│   ├── SiloConnection.cs            # Silo-to-silo connection
│   ├── GatewayInboundConnection.cs  # Client connections
│   └── ConnectionListener.cs        # Accept connections
├── MembershipService/
│   ├── ClusterMembershipService.cs  # Cluster view
│   ├── MembershipTableManager.cs    # Table coordination
│   ├── SiloHealthMonitor.cs         # Failure detection
│   └── MembershipGossiper.cs        # Gossip protocol
├── GrainDirectory/
│   ├── GrainDirectory.cs            # Local directory
│   ├── GrainDirectoryPartition.cs   # Directory partition
│   ├── GrainLocator.cs              # Activation locator
│   └── CachedGrainLocator.cs        # Caching locator
├── Placement/
│   ├── PlacementService.cs          # Placement coordinator
│   ├── RandomPlacementDirector.cs   # Random placement
│   ├── HashBasedDirector.cs         # Hash-based placement
│   └── ActivationCountDirector.cs   # Load-based placement
├── ConsistentRing/
│   └── ConsistentRingProvider.cs    # Consistent hashing
├── Storage/
│   └── GrainStorageHelpers.cs       # Storage utilities
├── Facet/
│   └── PersistentStateFacet.cs      # State management
├── Timers/
│   ├── GrainTimer.cs                # Timer implementation
│   └── ReminderRegistry.cs          # Reminder management
└── Lifecycle/
    └── GrainLifecycle.cs            # Lifecycle implementation
```

### Orleans.CodeGenerator

**Path**: `src/Orleans.CodeGenerator/`

**Purpose**: Roslyn source generator for serialization and RPC.

```
Orleans.CodeGenerator/
├── OrleansSourceGenerator.cs        # Generator entry point
├── CodeGenerator.cs                 # Code generation logic
├── SyntaxGeneration/
│   └── SyntaxFactoryExtensions.cs   # Roslyn syntax helpers
├── Generators/
│   ├── SerializerGenerator.cs       # Generate codecs
│   ├── CopierGenerator.cs           # Generate copiers
│   ├── ProxyGenerator.cs            # Generate grain proxies
│   ├── InvokableGenerator.cs        # Generate invokables
│   ├── ActivatorGenerator.cs        # Generate activators
│   └── MetadataGenerator.cs         # Generate metadata
├── Model/
│   ├── SerializableTypeDescription.cs
│   ├── GrainInterfaceDescription.cs
│   └── InvokableMethodDescription.cs
├── Diagnostics/
│   └── DiagnosticDescriptors.cs     # Compiler diagnostics
└── Hashing/
    └── JenkinsHash.cs               # Method ID hashing
```

## Feature Packages

### Orleans.Streaming

**Path**: `src/Orleans.Streaming/`

```
Orleans.Streaming/
├── Core/
│   ├── IStreamProvider.cs           # Stream provider interface
│   └── StreamImpl.cs                # Stream implementation
├── PersistentStreams/
│   └── PersistentStreamProvider.cs  # Persistent streams
└── SimpleMessageStreams/
    └── SimpleMessageStreamProvider.cs # Memory streams
```

### Orleans.Transactions

**Path**: `src/Orleans.Transactions/`

```
Orleans.Transactions/
├── ITransactionalState.cs           # Transactional state interface
├── TransactionManager.cs            # Transaction coordinator
└── DistributedTM/
    └── TransactionAgent.cs          # Distributed TX agent
```

### Orleans.EventSourcing

**Path**: `src/Orleans.EventSourcing/`

```
Orleans.EventSourcing/
├── JournaledGrain.cs                # Event-sourced grain base
└── LogConsistencyProvider.cs        # Log storage provider
```

### Orleans.Reminders

**Path**: `src/Orleans.Reminders/`

```
Orleans.Reminders/
├── ReminderService.cs               # Reminder service
└── IReminderTable.cs                # Storage interface
```

## Provider Packages

### AdoNet Providers

**Path**: `src/AdoNet/`

```
AdoNet/
├── Orleans.Clustering.AdoNet/       # SQL clustering
├── Orleans.Persistence.AdoNet/      # SQL storage
├── Orleans.Reminders.AdoNet/        # SQL reminders
└── Orleans.Streaming.AdoNet/        # SQL streaming
```

### Azure Providers

**Path**: `src/Azure/`

```
Azure/
├── Orleans.Clustering.AzureStorage/ # Azure Table clustering
├── Orleans.Persistence.AzureStorage/# Blob/Table/Cosmos storage
├── Orleans.Reminders.AzureStorage/  # Azure Table reminders
└── Orleans.Streaming.EventHubs/     # Event Hubs streaming
```

### AWS Providers

**Path**: `src/AWS/`

```
AWS/
├── Orleans.Clustering.DynamoDB/     # DynamoDB clustering
├── Orleans.Persistence.DynamoDB/    # DynamoDB storage
└── Orleans.Streaming.SQS/           # SQS streaming
```

## Host Integration

### Orleans.Client

**Path**: `src/Orleans.Client/`

**Purpose**: Client metapackage.

### Orleans.Server

**Path**: `src/Orleans.Server/`

**Purpose**: Server metapackage.

### Orleans.Sdk

**Path**: `src/Orleans.Sdk/`

**Purpose**: Full SDK metapackage (client + server).

## Testing

### Orleans.TestingHost

**Path**: `src/Orleans.TestingHost/`

```
Orleans.TestingHost/
├── TestCluster.cs                   # In-memory cluster
├── TestClusterBuilder.cs            # Cluster config
└── InMemoryTransportConnectionHub.cs# In-process networking
```

### Test Projects

**Path**: `test/`

```
test/
├── CodeGenerator.Tests/             # Codegen tests
├── NonSilo.Tests/                   # Unit tests
├── TesterInternal/                  # Integration tests
├── Grains/                          # Test grain implementations
└── TestGrainInterfaces/             # Test grain interfaces
```

## Build and Tools

```
src/
├── Orleans.Core.Abstractions/Orleans.Core.Abstractions.csproj
├── Orleans.CodeGenerator/Orleans.CodeGenerator.csproj
└── ...

build/
├── Build.sln                        # Solution file
└── props/                           # MSBuild properties
```

## Finding Specific Code

### By Feature

| Feature | Location |
|---------|----------|
| Grain activation | `Orleans.Runtime/Catalog/` |
| Messaging | `Orleans.Runtime/Messaging/` |
| Clustering | `Orleans.Runtime/MembershipService/` |
| Serialization | `Orleans.Serialization/` |
| Code generation | `Orleans.CodeGenerator/` |
| Storage | `Orleans.Runtime/Storage/`, `Orleans.Runtime/Facet/` |
| Placement | `Orleans.Runtime/Placement/` |
| Timers | `Orleans.Runtime/Timers/` |
| Reminders | `Orleans.Reminders/` |
| Streams | `Orleans.Streaming/` |
| Transactions | `Orleans.Transactions/` |

### By Component

| Component | Location |
|-----------|----------|
| Silo | `Orleans.Runtime/Silo/Silo.cs` |
| Catalog | `Orleans.Runtime/Catalog/Catalog.cs` |
| MessageCenter | `Orleans.Runtime/Messaging/MessageCenter.cs` |
| ClusterMembershipService | `Orleans.Runtime/MembershipService/ClusterMembershipService.cs` |
| GrainDirectory | `Orleans.Runtime/GrainDirectory/GrainDirectory.cs` |
| Scheduler | `Orleans.Runtime/Scheduler/OrleansTaskScheduler.cs` |

## Summary

Orleans source is organized into:

1. **Core libraries**: Abstractions, Core, Runtime, Serialization, CodeGenerator
2. **Feature packages**: Streaming, Transactions, EventSourcing, Reminders
3. **Provider packages**: AdoNet, Azure, AWS implementations
4. **Host integration**: Client, Server, SDK metapackages
5. **Testing**: TestingHost and test projects

Each package has clear responsibilities and dependencies, making the codebase navigable and maintainable.

---

**End of Orleans Internals Documentation**

[Return to Index](00-index.md)
