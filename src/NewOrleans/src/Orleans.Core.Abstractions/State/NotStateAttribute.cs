using System;

namespace Orleans
{
    /// <summary>
    /// Excludes a public property from state code generation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// By default, all public properties on grain classes are processed by the state property
    /// code generator, which generates <c>Get</c>/<c>Set</c> methods on the interface and
    /// <see cref="StateTask{T}"/> properties on the proxy.
    /// </para>
    /// <para>
    /// Use this attribute to exclude properties that should not be part of the grain's
    /// remote API, such as:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Injected dependencies (services, loggers)</description></item>
    /// <item><description>Internal configuration properties</description></item>
    /// <item><description>Properties already exposed through custom interface methods</description></item>
    /// </list>
    /// </remarks>
    /// <example>
    /// <code>
    /// public partial class PlayerGrain : Grain, IPlayerGrain
    /// {
    ///     private readonly ILogger&lt;PlayerGrain&gt; _logger;
    ///
    ///     // These are part of the remote API (Get/Set methods generated)
    ///     public partial string Name { get; set; }
    ///     public partial int Score { get; set; }
    ///
    ///     // Excluded from code generation - not exposed remotely
    ///     [NotState]
    ///     public ILogger&lt;PlayerGrain&gt; Logger =&gt; _logger;
    ///
    ///     // Also excluded - internal tracking property
    ///     [NotState]
    ///     public DateTime LastAccessedInternal { get; set; }
    ///
    ///     public PlayerGrain(ILogger&lt;PlayerGrain&gt; logger)
    ///     {
    ///         _logger = logger;
    ///     }
    /// }
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = false)]
    public sealed class NotStateAttribute : Attribute
    {
    }
}
