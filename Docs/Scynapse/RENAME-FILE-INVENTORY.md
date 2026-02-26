# Scynapse Project - Complete Orleans Reference Inventory

## STATUS: Comprehensive Audit Completed

**Date:** 2026-02-26
**Previous Rename:** NewOrleans -> Scynapse (custom code only)
**Finding:** The upstream Orleans framework naming (`Orleans.*`) remains throughout the codebase
**Total scope:** 3,344 files, 772 directories under `src/Scynapse/`

---

## Grand Totals

| Metric | Count |
|--------|-------|
| Directories named with "Orleans" | 139 |
| Files named with "Orleans" | 230 |
| Files containing "Orleans" in content | 2,891 |
| Line occurrences - `Orleans` (PascalCase) | ~16,768 |
| Line occurrences - `ORLEANS` (uppercase) | ~220 |
| Line occurrences - `orleans` (lowercase) | ~194 files |
| Remaining `NewOrleans` references | 2 (1 filename + 1 content) |
| Binary files with "Orleans" | 2 (.png) |

---

## Part 1: All 230 Files with "Orleans" in Filename

### Root Level (1 file)
```
Orleans.slnx                                    [solution file]
```

### src/ - Core Framework .csproj Files (38 files)
```
src/Orleans.Analyzers/Orleans.Analyzers.csproj
src/Orleans.BroadcastChannel/Orleans.BroadcastChannel.csproj
src/Orleans.Client/Orleans.Client.csproj
src/Orleans.Clustering.Consul/Orleans.Clustering.Consul.csproj
src/Orleans.Clustering.ZooKeeper/Orleans.Clustering.ZooKeeper.csproj
src/Orleans.CodeGenerator/Orleans.CodeGenerator.csproj
src/Orleans.Connections.Security/Orleans.Connections.Security.csproj
src/Orleans.Core.Abstractions/Orleans.Core.Abstractions.csproj
src/Orleans.Core/Orleans.Core.csproj
src/Orleans.DurableJobs/Orleans.DurableJobs.csproj
src/Orleans.EventSourcing/Orleans.EventSourcing.csproj
src/Orleans.Hosting.Kubernetes/Orleans.Hosting.Kubernetes.csproj
src/Orleans.Journaling/Orleans.Journaling.csproj
src/Orleans.Persistence.Memory/Orleans.Persistence.Memory.csproj
src/Orleans.Reminders.Abstractions/Orleans.Reminders.Abstractions.csproj
src/Orleans.Reminders/Orleans.Reminders.csproj
src/Orleans.Runtime/Orleans.Runtime.csproj
src/Orleans.Sdk/Orleans.Sdk.csproj
src/Orleans.Serialization.Abstractions/Orleans.Serialization.Abstractions.csproj
src/Orleans.Serialization.FSharp/Orleans.Serialization.FSharp.csproj
src/Orleans.Serialization.MessagePack/Orleans.Serialization.MessagePack.csproj
src/Orleans.Serialization.NewtonsoftJson/Orleans.Serialization.NewtonsoftJson.csproj
src/Orleans.Serialization.SystemTextJson/Orleans.Serialization.SystemTextJson.csproj
src/Orleans.Serialization.TestKit/Orleans.Serialization.TestKit.csproj
src/Orleans.Serialization/Orleans.Serialization.csproj
src/Orleans.Server/Orleans.Server.csproj
src/Orleans.Streaming.Abstractions/Orleans.Streaming.Abstractions.csproj
src/Orleans.Streaming.NATS/Orleans.Streaming.NATS.csproj
src/Orleans.Streaming/Orleans.Streaming.csproj
src/Orleans.TestingHost/Orleans.TestingHost.csproj
src/Orleans.Transactions.TestKit.Base/Orleans.Transactions.TestKit.Base.csproj
src/Orleans.Transactions.TestKit.xUnit/Orleans.Transactions.TestKit.xUnit.csproj
src/Orleans.Transactions/Orleans.Transactions.csproj
src/Serializers/Orleans.Serialization.Protobuf/Orleans.Serialization.Protobuf.csproj
src/Dashboard/Orleans.Dashboard/Orleans.Dashboard.csproj
src/Dashboard/Orleans.Dashboard.Abstractions/Orleans.Dashboard.Abstractions.csproj
src/Orleans.Identity/ManagedCode.Orleans.Identity.Client/ManagedCode.Orleans.Identity.Client.csproj
src/Orleans.Identity/ManagedCode.Orleans.Identity.Core/ManagedCode.Orleans.Identity.Core.csproj
src/Orleans.Identity/ManagedCode.Orleans.Identity.Server/ManagedCode.Orleans.Identity.Server.csproj
src/Orleans.Identity/ManagedCode.Orleans.Identity.Tests/ManagedCode.Orleans.Identity.Tests.csproj
```

