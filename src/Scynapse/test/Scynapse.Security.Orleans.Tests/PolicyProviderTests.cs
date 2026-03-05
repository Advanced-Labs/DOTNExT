using Scynapse.Security.Orleans;
using Xunit;

namespace Scynapse.Security.Orleans.Tests;

// Test grain interfaces for policy provider tests
[SecurityPolicy(RequiresAuthentication = true)]
public interface ISecureTestGrain : IGrainWithStringKey
{
    [RequireCapability(Action = "read")]
    Task<string> GetDataAsync();

    [RequireCapability(Action = "write")]
    Task SetDataAsync(string value);
}

[SecurityPolicy(AllowAnonymous = true)]
public interface IAnonymousTestGrain : IGrainWithStringKey
{
    Task<string> GetPublicDataAsync();
}

// No SecurityPolicy attribute — should default to requiring authentication
public interface IDefaultPolicyTestGrain : IGrainWithStringKey
{
    Task DoSomethingAsync();
}

[SecurityPolicy(RequiresAuthentication = true)]
public interface IPartiallySecuredGrain : IGrainWithStringKey
{
    [RequireCapability(Action = "admin")]
    Task AdminActionAsync();

    // No RequireCapability — requires authentication but no specific capability
    Task BasicActionAsync();
}

public class AttributeBasedPolicyProviderTests
{
    private readonly AttributeBasedPolicyProvider _provider = new();

    [Fact]
    public void SecureGrain_RequiresAuthentication()
    {
        var policy = _provider.GetPolicy(typeof(ISecureTestGrain));
        Assert.True(policy.RequiresAuthentication);
        Assert.False(policy.AllowAnonymous);
    }

    [Fact]
    public void AnonymousGrain_AllowsAnonymous()
    {
        var policy = _provider.GetPolicy(typeof(IAnonymousTestGrain));
        Assert.True(policy.AllowAnonymous);
    }

    [Fact]
    public void DefaultPolicy_RequiresAuthentication()
    {
        // No SecurityPolicy attribute defaults to secure
        var policy = _provider.GetPolicy(typeof(IDefaultPolicyTestGrain));
        Assert.True(policy.RequiresAuthentication);
    }

    [Fact]
    public void GetRequiredAction_ReturnsActionFromAttribute()
    {
        var method = typeof(ISecureTestGrain).GetMethod(nameof(ISecureTestGrain.GetDataAsync))!;
        var action = _provider.GetRequiredAction(method);
        Assert.Equal("read", action);
    }

    [Fact]
    public void GetRequiredAction_ReturnsNullWhenNoAttribute()
    {
        var method = typeof(IPartiallySecuredGrain).GetMethod(nameof(IPartiallySecuredGrain.BasicActionAsync))!;
        var action = _provider.GetRequiredAction(method);
        Assert.Null(action);
    }

    [Fact]
    public void GetRequiredResource_ReturnsResourceFromAttribute()
    {
        // Default resource is null (inferred from grain type)
        var method = typeof(ISecureTestGrain).GetMethod(nameof(ISecureTestGrain.GetDataAsync))!;
        var resource = _provider.GetRequiredResource(method);
        Assert.Null(resource);
    }

    [Fact]
    public void PolicyIsCached()
    {
        var policy1 = _provider.GetPolicy(typeof(ISecureTestGrain));
        var policy2 = _provider.GetPolicy(typeof(ISecureTestGrain));
        Assert.Same(policy1, policy2);
    }
}
