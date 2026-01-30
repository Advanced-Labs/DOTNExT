// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace System.OS
{
    /// <summary>
    /// Flags for VContext state.
    /// </summary>
    [Flags]
    [CLSCompliant(false)]
    public enum VContextFlags : uint
    {
        None = 0x0000,
        InTransaction = 0x0001,
        ReadOnly = 0x0002,
        WriteTx = 0x0004,
        Dirty = 0x0008
    }

    /// <summary>
    /// Execution context for virtual object operations.
    /// Carries transaction handles and state through driver operations.
    /// Phase 2: Transaction support for persistence.
    /// </summary>
    public sealed partial class VContext : IDisposable
    {
        private IntPtr _nativeHandle;
        private bool _disposed;
        private bool _ownsHandle;

        /// <summary>
        /// Create a new VContext.
        /// </summary>
        public VContext()
        {
            _nativeHandle = CreateContextInternal();
            _ownsHandle = true;
        }

        /// <summary>
        /// Wrap an existing native VContext handle.
        /// </summary>
        internal VContext(IntPtr handle, bool ownsHandle = false)
        {
            _nativeHandle = handle;
            _ownsHandle = ownsHandle;
        }

        /// <summary>
        /// Native handle (for internal use).
        /// </summary>
        internal IntPtr NativeHandle => _nativeHandle;

        /// <summary>
        /// Check if context has an active transaction.
        /// </summary>
        public bool HasTransaction => (_nativeHandle != IntPtr.Zero) && HasTransactionInternal(_nativeHandle);

        /// <summary>
        /// Check if context has a write transaction.
        /// </summary>
        public bool IsWriteTransaction => (_nativeHandle != IntPtr.Zero) && IsWriteTransactionInternal(_nativeHandle);

        /// <summary>
        /// Check if context has dirty objects pending flush.
        /// </summary>
        public bool IsDirty => (_nativeHandle != IntPtr.Zero) && IsDirtyInternal(_nativeHandle);

        /// <summary>
        /// Get context flags.
        /// </summary>
        public VContextFlags Flags => (_nativeHandle != IntPtr.Zero)
            ? (VContextFlags)GetFlagsInternal(_nativeHandle)
            : VContextFlags.None;

        /// <summary>
        /// Mark context as having dirty objects.
        /// </summary>
        public void SetDirty()
        {
            ThrowIfDisposed();
            SetDirtyInternal(_nativeHandle);
        }

        /// <summary>
        /// Clear dirty flag.
        /// </summary>
        public void ClearDirty()
        {
            ThrowIfDisposed();
            ClearDirtyInternal(_nativeHandle);
        }

        /// <summary>
        /// Dispose the context.
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                if (_ownsHandle && _nativeHandle != IntPtr.Zero)
                {
                    DestroyContextInternal(_nativeHandle);
                }
                _nativeHandle = IntPtr.Zero;
                _disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
        }

        // P/Invoke declarations
        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSContext_Create")]
        private static partial IntPtr CreateContextInternal();

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSContext_Destroy")]
        private static partial void DestroyContextInternal(IntPtr handle);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSContext_HasTransaction")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool HasTransactionInternal(IntPtr handle);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSContext_IsWriteTransaction")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool IsWriteTransactionInternal(IntPtr handle);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSContext_IsDirty")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static partial bool IsDirtyInternal(IntPtr handle);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSContext_GetFlags")]
        private static partial uint GetFlagsInternal(IntPtr handle);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSContext_SetDirty")]
        private static partial void SetDirtyInternal(IntPtr handle);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSContext_ClearDirty")]
        private static partial void ClearDirtyInternal(IntPtr handle);
    }

    /// <summary>
    /// Static methods for per-thread VContext management.
    /// </summary>
    public static partial class VContextManager
    {
        /// <summary>
        /// Get the current thread's VContext (may be null).
        /// </summary>
        public static VContext? Current
        {
            get
            {
                IntPtr handle = GetCurrentInternal();
                return handle != IntPtr.Zero ? new VContext(handle, ownsHandle: false) : null;
            }
        }

        /// <summary>
        /// Push a context onto the current thread's context stack.
        /// </summary>
        public static void Push(VContext context)
        {
            ArgumentNullException.ThrowIfNull(context);
            PushContextInternal(context.NativeHandle);
        }

        /// <summary>
        /// Pop the current context and restore the previous one.
        /// </summary>
        public static VContext? Pop()
        {
            IntPtr handle = PopContextInternal();
            return handle != IntPtr.Zero ? new VContext(handle, ownsHandle: false) : null;
        }

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSContext_GetCurrent")]
        private static partial IntPtr GetCurrentInternal();

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSContext_Push")]
        private static partial void PushContextInternal(IntPtr handle);

        [LibraryImport(RuntimeHelpers.QCall, EntryPoint = "TDSContext_Pop")]
        private static partial IntPtr PopContextInternal();
    }
}
