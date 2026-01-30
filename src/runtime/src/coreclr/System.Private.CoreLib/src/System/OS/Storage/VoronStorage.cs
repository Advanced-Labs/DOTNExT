// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
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
    [RequiresUnreferencedCode("Voron types are loaded dynamically via reflection to avoid compile-time dependency")]
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

            // Get StorageEnvironmentOptions.ForPathForTests method (simpler API for testing/dev)
            var optionsType = voronAssembly.GetType("Voron.StorageEnvironmentOptions")
                ?? throw new InvalidOperationException("Cannot find Voron.StorageEnvironmentOptions type");

            // ForPathForTests has optional params: (string path, LoggingResource? = null, LoggingComponent? = null)
            // Find the method by name and invoke with just the path, relying on default params
            var forPathMethod = optionsType.GetMethod("ForPathForTests")
                ?? throw new InvalidOperationException("Cannot find StorageEnvironmentOptions.ForPathForTests method");

            var options = forPathMethod.Invoke(null, new object?[] { dataPath, null, null })
                ?? throw new InvalidOperationException("ForPathForTests returned null");

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
            object? tx = null;
            try
            {
                tx = WriteTransaction();

                // Create required trees
                CreateTree(tx, "vobjects");    // Main VObject storage
                CreateTree(tx, "typeIndex");   // Type -> VUIDs index
                CreateTree(tx, "metadata");    // Runtime metadata

                Commit(tx);
            }
            finally
            {
                DisposeTransaction(tx);
            }
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
            // Voron has multiple ReadTransaction overloads - we want the 2-param version:
            // ReadTransaction(ByteStringContext context = null, TimeSpan? timeout = null)
            // NOT the 3-param version that requires TransactionPersistentContext
            var methods = _environment.GetType().GetMethods();
            System.Reflection.MethodInfo? method = null;
            int minParams = int.MaxValue;
            foreach (var m in methods)
            {
                if (m.Name == "ReadTransaction" && m.GetParameters().Length < minParams)
                {
                    method = m;
                    minParams = m.GetParameters().Length;
                }
            }
            if (method == null)
                throw new InvalidOperationException("Cannot find ReadTransaction method");

            // Use actual default values, not null (enums become 0 which may be invalid)
            var parameters = method.GetParameters();
            var args = parameters.Length > 0 ? new object?[parameters.Length] : null;
            if (args != null)
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : Type.Missing;
                }
            }
            return method.Invoke(_environment, args)
                ?? throw new InvalidOperationException("ReadTransaction returned null");
        }

        /// <summary>
        /// Create a write transaction.
        /// </summary>
        public object WriteTransaction()
        {
            ThrowIfDisposed();
            // Voron has multiple WriteTransaction overloads - we want the 2-param version:
            // WriteTransaction(ByteStringContext context = null, TimeSpan? timeout = null)
            // NOT the 3-param version that requires TransactionPersistentContext
            var methods = _environment.GetType().GetMethods();
            System.Reflection.MethodInfo? method = null;
            int minParams = int.MaxValue;
            foreach (var m in methods)
            {
                if (m.Name == "WriteTransaction" && m.GetParameters().Length < minParams)
                {
                    method = m;
                    minParams = m.GetParameters().Length;
                }
            }
            if (method == null)
                throw new InvalidOperationException("Cannot find WriteTransaction method");

            // Use actual default values, not null (enums become 0 which may be invalid)
            var parameters = method.GetParameters();
            var args = parameters.Length > 0 ? new object?[parameters.Length] : null;
            if (args != null)
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : Type.Missing;
                }
            }
            return method.Invoke(_environment, args)
                ?? throw new InvalidOperationException("WriteTransaction returned null");
        }

        /// <summary>
        /// Create or get a tree within a transaction.
        /// </summary>
        public static object CreateTree(object transaction, string name)
        {
            // Voron's CreateTree has optional params: (string name, RootObjectType type = ..., TreeFlags flags = ..., ...)
            // Find by name with first param being string, pick the one with fewest params
            var methods = transaction.GetType().GetMethods();
            System.Reflection.MethodInfo? method = null;
            int minParams = int.MaxValue;
            foreach (var m in methods)
            {
                if (m.Name == "CreateTree" && m.GetParameters().Length > 0
                    && m.GetParameters()[0].ParameterType == typeof(string)
                    && m.GetParameters().Length < minParams)
                {
                    method = m;
                    minParams = m.GetParameters().Length;
                }
            }
            if (method == null)
                throw new InvalidOperationException("Cannot find CreateTree method");

            // Build args with actual default values (not null - enums become 0 which is invalid)
            var parameters = method.GetParameters();
            var args = new object?[parameters.Length];
            args[0] = name;  // First param is the tree name
            for (int i = 1; i < parameters.Length; i++)
            {
                args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : Type.Missing;
            }
            return method.Invoke(transaction, args)
                ?? throw new InvalidOperationException("CreateTree returned null");
        }

        /// <summary>
        /// Read a tree within a transaction.
        /// </summary>
        public static object? ReadTree(object transaction, string name)
        {
            // Voron's ReadTree may have optional params - find by name with string first param
            var methods = transaction.GetType().GetMethods();
            System.Reflection.MethodInfo? method = null;
            int minParams = int.MaxValue;
            foreach (var m in methods)
            {
                if (m.Name == "ReadTree" && m.GetParameters().Length > 0
                    && m.GetParameters()[0].ParameterType == typeof(string)
                    && m.GetParameters().Length < minParams)
                {
                    method = m;
                    minParams = m.GetParameters().Length;
                }
            }
            if (method == null)
                throw new InvalidOperationException("Cannot find ReadTree method");

            // Build args with actual default values
            var parameters = method.GetParameters();
            var args = new object?[parameters.Length];
            args[0] = name;  // First param is the tree name
            for (int i = 1; i < parameters.Length; i++)
            {
                args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : Type.Missing;
            }
            return method.Invoke(transaction, args);
        }

        /// <summary>
        /// Commit a transaction.
        /// </summary>
        public static void Commit(object transaction)
        {
            // Find Commit method - may have optional parameters
            var methods = transaction.GetType().GetMethods();
            System.Reflection.MethodInfo? method = null;
            int minParams = int.MaxValue;
            foreach (var m in methods)
            {
                if (m.Name == "Commit" && m.GetParameters().Length < minParams)
                {
                    method = m;
                    minParams = m.GetParameters().Length;
                }
            }
            if (method == null)
                throw new InvalidOperationException("Cannot find Commit method");

            // Use actual default values
            var parameters = method.GetParameters();
            var args = parameters.Length > 0 ? new object?[parameters.Length] : null;
            if (args != null)
            {
                for (int i = 0; i < parameters.Length; i++)
                {
                    args[i] = parameters[i].HasDefaultValue ? parameters[i].DefaultValue : Type.Missing;
                }
            }
            method.Invoke(transaction, args);
        }

        /// <summary>
        /// Dispose a transaction.
        /// </summary>
        public static void DisposeTransaction(object? transaction)
        {
            if (transaction is IDisposable disposable)
            {
                disposable.Dispose();
            }
        }

        #region Tree Operations

        /// <summary>
        /// Find a suitable Slice.From method that accepts byte data.
        /// Voron uses: From(ByteStringContext, ReadOnlySpan&lt;byte&gt;, out Slice)
        /// or: From(ByteStringContext, string, out Slice)
        /// </summary>
        private static System.Reflection.MethodInfo? FindSliceFromMethodForBytes(Type sliceType, Type allocatorType)
        {
            var methods = sliceType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);

            // Priority 1: Look for ReadOnlySpan<byte> overload
            // Signature: From(ByteStringContext, ReadOnlySpan<byte>, out Slice)
            var spanType = typeof(ReadOnlySpan<byte>);
            foreach (var m in methods)
            {
                if (m.Name != "From")
                    continue;
                var parameters = m.GetParameters();
                // Look for 3-param version: (context, ReadOnlySpan<byte>, out Slice)
                if (parameters.Length == 3
                    && parameters[0].ParameterType.IsAssignableFrom(allocatorType)
                    && parameters[1].ParameterType == spanType
                    && parameters[2].IsOut)
                {
                    return m;
                }
            }

            // Priority 2: Look for string overload
            // Signature: From(ByteStringContext, string, out Slice)
            foreach (var m in methods)
            {
                if (m.Name != "From")
                    continue;
                var parameters = m.GetParameters();
                if (parameters.Length == 3
                    && parameters[0].ParameterType.IsAssignableFrom(allocatorType)
                    && parameters[1].ParameterType == typeof(string)
                    && parameters[2].IsOut)
                {
                    return m;
                }
            }

            return null;
        }

        /// <summary>
        /// Create a Slice from byte data using reflection.
        /// Voron's Slice.From returns InternalScope and outputs Slice via out param.
        /// </summary>
        private static object CreateSlice(Type sliceType, object allocator, byte[] data)
        {
            var fromMethod = FindSliceFromMethodForBytes(sliceType, allocator.GetType())
                ?? throw new InvalidOperationException("Cannot find suitable Slice.From method for byte data");

            var parameters = fromMethod.GetParameters();
            var secondParamType = parameters[1].ParameterType;

            // Prepare arguments array - third param is out Slice
            object?[] args = new object?[3];
            args[0] = allocator;
            args[2] = null; // out parameter placeholder

            if (secondParamType == typeof(ReadOnlySpan<byte>))
            {
                // Convert byte[] to ReadOnlySpan<byte>
                // We need to box it properly for reflection
                args[1] = new ReadOnlySpan<byte>(data).ToArray(); // This won't work directly...

                // Actually, we can't pass ReadOnlySpan via reflection easily.
                // Fall back to string overload which is more reflection-friendly
                var stringMethod = FindStringSliceFromMethod(sliceType, allocator.GetType());
                if (stringMethod != null)
                {
                    args[1] = System.Text.Encoding.UTF8.GetString(data);
                    stringMethod.Invoke(null, args);
                    return args[2] ?? throw new InvalidOperationException("Slice.From did not return slice via out param");
                }

                throw new InvalidOperationException("Cannot invoke Slice.From with ReadOnlySpan<byte> via reflection. String overload not available.");
            }
            else if (secondParamType == typeof(string))
            {
                args[1] = System.Text.Encoding.UTF8.GetString(data);
                fromMethod.Invoke(null, args);
                return args[2] ?? throw new InvalidOperationException("Slice.From did not return slice via out param");
            }

            throw new InvalidOperationException($"Unsupported Slice.From parameter type: {secondParamType}");
        }

        /// <summary>
        /// Find string overload of Slice.From specifically.
        /// </summary>
        private static System.Reflection.MethodInfo? FindStringSliceFromMethod(Type sliceType, Type allocatorType)
        {
            var methods = sliceType.GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            foreach (var m in methods)
            {
                if (m.Name != "From")
                    continue;
                var parameters = m.GetParameters();
                if (parameters.Length == 3
                    && parameters[0].ParameterType.IsAssignableFrom(allocatorType)
                    && parameters[1].ParameterType == typeof(string)
                    && parameters[2].IsOut)
                {
                    return m;
                }
            }
            return null;
        }

        /// <summary>
        /// Add a key-value pair to a tree.
        /// </summary>
        /// <param name="tree">The tree object (from CreateTree/ReadTree)</param>
        /// <param name="key">Key bytes</param>
        /// <param name="value">Value bytes</param>
        public static void TreeAdd(object tree, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
        {
            var treeType = tree.GetType();

            // Get LowLevelTransaction for allocator access
            var lltxProp = treeType.GetProperty("Llt")
                ?? throw new InvalidOperationException("Cannot find Llt property on Tree");
            var lltx = lltxProp.GetValue(tree)
                ?? throw new InvalidOperationException("Llt is null");
            var allocatorProp = lltx.GetType().GetProperty("Allocator")
                ?? throw new InvalidOperationException("Cannot find Allocator property");
            var allocator = allocatorProp.GetValue(lltx)
                ?? throw new InvalidOperationException("Allocator is null");

            // Create Slices from byte arrays
            var sliceType = treeType.Assembly.GetType("Voron.Slice")
                ?? throw new InvalidOperationException("Cannot find Voron.Slice type");

            var keySlice = CreateSlice(sliceType, allocator, key.ToArray());
            var valueSlice = CreateSlice(sliceType, allocator, value.ToArray());

            // Call Tree.Add - find by name since there may be multiple overloads
            var methods = treeType.GetMethods();
            System.Reflection.MethodInfo? addMethod = null;
            foreach (var m in methods)
            {
                if (m.Name == "Add")
                {
                    var ps = m.GetParameters();
                    if (ps.Length >= 2 && ps[0].ParameterType == sliceType && ps[1].ParameterType == sliceType)
                    {
                        addMethod = m;
                        break;
                    }
                }
            }
            if (addMethod == null)
                throw new InvalidOperationException("Cannot find Tree.Add method");

            var addParams = addMethod.GetParameters();
            var addArgs = new object?[addParams.Length];
            addArgs[0] = keySlice;
            addArgs[1] = valueSlice;
            for (int i = 2; i < addParams.Length; i++)
                addArgs[i] = addParams[i].HasDefaultValue ? addParams[i].DefaultValue : Type.Missing;

            addMethod.Invoke(tree, addArgs);
        }

        /// <summary>
        /// Read a value from a tree by key.
        /// </summary>
        /// <param name="tree">The tree object</param>
        /// <param name="key">Key bytes</param>
        /// <returns>Value bytes, or null if not found</returns>
        public static byte[]? TreeRead(object tree, ReadOnlySpan<byte> key)
        {
            var treeType = tree.GetType();

            // Get allocator
            var lltxProp = treeType.GetProperty("Llt")
                ?? throw new InvalidOperationException("Cannot find Llt property on Tree");
            var lltx = lltxProp.GetValue(tree)
                ?? throw new InvalidOperationException("Llt is null");
            var allocatorProp = lltx.GetType().GetProperty("Allocator")
                ?? throw new InvalidOperationException("Cannot find Allocator property");
            var allocator = allocatorProp.GetValue(lltx)
                ?? throw new InvalidOperationException("Allocator is null");

            // Create key Slice
            var sliceType = treeType.Assembly.GetType("Voron.Slice")
                ?? throw new InvalidOperationException("Cannot find Voron.Slice type");
            var keySlice = CreateSlice(sliceType, allocator, key.ToArray());

            // Call Tree.Read - find by name
            var methods = treeType.GetMethods();
            System.Reflection.MethodInfo? readMethod = null;
            foreach (var m in methods)
            {
                if (m.Name == "Read")
                {
                    var ps = m.GetParameters();
                    if (ps.Length >= 1 && ps[0].ParameterType == sliceType)
                    {
                        readMethod = m;
                        break;
                    }
                }
            }
            if (readMethod == null)
                throw new InvalidOperationException("Cannot find Tree.Read method");

            var readParams = readMethod.GetParameters();
            var readArgs = new object?[readParams.Length];
            readArgs[0] = keySlice;
            for (int i = 1; i < readParams.Length; i++)
                readArgs[i] = readParams[i].HasDefaultValue ? readParams[i].DefaultValue : Type.Missing;

            var readResult = readMethod.Invoke(tree, readArgs);

            if (readResult == null)
                return null;

            // Get ReadResult.Reader and copy to byte array
            var readerProp = readResult.GetType().GetProperty("Reader")
                ?? throw new InvalidOperationException("Cannot find Reader property");
            var reader = readerProp.GetValue(readResult);
            if (reader == null)
                return null;

            // Get the Length property from the reader
            var lengthProp = reader.GetType().GetProperty("Length")
                ?? throw new InvalidOperationException("Cannot find Length property");
            var length = (int)(lengthProp.GetValue(reader) ?? 0);
            if (length == 0)
                return Array.Empty<byte>();

            // Create destination array and copy using CopyTo or Read method
            var result = new byte[length];

            // Try to use CopyTo(int, byte[], int, int) method
            var copyToMethod = reader.GetType().GetMethod("CopyTo",
                new[] { typeof(int), typeof(byte[]), typeof(int), typeof(int) });
            if (copyToMethod != null)
            {
                copyToMethod.Invoke(reader, new object[] { 0, result, 0, length });
                return result;
            }

            // Fallback: Try Read(byte[], int, int) method
            var readerReadMethod = reader.GetType().GetMethod("Read",
                new[] { typeof(byte[]), typeof(int), typeof(int) });
            if (readerReadMethod != null)
            {
                readerReadMethod.Invoke(reader, new object[] { result, 0, length });
                return result;
            }

            throw new InvalidOperationException("Cannot find method to read bytes from ValueReader");
        }

        /// <summary>
        /// Delete a key from a tree.
        /// </summary>
        /// <param name="tree">The tree object</param>
        /// <param name="key">Key bytes</param>
        /// <returns>True if deleted, false if not found</returns>
        public static bool TreeDelete(object tree, ReadOnlySpan<byte> key)
        {
            var treeType = tree.GetType();

            // Get allocator
            var lltxProp = treeType.GetProperty("Llt")
                ?? throw new InvalidOperationException("Cannot find Llt property on Tree");
            var lltx = lltxProp.GetValue(tree)
                ?? throw new InvalidOperationException("Llt is null");
            var allocatorProp = lltx.GetType().GetProperty("Allocator")
                ?? throw new InvalidOperationException("Cannot find Allocator property");
            var allocator = allocatorProp.GetValue(lltx)
                ?? throw new InvalidOperationException("Allocator is null");

            // Create key Slice
            var sliceType = treeType.Assembly.GetType("Voron.Slice")
                ?? throw new InvalidOperationException("Cannot find Voron.Slice type");
            var keySlice = CreateSlice(sliceType, allocator, key.ToArray());

            // Call Tree.Delete - find by name
            var methods = treeType.GetMethods();
            System.Reflection.MethodInfo? deleteMethod = null;
            foreach (var m in methods)
            {
                if (m.Name == "Delete")
                {
                    var ps = m.GetParameters();
                    if (ps.Length >= 1 && ps[0].ParameterType == sliceType)
                    {
                        deleteMethod = m;
                        break;
                    }
                }
            }
            if (deleteMethod == null)
                throw new InvalidOperationException("Cannot find Tree.Delete method");

            var deleteParams = deleteMethod.GetParameters();
            var deleteArgs = new object?[deleteParams.Length];
            deleteArgs[0] = keySlice;
            for (int i = 1; i < deleteParams.Length; i++)
                deleteArgs[i] = deleteParams[i].HasDefaultValue ? deleteParams[i].DefaultValue : Type.Missing;

            var result = deleteMethod.Invoke(tree, deleteArgs);

            return result is bool b && b;
        }

        #endregion

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
