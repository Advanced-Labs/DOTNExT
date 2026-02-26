using Scynapse;

namespace UnitTests.SplitGrain.Contracts;

/// <summary>
/// Test grain interface for split-assembly pattern testing.
/// </summary>
public interface ITestSplitGrain : IGrainWithStringKey
{
    /// <summary>
    /// Echoes back the provided message.
    /// </summary>
    Task<string> Echo(string message);

    /// <summary>
    /// Gets the current counter value.
    /// </summary>
    Task<int> GetCounter();

    /// <summary>
    /// Increments the counter and returns the new value.
    /// </summary>
    Task<int> IncrementCounter();
}

/// <summary>
/// Calculator grain interface for split-assembly pattern testing.
/// </summary>
public interface ICalculatorSplitGrain : IGrainWithIntegerKey
{
    /// <summary>
    /// Adds two numbers.
    /// </summary>
    Task<int> Add(int a, int b);

    /// <summary>
    /// Multiplies two numbers.
    /// </summary>
    Task<int> Multiply(int a, int b);

    /// <summary>
    /// Gets the last calculation result.
    /// </summary>
    Task<int> GetLastResult();
}