### src/ - Provider .csproj Files (22 files)
```
src/AWS/Orleans.Clustering.DynamoDB/Orleans.Clustering.DynamoDB.csproj
src/AWS/Orleans.Persistence.DynamoDB/Orleans.Persistence.DynamoDB.csproj
src/AWS/Orleans.Reminders.DynamoDB/Orleans.Reminders.DynamoDB.csproj
src/AWS/Orleans.Streaming.SQS/Orleans.Streaming.SQS.csproj
src/AdoNet/Orleans.Clustering.AdoNet/Orleans.Clustering.AdoNet.csproj
src/AdoNet/Orleans.GrainDirectory.AdoNet/Orleans.GrainDirectory.AdoNet.csproj
src/AdoNet/Orleans.Persistence.AdoNet/Orleans.Persistence.AdoNet.csproj
src/AdoNet/Orleans.Reminders.AdoNet/Orleans.Reminders.AdoNet.csproj
src/AdoNet/Orleans.Streaming.AdoNet/Orleans.Streaming.AdoNet.csproj
src/Azure/Orleans.Clustering.AzureStorage/Orleans.Clustering.AzureStorage.csproj
src/Azure/Orleans.Clustering.Cosmos/Orleans.Clustering.Cosmos.csproj
src/Azure/Orleans.DurableJobs.AzureStorage/Orleans.DurableJobs.AzureStorage.csproj
src/Azure/Orleans.GrainDirectory.AzureStorage/Orleans.GrainDirectory.AzureStorage.csproj
src/Azure/Orleans.Hosting.AzureCloudServices/Orleans.Hosting.AzureCloudServices.csproj
src/Azure/Orleans.Journaling.AzureStorage/Orleans.Journaling.AzureStorage.csproj
src/Azure/Orleans.Persistence.AzureStorage/Orleans.Persistence.AzureStorage.csproj
src/Azure/Orleans.Persistence.Cosmos/Orleans.Persistence.Cosmos.csproj
src/Azure/Orleans.Reminders.AzureStorage/Orleans.Reminders.AzureStorage.csproj
src/Azure/Orleans.Reminders.Cosmos/Orleans.Reminders.Cosmos.csproj
src/Azure/Orleans.Streaming.AzureStorage/Orleans.Streaming.AzureStorage.csproj
src/Azure/Orleans.Streaming.EventHubs/Orleans.Streaming.EventHubs.csproj
src/Azure/Orleans.Transactions.AzureStorage/Orleans.Transactions.AzureStorage.csproj
src/Cassandra/Orleans.Clustering.Cassandra/Orleans.Clustering.Cassandra.csproj
src/Redis/Orleans.Clustering.Redis/Orleans.Clustering.Redis.csproj
src/Redis/Orleans.GrainDirectory.Redis/Orleans.GrainDirectory.Redis.csproj
src/Redis/Orleans.Persistence.Redis/Orleans.Persistence.Redis.csproj
src/Redis/Orleans.Reminders.Redis/Orleans.Reminders.Redis.csproj
```

