using System;

namespace Scynapse
{
    /// <summary>
    /// Excludes a public event from Scynapse Event code generation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// By default, all public events declared on grain classes that implement
    /// <c>IGrainWithXXXKey</c> interfaces are processed by the Scynapse Events
    /// code generator. This attribute allows you to opt out specific events
    /// that should remain local-only.
    /// </para>
    /// <para>
    /// Events marked with this attribute will:
    /// </para>
    /// <list type="bullet">
    /// <item><description>NOT be added to the grain interface</description></item>
    /// <item><description>NOT have subscription methods generated</description></item>
    /// <item><description>NOT be connected to Scynapse Streams</description></item>
    /// <item><description>Behave as standard C# events (local invocation only)</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// public partial class PlayerGrain : Grain, IPlayerGrain
    /// {
    ///     // This event will be distributed via Scynapse Streams
    ///     public event EventHandler&lt;string&gt;? ChatMessage;
    ///
    ///     // This event stays local - no codegen, no streams
    ///     [NotEvent]
    ///     public event EventHandler? DiagnosticTick;
    ///
    ///     // This event stays local - used only within the grain
    ///     [NotEvent]
    ///     public event EventHandler&lt;int&gt;? InternalStateChanged;
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Event, AllowMultiple = false, Inherited = false)]
    public sealed class NotEventAttribute : Attribute
    {
    }
}
