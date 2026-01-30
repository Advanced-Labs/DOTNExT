// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;

// Note: Voron namespace will be available when Voron.dll is deployed
// TAI: Ensure Voron.dll and Sparrow.dll are copied to Core_Root
#pragma warning disable CS8019 // Unnecessary using directive (Voron not yet available)
// using Voron;
// using Voron.Impl;
#pragma warning restore CS8019

namespace System.OS.Storage
{
    /// <summary>
    /// Voron storage environment wrapper for VAYRON.
    /// Manages the lifecycle of the Voron storage backend.
    ///
    /// Phase 2: This class wraps Voron's StorageEnvironment to provide
    /// durable storage for virtual objects.
    /// </summary>
    internal sealed class VoronStorage : IDisposable
    {
        private static VoronStorage? s_instance;
        private static readonly object s_lock = new();

        private readonly string _dataPath;
        private readonly object _environment;  // Voron.StorageEnvironment when Voron is loaded
        private bool _disposed;

        /// <summary>
        /// Get the singleton VoronStorage instance.
        /// Initializes on first access.
        /// </summary>
        public static VoronStorage Instance
        {
            get
            {
                if (s_instance == null)
                {
                    lock (s_lock)
                    {
                        s_instance ??= new VoronStorage(GetDefaultDataPath());
                    }
                }
                return s_instance;
            }
        }

        /// <summary>
        /// Check if VoronStorage has been initialized.
        /// </summary>
        public static bool IsInitialized => s_instance != null;

        private VoronStorage(string dataPath)
        {
            _dataPath = dataPath;
            Directory.CreateDirectory(_dataPath);

            // Initialize Voron storage environment
            // TAI: This will be implemented when Voron.dll is available
            _environment = InitializeVoronEnvironment(dataPath);

            InitializeTrees();
        }

        /// <summary>
        /// Initialize Voron StorageEnvironment.
        /// Uses reflection to avoid compile-time dependency on Voron.dll.
        /// </summary>
        private static object InitializeVoronEnvironment(string dataPath)
        {
            // Load Voron assembly dynamically
            var voronAssembly = System.Reflection.Assembly.Load("Voron");

            // Get StorageEnvironmentOptions.ForPath method
            var optionsType = voronAssembly.GetType("Voron.StorageEnvironmentOptions")
                ?? throw new InvalidOperationException("Cannot find Voron.StorageEnvironmentOptions type");

            var forPathMethod = optionsType.GetMethod("ForPath", new[] { typeof(string) })
                ?? throw new InvalidOperationException("Cannot find StorageEnvironmentOptions.ForPath method");

            var options = forPathMethod.Invoke(null, new object[] { dataPath })
                ?? throw new InvalidOperationException("ForPath returned null");

            // Set initial options
            var initialFileSizeProp = optionsType.GetProperty("InitialFileSize");
            initialFileSizeProp?.SetValue(options, 64L * 1024 * 1024);  // 64MB

            var maxLogFileSizeProp = optionsType.GetProperty("MaxLogFileSize");
            maxLogFileSizeProp?.SetValue(options, 256L * 1024 * 1024);  // 256MB journal

            // Create StorageEnvironment
            var envType = voronAssembly.GetType("Voron.StorageEnvironment")
                ?? throw new InvalidOperationException("Cannot find Voron.StorageEnvironment type");

            var env = Activator.CreateInstance(envType, options)
                ?? throw new InvalidOperationException("Failed to create StorageEnvironment");

            return env;
        }

        /// <summary>
        /// Initialize required trees on first startup.
        /// </summary>
        private void InitializeTrees()
        {
            using var tx = WriteTransaction();

            // Create required trees
            CreateTree(tx, "vobjects");    // Main VObject storage
            CreateTree(tx, "typeIndex");   // Type -> VUIDs index
            CreateTree(tx, "metadata");    // Runtime metadata

            Commit(tx);
        }

        /// <summary>
        /// Get the underlying Voron StorageEnvironment.
        /// </summary>
        public object Environment => _environment;

        /// <summary>
        /// Create a read transaction.
        /// </summary>
        public object ReadTransaction()
        {
            ThrowIfDisposed();
            var method = _environment.GetType().GetMethod("ReadTransaction", Type.EmptyTypes)
                ?? throw new InvalidOperationException("Cannot find ReadTransaction method");
            return method.Invoke(_environment, null)
                ?? throw new InvalidOperationException("ReadTransaction returned null");
        }

        /// <summary>
        /// Create a write transaction.
        /// </summary>
        public object WriteTransaction()
        {
            ThrowIfDisposed();
            var method = _environment.GetType().GetMethod("WriteTransaction", Type.EmptyTypes)
                ?? throw new InvalidOperationException("Cannot find WriteTransaction method");
            return method.Invoke(_environment, null)
                ?? throw new InvalidOperationException("WriteTransaction returned null");
        }

        /// <summary>
        /// Create or get a tree within a transaction.
        /// </summary>
        public object CreateTree(object transaction, string name)
        {
            var method = transaction.GetType().GetMethod("CreateTree", new[] { typeof(string) })
                ?? throw new InvalidOperationException("Cannot find CreateTree method");
            return method.Invoke(transaction, new object[] { name })
                ?? throw new InvalidOperationException("CreateTree returned null");
        }

        /// <summary>
        /// Read a tree within a transaction.
        /// </summary>
        public object? ReadTree(object transaction, string name)
        {
            var method = transaction.GetType().GetMethod("ReadTree", new[] { typeof(string) })
                ?? throw new InvalidOperationException("Cannot find ReadTree method");
            return method.Invoke(transaction, new object[] { name });
        }

        /// <summary>
        /// Commit a transaction.
        /// </summary>
        public void Commit(object transaction)
        {
            var method = transaction.GetType().GetMethod("Commit", Type.EmptyTypes)
                ?? throw new InvalidOperationException("Cannot find Commit method");
            method.Invoke(transaction, null);
        }

        /// <summary>
        /// Dispose a transaction.
        /// </summary>
        public void DisposeTransaction(object transaction)
        {
            if (transaction is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        /// <summary>
        /// Get the default data path for VAYRON storage.
        /// </summary>
        private static string GetDefaultDataPath()
        {
            // Check environment variable first
            var envPath = System.Environment.GetEnvironmentVariable("VAYRON_DATA_PATH");
            if (!string.IsNullOrEmpty(envPath))
                return envPath;

            // Default: ./vayron-data/ in application base directory
            return Path.Combine(AppContext.BaseDirectory, "vayron-data");
        }

        /// <summary>
        /// Get the data path for this storage instance.
        /// </summary>
        public string DataPath => _dataPath;

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_environment is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                _disposed = true;
            }
        }

        /// <summary>
        /// Initialize VoronStorage explicitly.
        /// Called by VKernel.Initialize().
        /// </summary>
        internal static void Initialize()
        {
            // Accessing Instance triggers initialization
            _ = Instance;
        }

        /// <summary>
        /// Shutdown VoronStorage and release resources.
        /// Called on process exit.
        /// </summary>
        internal static void Shutdown()
        {
            lock (s_lock)
            {
                s_instance?.Dispose();
                s_instance = null;
            }
        }
    }
}
