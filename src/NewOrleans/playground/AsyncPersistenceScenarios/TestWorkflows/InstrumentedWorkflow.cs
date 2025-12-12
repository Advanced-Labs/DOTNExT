using System.Runtime.CompilerServices;
using DOTNExT.Persistence;

namespace AsyncPersistenceScenarios.TestWorkflows;

/// <summary>
/// This file demonstrates what Roslyn-generated code would look like with persistence support.
/// It's a manually-written state machine that mimics what the compiler produces,
/// but with persistence calls injected.
///
/// NOTE: Field names are simplified for C# compatibility. The actual compiler-generated
/// code uses names like &lt;&gt;1__state, &lt;step1&gt;5__1 which are valid IL but not C#.
///
/// THIS IS WHAT ROSLYN WOULD GENERATE for:
///
/// [Persistable]
/// public async Task&lt;int&gt; InstrumentedSimpleWorkflow(int input)
/// {
///     Console.WriteLine($"Step 1: input = {input}");
///     var step1 = await Task.Delay(500).ContinueWith(_ => input * 2);
///
///     Console.WriteLine($"Step 2: step1 = {step1}");
///     var step2 = await Task.Delay(500).ContinueWith(_ => step1 + 10);
///
///     Console.WriteLine($"Result: {step2}");
///     return step2;
/// }
/// </summary>
public class InstrumentedWorkflowRunner
{
    private readonly string _workflowId;

    public InstrumentedWorkflowRunner(string workflowId)
    {
        _workflowId = workflowId;
    }

    /// <summary>
    /// Entry point that creates and starts the state machine.
    /// This is the "kickoff" method that Roslyn generates.
    /// </summary>
    public Task<int> InstrumentedSimpleWorkflow(int input)
    {
        var stateMachine = new InstrumentedSimpleWorkflow_StateMachine
        {
            // Captured parameters
            input = input,
            workflowId = _workflowId,

            // Initial state (compiler uses <>1__state)
            __state = -1,

            // Builder (compiler uses <>t__builder)
            __builder = AsyncTaskMethodBuilder<int>.Create()
        };

        stateMachine.__builder.Start(ref stateMachine);
        return stateMachine.__builder.Task;
    }
}

/// <summary>
/// The compiler-generated state machine WITH persistence support.
/// This shows exactly where checkpoint and restore calls go.
///
/// Field name mapping (C# source → IL/compiler):
///   __state    → &lt;&gt;1__state
///   __builder  → &lt;&gt;t__builder
///   _step1     → &lt;step1&gt;5__1
///   _step2     → &lt;step2&gt;5__2
///   __awaiter  → &lt;&gt;u__1
///
/// NOTE: This is a CLASS (not struct) to allow persistence deserialization to work.
/// Real Roslyn generates structs, but struct boxing prevents in-place field updates.
/// A production implementation would need a more sophisticated approach.
/// </summary>
public class InstrumentedSimpleWorkflow_StateMachine : IAsyncStateMachine
{
    // ===== COMPILER-GENERATED FIELDS =====

    /// <summary>Current state: -1=running, -2=finished, 0+=await point</summary>
    /// <remarks>Compiler name: &lt;&gt;1__state</remarks>
    public int __state;

    /// <summary>The async method builder</summary>
    /// <remarks>Compiler name: &lt;&gt;t__builder</remarks>
    public AsyncTaskMethodBuilder<int> __builder;

    /// <summary>Captured parameter: input</summary>
    public int input;

    /// <summary>Hoisted local: step1 result (alive across second await)</summary>
    /// <remarks>Compiler name: &lt;step1&gt;5__1</remarks>
    public int _step1;

    /// <summary>Hoisted local: step2 result</summary>
    /// <remarks>Compiler name: &lt;step2&gt;5__2</remarks>
    public int _step2;

    /// <summary>Awaiter field</summary>
    /// <remarks>Compiler name: &lt;&gt;u__1</remarks>
    private TaskAwaiter<int> __awaiter;

    // ===== PERSISTENCE FIELDS (workflow ID passed in) =====
    public string workflowId = "";

    // ===== PERSISTENCE LOCAL (cached in MoveNext) =====
    private IAsyncPersistenceService? _persistenceService;

