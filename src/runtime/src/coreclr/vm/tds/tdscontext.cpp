// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
//
// TDSCONTEXT.CPP
//
// TypeDriver System (TDS) - VContext Management Implementation

#include "common.h"
#include "tdscontext.h"

// Thread-local storage for current VContext
// Using __thread for portability (POSIX TLS)
#ifdef _MSC_VER
__declspec(thread) static VContext* t_currentContext = nullptr;
__declspec(thread) static VContext* t_contextStack[16] = { nullptr };
__declspec(thread) static int t_contextStackTop = 0;
#else
__thread static VContext* t_currentContext = nullptr;
__thread static VContext* t_contextStack[16] = { nullptr };
__thread static int t_contextStackTop = 0;
#endif

namespace TDS
{
    //=========================================================================
    // VContext lifecycle management
    //=========================================================================

    VContext* CreateContext()
    {
        VContext* ctx = new (nothrow) VContext();
        if (ctx != nullptr)
        {
            InitContext(ctx);
        }
        return ctx;
    }

    void DestroyContext(VContext* ctx)
    {
        if (ctx != nullptr && ctx != &g_NullContext)
        {
            // Ensure transaction is unbound before destruction
            if (HasTransaction(ctx))
            {
                UnbindTransaction(ctx);
            }
            delete ctx;
        }
    }

    void InitContext(VContext* ctx)
    {
        if (ctx == nullptr)
            return;

        ctx->version = VCONTEXT_VERSION;
        ctx->flags = VCONTEXT_FLAG_NONE;
        ctx->transaction = nullptr;
        ctx->transactionScope = nullptr;
        ctx->securityCtx = nullptr;
        ctx->activationCtx = nullptr;
        ctx->reserved[0] = nullptr;
        ctx->reserved[1] = nullptr;
    }

    //=========================================================================
    // Transaction binding
    //=========================================================================

    void BindTransaction(VContext* ctx, void* txHandle, bool isWrite)
    {
        if (ctx == nullptr || ctx == &g_NullContext)
            return;

        ctx->transaction = txHandle;
        ctx->flags |= VCONTEXT_FLAG_INTRANSACTION;

        if (isWrite)
        {
            ctx->flags |= VCONTEXT_FLAG_WRITE_TX;
            ctx->flags &= ~VCONTEXT_FLAG_READONLY;
        }
        else
        {
            ctx->flags |= VCONTEXT_FLAG_READONLY;
            ctx->flags &= ~VCONTEXT_FLAG_WRITE_TX;
        }
    }

    void UnbindTransaction(VContext* ctx)
    {
        if (ctx == nullptr || ctx == &g_NullContext)
            return;

        ctx->transaction = nullptr;
        ctx->transactionScope = nullptr;
        ctx->flags &= ~(VCONTEXT_FLAG_INTRANSACTION |
                        VCONTEXT_FLAG_READONLY |
                        VCONTEXT_FLAG_WRITE_TX);
    }

    bool HasTransaction(VContext* ctx)
    {
        if (ctx == nullptr)
            return false;

        return (ctx->flags & VCONTEXT_FLAG_INTRANSACTION) != 0;
    }

    bool IsWriteTransaction(VContext* ctx)
    {
        if (ctx == nullptr)
            return false;

        return (ctx->flags & VCONTEXT_FLAG_WRITE_TX) != 0;
    }

    void* GetTransaction(VContext* ctx)
    {
        if (ctx == nullptr)
            return nullptr;

        return ctx->transaction;
    }

    //=========================================================================
    // Dirty tracking flags
    //=========================================================================

    void SetDirty(VContext* ctx)
    {
        if (ctx != nullptr && ctx != &g_NullContext)
        {
            ctx->flags |= VCONTEXT_FLAG_DIRTY;
        }
    }

    void ClearDirty(VContext* ctx)
    {
        if (ctx != nullptr && ctx != &g_NullContext)
        {
            ctx->flags &= ~VCONTEXT_FLAG_DIRTY;
        }
    }

    bool IsDirty(VContext* ctx)
    {
        if (ctx == nullptr)
            return false;

        return (ctx->flags & VCONTEXT_FLAG_DIRTY) != 0;
    }

    //=========================================================================
    // Per-thread context management
    //=========================================================================

    VContext* GetCurrentContext()
    {
        return t_currentContext;
    }

    VContext* SetCurrentContext(VContext* ctx)
    {
        VContext* previous = t_currentContext;
        t_currentContext = ctx;
        return previous;
    }

    void PushContext(VContext* ctx)
    {
        // Max stack depth is 16 (should be plenty for nested transactions)
        if (t_contextStackTop < 16)
        {
            t_contextStack[t_contextStackTop++] = t_currentContext;
            t_currentContext = ctx;
        }
        else
        {
            // Stack overflow - this shouldn't happen in normal use
            _ASSERTE(!"VContext stack overflow");
        }
    }

    VContext* PopContext()
    {
        VContext* popped = t_currentContext;

        if (t_contextStackTop > 0)
        {
            t_currentContext = t_contextStack[--t_contextStackTop];
        }
        else
        {
            // Stack underflow
            t_currentContext = nullptr;
        }

        return popped;
    }

    //=========================================================================
    // Context initialization (called during runtime startup)
    //=========================================================================

    void InitializeContextSystem()
    {
        // Initialize the null context
        g_NullContext.version = VCONTEXT_VERSION;
        g_NullContext.flags = VCONTEXT_FLAG_NONE;
        g_NullContext.transaction = nullptr;
        g_NullContext.transactionScope = nullptr;
        g_NullContext.securityCtx = nullptr;
        g_NullContext.activationCtx = nullptr;
        g_NullContext.reserved[0] = nullptr;
        g_NullContext.reserved[1] = nullptr;
    }

    void ShutdownContextSystem()
    {
        // Nothing to clean up for now
        // Future: could track and warn about leaked contexts
    }

} // namespace TDS