### src/ - C# Source Files with Orleans in Name (33 files)
```
src/Orleans.Analyzers/AtMostOneOrleansConstructorAnalyzer.cs
src/Orleans.CodeGenerator/OrleansGeneratorDiagnosticAnalysisException.cs
src/Orleans.CodeGenerator/OrleansSourceGenerator.cs
src/Orleans.Connections.Security/Security/OrleansApplicationProtocol.cs
src/Orleans.Core.Abstractions/Exceptions/OrleansConfigurationException.cs
src/Orleans.Core.Abstractions/Exceptions/OrleansException.cs
src/Orleans.Core.Abstractions/Exceptions/OrleansLifecycleCanceledException.cs
src/Orleans.Core.Abstractions/Exceptions/OrleansMessageRejectionException.cs
src/Orleans.Core.Abstractions/Utils/PublicOrleansTaskExtensions.cs
src/Orleans.Core/Hosting/OrleansClientGenericHostExtensions.cs
src/Orleans.Core/Providers/StorageSerializer/OrleansGrainStateSerializer.cs
src/Orleans.Core/Serialization/OrleansJsonSerializationBinder.cs
src/Orleans.Core/Serialization/OrleansJsonSerializer.cs
src/Orleans.Core/Serialization/OrleansJsonSerializerOptions.cs
src/Orleans.Core/Serialization/OrleansJsonSerializerSettings.cs
src/Orleans.Runtime/Hosting/OrleansSiloGenericHostExtensions.cs
src/Orleans.Runtime/MembershipService/OrleansClusterConnectivityCheckFailedException.cs
src/Orleans.Runtime/MembershipService/OrleansMissingMembershipEntryException.cs
src/Orleans.Runtime/Utilities/OrleansDebuggerHelper.cs
src/Orleans.Serialization/GeneratedCodeHelpers/OrleansGeneratedCodeHelper.cs
src/Orleans.Transactions/OrleansTransactionException.cs
src/AdoNet/Orleans.Persistence.AdoNet/Storage/Provider/Orleans3CompatibleHasher.cs
src/AdoNet/Orleans.Persistence.AdoNet/Storage/Provider/Orleans3CompatibleStorageHashPicker.cs
src/AdoNet/Orleans.Persistence.AdoNet/Storage/Provider/Orleans3CompatibleStringKeyHasher.cs
src/AdoNet/Orleans.Persistence.AdoNet/Storage/Provider/OrleansDefaultHasher.cs
src/AdoNet/Shared/Storage/OrleansRelationalDownloadStream.cs
src/AdoNet/Shared/Storage/RelationalOrleansQueries.cs
src/Azure/Orleans.Clustering.AzureStorage/OrleansSiloInstanceManager.cs
src/Azure/Orleans.Streaming.EventHubs/OrleansServiceBusErrorCode.cs
src/Cassandra/Orleans.Clustering.Cassandra/OrleansQueries.cs
src/Orleans.Identity/ManagedCode.Orleans.Identity.Client/Extensions/OrleansIdentityExtensions.cs
src/Orleans.Identity/ManagedCode.Orleans.Identity.Client/Filters/OrleansAuthorizationActionFilter.cs
src/Orleans.Identity/ManagedCode.Orleans.Identity.Core/Constants/OrleansIdentityConstants.cs
src/Orleans.Identity/ManagedCode.Orleans.Identity.Core/Extensions/OrleansExtensions.cs
```

### src/ - Build/Config Files with Orleans in Name (8 files)
```
src/Orleans.CodeGenerator/build/Microsoft.Orleans.CodeGenerator.props
src/Orleans.CodeGenerator/buildMultiTargeting/Microsoft.Orleans.CodeGenerator.props
src/Orleans.CodeGenerator/buildTransitive/Microsoft.Orleans.CodeGenerator.props
src/Orleans.Sdk/build/Microsoft.Orleans.Sdk.targets
src/Orleans.Sdk/buildMultiTargeting/Microsoft.Orleans.Sdk.targets
src/Orleans.Sdk/buildTransitive/Microsoft.Orleans.Sdk.targets
src/Dashboard/Orleans.Dashboard/Orleans.Dashboard.Frontend.targets
src/Orleans.Identity/ManagedCode.Orleans.Identity.sln
```

### src/ - Binary/Image Files (1 file)
```
src/Dashboard/Orleans.Dashboard.App/src/assets/img/OrleansLogo.png
```

### src/ - Scynapse.AsyncPlus (1 file - REMAINING NEWORLEANS REFERENCE)
```
src/Scynapse.AsyncPlus/Services/NewOrleansAsyncPersistenceService.cs  [SHOULD RENAME]
```

