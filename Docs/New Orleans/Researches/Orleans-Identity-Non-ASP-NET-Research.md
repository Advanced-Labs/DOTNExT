# Orleans.Identity for Non-ASP.NET Usage - Research Report

**Date:** 2026-01-16
**Source Library:** [managedcode/Orleans.Identity](https://github.com/managedcode/Orleans.Identity)
**Local Clone:** `/src/NewOrleans/src/Orleans.Identity`

---

## Executive Summary

Orleans.Identity **can be adapted** for non-ASP.NET usage with moderate modifications. The Server+Core packages have implicit ASP.NET dependencies through SDK choice that must be changed. The library does NOT use `IAuthorizationService` - it implements custom attribute-based authorization that can work without ASP.NET infrastructure.

**Key verdict:** Usable with modifications. The core logic is sound, but SDK references and some authorization attributes need adjustment.

---

## 1. ASP.NET-Free Usage Confirmation

### 1.1 RequestContext Key

**Key:** `"MC-UserClaims"`

**Location:** `ManagedCode.Orleans.Identity.Core/Constants/OrleansIdentityConstants.cs:5`

```csharp
public static class OrleansIdentityConstants
{
    public const string USER_CLAIMS = "MC-UserClaims";
}
```

**Usage Pattern:**
- **Set:** `RequestContext.Set(OrleansIdentityConstants.USER_CLAIMS, claimsPrincipal);`
- **Get:** `RequestContext.Get(OrleansIdentityConstants.USER_CLAIMS) as ClaimsPrincipal`

### 1.2 ASP.NET Runtime Dependencies in Server Package

**File:** `GrainAuthorizationIncomingFilter.cs:1-11`

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;         // BCL - OK
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;  // <-- ASP.NET DEPENDENCY
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Runtime;
using ManagedCode.Orleans.Identity.Core.Constants;
```

**The `Microsoft.AspNetCore.Authorization` import is used for:**
- `AuthorizeAttribute` - Line 75-82
- `AllowAnonymousAttribute` - Line 68

**Root Cause:** Both .csproj files use `Microsoft.NET.Sdk.Web`:

```xml
<!-- ManagedCode.Orleans.Identity.Server.csproj:1 -->
<Project Sdk="Microsoft.NET.Sdk.Web">

<!-- ManagedCode.Orleans.Identity.Core.csproj:1 -->
<Project Sdk="Microsoft.NET.Sdk.Web">
```

### 1.3 Core Package Serialization - ASP.NET Dependencies

**VERDICT: NO ASP.NET DEPENDENCIES IN SERIALIZATION**

The surrogates only use BCL types:
- `System.Security.Claims.Claim`
- `System.Security.Claims.ClaimsIdentity`
- `System.Security.Claims.ClaimsPrincipal`

**ClaimSurrogate.cs:9-25:**
```csharp
[GenerateSerializer]
public struct ClaimSurrogate(string type, string value, string valueType, string issuer, string originalIssuer)
{
    [Id(0)] public string Issuer { get; set; } = issuer;
    [Id(1)] public string OriginalIssuer { get; set; } = originalIssuer;
    [Id(2)] public string Type { get; set; } = type;
    [Id(3)] public string Value { get; set; } = value;
    [Id(4)] public string ValueType { get; set; } = valueType;
}
```

### 1.4 Verdict: Can Server+Core Be Used Without ASP.NET?

**YES, with the following modifications:**

| Package | File | Change Required |
|---------|------|-----------------|
| Core | `.csproj:1` | Change `Microsoft.NET.Sdk.Web` to `Microsoft.NET.Sdk` |
| Server | `.csproj:1` | Change `Microsoft.NET.Sdk.Web` to `Microsoft.NET.Sdk` |
| Server | `GrainAuthorizationIncomingFilter.cs` | Replace ASP.NET `AuthorizeAttribute` with custom attribute |

### 1.5 Required Modifications

**1.5.1 Create Custom Authorization Attributes**

Create in Core package (`ManagedCode.Orleans.Identity.Core/Attributes/`):

```csharp
// OrleansAuthorizeAttribute.cs
namespace ManagedCode.Orleans.Identity.Core.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = true)]
public class OrleansAuthorizeAttribute : Attribute
{
    public string? Roles { get; set; }
    public string? Policy { get; set; }
}

