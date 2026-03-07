using Scynapse.Security;
using Xunit;

namespace Scynapse.Security.Tests;

public interface ITestGrain { }

public class GrainResourceInferenceTests
{
    [Fact]
    public void FromGrainInterface_ProducesCorrectUri()
    {
        var resource = GrainResourceInference.FromGrainInterface(typeof(ITestGrain));
        Assert.Equal("scynapse.app.ITestGrain", resource);
    }

    [Fact]
    public void FromGrainMethod_ProducesCorrectUri()
    {
        var resource = GrainResourceInference.FromGrainMethod(typeof(ITestGrain), "DoWork");
        Assert.Equal("scynapse.app.ITestGrain.DoWork", resource);
    }

    [Fact]
    public void WildcardForGrain_ProducesCorrectPattern()
    {
        var pattern = GrainResourceInference.WildcardForGrain(typeof(ITestGrain));
        Assert.Equal("scynapse.app.ITestGrain.>", pattern);
    }

    [Fact]
    public void WildcardAllApp_CoversAllAppGrains()
    {
        Assert.Equal("scynapse.app.>", GrainResourceInference.WildcardAllApp);
    }

    [Fact]
    public void WildcardAll_CoversEverything()
    {
        Assert.Equal("scynapse.>", GrainResourceInference.WildcardAll);
    }

    [Fact]
    public void SystemResource_ProducesCorrectUri()
    {
        Assert.Equal("scynapse.system.security.gateway", GrainResourceInference.SystemResource("security.gateway"));
    }
}

public class SubjectNameMatcherTests
{
    [Theory]
    [InlineData("scynapse.app.IOrderGrain", "scynapse.app.IOrderGrain", true)]
    [InlineData("scynapse.app.IOrderGrain", "scynapse.app.IOtherGrain", false)]
    [InlineData("scynapse.app.*", "scynapse.app.IOrderGrain", true)]
    [InlineData("scynapse.app.*", "scynapse.app.IOrderGrain.PlaceOrder", false)] // * matches ONE segment
    [InlineData("scynapse.app.>", "scynapse.app.IOrderGrain", true)]
    [InlineData("scynapse.app.>", "scynapse.app.IOrderGrain.PlaceOrder", true)] // > matches one or more
    [InlineData("scynapse.>", "scynapse.app.IOrderGrain.PlaceOrder", true)]
    [InlineData("scynapse.>", "scynapse.system.membership", true)]
    [InlineData("scynapse.app.*.PlaceOrder", "scynapse.app.IOrderGrain.PlaceOrder", true)]
    [InlineData("scynapse.app.*.PlaceOrder", "scynapse.app.IInventoryGrain.PlaceOrder", true)]
    [InlineData("scynapse.app.*.PlaceOrder", "scynapse.app.IOrderGrain.CancelOrder", false)]
    [InlineData(">", "anything.at.all", true)]
    [InlineData("*", "single", true)]
    [InlineData("*", "multi.segment", false)]
    [InlineData("exact", "exact", true)]
    [InlineData("exact", "different", false)]
    public void Matches_Theory(string pattern, string subject, bool expected)
    {
        Assert.Equal(expected, SubjectNameMatcher.Matches(pattern, subject));
    }
}
