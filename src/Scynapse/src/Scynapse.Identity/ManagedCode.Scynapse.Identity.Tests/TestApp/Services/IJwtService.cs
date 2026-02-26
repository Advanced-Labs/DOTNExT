using System.Security.Claims;

namespace ManagedCode.Scynapse.Identity.Tests.TestApp.Services;

public interface IJwtService
{
    string GenerateToken(string userId, string username, IEnumerable<string> roles);
    ClaimsPrincipal? ValidateToken(string token);
} 