// OrleansAllowAnonymousAttribute.cs
namespace ManagedCode.Orleans.Identity.Core.Attributes;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = true)]
public class OrleansAllowAnonymousAttribute : Attribute { }
```

**1.5.2 Update GrainAuthorizationIncomingFilter.cs**

```csharp
// Replace:
using Microsoft.AspNetCore.Authorization;

// With:
using ManagedCode.Orleans.Identity.Core.Attributes;

// Replace all:
AuthorizeAttribute -> OrleansAuthorizeAttribute
AllowAnonymousAttribute -> OrleansAllowAnonymousAttribute
```

**1.5.3 Update .csproj Files**

```xml
<!-- Both Server and Core .csproj files -->
<Project Sdk="Microsoft.NET.Sdk">
    <!-- Remove <OutputType>Library</OutputType> - implicit for SDK -->
```

---

## 2. mTLS Integration

### 2.1 Does Orleans mTLS Require ASP.NET?

**PARTIALLY - Uses connection abstractions, not HTTP stack**

**Orleans.Connections.Security.csproj:1-15:**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <!-- Uses standard SDK, NOT Sdk.Web -->
  <PropertyGroup>
    <PackageId>Microsoft.Orleans.Connections.Security</PackageId>
  </PropertyGroup>
  <ItemGroup>
    <ProjectReference Include="..\Orleans.Runtime\Orleans.Runtime.csproj" />
  </ItemGroup>
</Project>
```

**However, it imports connection abstractions:**
```csharp
// TlsServerConnectionMiddleware.cs:7-8
using Microsoft.AspNetCore.Connections;
using Microsoft.AspNetCore.Connections.Features;
```

These are from `Microsoft.AspNetCore.Connections.Abstractions` - a low-level package that doesn't require the full ASP.NET HTTP stack. Orleans itself depends on this.

### 2.2 mTLS Operating Layer

**TRANSPORT/CONNECTION LAYER (not grain-call layer)**

mTLS operates in the connection middleware pipeline:

```
TCP Connection
    ↓
TlsServerConnectionMiddleware.OnConnectionAsync()  ← mTLS handshake here
    ↓
ConnectionContext.Features.Set<ITlsConnectionFeature>(feature)
    ↓
feature.RemoteCertificate = sslStream.RemoteCertificate  ← Certificate captured
    ↓
Orleans Message Pipeline
    ↓
Grain Call Filter (no access to ConnectionContext)
```

**Key code from TlsServerConnectionMiddleware.cs:58-183:**
```csharp
private async Task InnerOnConnectionAsync(ConnectionContext context)
{
    var feature = new TlsConnectionFeature();
    context.Features.Set<ITlsConnectionFeature>(feature);

    // ... TLS handshake ...

    // After successful handshake:
    feature.RemoteCertificate = ConvertToX509Certificate2(sslStream.RemoteCertificate);

    await _next(context);  // Continue pipeline
}
```

### 2.3 Can We Extract Identity Before Orleans.Identity's Filter?

**YES, BUT NOT DIRECTLY IN A GRAIN CALL FILTER**

The challenge is that `IIncomingGrainCallContext` does not expose `ConnectionContext`:

```csharp
// IGrainCallContext.cs - NO connection access
public interface IIncomingGrainCallContext : IGrainCallContext
{
    public IGrainContext TargetContext { get; }
    MethodInfo ImplementationMethod { get; }
    // No ConnectionContext or connection features
}
```

**Solution: Connection-level middleware that populates RequestContext**

### 2.4 mTLS Identity Flow Architecture

