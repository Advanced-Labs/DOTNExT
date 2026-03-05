using Scynapse.Security.Orleans;
using Xunit;

namespace Scynapse.Security.Orleans.Tests;

public class SecurityPolicyAttributeTests
{
    [Fact]
    public void DefaultPolicy_RequiresAuthentication()
    {
        var attr = new SecurityPolicyAttribute();
        Assert.True(attr.RequiresAuthentication);
        Assert.False(attr.AllowAnonymous);
    }

    [Fact]
    public void AllowAnonymous_DisablesAuthentication()
    {
        var attr = new SecurityPolicyAttribute { AllowAnonymous = true };
        Assert.True(attr.AllowAnonymous);
    }
}

public class RequireCapabilityAttributeTests
{
    [Fact]
    public void Action_CanBeSet()
    {
        var attr = new RequireCapabilityAttribute { Action = "read" };
        Assert.Equal("read", attr.Action);
    }

    [Fact]
    public void Resource_DefaultsToNull()
    {
        var attr = new RequireCapabilityAttribute { Action = "write" };
        Assert.Null(attr.Resource);
    }

    [Fact]
    public void Resource_CanBeExplicitlySet()
    {
        var attr = new RequireCapabilityAttribute { Action = "read", Resource = "scynapse:data" };
        Assert.Equal("scynapse:data", attr.Resource);
    }
}
