using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Orleans.Hosting;
using Orleans.Runtime.DynamicGrains;
using Orleans.TestingHost;

namespace TestExtensions
{
    /// <summary>
    /// Base test cluster fixture that loads grain plugin assemblies after host build.
    /// This ensures that plugin grain loading is always tested as part of the test infrastructure.
    /// </summary>
    public abstract class PluginLoadingTestClusterFixture : BaseTestClusterFixture
    {
        /// <summary>
        /// Gets the list of grain assembly names to load dynamically.
        /// By default, loads the main TestGrains assembly.
        /// Override to specify different assemblies.
        /// </summary>
        protected virtual IEnumerable<string> GetGrainAssemblyNames()
        {
            yield return "TestGrains.dll";
        }

        /// <summary>
        /// Gets the search paths for grain assemblies.
        /// By default, searches in the test/Grains directory structure.
        /// </summary>
        protected virtual IEnumerable<string> GetAssemblySearchPaths()
        {
            var baseDir = AppContext.BaseDirectory;

            // Try to find the Orleans repository root
            var currentDir = new DirectoryInfo(baseDir);
            while (currentDir != null && !File.Exists(Path.Combine(currentDir.FullName, "Orleans.slnx")))
            {
                currentDir = currentDir.Parent;
            }

            if (currentDir != null)
            {
                var testGrainsDir = Path.Combine(currentDir.FullName, "test", "Grains");
                if (Directory.Exists(testGrainsDir))
                {
                    // Search all subdirectories for bin folders
                    foreach (var binDir in Directory.GetDirectories(testGrainsDir, "bin", SearchOption.AllDirectories))
                    {
                        yield return binDir;

                        // Also check Debug/Release subdirectories
                        var debugDir = Path.Combine(binDir, "Debug");
                        if (Directory.Exists(debugDir))
                        {
                            foreach (var netDir in Directory.GetDirectories(debugDir, "net*"))
                            {
                                yield return netDir;
                            }
                        }

                        var releaseDir = Path.Combine(binDir, "Release");
                        if (Directory.Exists(releaseDir))
                        {
                            foreach (var netDir in Directory.GetDirectories(releaseDir, "net*"))
                            {
                                yield return netDir;
                            }
                        }
                    }
                }
            }

            // Also try the base directory itself
            yield return baseDir;
        }

        /// <summary>
        /// Finds the full path to a grain assembly.
        /// </summary>
        protected virtual string FindAssemblyPath(string assemblyName)
        {
            foreach (var searchPath in GetAssemblySearchPaths())
            {
                var fullPath = Path.Combine(searchPath, assemblyName);
                if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            return null;
        }

        protected override void ConfigureTestCluster(TestClusterBuilder builder)
        {
            base.ConfigureTestCluster(builder);
            // Plugin grain loading is now enabled by default - no explicit configuration needed
        }

        public override async Task InitializeAsync()
        {
            // First, build and deploy the cluster normally
            await base.InitializeAsync();

            // Then, dynamically load grain assemblies
            await LoadGrainAssembliesDynamically();
        }

        /// <summary>
        /// Loads grain assemblies dynamically after the cluster has started.
        /// This method loads assemblies on all silos in the cluster.
        /// </summary>
        protected virtual async Task LoadGrainAssembliesDynamically()
        {
            var assembliesToLoad = new List<string>();

            // Find all assembly paths
            foreach (var assemblyName in GetGrainAssemblyNames())
            {
                var assemblyPath = FindAssemblyPath(assemblyName);
                if (assemblyPath == null)
                {
                    Logger?.LogWarning("Could not find grain assembly: {AssemblyName}. Skipping dynamic load.", assemblyName);
                    continue;
                }

                assembliesToLoad.Add(assemblyPath);
                Logger?.LogInformation("Found grain assembly for dynamic loading: {AssemblyPath}", assemblyPath);
            }

            if (assembliesToLoad.Count == 0)
            {
                Logger?.LogWarning("No grain assemblies found for dynamic loading. Tests may fail.");
                return;
            }

            // Load assemblies on all silos
            foreach (var silo in HostedCluster.Silos)
            {
                // Skip standalone (out-of-process) silos as they don't expose IServiceProvider
                // and cannot be accessed for dynamic loading. Standalone silos are used by tests
                // that require process isolation (e.g., ManagementGrainTests for accurate statistics).
                if (silo is not InProcessSiloHandle)
                {
                    Logger?.LogWarning(
                        "Skipping dynamic grain loading for silo {SiloName} ({SiloType}). " +
                        "Dynamic loading is only supported for in-process silos. " +
                        "Standalone silos running in separate processes should pre-configure grain assemblies via builder.Properties[\"GrainAssembly\"].",
                        silo.Name, silo.GetType().Name);
                    continue;
                }

                var serviceProvider = HostedCluster.GetSiloServiceProvider(silo.SiloAddress);
                var grainLoader = serviceProvider.GetRequiredService<IPluginGrainLoader>();

                foreach (var assemblyPath in assembliesToLoad)
                {
                    Logger?.LogInformation("Loading {AssemblyPath} on silo {SiloName}...", assemblyPath, silo.Name);

                    var result = await grainLoader.LoadGrainAssemblyAsync(assemblyPath);

                    if (!result.Success)
                    {
                        var errors = string.Join(", ", result.Errors);
                        throw new InvalidOperationException(
                            $"Failed to dynamically load assembly {assemblyPath} on silo {silo.Name}: {errors}");
                    }

                    Logger?.LogInformation(
                        "Successfully loaded {AssemblyPath} on silo {SiloName}. Grain types: {GrainTypeCount}, Duration: {Duration}ms",
                        assemblyPath, silo.Name, result.GrainTypes.Count, result.LoadDuration.TotalMilliseconds);
                }
            }

            // Give the cluster a moment to propagate manifests
            await Task.Delay(500);

            Logger?.LogInformation("Dynamic grain loading complete for all silos.");
        }
    }
}