```
┌─────────────────────────────────────────────────────────────────────┐
│ TCP Connection Established                                          │
└─────────────────────────────────────────────────────────────────────┘
                                  ↓
┌─────────────────────────────────────────────────────────────────────┐
│ TlsServerConnectionMiddleware                                        │
│ - SslStream.AuthenticateAsServerAsync()                             │
│ - ITlsConnectionFeature.RemoteCertificate = client cert             │
└─────────────────────────────────────────────────────────────────────┘
                                  ↓
┌─────────────────────────────────────────────────────────────────────┐
│ CUSTOM: CertificateIdentityMiddleware (NEW - you create this)       │
│ - Read: context.Features.Get<ITlsConnectionFeature>()               │
│ - Extract: X509Certificate2 → ClaimsPrincipal                       │
│ - Store: Store principal for connection (NOT RequestContext yet)    │
└─────────────────────────────────────────────────────────────────────┘
                                  ↓
┌─────────────────────────────────────────────────────────────────────┐
│ Orleans Message Deserialization                                      │
└─────────────────────────────────────────────────────────────────────┘
                                  ↓
┌─────────────────────────────────────────────────────────────────────┐
│ CUSTOM: IdentityPropagationGrainCallFilter (IIncomingGrainCallFilter)│
│ - Retrieve connection-scoped ClaimsPrincipal                         │
│ - RequestContext.Set("MC-UserClaims", claimsPrincipal)              │
│ - ORDER: Must run BEFORE GrainAuthorizationIncomingFilter           │
└─────────────────────────────────────────────────────────────────────┘
                                  ↓
┌─────────────────────────────────────────────────────────────────────┐
│ GrainAuthorizationIncomingFilter (Orleans.Identity)                  │
│ - Read: RequestContext.Get("MC-UserClaims")                         │
│ - Check: [Authorize] attributes                                      │
│ - Throw: UnauthorizedAccessException if not authorized              │
└─────────────────────────────────────────────────────────────────────┘
                                  ↓
┌─────────────────────────────────────────────────────────────────────┐
│ Grain Method Execution                                               │
│ - this.GetCurrentUser() → ClaimsPrincipal                           │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.5 Implementation Sketch: Certificate to ClaimsPrincipal Bridge

**Challenge:** Connection middleware and grain call filters operate in different contexts. Need to bridge them.

**Approach 1: Connection-scoped storage + AsyncLocal**

```csharp
// Store identity in connection features, bridge via AsyncLocal

public class ConnectionIdentityFeature
{
    public ClaimsPrincipal? Principal { get; set; }
}

// Connection middleware (runs once per connection)
public class CertificateIdentityMiddleware
{
    private readonly ConnectionDelegate _next;

    public async Task OnConnectionAsync(ConnectionContext context)
    {
        var tlsFeature = context.Features.Get<ITlsConnectionFeature>();
        if (tlsFeature?.RemoteCertificate != null)
        {
            var principal = CreatePrincipalFromCertificate(tlsFeature.RemoteCertificate);
            context.Features.Set(new ConnectionIdentityFeature { Principal = principal });
        }
        await _next(context);
    }

    private ClaimsPrincipal CreatePrincipalFromCertificate(X509Certificate2 cert)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.X500DistinguishedName, cert.Subject),
            new Claim(ClaimTypes.Thumbprint, cert.Thumbprint),
            new Claim("issuer", cert.Issuer),
            // Extract CN, OU, O from Subject for roles/identity
        };

        // Parse Subject for common name
        var cn = GetCommonName(cert.Subject);
        if (!string.IsNullOrEmpty(cn))
        {
            claims.Add(new Claim(ClaimTypes.Name, cn));
            claims.Add(new Claim(ClaimTypes.NameIdentifier, cn));
        }

        var identity = new ClaimsIdentity(claims, "X509Certificate");
        return new ClaimsPrincipal(identity);
    }
}
```

**Approach 2: Grain call filter with connection access (requires Orleans modification)**

This would require modifying Orleans to expose connection context to grain call filters - more invasive but cleaner.

---

## 3. Custom AuthN/AuthZ Services

### 3.1 Does It Use IAuthorizationService?

**NO - Implements custom attribute checking**

**GrainAuthorizationIncomingFilter.cs:64-87:**
```csharp
private static bool IsGrainAuthorized(MemberInfo methodInfo, out List<AuthorizeAttribute> attributes)
{
    attributes = [];

    // Check for AllowAnonymous - bypass all auth
    if (Attribute.IsDefined(methodInfo, typeof(AllowAnonymousAttribute)))
    {
        return false;
    }

    // Check class-level [Authorize]
    if (methodInfo.DeclaringType != null &&
        Attribute.IsDefined(methodInfo.DeclaringType, typeof(AuthorizeAttribute)))
    {
        attributes.AddRange(Attribute.GetCustomAttributes(
            methodInfo.DeclaringType, typeof(AuthorizeAttribute))
            .Cast<AuthorizeAttribute>());
    }

    // Check method-level [Authorize]
    if (Attribute.IsDefined(methodInfo, typeof(AuthorizeAttribute)))
    {
        attributes.AddRange(Attribute.GetCustomAttributes(
            methodInfo, typeof(AuthorizeAttribute))
            .Cast<AuthorizeAttribute>());
        return true;
    }

    return attributes.Any();
}
```

### 3.2 Role Checking Implementation

**GrainAuthorizationIncomingFilter.cs:29-52:**
```csharp
if (rolesRequired)
{
    // Get user's roles from claims
    var userRoles = user.FindAll(ClaimTypes.Role).Select(c => c.Value).ToHashSet();

    var hasRequiredRole = attributes.Any(attribute =>
    {
        if (string.IsNullOrWhiteSpace(attribute.Roles))
            return true;

        // Parse comma-separated roles
        var requiredRoles = attribute.Roles.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(r => r.Trim());

        // ANY role match = authorized
        return requiredRoles.Any(role => userRoles.Contains(role));
    });

    if (!hasRequiredRole)
    {
        throw new UnauthorizedAccessException("Access denied. User does not have required roles.");
    }
}
```

### 3.3 Extension Points for Custom Authorization

**Option 1: Replace the entire filter**
```csharp
public class CustomAuthorizationFilter : IIncomingGrainCallFilter
{
    private readonly IAuthorizationGrain _authGrain;  // Your Orleans-based AuthZ

