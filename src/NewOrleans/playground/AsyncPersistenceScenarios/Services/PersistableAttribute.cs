namespace DOTNExT.Persistence;

/// <summary>
/// Marks an async method or class for automatic persistence support.
///
/// When the modified Roslyn compiler sees this attribute on an async method,
/// it will inject:
/// 1. Restoration check at the start of MoveNext()
/// 2. Checkpoint calls before each await suspension
///
/// This enables pause/resume, crash recovery, and distributed execution
/// of async workflows.
/// </summary>
/// <example>
/// <code>
/// [Persistable]
/// public async Task&lt;int&gt; LongRunningWorkflow(int input)
/// {
///     var step1 = await DoStep1Async(input);  // Checkpoint 0
///     var step2 = await DoStep2Async(step1);  // Checkpoint 1
///     return step2;
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true, AllowMultiple = false)]
public class PersistableAttribute : Attribute
{
    /// <summary>
    /// Optional persistence scope identifier.
    /// If not specified, defaults to the fully-qualified method name.
    /// </summary>
    public string? ScopeId { get; set; }

    /// <summary>
    /// Whether to persist on every await or only on explicit calls.
    /// Default is true (persist on every await).
    /// </summary>
    public bool AutoCheckpoint { get; set; } = true;

    /// <summary>
    /// Creates a new PersistableAttribute with default settings.
    /// </summary>
    public PersistableAttribute() { }

    /// <summary>
    /// Creates a new PersistableAttribute with a specific scope identifier.
    /// </summary>
    /// <param name="scopeId">The persistence scope identifier.</param>
    public PersistableAttribute(string scopeId)
    {
        ScopeId = scopeId;
    }
}
