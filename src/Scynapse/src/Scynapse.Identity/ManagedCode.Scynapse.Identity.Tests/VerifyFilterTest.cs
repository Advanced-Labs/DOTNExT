using System.Security.Claims;
using ManagedCode.Scynapse.Identity.Core.Constants;
using ManagedCode.Scynapse.Identity.Tests.Cluster;
using ManagedCode.Scynapse.Identity.Tests.Cluster.Grains;
using ManagedCode.Scynapse.Identity.Tests.Constants;
using Scynapse.Runtime;
using Shouldly;
using Xunit;
using Xunit.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ManagedCode.Scynapse.Identity.Client.Filters;

namespace ManagedCode.Scynapse.Identity.Tests;

[Collection(nameof(TestClusterApplication))]
public class VerifyFilterTest(TestClusterApplication testApp, ITestOutputHelper outputHelper)
{
    [Fact]
    public async Task VerifyFilter_DirectGrainCall_WithoutRole_ShouldThrow()
    {
        // First test through HTTP to ensure the setup is correct
        var client = testApp.CreateClient();
        
        // Create HttpContext and setup authentication
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, "testuser"),
            new(ClaimTypes.NameIdentifier, "testuser"), 
            new(ClaimTypes.Actor, "testuser"),
            new(ClaimTypes.Role, TestRoles.USER) // Only user role, not admin
        };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        httpContext.User = principal;
        
        // Simulate the action filter behavior
        RequestContext.Set(ScynapseIdentityConstants.USER_CLAIMS, principal);
        
        try
        {
            // Get grain directly from cluster client
            var userGrain = testApp.Cluster.Client.GetGrain<IUserGrain>("testuser");
            
            // This should throw UnauthorizedAccessException
            var result = await userGrain.BanUser();
            
            // If we get here, filter is not working
            outputHelper.WriteLine($"ERROR: Method returned: {result}");
            true.ShouldBe(false, "Expected UnauthorizedAccessException but method succeeded");
        }
        catch (UnauthorizedAccessException ex)
        {
            outputHelper.WriteLine($"Success: Got expected exception - {ex.Message}");
            ex.Message.ShouldContain("Access denied");
        }
        catch (Exception ex)
        {
            outputHelper.WriteLine($"ERROR: Got unexpected exception type {ex.GetType().Name}: {ex.Message}");
            throw;
        }
        finally
        {
            RequestContext.Clear();
        }
    }
}