    public async Task Invoke(IIncomingGrainCallContext context)
    {
        var user = RequestContext.Get(OrleansIdentityConstants.USER_CLAIMS) as ClaimsPrincipal;

        // Call your custom Orleans AuthZ grain
        var authorized = await _authGrain.IsAuthorizedAsync(
            user?.Identity?.Name,
            context.MethodName,
            context.TargetId);

        if (!authorized)
            throw new UnauthorizedAccessException();

        await context.Invoke();
    }
}

// Registration
siloBuilder.AddIncomingGrainCallFilter<CustomAuthorizationFilter>();
```

**Option 2: Chain with existing filter**
```csharp
siloBuilder.AddIncomingGrainCallFilter<CustomPolicyFilter>();  // Runs first
siloBuilder.AddOrleansIdentity();  // Then Orleans.Identity filter
```

### 3.4 Available Interfaces for Extension

| Interface | Purpose | Location |
|-----------|---------|----------|
| `IIncomingGrainCallFilter` | Server-side call interception | Orleans.Core.Abstractions |
| `IOutgoingGrainCallFilter` | Client-side call interception | Orleans.Core.Abstractions |
| `IConverter<T, TSurrogate>` | Custom serialization | Orleans |

---

## 4. AuthZ Support Analysis

### 4.1 Policy-Based Authorization

**NOT SUPPORTED**

The current implementation only checks:
- `[Authorize]` - Requires authentication
- `[Authorize(Roles = "X,Y")]` - Requires any of the listed roles
- `[AllowAnonymous]` - Bypasses authorization

**No support for:**
- `[Authorize(Policy = "AdminPolicy")]`
- Custom `IAuthorizationRequirement`
- Custom `IAuthorizationHandler`

### 4.2 AuthorizationOptions / Policy Registration

**NOT USED**

There is no integration with ASP.NET Core's `AuthorizationOptions` or policy builder.

### 4.3 Adding Custom Policy Support

**Required changes to GrainAuthorizationIncomingFilter:**

```csharp
public class PolicyAwareAuthorizationFilter : IIncomingGrainCallFilter
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IPolicyProvider _policyProvider;  // Your custom policy provider

    public async Task Invoke(IIncomingGrainCallContext context)
    {
        var attributes = GetAuthorizeAttributes(context.ImplementationMethod);
        var user = GetUserFromRequestContext();

        foreach (var attr in attributes)
        {
            if (!string.IsNullOrEmpty(attr.Policy))
            {
                // Custom policy evaluation
                var policy = await _policyProvider.GetPolicyAsync(attr.Policy);
                var requirements = policy.GetRequirements();

                foreach (var requirement in requirements)
                {
                    var handler = _serviceProvider.GetService(
                        typeof(IRequirementHandler<>).MakeGenericType(requirement.GetType()));

                    if (!await ((dynamic)handler).HandleAsync(user, requirement, context))
                    {
                        throw new UnauthorizedAccessException(
                            $"Policy '{attr.Policy}' not satisfied");
                    }
                }
            }
            // ... existing role checking ...
        }

        await context.Invoke();
    }
}
```

### 4.4 Custom AuthZ Without ASP.NET Infrastructure

**Create a grain-based authorization system:**

```csharp
// Interfaces
public interface IAuthorizationGrain : IGrainWithStringKey
{
    Task<bool> IsAuthorizedAsync(string userId, string resource, string action);
    Task<bool> CheckPolicyAsync(string userId, string policyName, object context);
}