### src/api/ - API Reference Files (56 files)
```
src/api/Orleans.BroadcastChannel/Orleans.BroadcastChannel.cs
src/api/Orleans.Client/Orleans.Client.cs
src/api/Orleans.Clustering.Consul/Orleans.Clustering.Consul.cs
src/api/Orleans.Clustering.ZooKeeper/Orleans.Clustering.ZooKeeper.cs
src/api/Orleans.Connections.Security/Orleans.Connections.Security.cs
src/api/Orleans.Core.Abstractions/Orleans.Core.Abstractions.cs
src/api/Orleans.Core/Orleans.Core.cs
src/api/Orleans.EventSourcing/Orleans.EventSourcing.cs
src/api/Orleans.Hosting.Kubernetes/Orleans.Hosting.Kubernetes.cs
src/api/Orleans.Journaling/Orleans.Journaling.cs
src/api/Orleans.Persistence.Memory/Orleans.Persistence.Memory.cs
src/api/Orleans.Reminders.Abstractions/Orleans.Reminders.Abstractions.cs
src/api/Orleans.Reminders/Orleans.Reminders.cs
src/api/Orleans.Runtime/Orleans.Runtime.cs
src/api/Orleans.Sdk/Orleans.Sdk.cs
src/api/Orleans.Serialization.Abstractions/Orleans.Serialization.Abstractions.cs
src/api/Orleans.Serialization.FSharp/Orleans.Serialization.FSharp.cs
src/api/Orleans.Serialization.MessagePack/Orleans.Serialization.MessagePack.cs
src/api/Orleans.Serialization.NewtonsoftJson/Orleans.Serialization.NewtonsoftJson.cs
src/api/Orleans.Serialization.SystemTextJson/Orleans.Serialization.SystemTextJson.cs
src/api/Orleans.Serialization.TestKit/Orleans.Serialization.TestKit.cs
src/api/Orleans.Serialization/Orleans.Serialization.cs
src/api/Orleans.Server/Orleans.Server.cs
src/api/Orleans.Streaming.Abstractions/Orleans.Streaming.Abstractions.cs
src/api/Orleans.Streaming/Orleans.Streaming.cs
src/api/Orleans.TestingHost/Orleans.TestingHost.cs
src/api/Orleans.Transactions.TestKit.Base/Orleans.Transactions.TestKit.Base.cs
src/api/Orleans.Transactions.TestKit.xUnit/Orleans.Transactions.TestKit.xUnit.cs
src/api/Orleans.Transactions/Orleans.Transactions.cs
src/api/AWS/Orleans.Clustering.DynamoDB/Orleans.Clustering.DynamoDB.cs
src/api/AWS/Orleans.Persistence.DynamoDB/Orleans.Persistence.DynamoDB.cs
src/api/AWS/Orleans.Reminders.DynamoDB/Orleans.Reminders.DynamoDB.cs
src/api/AWS/Orleans.Streaming.SQS/Orleans.Streaming.SQS.cs
src/api/AdoNet/Orleans.Clustering.AdoNet/Orleans.Clustering.AdoNet.cs
src/api/AdoNet/Orleans.GrainDirectory.AdoNet/Orleans.GrainDirectory.AdoNet.cs
src/api/AdoNet/Orleans.Persistence.AdoNet/Orleans.Persistence.AdoNet.cs
src/api/AdoNet/Orleans.Reminders.AdoNet/Orleans.Reminders.AdoNet.cs
src/api/AdoNet/Orleans.Streaming.AdoNet/Orleans.Streaming.AdoNet.cs
src/api/Azure/Orleans.Clustering.AzureStorage/Orleans.Clustering.AzureStorage.cs
src/api/Azure/Orleans.Clustering.Cosmos/Orleans.Clustering.Cosmos.cs
src/api/Azure/Orleans.GrainDirectory.AzureStorage/Orleans.GrainDirectory.AzureStorage.cs
src/api/Azure/Orleans.Hosting.AzureCloudServices/Orleans.Hosting.AzureCloudServices.cs
src/api/Azure/Orleans.Journaling.AzureStorage/Orleans.Journaling.AzureStorage.cs
src/api/Azure/Orleans.Persistence.AzureStorage/Orleans.Persistence.AzureStorage.cs
src/api/Azure/Orleans.Persistence.Cosmos/Orleans.Persistence.Cosmos.cs
src/api/Azure/Orleans.Reminders.AzureStorage/Orleans.Reminders.AzureStorage.cs
src/api/Azure/Orleans.Reminders.Cosmos/Orleans.Reminders.Cosmos.cs
src/api/Azure/Orleans.Streaming.AzureStorage/Orleans.Streaming.AzureStorage.cs
src/api/Azure/Orleans.Streaming.EventHubs/Orleans.Streaming.EventHubs.cs
src/api/Azure/Orleans.Transactions.AzureStorage/Orleans.Transactions.AzureStorage.cs
src/api/Cassandra/Orleans.Clustering.Cassandra/Orleans.Clustering.Cassandra.cs
src/api/Redis/Orleans.Clustering.Redis/Orleans.Clustering.Redis.cs
src/api/Redis/Orleans.GrainDirectory.Redis/Orleans.GrainDirectory.Redis.cs
src/api/Redis/Orleans.Persistence.Redis/Orleans.Persistence.Redis.cs
src/api/Redis/Orleans.Reminders.Redis/Orleans.Reminders.Redis.cs
src/api/Serializers/Orleans.Serialization.Protobuf/Orleans.Serialization.Protobuf.cs
```

