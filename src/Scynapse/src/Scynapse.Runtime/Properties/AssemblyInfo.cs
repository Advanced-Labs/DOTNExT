using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Scynapse.Streaming")]
[assembly: InternalsVisibleTo("Scynapse.Reminders")]
[assembly: InternalsVisibleTo("Scynapse.DurableJobs")]
[assembly: InternalsVisibleTo("Scynapse.Journaling")]
[assembly: InternalsVisibleTo("Scynapse.TestingHost")]

[assembly: InternalsVisibleTo("AWSUtils.Tests")]
[assembly: InternalsVisibleTo("LoadTestGrains")]
[assembly: InternalsVisibleTo("NonSilo.Tests")]
[assembly: InternalsVisibleTo("Tester.AzureUtils")]
[assembly: InternalsVisibleTo("Tester.AdoNet")]
[assembly: InternalsVisibleTo("TesterInternal")]
[assembly: InternalsVisibleTo("TestInternalGrains")]
[assembly: InternalsVisibleTo("Benchmarks")]

// Mocking libraries
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2")]