public interface IPolicyGrain : IGrainWithStringKey  // Key = policy name
{
    Task<bool> EvaluateAsync(ClaimsPrincipal user, object context);
    Task SetRequirementsAsync(List<AuthorizationRequirement> requirements);
}

// Grain implementations store policies in grain state
// Filter calls these grains for authorization decisions
```

---

## 5. Federation & Multi-Cluster

### 5.1 ClaimsPrincipal Serialization Details

**What IS serialized:**

| Property | Preserved | Location |
|----------|-----------|----------|
| `Claim.Type` | Yes | `ClaimSurrogate.cs:16` |
| `Claim.Value` | Yes | `ClaimSurrogate.cs:19` |
| `Claim.ValueType` | Yes | `ClaimSurrogate.cs:22` |
| `Claim.Issuer` | Yes | `ClaimSurrogate.cs:10` |
| `Claim.OriginalIssuer` | Yes | `ClaimSurrogate.cs:13` |
| `ClaimsIdentity.AuthenticationType` | Yes | `ClaimsIdentitySurrogate.cs:13` |
| `ClaimsIdentity.NameClaimType` | Yes | `ClaimsIdentitySurrogate.cs:22` |
| `ClaimsIdentity.RoleClaimType` | Yes | `ClaimsIdentitySurrogate.cs:19` |
| Multiple Identities | Yes | `ClaimsPrincipalSurrogate.cs:13` |

**What is NOT serialized:**

| Property | Preserved | Impact |
|----------|-----------|--------|
| `Claim.Properties` | **NO** | Custom claim properties lost |
| `ClaimsIdentity.Actor` | **NO** | Delegation chain broken |
| `ClaimsIdentity.BootstrapContext` | **NO** | Token reference lost |
| `ClaimsIdentity.Label` | **NO** | Identity label lost |

### 5.2 Multiple ClaimsIdentity Support

**SUPPORTED**

```csharp
// ClaimsPrincipalSurrogateConverter.cs:21-25
public ClaimsPrincipalSurrogate ConvertToSurrogate(in ClaimsPrincipal value)
{
    var identities = value.Identities?.ToList();  // All identities preserved
    return new ClaimsPrincipalSurrogate(identities, value.Identity?.AuthenticationType);
}
```

### 5.3 Actor Delegation Chain

**NOT SUPPORTED**

The `ClaimsIdentity.Actor` property (for delegation chains like "User A acting on behalf of User B") is NOT serialized.

**ClaimsIdentitySurrogate fields (missing Actor):**
```csharp
// ClaimsIdentitySurrogate.cs:10-23
public struct ClaimsIdentitySurrogate(
    List<Claim>? claims,
    string? authenticationType,
    string? nameType,
    string? roleType)  // No Actor parameter
{
    [Id(0)] public string? AuthenticationType
    [Id(1)] public List<Claim>? Claims
    [Id(2)] public string? RoleType
    [Id(3)] public string? NameType
    // Actor is MISSING
}
```

### 5.4 Trusted Issuers / Federation Domains

**NOT IMPLEMENTED**

The library preserves `Claim.Issuer` and `Claim.OriginalIssuer` but does NOT validate them. There is no concept of trusted issuers.

### 5.5 Cross-Cluster Identity Propagation

**NOT SPECIFICALLY HANDLED**

When a grain call crosses cluster boundaries (Orleans geo-replication), the `RequestContext` data flows with the message, so `ClaimsPrincipal` will serialize correctly.

However:
- No issuer validation on receiving cluster
- No trust relationship between clusters
- No claim transformation for federation boundaries

---

## 6. Required Modifications for Federation

### 6.1 Add Actor Chain Serialization

**File: ClaimsIdentitySurrogate.cs**

```csharp
[GenerateSerializer]
public struct ClaimsIdentitySurrogate(
    List<Claim>? claims,
    string? authenticationType,
    string? nameType,
    string? roleType,
    ClaimsIdentity? actor)  // ADD this
{
    [Id(0)] public string? AuthenticationType { get; set; } = authenticationType;
    [Id(1)] public List<Claim>? Claims { get; set; } = claims;
    [Id(2)] public string? RoleType { get; set; } = roleType;
    [Id(3)] public string? NameType { get; set; } = nameType;
    [Id(4)] public ClaimsIdentity? Actor { get; set; } = actor;  // ADD this
}
```

**File: ClaimsIdentitySurrogateConverter.cs**

```csharp
public ClaimsIdentitySurrogate ConvertToSurrogate(in ClaimsIdentity value)
{
    return new ClaimsIdentitySurrogate(
        value.Claims.ToList(),
        value.AuthenticationType,
        value.NameClaimType,
        value.RoleClaimType,
        value.Actor);  // ADD this
}