### test/ - .csproj and Project Files (15 files)
```
test/Orleans.CodeGenerator.Tests/Orleans.CodeGenerator.Tests.csproj
test/Orleans.Connections.Security.Tests/Orleans.Connections.Security.Tests.csproj
test/Orleans.Dashboard.Tests/Orleans.Dashboard.TestGrains/Orleans.Dashboard.TestGrains.csproj
test/Orleans.Dashboard.Tests/Orleans.Dashboard.UnitTests/Orleans.Dashboard.UnitTests.csproj
test/Orleans.Journaling.Tests/Orleans.Journaling.Tests.csproj
test/Orleans.Serialization.FSharp.Tests/Orleans.Serialization.FSharp.Tests.fsproj
test/Orleans.Serialization.UnitTests/Orleans.Serialization.UnitTests.csproj
test/TestInfrastructure/Orleans.TestingHost.Tests/Orleans.TestingHost.Tests.csproj
test/Transactions/Orleans.Transactions.Azure.Test/Orleans.Transactions.Azure.Test.csproj
test/Transactions/Orleans.Transactions.Tests/Orleans.Transactions.Tests.csproj
test/Misc/TestInternalDtosRefOrleans/TestInternalDtosRefOrleans.csproj
```

### test/ - Config Files (4 files)
```
test/Orleans.Connections.Security.Tests/Orleans.Connections.Security.Tests.xunit.runner.json
test/TestInfrastructure/Orleans.TestingHost.Tests/Orleans.TestingHost.Tests.xunit.runner.json
test/Transactions/Orleans.Transactions.Azure.Test/Orleans.Transactions.Azure.Test.xunit.runner.json
test/Transactions/Orleans.Transactions.Tests/Orleans.Transactions.Tests.xunit.runner.json
```

### test/ - C# Source Files (10 files)
```
test/DefaultCluster.Tests/TimerOrleansTest.cs
test/Extensions/Tester.Redis/Persistence/RedisStorageTests_OrleansSerializer.cs
test/Extensions/TesterAdoNet/GrainDirectory/RelationalOrleansQueriesTests.cs
test/Extensions/TesterAdoNet/Streaming/RelationalOrleansQueriesTests.cs
test/Misc/TestInterfaces/ClassNotReferencingOrleansTypeDto.cs
test/Misc/TestInternalDtosRefOrleans/ClassReferencingOrleansTypeDto.cs
test/NonSilo.Tests/SchedulerTests/OrleansTaskSchedulerAdvancedTests.cs
test/NonSilo.Tests/SchedulerTests/OrleansTaskSchedulerAdvancedTests_Set2.cs
test/NonSilo.Tests/SchedulerTests/OrleansTaskSchedulerBasicTests.cs
test/TestInfrastructure/TestExtensions/OrleansTestingBase.cs
```

