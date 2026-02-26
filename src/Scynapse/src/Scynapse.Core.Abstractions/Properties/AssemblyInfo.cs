using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Scynapse.Core")]
[assembly: InternalsVisibleTo("Scynapse.Runtime")]
[assembly: InternalsVisibleTo("Scynapse.TestingHost")]
[assembly: InternalsVisibleTo("Scynapse.Streaming")]
[assembly: InternalsVisibleTo("Scynapse.Streaming.Abstractions")]
[assembly: InternalsVisibleTo("Scynapse.Reminders")]

[assembly: InternalsVisibleTo("DefaultCluster.Tests")]
[assembly: InternalsVisibleTo("NonSilo.Tests")]
[assembly: InternalsVisibleTo("ServiceBus.Tests")]
[assembly: InternalsVisibleTo("Tester.AzureUtils")]
[assembly: InternalsVisibleTo("AWSUtils.Tests")]
[assembly: InternalsVisibleTo("TesterInternal")]
[assembly: InternalsVisibleTo("TestInternalGrainInterfaces")]
[assembly: InternalsVisibleTo("TestInternalGrains")]

// Mocking libraries
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