public ClaimsIdentity ConvertFromSurrogate(in ClaimsIdentitySurrogate surrogate)
{
    var identity = new ClaimsIdentity(
        surrogate.Claims,
        surrogate.AuthenticationType,
        surrogate.NameType,
        surrogate.RoleType);
    identity.Actor = surrogate.Actor;  // ADD this
    return identity;
}
```

### 6.2 Add Issuer Validation

**Create: TrustedIssuerValidator.cs**

```csharp
namespace ManagedCode.Orleans.Identity.Core.Federation;

public interface ITrustedIssuerValidator
{
    bool IsTrusted(string issuer);
    bool IsTrustedForClaim(string issuer, string claimType);
}

public class TrustedIssuerValidator : ITrustedIssuerValidator
{
    private readonly HashSet<string> _trustedIssuers;
    private readonly Dictionary<string, HashSet<string>> _issuerClaimRestrictions;

    public TrustedIssuerValidator(TrustedIssuerOptions options)
    {
        _trustedIssuers = options.TrustedIssuers.ToHashSet();
        _issuerClaimRestrictions = options.ClaimRestrictions;
    }

    public bool IsTrusted(string issuer) => _trustedIssuers.Contains(issuer);

    public bool IsTrustedForClaim(string issuer, string claimType)
    {
        if (!IsTrusted(issuer)) return false;
        if (!_issuerClaimRestrictions.TryGetValue(issuer, out var allowed))
            return true;  // No restrictions = all claims allowed
        return allowed.Contains(claimType);
    }
}

public class TrustedIssuerOptions
{
    public List<string> TrustedIssuers { get; set; } = new();
    public Dictionary<string, HashSet<string>> ClaimRestrictions { get; set; } = new();
}
```

**Integrate into GrainAuthorizationIncomingFilter:**

```csharp
public class FederatedAuthorizationFilter : IIncomingGrainCallFilter
{
    private readonly ITrustedIssuerValidator _issuerValidator;

    public async Task Invoke(IIncomingGrainCallContext context)
    {
        var user = GetUserFromRequestContext();

        // Validate all claim issuers
        foreach (var claim in user?.Claims ?? Enumerable.Empty<Claim>())
        {
            if (!_issuerValidator.IsTrustedForClaim(claim.Issuer, claim.Type))
            {
                throw new UnauthorizedAccessException(
                    $"Untrusted issuer '{claim.Issuer}' for claim type '{claim.Type}'");
            }
        }

        // Continue with existing authorization...
        await context.Invoke();
    }
}
```

### 6.3 Cross-Cluster Identity Propagation

**Create: ClusterIdentityPropagationFilter.cs**

```csharp
namespace ManagedCode.Orleans.Identity.Server.Federation;

public class ClusterIdentityPropagationFilter : IOutgoingGrainCallFilter
{
    private readonly string _localClusterId;

