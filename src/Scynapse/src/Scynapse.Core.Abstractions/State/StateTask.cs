using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace Scynapse
{
    /// <summary>
    /// Wraps remote grain property access with awaitable get and operator-based set.
    /// This type enables property-like syntax for accessing grain state remotely.
    /// </summary>
    /// <typeparam name="T">The property value type.</typeparam>
    /// <remarks>
    /// <para>
    /// <c>StateTask&lt;T&gt;</c> provides two ways to interact with grain properties:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>Awaiting the property to get its value: <c>string name = await grain.Name;</c></description>
    /// </item>
    /// <item>
    /// <description>Using the <c>&lt;&lt;</c> operator to set: <c>await (grain.Name &lt;&lt; "Louis");</c></description>
    /// </item>
    /// </list>
    /// <para>
    /// Each access creates fresh invokables for thread safety - concurrent get/set operations
    /// on the same property will not interfere with each other.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// IPlayerGrain player = client.GetGrain&lt;IPlayerGrain&gt;("player-1");
    ///
    /// // Get property value
    /// string name = await player.Name;
    ///
    /// // Set property value
    /// await (player.Name &lt;&lt; "Louis");
    ///
    /// // Multiple concurrent operations are safe
    /// var t1 = player.Name &lt;&lt; "Alice";
    /// var t2 = player.Name &lt;&lt; "Bob";
    /// await Task.WhenAll(t1.AsTask(), t2.AsTask());
    /// </code>
    /// </example>
    public readonly struct StateTask<T>
    {
        private readonly Func<ValueTask<T>> _getter;
        private readonly Func<T, ValueTask> _setter;

        /// <summary>
        /// Creates a new StateTask for a grain property.
        /// </summary>
        /// <param name="getter">Delegate that retrieves the property value from the grain.</param>
        /// <param name="setter">Delegate that sets the property value on the grain.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown when <paramref name="getter"/> or <paramref name="setter"/> is null.
        /// </exception>
        public StateTask(
            Func<ValueTask<T>> getter,
            Func<T, ValueTask> setter)
        {
            _getter = getter ?? throw new ArgumentNullException(nameof(getter));
            _setter = setter ?? throw new ArgumentNullException(nameof(setter));
        }

        /// <summary>
        /// Asynchronously retrieves the property value from the grain.
        /// </summary>
        /// <returns>A <see cref="ValueTask{T}"/> that completes with the property value.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the StateTask was not properly initialized.
        /// </exception>
        public ValueTask<T> GetAsync()
        {
            if (_getter is null)
            {
                throw new InvalidOperationException("StateTask was not properly initialized. This typically occurs when using a default-constructed StateTask.");
            }

            return _getter();
        }

        /// <summary>
        /// Asynchronously sets the property value on the grain.
        /// </summary>
        /// <param name="value">The value to set.</param>
        /// <returns>A <see cref="ValueTask"/> that completes when the set operation completes.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when the StateTask was not properly initialized.
        /// </exception>
        public ValueTask SetAsync(T value)
        {
            if (_setter is null)
            {
                throw new InvalidOperationException("StateTask was not properly initialized. This typically occurs when using a default-constructed StateTask.");
            }

            return _setter(value);
        }

        // ═══════════════════════════════════════════════════════════════
        // AWAITABLE PATTERN
        // Enables: string name = await grain.Name;
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Gets an awaiter for the property value (enables await syntax).
        /// </summary>
        /// <returns>A <see cref="ValueTaskAwaiter{T}"/> for the get operation.</returns>
        /// <remarks>
        /// This method enables the following syntax:
        /// <code>
        /// string name = await grain.Name;
        /// </code>
        /// </remarks>
        public ValueTaskAwaiter<T> GetAwaiter() => GetAsync().GetAwaiter();

        // ═══════════════════════════════════════════════════════════════
        // SHIFT OPERATOR FOR SET
        // Enables: await (grain.Name << "Louis");
        // C# 11+ allows any return type from shift operators
        // ═══════════════════════════════════════════════════════════════

        /// <summary>
        /// Sets the property value using shift-left syntax.
        /// </summary>
        /// <param name="state">The StateTask representing the property.</param>
        /// <param name="value">The value to set.</param>
        /// <returns>A <see cref="ValueTask"/> that completes when the set operation completes.</returns>
        /// <remarks>
        /// <para>
        /// This operator enables the following syntax:
        /// </para>
        /// <code>
        /// await (grain.Name &lt;&lt; "Louis");
        /// </code>
        /// <para>
        /// The <c>&lt;&lt;</c> operator was chosen because:
        /// </para>
        /// <list type="bullet">
        /// <item><description>C# property setters cannot return Task/ValueTask</description></item>
        /// <item><description>Assignment expressions evaluate to the right-hand side value</description></item>
        /// <item><description><c>&lt;&lt;</c> visually suggests "pushing" a value into something</description></item>
        /// <item><description>C# 11+ allows shift operators to return any type</description></item>
        /// </list>
        /// </remarks>
        public static ValueTask operator <<(StateTask<T> state, T value)
            => state.SetAsync(value);
    }

    /// <summary>
    /// Provides extension methods for <see cref="StateTask{T}"/>.
    /// </summary>
    public static class StateTaskExtensions
    {
        /// <summary>
        /// Converts the <see cref="StateTask{T}"/> get operation to a <see cref="Task{T}"/>.
        /// </summary>
        /// <typeparam name="T">The property value type.</typeparam>
        /// <param name="stateTask">The state task to convert.</param>
        /// <returns>A <see cref="Task{T}"/> representing the get operation.</returns>
        public static Task<T> AsTask<T>(this StateTask<T> stateTask)
            => stateTask.GetAsync().AsTask();
    }
}
