using System.Runtime.CompilerServices;
using DOTNExT.Persistence;

namespace AsyncPersistenceScenarios.TestWorkflows;

/// <summary>
/// This file demonstrates what Roslyn-generated code would look like with persistence support.
/// It's a manually-written state machine that mimics what the compiler produces,
/// but with persistence calls injected.
///
/// THIS IS WHAT ROSLYN WOULD GENERATE for:
///
/// [Persistable]
/// public async Task<int> InstrumentedSimpleWorkflow(int input)
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

            // Initial state
            <>1__state = -1,

            // Builder
            <>t__builder = AsyncTaskMethodBuilder<int>.Create()
        };

        stateMachine.<>t__builder.Start(ref stateMachine);
        return stateMachine.<>t__builder.Task;
    }
}

/// <summary>
/// The compiler-generated state machine WITH persistence support.
/// This shows exactly where checkpoint and restore calls go.
/// </summary>
public struct InstrumentedSimpleWorkflow_StateMachine : IAsyncStateMachine
{
    // ===== COMPILER-GENERATED FIELDS =====

    /// <summary>Current state: -1=running, -2=finished, 0+=await point</summary>
    public int <>1__state;

    /// <summary>The async method builder</summary>
    public AsyncTaskMethodBuilder<int> <>t__builder;

    /// <summary>Captured parameter: input</summary>
    public int input;

    /// <summary>Hoisted local: step1 result (alive across second await)</summary>
    public int <step1>5__1;

    /// <summary>Hoisted local: step2 result</summary>
    public int <step2>5__2;

    /// <summary>Awaiter field</summary>
    private TaskAwaiter<int> <>u__1;

    // ===== PERSISTENCE FIELDS (workflow ID passed in) =====
    public string workflowId;

    // ===== PERSISTENCE LOCAL (cached in MoveNext) =====
    private IAsyncPersistenceService? _persistenceService;

    /// <summary>
    /// The MoveNext method - this is where the magic happens.
    /// </summary>
    public void MoveNext()
    {
        int num = <>1__state;
        int result;

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
                int restoredState = _persistenceService.TryRestore(this, workflowId);
                if (restoredState >= 0)
                {
                    // Restoration happened - fields are now populated
                    // Update our state to resume from correct point
                    num = restoredState;
                    <>1__state = restoredState;
                    Console.WriteLine($"[SM] Restored from state {restoredState}");
                }
            }
            // ========================================

            TaskAwaiter<int> awaiter;

            switch (num)
            {
                case 0:
                    goto Label_AwaitPoint0;
                case 1:
                    goto Label_AwaitPoint1;
            }

            // ----- STATE -1: Initial execution -----
            Console.WriteLine($"Step 1: input = {input}");

            // Start the first async operation
            awaiter = Task.Delay(500).ContinueWith(_ => input * 2).GetAwaiter();

            if (!awaiter.IsCompleted)
            {
                // Set state for this await point
                num = 0;
                <>1__state = 0;
                <>u__1 = awaiter;

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

                <>t__builder.AwaitUnsafeOnCompleted(ref awaiter, ref this);
                return;
            }
            goto Label_GetResult0;

        Label_AwaitPoint0:
            awaiter = <>u__1;
            <>u__1 = default;
            <>1__state = -1;

        Label_GetResult0:
            // Get result from first await
            <step1>5__1 = awaiter.GetResult();
            Console.WriteLine($"Step 2: step1 = {<step1>5__1}");

            // Start the second async operation
            var temp = <step1>5__1; // Capture for lambda
            awaiter = Task.Delay(500).ContinueWith(_ => temp + 10).GetAwaiter();

            if (!awaiter.IsCompleted)
            {
                num = 1;
                <>1__state = 1;
                <>u__1 = awaiter;

                // ========================================
                // NEW: CHECKPOINT BEFORE SUSPENSION
                // ========================================
                if (_persistenceService != null)
                {
                    _persistenceService.Checkpoint(this, 1, workflowId);
                    Console.WriteLine($"[SM] Checkpoint at state 1");
                }
                // ========================================

                <>t__builder.AwaitUnsafeOnCompleted(ref awaiter, ref this);
                return;
            }
            goto Label_GetResult1;

        Label_AwaitPoint1:
            awaiter = <>u__1;
            <>u__1 = default;
            <>1__state = -1;

        Label_GetResult1:
            <step2>5__2 = awaiter.GetResult();
            Console.WriteLine($"Result: {<step2>5__2}");
            result = <step2>5__2;
        }
        catch (Exception ex)
        {
            <>1__state = -2;

            // ========================================
            // NEW: FAULT NOTIFICATION
            // ========================================
            if (_persistenceService != null)
            {
                _persistenceService.Fault(workflowId, ex);
            }
            // ========================================

            <>t__builder.SetException(ex);
            return;
        }

        // Completed successfully
        <>1__state = -2;

        // ========================================
        // NEW: COMPLETION NOTIFICATION
        // ========================================
        if (_persistenceService != null)
        {
            _persistenceService.Complete(workflowId, result);
        }
        // ========================================

        <>t__builder.SetResult(result);
    }

    public void SetStateMachine(IAsyncStateMachine stateMachine)
    {
        <>t__builder.SetStateMachine(stateMachine);
    }
}
