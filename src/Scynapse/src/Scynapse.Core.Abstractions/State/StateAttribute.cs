using System;

namespace Scynapse
{
    /// <summary>
    /// Configures code generation behavior for a grain state property.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This attribute is optional - all public properties on grains are processed by default
    /// unless marked with <see cref="NotStateAttribute"/>.
    /// </para>
    /// <para>
    /// When applied, this attribute allows you to customize how the property is exposed
    /// through the grain interface and how it interacts with Scynapse persistence.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// public partial class PlayerGrain : Grain, IPlayerGrain
    /// {
    ///     private readonly IPersistentState&lt;PlayerData&gt; _state;
    ///
    ///     // Simple in-memory state
    ///     public partial string Name { get; set; }
    ///
    ///     // Persisted to Scynapse state with auto-save
    ///     [State(Persisted = true, StateProperty = nameof(_state), AutoSave = true)]
    ///     public partial int Score { get; set; }
    ///
    ///     // Read-only from client perspective
    ///     [State(CanSet = false)]
    ///     public partial DateTime CreatedAt { get; }
    ///
    ///     // Custom method names
    ///     [State(MethodName = "DisplayName")]
    ///     public partial string Title { get; set; }  // Generates GetDisplayName/SetDisplayName
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class StateAttribute : Attribute
    {
        /// <summary>
        /// Gets or sets a value indicating whether the property maps to an <c>IPersistentState</c> field.
        /// </summary>
        /// <value>
        /// <c>true</c> if the property value should be stored in and retrieved from a persistent state object;
        /// <c>false</c> to use a simple backing field. Default is <c>false</c>.
        /// </value>
        /// <remarks>
        /// When set to <c>true</c>, <see cref="StateProperty"/> must also be set to specify
        /// which <c>IPersistentState&lt;T&gt;</c> field to use.
        /// </remarks>
        public bool Persisted { get; init; } = false;

        /// <summary>
        /// Gets or sets the name of the <c>IPersistentState&lt;T&gt;</c> field to map this property to.
        /// </summary>
        /// <value>
        /// The name of the field containing the persistent state.
        /// The <c>T</c> type must have a property with a matching name and compatible type.
        /// </value>
        /// <remarks>
        /// Only used when <see cref="Persisted"/> is <c>true</c>.
        /// </remarks>
        /// <example>
        /// <code>
        /// private readonly IPersistentState&lt;PlayerData&gt; _playerState;
        ///
        /// [State(Persisted = true, StateProperty = nameof(_playerState))]
        /// public partial int Score { get; set; }
        /// </code>
        /// </example>
        public string? StateProperty { get; init; }

        /// <summary>
        /// Gets or sets a value indicating whether <c>WriteStateAsync()</c> is called automatically
        /// after each set operation.
        /// </summary>
        /// <value>
        /// <c>true</c> to automatically persist changes on every set; <c>false</c> for manual persistence.
        /// Default is <c>false</c>.
        /// </value>
        /// <remarks>
        /// <para>
        /// For persisted properties (<see cref="Persisted"/> = <c>true</c>), this calls
        /// the <c>IPersistentState.WriteStateAsync()</c> method.
        /// </para>
        /// <para>
        /// For non-persisted properties, this calls the grain's <c>WriteStateAsync()</c> if available.
        /// </para>
        /// <para>
        /// <strong>Performance consideration:</strong> Auto-save on every set can impact performance
        /// for frequently updated properties. Consider batching updates manually for hot paths.
        /// </para>
        /// </remarks>
        public bool AutoSave { get; init; } = false;

        /// <summary>
        /// Gets or sets a value indicating whether a setter method should be generated for this property.
        /// </summary>
        /// <value>
        /// <c>true</c> to generate both getter and setter methods; <c>false</c> to generate only a getter.
        /// Default is <c>true</c>.
        /// </value>
        /// <remarks>
        /// <para>
        /// When set to <c>false</c>, only a getter method (e.g., <c>GetName()</c>) is generated
        /// in the interface. The property can still have a setter for internal grain use,
        /// but clients cannot modify the value remotely.
        /// </para>
        /// <para>
        /// This is useful for read-only computed values or properties that should only be
        /// modified by the grain itself (e.g., timestamps, internal counters).
        /// </para>
        /// </remarks>
        public bool CanSet { get; init; } = true;

        /// <summary>
        /// Gets or sets a custom name for the generated Get/Set methods.
        /// </summary>
        /// <value>
        /// The base name to use for method generation, or <c>null</c> to use the property name.
        /// Default is <c>null</c>.
        /// </value>
        /// <remarks>
        /// <para>
        /// By default, a property named <c>Name</c> generates <c>GetName()</c> and <c>SetName()</c> methods.
        /// </para>
        /// <para>
        /// Use this property when you want different method names, for example to avoid conflicts
        /// with existing methods or to provide a more descriptive API.
        /// </para>
        /// </remarks>
        /// <example>
        /// <code>
        /// [State(MethodName = "DisplayName")]
        /// public partial string Title { get; set; }
        /// // Generates: GetDisplayName() and SetDisplayName(string value)
        /// </code>
        /// </example>
        public string? MethodName { get; init; }
    }
}
