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
        Assert.Equal("scynapse:grain/ITestGrain", resource);
    }

    [Fact]
    public void FromGrainMethod_ProducesCorrectUri()
    {
        var resource = GrainResourceInference.FromGrainMethod(typeof(ITestGrain), "DoWork");
        Assert.Equal("scynapse:grain/ITestGrain/DoWork", resource);
    }

    [Fact]
    public void WildcardForGrain_ProducesCorrectPattern()
    {
        var pattern = GrainResourceInference.WildcardForGrain(typeof(ITestGrain));
        Assert.Equal("scynapse:grain/ITestGrain/*", pattern);
    }

    [Fact]
    public void WildcardAll_CoversAllGrains()
    {
        Assert.Equal("scynapse:grain/*", GrainResourceInference.WildcardAll);
    }
}
