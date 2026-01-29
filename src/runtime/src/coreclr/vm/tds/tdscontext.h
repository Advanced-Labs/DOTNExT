// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// TDSCONTEXT.H
//
// TypeDriver System (TDS) - VContext Management
// Provides lifecycle management and thread-local context for VContext.

#ifndef _TDS_CONTEXT_H_
#define _TDS_CONTEXT_H_

#include "common.h"
#include "tdsinterfaces.h"

namespace TDS
{
    //=========================================================================
    // VContext lifecycle management
    //=========================================================================

    // Create a new VContext with version 2 defaults
    VContext* CreateContext();

    // Destroy a VContext and free resources
    void DestroyContext(VContext* ctx);

    // Initialize a VContext in-place (for stack-allocated contexts)
    void InitContext(VContext* ctx);

    //=========================================================================
    // Transaction binding
    //=========================================================================

    // Bind a transaction handle to the context
    // txHandle: Managed object reference to Voron transaction
    // isWrite: true for write transaction, false for read-only
    void BindTransaction(VContext* ctx, void* txHandle, bool isWrite);

    // Unbind transaction from context (clears transaction state)
    void UnbindTransaction(VContext* ctx);

    // Check if context has an active transaction
    bool HasTransaction(VContext* ctx);

    // Check if context has a write transaction
    bool IsWriteTransaction(VContext* ctx);

    // Get the current transaction handle (may be null)
    void* GetTransaction(VContext* ctx);

    //=========================================================================
    // Dirty tracking flags
    //=========================================================================

    // Mark context as having dirty objects
    void SetDirty(VContext* ctx);

    // Clear dirty flag
    void ClearDirty(VContext* ctx);

    // Check if context has dirty objects
    bool IsDirty(VContext* ctx);

    //=========================================================================
    // Per-thread context management
    //
    // Each thread can have an implicit "current" context for operations
    // that don't explicitly pass a VContext.
    //=========================================================================

    // Get the current thread's VContext (may be null or g_NullContext)
    VContext* GetCurrentContext();

    // Set the current thread's VContext
    // Returns the previous context (for restore on scope exit)
    VContext* SetCurrentContext(VContext* ctx);

    // Push a context onto the thread's context stack
    void PushContext(VContext* ctx);

    // Pop the current context and restore previous
    VContext* PopContext();

    // Get effective context: returns explicit ctx if non-null,
    // otherwise returns current thread context or g_NullContext
    inline VContext* GetEffectiveContext(VContext* ctx)
    {
        if (ctx != nullptr)
            return ctx;

        VContext* current = GetCurrentContext();
        return current ? current : &g_NullContext;
    }

    //=========================================================================
    // Context initialization (called during runtime startup)
    //=========================================================================

    void InitializeContextSystem();
    void ShutdownContextSystem();

} // namespace TDS

#endif // _TDS_CONTEXT_H_
