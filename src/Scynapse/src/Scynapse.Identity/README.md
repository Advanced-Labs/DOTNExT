<img alt="managed code Identity" src="https://github.com/managed-code-hub/Identity/raw/main/logo.png" width="300px" />

# Scynapse.Identity

Scynapse.Identity expands ASP.NET Core authentication and authorization into Scynapse grains. It forwards the `ClaimsPrincipal`
created by ASP.NET Identity (JWT, cookies, etc.) to grains, validates `[Authorize]` attributes inside the cluster, and exposes
helpers that make the current user available inside grain code.

The repository ships three NuGet packages:

| Package | Purpose |
| --- | --- |
| `ManagedCode.Scynapse.Identity.Server` | Registers an Scynapse incoming grain call filter that enforces ASP.NET Core authorization attributes in the silo. |
| `ManagedCode.Scynapse.Identity.Client` | Adds MVC and SignalR filters that copy the authenticated `ClaimsPrincipal` into Scynapse `RequestContext` before grains are invoked. |
| `ManagedCode.Scynapse.Identity.Core` | Shared helpers (claim surrogates, extensions, constants). |

## Key capabilities

- **Authorization parity with ASP.NET Core** – Grains honor `[Authorize]`, `[AllowAnonymous]`, and role restrictions declared on
grains or grain interfaces. Unauthorized calls throw `UnauthorizedAccessException` before grain logic runs.
- **Automatic claim propagation** – HTTP controllers and SignalR hubs copy the authenticated user into Scynapse `RequestContext`
so that the grain filter can evaluate claims and roles consistently.
- **Grain-side helpers** – Call `this.GetCurrentUser()` inside a grain to access the caller’s `ClaimsPrincipal` without repeating
boilerplate request-context lookups.
- **SignalR and REST coverage** – Integration tests verify JWT, cookie, and SignalR scenarios end-to-end with role checks and
anonymous access rules.

## Getting started

### 1. Configure the Scynapse silo

```csharp
var host = Host.CreateDefaultBuilder(args)
    .UseScynapse(siloBuilder =>
    {
        siloBuilder
            .UseLocalhostClustering()
            .AddScynapseIdentity(); // registers the authorization grain filter
    })
    .Build();

await host.RunAsync();
```

The extension registers `GrainAuthorizationIncomingFilter`, which inspects grain metadata and enforces ASP.NET authorization
attributes inside the silo.

### 2. Configure the ASP.NET Core host

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddSignalR();

// Add authentication (JWT, cookies, etc.)
builder.Services.AddAuthentication(/* your schemes */);

// Forward ClaimsPrincipal values to Scynapse
builder.Services.AddScynapseIdentity();

builder.Services.AddScynapseClient(client =>
{
    client.UseLocalhostClustering();
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chat");

app.Run();
```

The MVC and SignalR filters installed by `AddScynapseIdentity` push the authenticated user into `RequestContext` whenever a
controller action or hub method is invoked.

### 3. Enforce authorization in grains

```csharp
[Authorize]
public interface IUserGrain : IGrainWithGuidKey
{
    Task<string> GetProfileAsync();

    [Authorize(Roles = "Admin")]
    Task<string> GetAdminPanelAsync();
}

public class UserGrain : Grain, IUserGrain
{
    public Task<string> GetProfileAsync()
    {
        var user = this.GetCurrentUser();
        return Task.FromResult($"Hello, {user.Identity?.Name ?? "anonymous"}!");
    }

    public Task<string> GetAdminPanelAsync()
    {
        return Task.FromResult("Admin only data");
    }
}
```

When the grain call arrives, the filter validates the caller’s authentication state and roles before executing grain logic, and
the grain extension retrieves the caller’s claims for business logic.

## Testing

Run the integration suite to exercise the ASP.NET + Scynapse pipeline:

```bash
dotnet test
```

The tests spin up an Scynapse test cluster and an ASP.NET Core host to validate JWT, cookie, and SignalR flows, including role
checks and anonymous endpoints.

## License

MIT License
