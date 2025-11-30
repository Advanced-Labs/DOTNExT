using AsyncPersistenceScenarios.Services;

namespace AsyncPersistenceScenarios.TestWorkflows;

/// <summary>
/// Basic test workflows for async persistence scenarios.
/// These are designed to test incremental persistence challenges.
///
/// NOTE: Currently these are "manually instrumented" - they call the persistence
/// service explicitly. Once Roslyn is modified, the [Persistable] attribute will
/// trigger automatic codegen that does this.
/// </summary>
public class BasicWorkflows
{
    private readonly IAsyncPersistenceService? _persistence;

    public BasicWorkflows(IAsyncPersistenceService? persistence = null)
    {
        _persistence = persistence;
    }

    /// <summary>
    /// Challenge 1: Simple two-step workflow.
    /// Tests basic checkpoint and resume functionality.
    /// </summary>
    [Persistable]
    public async Task<int> SimpleWorkflow(int input, string workflowId)
    {
        Console.WriteLine($"[Workflow] Starting SimpleWorkflow with input={input}");

        // Step 1
        Console.WriteLine($"[Workflow] Step 1: Calculating input * 2...");
        var step1Result = await SimulateWorkAsync($"Step1-{workflowId}", input * 2, delayMs: 500);
        Console.WriteLine($"[Workflow] Step 1 complete: {step1Result}");

        // Checkpoint after step 1 (manual for now - Roslyn will automate this)
        // In real impl, this would be injected by Roslyn at each await
        ManualCheckpoint(workflowId, 1, ("step1Result", step1Result), ("input", input));

        // Step 2
        Console.WriteLine($"[Workflow] Step 2: Calculating step1 + 10...");
        var step2Result = await SimulateWorkAsync($"Step2-{workflowId}", step1Result + 10, delayMs: 500);
        Console.WriteLine($"[Workflow] Step 2 complete: {step2Result}");

        // Complete
        _persistence?.Complete(workflowId, step2Result);
        Console.WriteLine($"[Workflow] SimpleWorkflow complete with result={step2Result}");
        return step2Result;
    }

    /// <summary>
    /// Challenge 2: Workflow with multiple data types.
    /// Tests serialization of complex objects.
    /// </summary>
    [Persistable]
    public async Task<OrderResult> ProcessOrderWorkflow(Order order, string workflowId)
    {
        Console.WriteLine($"[Workflow] Starting ProcessOrderWorkflow for order {order.OrderId}");

        // Step 1: Validate
        Console.WriteLine($"[Workflow] Step 1: Validating order...");
        var isValid = await ValidateOrderAsync(order);
        Console.WriteLine($"[Workflow] Validation result: {isValid}");

        ManualCheckpoint(workflowId, 1, ("order", order), ("isValid", isValid));

        if (!isValid)
        {
            var failResult = new OrderResult { Success = false, Message = "Validation failed" };
            _persistence?.Complete(workflowId, failResult);
            return failResult;
        }

        // Step 2: Calculate total
        Console.WriteLine($"[Workflow] Step 2: Calculating total...");
        var total = await CalculateTotalAsync(order);
        Console.WriteLine($"[Workflow] Total: {total:C}");

        ManualCheckpoint(workflowId, 2, ("order", order), ("isValid", isValid), ("total", total));

        // Step 3: Process payment
        Console.WriteLine($"[Workflow] Step 3: Processing payment...");
        var paymentSuccess = await ProcessPaymentAsync(order.CustomerId, total);
        Console.WriteLine($"[Workflow] Payment result: {paymentSuccess}");

        var result = new OrderResult
        {
            Success = paymentSuccess,
            OrderId = order.OrderId,
            Total = total,
            Message = paymentSuccess ? "Order processed successfully" : "Payment failed"
        };

        _persistence?.Complete(workflowId, result);
        Console.WriteLine($"[Workflow] ProcessOrderWorkflow complete");
        return result;
    }

    /// <summary>
    /// Challenge 3: Workflow that calls another workflow.
    /// Tests nested async persistence.
    /// </summary>
    [Persistable]
    public async Task<int> OuterWorkflow(int x, string workflowId)
    {
        Console.WriteLine($"[Workflow] Starting OuterWorkflow with x={x}");

        // First inner call
        Console.WriteLine($"[Workflow] Calling InnerWorkflow (first time)...");
        var a = await InnerWorkflow(x, $"{workflowId}-inner1");
        Console.WriteLine($"[Workflow] First inner result: {a}");

        ManualCheckpoint(workflowId, 1, ("x", x), ("a", a));

        // Second inner call
        Console.WriteLine($"[Workflow] Calling InnerWorkflow (second time)...");
        var b = await InnerWorkflow(a, $"{workflowId}-inner2");
        Console.WriteLine($"[Workflow] Second inner result: {b}");

        _persistence?.Complete(workflowId, b);
        Console.WriteLine($"[Workflow] OuterWorkflow complete with result={b}");
        return b;
    }

    [Persistable]
    public async Task<int> InnerWorkflow(int x, string workflowId)
    {
        Console.WriteLine($"[Workflow]   InnerWorkflow starting with x={x}");
        var result = await SimulateWorkAsync($"Inner-{workflowId}", x * 2, delayMs: 300);
        _persistence?.Complete(workflowId, result);
        Console.WriteLine($"[Workflow]   InnerWorkflow complete with result={result}");
        return result;
    }

