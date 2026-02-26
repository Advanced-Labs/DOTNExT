# Scynapse Dashboard - Cohosted Example

This example demonstrates how to cohost the Scynapse Dashboard within the same web application that runs your Scynapse silo.

## Overview

This is the simplest way to add the Scynapse Dashboard to your application. The dashboard is hosted within the same process as your silo, using ASP.NET Core minimal APIs.

## Key Features

- **Single Process**: Both Scynapse and the dashboard run in the same web application
- **Minimal Configuration**: Simple setup with just a few lines of code
- **ASP.NET Core Integration**: Uses `WebApplication.CreateBuilder()` and minimal APIs

## How It Works

The example shows:

1. Creating a web application with `WebApplication.CreateBuilder()`
2. Configuring Scynapse using `builder.UseScynapse()`
3. Adding the dashboard with `siloBuilder.AddDashboard()`
4. Mapping dashboard endpoints with `app.MapScynapseDashboard()`

## Running the Example

```bash
dotnet run
```

Once running, open your browser to:
- **Dashboard**: https://localhost:55355/ (or http://localhost:55356/)

## Code Walkthrough

```csharp
var builder = WebApplication.CreateBuilder(args);

// Configure Scynapse
builder.UseScynapse(siloBuilder =>
{
    siloBuilder.UseLocalhostClustering();
    siloBuilder.UseInMemoryReminderService();
    siloBuilder.AddMemoryGrainStorageAsDefault();

    // Add the dashboard
    siloBuilder.AddDashboard();
});

var app = builder.Build();

// Map dashboard endpoints
app.MapScynapseDashboard();

app.Run();
```

## When to Use This Approach

Use this cohosted approach when:
- You want the simplest setup
- You're building a web application that also hosts Scynapse
- You want dashboard access on the same port as your web app
- You're running a single silo or don't need a centralized dashboard

## Customization

### Custom Route Prefix

```csharp
app.MapScynapseDashboard(routePrefix: "/dashboard");
```

### Add Authentication

```csharp
app.MapScynapseDashboard().RequireAuthorization();
```

### Configure Update Interval

```csharp
siloBuilder.AddDashboard(options =>
{
    options.CounterUpdateIntervalMs = 2000; // Update every 2 seconds
});
```

## Important Notes

> [!WARNING]
> The Scynapse Dashboard is designed for **development and testing scenarios only**. It is not recommended for production deployments as it can have a significant performance impact on your cluster.

## Related Examples

- **DashboardSeparateHost**: Shows how to host the dashboard in a separate application
- **DashboardToy**: Advanced example with .NET Aspire integration

## Learn More

- [Scynapse Dashboard Documentation](../../../src/Dashboard/Scynapse.Dashboard/README.md)
- [Microsoft Scynapse Documentation](https://learn.microsoft.com/dotnet/scynapse/)