    /// <summary>
    /// The MoveNext method - this is where the magic happens.
    /// </summary>
    public void MoveNext()
    {
        int num = __state;
        int result;
        bool justRestored = false;  // Flag to distinguish restoration from normal continuation

        try
        {
            // ========================================
            // NEW: PERSISTENCE RESTORATION CHECK
            // This is injected by modified Roslyn
            // ========================================
            _persistenceService = AsyncPersistenceContext.Current;
            if (_persistenceService != null && num == -1)
            {
                // Check if we should restore from a checkpoint
                // NOTE: Using obsolete non-generic method because this is a CLASS-based state machine.
                // The new generic TryRestore<T>(ref T, string) is designed for STRUCT state machines
                // (which Roslyn generates). For classes, the old API works fine since objects are
                // passed by reference anyway.
#pragma warning disable CS0618 // Type or member is obsolete
                int restoredState = _persistenceService.TryRestore(this, workflowId);
#pragma warning restore CS0618
                if (restoredState >= 0)
                {
                    // Restoration happened - fields are now populated
                    // Update our state to resume from correct point
                    num = restoredState;
                    __state = restoredState;
                    justRestored = true;  // Mark that we just restored (awaiter not available)
                    Console.WriteLine($"[SM] Restored from state {restoredState}");
                }
            }
            // ========================================

            TaskAwaiter<int> awaiter;

            // When restoring, we need to re-run the async operation (awaiter wasn't serialized)
            // When continuing normally, awaiter has the result ready
            switch (num)
            {
                case 0:
                    if (justRestored)
                        goto Label_StartOp0;  // Re-run first async operation
                    else
                        goto Label_AwaitPoint0;  // Normal continuation - get awaiter result
                case 1:
                    if (justRestored)
                        goto Label_StartOp1;  // Re-run second async operation
                    else
                        goto Label_AwaitPoint1;  // Normal continuation - get awaiter result
            }

            // ----- STATE -1: Initial execution -----
            Console.WriteLine($"Step 1: input = {input}");

        Label_StartOp0:
            // Start the first async operation
            var inputCopy = input; // Capture for lambda (structs cannot capture 'this')
            awaiter = Task.Delay(500).ContinueWith(_ => inputCopy * 2).GetAwaiter();

            if (!awaiter.IsCompleted)
            {
                // Set state for this await point
                num = 0;
                __state = 0;
                __awaiter = awaiter;

                // ========================================
                // NEW: CHECKPOINT BEFORE SUSPENSION
                // This is injected by modified Roslyn
                // ========================================
                if (_persistenceService != null)
                {
                    _persistenceService.Checkpoint(this, 0, workflowId);
                    Console.WriteLine($"[SM] Checkpoint at state 0");
                }
                // ========================================

                // For class-based state machine, store 'this' in a variable to pass by ref
                var stateMachine = this;
                __builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
                return;
            }
            goto Label_GetResult0;

        Label_AwaitPoint0:
            awaiter = __awaiter;
            __awaiter = default;
            __state = -1;

        Label_GetResult0:
            // Get result from first await
            _step1 = awaiter.GetResult();

        Label_StartOp1:
            // When restored from state 1, _step1 is already populated from checkpoint
            Console.WriteLine($"Step 2: step1 = {_step1}");

            // Start the second async operation
            var temp = _step1; // Capture for lambda
            awaiter = Task.Delay(500).ContinueWith(_ => temp + 10).GetAwaiter();

            if (!awaiter.IsCompleted)
            {
                num = 1;
                __state = 1;
                __awaiter = awaiter;

                // ========================================
                // NEW: CHECKPOINT BEFORE SUSPENSION
                // ========================================
                if (_persistenceService != null)
                {
                    _persistenceService.Checkpoint(this, 1, workflowId);
                    Console.WriteLine($"[SM] Checkpoint at state 1");
                }
                // ========================================

                // For class-based state machine, store 'this' in a variable to pass by ref
                var stateMachine = this;
                __builder.AwaitUnsafeOnCompleted(ref awaiter, ref stateMachine);
                return;
            }
            goto Label_GetResult1;

        Label_AwaitPoint1:
            awaiter = __awaiter;
            __awaiter = default;
            __state = -1;

        Label_GetResult1:
            _step2 = awaiter.GetResult();
            Console.WriteLine($"Result: {_step2}");
            result = _step2;
        }
        catch (Exception ex)
        {
            __state = -2;

            // ========================================
            // NEW: FAULT NOTIFICATION
            // ========================================
            if (_persistenceService != null)
            {
                _persistenceService.Fault(workflowId, ex);
            }
            // ========================================

            __builder.SetException(ex);
            return;
        }

        // Completed successfully
        __state = -2;

        // ========================================
        // NEW: COMPLETION NOTIFICATION
        // ========================================
        if (_persistenceService != null)
        {
            _persistenceService.Complete(workflowId, result);
        }
        // ========================================

        __builder.SetResult(result);
    }

    public void SetStateMachine(IAsyncStateMachine stateMachine)
    {
        __builder.SetStateMachine(stateMachine);
    }
}