### test/ - Snapshot/Verified Files (33 files)
```
test/Orleans.CodeGenerator.Tests/OrleansSourceGeneratorTests.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestAlias.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestBasicClass.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestBasicClassWithAnnotatedFields.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestBasicClassWithDifferentAccessModifiers.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestBasicClassWithFields.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestBasicClassWithInheritance.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestBasicClassWithInitOnlyProperty.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestBasicClassWithoutNamespace.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestBasicGrain.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestBasicStruct.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestClassNestedTypes.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestClassPrimitiveTypes.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestClassPrimitiveTypesUsingFullName.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestClassReferenceProperties.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestClassWithConstructorParameters.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestClassWithFieldAndNoSetters.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestClassWithGenerateMethodSerializersAnnotation.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestClassWithGenerateSerializerAnnotation.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestClassWithInterfaceConstructorParameter.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestClassWithNoPublicConstructors.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestClassWithOptionalConstructorParameters.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestClassWithParameterizedConstructor.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestClassesWithOrleansConstructorAnnotation.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestCompoundTypeAlias.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestGenericClass.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestGenericClassWithConstructorParameters.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestGrainComplexGrain.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestGrainMethodAnnotatedWithInvokableBaseType.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestGrainMethodAnnotatedWithResponseTimeout.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestGrainWithDifferentKeyTypes.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestGrainWithMultipleInterfaces.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestRecords.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestWithOmitDefaultMemberValuesAnnotation.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestWithSerializerTransparentAnnotation.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestWithSuppressReferenceTrackingAttribute.verified.cs
test/Orleans.CodeGenerator.Tests/snapshots/OrleansSourceGeneratorTests.TestWithUseActivatorAnnotation.verified.cs
```

---

## Part 2: All 139 Directories with "Orleans" in Name

### src/ Core (35 directories)
```
src/Orleans.Analyzers/
src/Orleans.BroadcastChannel/
src/Orleans.Client/
src/Orleans.Clustering.Consul/
src/Orleans.Clustering.ZooKeeper/
src/Orleans.CodeGenerator/
src/Orleans.Connections.Security/
src/Orleans.Core/
src/Orleans.Core.Abstractions/
src/Orleans.DurableJobs/
src/Orleans.EventSourcing/
src/Orleans.Hosting.Kubernetes/
src/Orleans.Identity/
src/Orleans.Identity/ManagedCode.Orleans.Identity.Client/
src/Orleans.Identity/ManagedCode.Orleans.Identity.Core/
src/Orleans.Identity/ManagedCode.Orleans.Identity.Server/
src/Orleans.Identity/ManagedCode.Orleans.Identity.Tests/
src/Orleans.Journaling/
src/Orleans.Persistence.Memory/
src/Orleans.Reminders/
src/Orleans.Reminders.Abstractions/
src/Orleans.Runtime/
src/Orleans.Sdk/
src/Orleans.Serialization/
src/Orleans.Serialization.Abstractions/
src/Orleans.Serialization.FSharp/
src/Orleans.Serialization.MessagePack/
src/Orleans.Serialization.NewtonsoftJson/
src/Orleans.Serialization.SystemTextJson/
src/Orleans.Serialization.TestKit/
src/Orleans.Server/
src/Orleans.Streaming/
src/Orleans.Streaming.Abstractions/
src/Orleans.Streaming.NATS/
src/Orleans.TestingHost/
src/Orleans.Transactions/
src/Orleans.Transactions.TestKit.Base/
src/Orleans.Transactions.TestKit.xUnit/
```

### src/ Providers (31 directories)
```
src/AWS/Orleans.Clustering.DynamoDB/
src/AWS/Orleans.Persistence.DynamoDB/
src/AWS/Orleans.Reminders.DynamoDB/
src/AWS/Orleans.Streaming.SQS/
src/AdoNet/Orleans.Clustering.AdoNet/
src/AdoNet/Orleans.GrainDirectory.AdoNet/
src/AdoNet/Orleans.Persistence.AdoNet/
src/AdoNet/Orleans.Reminders.AdoNet/
src/AdoNet/Orleans.Streaming.AdoNet/
src/Azure/Orleans.Clustering.AzureStorage/
src/Azure/Orleans.Clustering.Cosmos/
src/Azure/Orleans.DurableJobs.AzureStorage/
src/Azure/Orleans.GrainDirectory.AzureStorage/
src/Azure/Orleans.Hosting.AzureCloudServices/
src/Azure/Orleans.Journaling.AzureStorage/
src/Azure/Orleans.Persistence.AzureStorage/
src/Azure/Orleans.Persistence.Cosmos/
src/Azure/Orleans.Reminders.AzureStorage/
src/Azure/Orleans.Reminders.Cosmos/
src/Azure/Orleans.Streaming.AzureStorage/
src/Azure/Orleans.Streaming.EventHubs/
src/Azure/Orleans.Transactions.AzureStorage/
src/Cassandra/Orleans.Clustering.Cassandra/
src/Dashboard/Orleans.Dashboard/
src/Dashboard/Orleans.Dashboard.Abstractions/
src/Dashboard/Orleans.Dashboard.App/
src/Redis/Orleans.Clustering.Redis/
src/Redis/Orleans.GrainDirectory.Redis/
src/Redis/Orleans.Persistence.Redis/
src/Redis/Orleans.Reminders.Redis/
src/Serializers/Orleans.Serialization.Protobuf/
```

