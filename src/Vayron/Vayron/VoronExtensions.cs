// VAYRON - Runtime-Integrated Persistent Storage
// Extension methods for Voron types

using Sparrow.Server;
using Voron.Data;
using Voron.Data.BTrees;
using Voron.Impl;

namespace Vayron;

/// <summary>
/// Extension methods for Voron Transaction.
/// </summary>
internal static unsafe class VoronExtensions
{
    /// <summary>
    /// Creates a tree if it doesn't exist, or returns the existing one.
    /// </summary>
    public static Tree CreateTree(this Transaction tx, string treeName)
    {
        using (Slice.From(tx.Allocator, treeName, ByteStringType.Immutable, out var treeNameSlice))
        {
            var existing = tx.ReadTree(treeNameSlice);
            if (existing != null)
            {
                return existing;
            }

            // Create new tree via root objects
            return Tree.Create(tx.LowLevelTransaction, tx, treeNameSlice);
        }
    }
}