    /// <summary>
    /// Challenge 4: Workflow with exception handling.
    /// Tests state preservation across try/catch.
    /// </summary>
    [Persistable]
    public async Task<int> WorkflowWithExceptionHandling(int input, bool shouldFail, string workflowId)
    {
        Console.WriteLine($"[Workflow] Starting WorkflowWithExceptionHandling, shouldFail={shouldFail}");

        try
        {
            Console.WriteLine($"[Workflow] Step 1: Normal operation...");
            var step1 = await SimulateWorkAsync($"Step1-{workflowId}", input * 2, delayMs: 300);
            Console.WriteLine($"[Workflow] Step 1 result: {step1}");

            ManualCheckpoint(workflowId, 1, ("input", input), ("step1", step1), ("shouldFail", shouldFail));

            if (shouldFail)
            {
                Console.WriteLine($"[Workflow] Step 2: About to fail...");
                await SimulateFailureAsync();
            }

            Console.WriteLine($"[Workflow] Step 2: Success path...");
            var step2 = await SimulateWorkAsync($"Step2-{workflowId}", step1 + 10, delayMs: 300);
            _persistence?.Complete(workflowId, step2);
            return step2;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Workflow] Caught exception: {ex.Message}");
            Console.WriteLine($"[Workflow] Executing fallback...");

            ManualCheckpoint(workflowId, 10, ("input", input), ("inCatch", true));

            var fallback = await SimulateWorkAsync($"Fallback-{workflowId}", input, delayMs: 200);
            _persistence?.Complete(workflowId, fallback);
            Console.WriteLine($"[Workflow] Fallback complete with result={fallback}");
            return fallback;
        }
    }

    /// <summary>
    /// Challenge 5: Workflow with a loop.
    /// Tests state preservation across multiple iterations of the same await.
    /// </summary>
    [Persistable]
    public async Task<int> LoopWorkflow(int iterations, string workflowId)
    {
        Console.WriteLine($"[Workflow] Starting LoopWorkflow with {iterations} iterations");

        int sum = 0;
        for (int i = 0; i < iterations; i++)
        {
            Console.WriteLine($"[Workflow] Loop iteration {i + 1}/{iterations}...");
            var value = await SimulateWorkAsync($"Loop-{workflowId}-{i}", i * 10, delayMs: 200);
            sum += value;
            Console.WriteLine($"[Workflow] Iteration {i + 1} added {value}, sum={sum}");

            // Checkpoint after each iteration
            ManualCheckpoint(workflowId, i + 1, ("iterations", iterations), ("sum", sum), ("i", i));
        }

        _persistence?.Complete(workflowId, sum);
        Console.WriteLine($"[Workflow] LoopWorkflow complete with sum={sum}");
        return sum;
    }

    // ========== Helper methods ==========

    private async Task<int> SimulateWorkAsync(string label, int value, int delayMs)
    {
        await Task.Delay(delayMs);
        return value;
    }

    private async Task<bool> ValidateOrderAsync(Order order)
    {
        await Task.Delay(200);
        return order.Items.Count > 0 && !string.IsNullOrEmpty(order.CustomerId);
    }

    private async Task<decimal> CalculateTotalAsync(Order order)
    {
        await Task.Delay(300);
        return order.Items.Sum(i => i.Price * i.Quantity);
    }

    private async Task<bool> ProcessPaymentAsync(string customerId, decimal amount)
    {
        await Task.Delay(500);
        return true; // Always succeeds for testing
    }

    private async Task SimulateFailureAsync()
    {
        await Task.Delay(100);
        throw new InvalidOperationException("Simulated failure");
    }

    /// <summary>
    /// Manual checkpoint helper - simulates what Roslyn will generate.
    /// This is temporary until we modify Roslyn.
    /// </summary>
    private void ManualCheckpoint(string workflowId, int state, params (string name, object? value)[] fields)
    {
        if (_persistence == null) return;

        // Create a fake state machine snapshot
        // In real impl, Roslyn would generate code that passes the actual state machine
        var snapshot = new StateMachineSnapshot
        {
            State = state,
            TypeName = "ManualCheckpoint",
            Timestamp = DateTimeOffset.UtcNow,
            Fields = fields.ToDictionary(f => f.name, f => f.value)
        };

        // Store directly (bypassing the generic interface for now)
        if (_persistence is InMemoryAsyncPersistenceService memService)
        {
            // Use reflection to store the snapshot
            var field = typeof(InMemoryAsyncPersistenceService)
                .GetField("_snapshots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            if (field?.GetValue(memService) is System.Collections.Concurrent.ConcurrentDictionary<string, StateMachineSnapshot> dict)
            {
                dict[workflowId] = snapshot;
                Console.WriteLine($"[Persistence] CHECKPOINT: {workflowId} at state {state}");
                Console.WriteLine($"             Fields: {string.Join(", ", fields.Select(f => $"{f.name}={f.value}"))}");
            }
        }
    }
}

// ========== Data types ==========

public class Order
{
    public string OrderId { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public List<OrderItem> Items { get; set; } = new();
}

public class OrderItem
{
    public string ProductId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int Quantity { get; set; }
}

public class OrderResult
{
    public bool Success { get; set; }
    public string OrderId { get; set; } = string.Empty;
    public decimal Total { get; set; }
    public string Message { get; set; } = string.Empty;
}