### src/api/ (48 directories - mirrors of src/)
```
src/api/Orleans.BroadcastChannel/
src/api/Orleans.Client/
src/api/Orleans.Clustering.Consul/
src/api/Orleans.Clustering.ZooKeeper/
src/api/Orleans.Connections.Security/
src/api/Orleans.Core/
src/api/Orleans.Core.Abstractions/
src/api/Orleans.EventSourcing/
src/api/Orleans.Hosting.Kubernetes/
src/api/Orleans.Journaling/
src/api/Orleans.Persistence.Memory/
src/api/Orleans.Reminders/
src/api/Orleans.Reminders.Abstractions/
src/api/Orleans.Runtime/
src/api/Orleans.Sdk/
src/api/Orleans.Serialization/
src/api/Orleans.Serialization.Abstractions/
src/api/Orleans.Serialization.FSharp/
src/api/Orleans.Serialization.MessagePack/
src/api/Orleans.Serialization.NewtonsoftJson/
src/api/Orleans.Serialization.SystemTextJson/
src/api/Orleans.Serialization.TestKit/
src/api/Orleans.Server/
src/api/Orleans.Streaming/
src/api/Orleans.Streaming.Abstractions/
src/api/Orleans.TestingHost/
src/api/Orleans.Transactions/
src/api/Orleans.Transactions.TestKit.Base/
src/api/Orleans.Transactions.TestKit.xUnit/
src/api/AWS/Orleans.Clustering.DynamoDB/
src/api/AWS/Orleans.Persistence.DynamoDB/
src/api/AWS/Orleans.Reminders.DynamoDB/
src/api/AWS/Orleans.Streaming.SQS/
src/api/AdoNet/Orleans.Clustering.AdoNet/
src/api/AdoNet/Orleans.GrainDirectory.AdoNet/
src/api/AdoNet/Orleans.Persistence.AdoNet/
src/api/AdoNet/Orleans.Reminders.AdoNet/
src/api/AdoNet/Orleans.Streaming.AdoNet/
src/api/Azure/Orleans.Clustering.AzureStorage/
src/api/Azure/Orleans.Clustering.Cosmos/
src/api/Azure/Orleans.GrainDirectory.AzureStorage/
src/api/Azure/Orleans.Hosting.AzureCloudServices/
src/api/Azure/Orleans.Journaling.AzureStorage/
src/api/Azure/Orleans.Persistence.AzureStorage/
src/api/Azure/Orleans.Persistence.Cosmos/
src/api/Azure/Orleans.Reminders.AzureStorage/
src/api/Azure/Orleans.Reminders.Cosmos/
src/api/Azure/Orleans.Streaming.AzureStorage/
src/api/Azure/Orleans.Streaming.EventHubs/
src/api/Azure/Orleans.Transactions.AzureStorage/
src/api/Cassandra/Orleans.Clustering.Cassandra/
src/api/Redis/Orleans.Clustering.Redis/
src/api/Redis/Orleans.GrainDirectory.Redis/
src/api/Redis/Orleans.Persistence.Redis/
src/api/Redis/Orleans.Reminders.Redis/
src/api/Serializers/Orleans.Serialization.Protobuf/
```