    public async Task Invoke(IOutgoingGrainCallContext context)
    {
        // Mark identity with source cluster
        var user = RequestContext.Get(OrleansIdentityConstants.USER_CLAIMS) as ClaimsPrincipal;

        if (user != null)
        {
            // Add cluster provenance claim if crossing clusters
            var identity = user.Identity as ClaimsIdentity;
            if (identity != null && !identity.HasClaim("source_cluster", _localClusterId))
            {
                identity.AddClaim(new Claim("source_cluster", _localClusterId,
                    ClaimValueTypes.String, _localClusterId));
            }
        }

        await context.Invoke();
    }
}
```

---

## 7. Summary of All Required Changes

### 7.1 For ASP.NET-Free Usage (Minimal)

| File | Change |
|------|--------|
| `Core/ManagedCode.Orleans.Identity.Core.csproj:1` | `Microsoft.NET.Sdk.Web` → `Microsoft.NET.Sdk` |
| `Server/ManagedCode.Orleans.Identity.Server.csproj:1` | `Microsoft.NET.Sdk.Web` → `Microsoft.NET.Sdk` |
| `Core/Attributes/` (new) | Create `OrleansAuthorizeAttribute.cs`, `OrleansAllowAnonymousAttribute.cs` |
| `Server/GrainCallFilter/GrainAuthorizationIncomingFilter.cs:7` | Replace `Microsoft.AspNetCore.Authorization` with custom attributes |

### 7.2 For mTLS Integration

| Component | Change |
|-----------|--------|
| Connection Middleware (new) | Create `CertificateIdentityMiddleware` to extract cert and create `ClaimsPrincipal` |
| Grain Filter (new) | Create `IdentityPropagationFilter` to bridge connection identity to `RequestContext` |
| Configuration | Register middleware in correct order: TLS → CertificateIdentity → Orleans |

### 7.3 For Policy-Based Authorization

| File | Change |
|------|--------|
| `Core/Attributes/OrleansAuthorizeAttribute.cs` | Add `Policy` property |
| `Server/GrainCallFilter/` (new) | Create `PolicyAwareAuthorizationFilter.cs` |
| Core (new) | Create `IPolicyProvider`, `IRequirementHandler<T>` interfaces |

### 7.4 For Federation Support

| File | Change |
|------|--------|
| `Core/Serializations/ClaimsIdentitySurrogate.cs` | Add `Actor` field |
| `Core/Serializations/ClaimsIdentitySurrogateConverter.cs` | Serialize/deserialize `Actor` property |
| `Core/Federation/` (new) | Create `ITrustedIssuerValidator`, `TrustedIssuerOptions` |
| `Server/Federation/` (new) | Create `FederatedAuthorizationFilter`, `ClusterIdentityPropagationFilter` |

---

## 8. Compatibility and Breaking Changes

### 8.1 Serialization Compatibility

Adding the `Actor` field to `ClaimsIdentitySurrogate` is **backwards compatible** because:
- New field has `[Id(4)]` - won't conflict with existing fields
- Nullable type - old messages will deserialize as `Actor = null`

### 8.2 Attribute Changes

Changing from `Microsoft.AspNetCore.Authorization.AuthorizeAttribute` to custom attributes is a **breaking change**:
- Existing grains using `[Authorize]` must update using statements
- Or: Create attribute that inherits from both (not recommended)

### 8.3 Filter Order Dependencies

The filter registration order matters:
```csharp
// Correct order:
siloBuilder.AddIncomingGrainCallFilter<IdentityPropagationFilter>();  // Sets identity
siloBuilder.AddIncomingGrainCallFilter<FederatedAuthorizationFilter>();  // Validates
siloBuilder.AddOrleansIdentity();  // Orleans.Identity authorization
```

---

## 9. Conclusion

Orleans.Identity is a solid foundation for non-ASP.NET usage with moderate modifications. The core authorization logic is custom-implemented and doesn't depend on ASP.NET's authorization infrastructure. The main changes needed are:

1. **SDK Change** - Switch from Web SDK to standard SDK
2. **Custom Attributes** - Replace ASP.NET authorization attributes
3. **mTLS Bridge** - Create middleware to bridge connection certificates to RequestContext
4. **Federation Enhancements** - Add Actor serialization and issuer validation

The library's architecture is sound and extensible. The changes are surgical rather than fundamental redesigns.
