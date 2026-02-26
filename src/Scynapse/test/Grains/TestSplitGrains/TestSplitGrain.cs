using Scynapse;
using UnitTests.SplitGrain.Contracts;

namespace UnitTests.SplitGrain.Grains;

/// <summary>
/// Implementation of ITestSplitGrain.
/// This class is in a separate assembly from the interface to test split-assembly loading.
/// </summary>
public class TestSplitGrain : Grain, ITestSplitGrain
{
    private int _counter = 0;

    public Task<string> Echo(string message)
    {
        return Task.FromResult($"Echo: {message}");
    }

    public Task<int> GetCounter()
    {
        return Task.FromResult(_counter);
    }

    public Task<int> IncrementCounter()
    {
        _counter++;
        return Task.FromResult(_counter);
    }
}

/// <summary>
/// Implementation of ICalculatorSplitGrain.
/// This class is in a separate assembly from the interface to test split-assembly loading.
/// </summary>
public class CalculatorSplitGrain : Grain, ICalculatorSplitGrain
{
    private int _lastResult = 0;

    public Task<int> Add(int a, int b)
    {
        _lastResult = a + b;
        return Task.FromResult(_lastResult);
    }

    public Task<int> Multiply(int a, int b)
    {
        _lastResult = a * b;
        return Task.FromResult(_lastResult);
    }

    public Task<int> GetLastResult()
    {
        return Task.FromResult(_lastResult);
    }
}