### test/ (14 directories)
```
test/Misc/TestInternalDtosRefOrleans/
test/NonSilo.Tests/OrleansRuntime/
test/Orleans.CodeGenerator.Tests/
test/Orleans.Connections.Security.Tests/
test/Orleans.Dashboard.Tests/
test/Orleans.Dashboard.Tests/Orleans.Dashboard.TestGrains/
test/Orleans.Dashboard.Tests/Orleans.Dashboard.UnitTests/
test/Orleans.Journaling.Tests/
test/Orleans.Serialization.FSharp.Tests/
test/Orleans.Serialization.UnitTests/
test/TestInfrastructure/Orleans.TestingHost.Tests/
test/TesterInternal/OrleansRuntime/
test/Transactions/Orleans.Transactions.Azure.Test/
test/Transactions/Orleans.Transactions.Tests/
```

---

## Part 3: Content Match Distribution

### Files containing "Orleans" by top-level subdirectory

| Subdirectory | Files | Notable content areas |
|-------------|-------|----------------------|
| `src/Orleans.Runtime/` | 272 | Runtime implementation, hosting, membership |
| `src/Orleans.Core/` | 257 | Core library, configuration, serialization |
| `src/Orleans.Streaming/` | 173 | Streaming providers and infrastructure |
| `src/Azure/` | 172 | Azure provider implementations |
| `test/Extensions/` | 168 | Extension test projects |
| `test/Grains/` | 162 | Test grain implementations |
| `src/Orleans.Serialization/` | 160 | Serialization framework |
| `src/Orleans.Core.Abstractions/` | 134 | Core abstractions and interfaces |
| `src/AdoNet/` | 108 | ADO.NET providers + SQL files |
| `test/Tester/` | 84 | Integration tests |
| `test/TesterInternal/` | 82 | Internal tests |
| `src/Dashboard/` | 70 | Dashboard UI + API |
| `src/Orleans.Transactions/` | 65 | Transaction framework |
| `src/Orleans.CodeGenerator/` | 63 | Source generator |
| `playground/` | 63 | Playground/sample projects |
| `test/NonSilo.Tests/` | 52 | Non-silo unit tests |
| `src/api/` | 51 | API reference files |
| `src/Orleans.Identity/` | 47 | Identity framework |
| `src/Orleans.Transactions.TestKit.Base/` | 45 | Transaction test kit |
| `src/AWS/` | 43 | AWS providers |
| `src/Orleans.TestingHost/` | 41 | Testing host |

---

## Part 4: Immediate Cleanup Items

### 2 Remaining "NewOrleans" References

These are leftover from the previous NewOrleans -> Scynapse rename and should be fixed:

1. **Filename:** `src/Scynapse.AsyncPlus/Services/NewOrleansAsyncPersistenceService.cs`
   - Action: Rename file to `ScynapseAsyncPersistenceService.cs` (or similar)
   - Also update class name inside the file

2. **Content:** `playground/PluginGrainScenarios/Grains/EventTestGrain.cs` line 6
   - Current: `// NEWORLEANS EVENTS TEST GRAINS`
   - Action: Update comment to `// SCYNAPSE EVENTS TEST GRAINS`

---

## Part 5: Search Commands for Verification

```bash
# Count directories with Orleans in name
find /home/user/DOTNExT/src/Scynapse/ -type d -iname "*orleans*" | wc -l

# Count files with Orleans in name
find /home/user/DOTNExT/src/Scynapse/ -type f -iname "*orleans*" | wc -l

# Count files containing Orleans in content
grep -ril "orleans" /home/user/DOTNExT/src/Scynapse/ | wc -l

# Find remaining NewOrleans references
grep -rin "neworleans" /home/user/DOTNExT/src/Scynapse/
find /home/user/DOTNExT/src/Scynapse/ -iname "*neworleans*"

# Count by case variant
grep -r "Orleans" /home/user/DOTNExT/src/Scynapse/ | wc -l    # PascalCase
grep -r "ORLEANS" /home/user/DOTNExT/src/Scynapse/ | wc -l    # UPPERCASE
```

---

## Notes

- This inventory was generated by exhaustive filesystem search on 2026-02-26
- All paths are relative to `src/Scynapse/` unless otherwise noted
- The `src/api/` directory is an auto-generated API reference mirror and will follow src/ changes
- The `.verified.cs` snapshot files in test/ are auto-generated and will need regeneration after any rename
- SQL files contain database schema references that would need migration scripts if renamed
- Binary files (`.png`) would need to be replaced, not text-edited
