using System.Reflection;
using Scynapse;
using Scynapse.Runtime;
using Scynapse.Serialization.Invocation;

namespace Scynapse.Security.Orleans.Tests;

/// <summary>
/// Minimal mock of IIncomingGrainCallContext for testing call filters in isolation.
/// Captures RequestContext values at the point of Invoke() — this is where grain code
/// runs in real Orleans, and where verified caller identity should be visible.
/// </summary>
internal sealed class TestIncomingGrainCallContext : IIncomingGrainCallContext
{
    public IInvokable Request => throw new NotImplementedException();
    public object Grain { get; set; } = null!;
    public GrainId? SourceId { get; set; }
    public GrainId TargetId { get; set; }
    public GrainInterfaceType InterfaceType { get; set; }
    public string InterfaceName { get; set; } = "";
    public string MethodName { get; set; } = "";
    public MethodInfo InterfaceMethod { get; set; } = null!;
    public object? Result { get; set; }
    public Response? Response { get; set; }
    public IGrainContext TargetContext => throw new NotImplementedException();
    public MethodInfo ImplementationMethod => InterfaceMethod;

    public bool Invoked { get; private set; }

    /// <summary>
    /// RequestContext values captured at the moment Invoke() runs (grain execution point).
    /// </summary>
    public Dictionary<string, object?> CapturedRequestContext { get; } = new();

    /// <summary>
    /// Keys to capture from RequestContext when Invoke() is called.
    /// </summary>
    public List<string> KeysToCapture { get; } = new();

    public Task Invoke()
    {
        Invoked = true;
        foreach (var key in KeysToCapture)
        {
            CapturedRequestContext[key] = RequestContext.Get(key);
        }
        return Task.CompletedTask;
    }
}

/// <summary>
/// Minimal mock of IOutgoingGrainCallContext for testing call filters in isolation.
/// </summary>
internal sealed class TestOutgoingGrainCallContext : IOutgoingGrainCallContext
{
    public IInvokable Request => throw new NotImplementedException();
    public object Grain { get; set; } = null!;
    public GrainId? SourceId { get; set; }
    public GrainId TargetId { get; set; }
    public GrainInterfaceType InterfaceType { get; set; }
    public string InterfaceName { get; set; } = "";
    public string MethodName { get; set; } = "";
    public MethodInfo InterfaceMethod { get; set; } = null!;
    public object? Result { get; set; }
    public Response? Response { get; set; }
    public IGrainContext? SourceContext => null;

    public bool Invoked { get; private set; }

    public Dictionary<string, object?> CapturedRequestContext { get; } = new();
    public List<string> KeysToCapture { get; } = new();

    public Task Invoke()
    {
        Invoked = true;
        foreach (var key in KeysToCapture)
        {
            CapturedRequestContext[key] = RequestContext.Get(key);
        }
        return Task.CompletedTask;
    }
}
