#nullable enable
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Scynapse.Hosting;
using Scynapse.Runtime;
using Scynapse.Dashboard.Implementation;
using Scynapse.Dashboard.Implementation.Details;
using Scynapse.Dashboard.Metrics;
using Scynapse.Dashboard.Metrics.Details;
using Scynapse.Dashboard.Model;
using System.Diagnostics.CodeAnalysis;
using Scynapse.Dashboard.Core;
using Microsoft.AspNetCore.Mvc;
using Scynapse.Configuration.Internal;

// ReSharper disable CheckNamespace
namespace Scynapse.Dashboard;

/// <summary>
/// Provides extension methods for configuring and integrating the Scynapse Dashboard.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds Scynapse Dashboard services to the silo builder.
    /// </summary>
    /// <param name="siloBuilder">The silo builder.</param>
    /// <param name="configureOptions">Optional configuration action for <see cref="DashboardOptions"/>.</param>
    /// <returns>The silo builder for method chaining.</returns>
    public static ISiloBuilder AddDashboard(this ISiloBuilder siloBuilder, Action<DashboardOptions>? configureOptions = null)
    {
        siloBuilder.Services.AddScynapseDashboardForSiloCore();
        return siloBuilder;
    }

    internal static IServiceCollection AddScynapseDashboardForSiloCore(
        this IServiceCollection services,
        Action<DashboardOptions>? configureOptions = null)
    {
        services.AddGrainService<SiloGrainService>();
        services.AddHostedService<DashboardHost>();
        services.Configure(configureOptions ?? (x => { }));
        services.AddSingleton<DashboardTelemetryExporter>();
        services.AddOptions<GrainProfilerOptions>();

        services.AddSingleton<EmbeddedAssetProvider>();
        services.AddSingleton<SiloStatusOracleSiloDetailsProvider>();
        services.AddSingleton<MembershipTableSiloDetailsProvider>();
        services.AddSingleton<IDashboardClient, DashboardClient>();
        services.AddSingleton<DashboardLogger>();
        services.AddFromExisting<ILoggerProvider, DashboardLogger>();
        services.AddSingleton<IGrainProfiler, GrainProfiler>();
        services.AddSingleton(c => (ILifecycleParticipant<ISiloLifecycle>)c.GetRequiredService<IGrainProfiler>());
        services.AddSingleton<IIncomingGrainCallFilter, GrainProfilerFilter>();

        services.AddSingleton<ISiloGrainClient, SiloGrainClient>();

        services.AddSingleton<ISiloDetailsProvider>(c
            => c.GetService<IMembershipTable>() switch
            {
                not null =>
                c.GetRequiredService<MembershipTableSiloDetailsProvider>(),
                null => c.GetRequiredService<SiloStatusOracleSiloDetailsProvider>(),
            });

        services.TryAddSingleton(GrainProfilerFilter.DefaultGrainMethodFormatter);

        return services;
    }

    /// <summary>
    /// Maps Scynapse Dashboard endpoints using ASP.NET Core minimal APIs.
    /// Returns an <see cref="IEndpointConventionBuilder"/> that can be used to apply authentication,
    /// authorization, or other endpoint configuration.
    /// </summary>
    /// <example>
    /// <code>
    /// // Basic usage
    /// app.MapScynapseDashboard();
    ///
    /// // With authentication
    /// app.MapScynapseDashboard().RequireAuthorization();
    ///
    /// // With custom base path
    /// app.MapScynapseDashboard(routePrefix: "/dashboard");
    /// </code>
    /// </example>
    public static RouteGroupBuilder MapScynapseDashboard(
        this IEndpointRouteBuilder endpoints,
        [StringSyntax("Route")] string? routePrefix = null)
    {
        // Create static assets provider
        var assets = endpoints.ServiceProvider.GetService<EmbeddedAssetProvider>()
            ?? throw new InvalidOperationException("Scynapse Dashboard services have not been registered. " +
                "Please call AddServicesForSelfHostedDashboard or AddScynapseDashboard on the IServiceCollection.");

        // Create a route group for all dashboard endpoints
        var group = endpoints.MapGroup(routePrefix ?? "");

        // Static assets - these match the paths referenced in the built CSS/HTML
        group.MapGet("/", (HttpContext ctx) => assets.ServeAsset("index.html", ctx));
        group.MapGet("/index.html", (HttpContext ctx) => assets.ServeAsset("index.html", ctx));
        group.MapGet("/favicon.ico", (HttpContext ctx) => assets.ServeAsset("favicon.ico", ctx));
        group.MapGet("/index.min.js", (HttpContext ctx) => assets.ServeAsset("index.min.js", ctx));
        group.MapGet("/index.css", (HttpContext ctx) => assets.ServeAsset("index.css", ctx));

        // Font files - catch-all route for /fonts/ directory
        group.MapGet("/fonts/{**path}", (string path, HttpContext ctx) => assets.ServeAsset($"fonts.{path}", ctx));

        // Image files - catch-all route for /img/ directory
        group.MapGet("/img/{**path}", (string path, HttpContext ctx) => assets.ServeAsset($"img.{path}", ctx));

        // API endpoints
        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            IncludeFields = true,
            Converters = { new TimeSpanConverter() }
        };

        group.MapGet("/version", () => Results.Json(
            new { version = typeof(EmbeddedAssetProvider).Assembly.GetName().Version?.ToString() },
            jsonOptions));

        group.MapGet("/DashboardCounters", async ([FromServices] IDashboardClient client) =>
        {
            try
            {
                var result = await client.DashboardCounters();
                return Results.Json(result.Value, jsonOptions);
            }
            catch (SiloUnavailableException)
            {
                return CreateUnavailableResult(true);
            }
        });

        group.MapGet("/ClusterStats", async ([FromServices] IDashboardClient client) =>
        {
            try
            {
                var result = await client.ClusterStats();
                return Results.Json(result.Value, jsonOptions);
            }
            catch (SiloUnavailableException)
            {
                return CreateUnavailableResult(true);
            }
        });

        group.MapGet("/Reminders", async ([FromServices] IDashboardClient client) => await GetRemindersPage(1, client, jsonOptions));
        group.MapGet("/Reminders/{page:int}", async (int page, [FromServices] IDashboardClient client) => await GetRemindersPage(page, client, jsonOptions));

        group.MapGet("/HistoricalStats/{*path}", async (string path, [FromServices] IDashboardClient client) =>
        {
            try
            {
                var result = await client.HistoricalStats(path);
                return Results.Json(result.Value, jsonOptions);
            }
            catch (SiloUnavailableException)
            {
                return CreateUnavailableResult(true);
            }
        });

        group.MapGet("/SiloProperties/{*address}", async (string address, [FromServices] IDashboardClient client) =>
        {
            try
            {
                var result = await client.SiloProperties(address);
                return Results.Json(result.Value, jsonOptions);
            }
            catch (SiloUnavailableException)
            {
                return CreateUnavailableResult(true);
            }
        });

        group.MapGet("/SiloStats/{*address}", async (string address, [FromServices] IDashboardClient client) =>
        {
            try
            {
                var result = await client.SiloStats(address);
                return Results.Json(result.Value, jsonOptions);
            }
            catch (SiloUnavailableException)
            {
                return CreateUnavailableResult(true);
            }
        });

        group.MapGet("/SiloCounters/{*address}", async (string address, [FromServices] IDashboardClient client) =>
        {
            try
            {
                var result = await client.GetCounters(address);
                return Results.Json(result.Value, jsonOptions);
            }
            catch (SiloUnavailableException)
            {
                return CreateUnavailableResult(true);
            }
        });

        group.MapGet("/GrainStats/{*grainName}", async (string grainName, [FromServices] IDashboardClient client) =>
        {
            try
            {
                var result = await client.GrainStats(grainName);
                return Results.Json(result.Value, jsonOptions);
            }
            catch (SiloUnavailableException)
            {
                return CreateUnavailableResult(true);
            }
        });

        group.MapGet("/TopGrainMethods", async ([FromServices] IDashboardClient client) =>
        {
            try
            {
                var result = await client.TopGrainMethods(take: 5);
                return Results.Json(result.Value, jsonOptions);
            }
            catch (SiloUnavailableException)
            {
                return CreateUnavailableResult(true);
            }
        });

        group.MapGet("/GrainState", async (HttpContext context, [FromServices] IDashboardClient client) =>
        {
            try
            {
                context.Request.Query.TryGetValue("grainId", out var grainId);
                context.Request.Query.TryGetValue("grainType", out var grainType);
                var result = await client.GetGrainState(grainId, grainType);
                return Results.Json(result.Value, jsonOptions);
            }
            catch (SiloUnavailableException)
            {
                return CreateUnavailableResult(true);
            }
        });

        group.MapGet("/GrainTypes", async ([FromServices] IDashboardClient client) =>
        {
            try
            {
                var result = await client.GetGrainTypes();
                return Results.Json(result.Value, jsonOptions);
            }
            catch (SiloUnavailableException)
            {
                return CreateUnavailableResult(true);
            }
        });

        group.MapGet("/Trace", async (HttpContext context, [FromServices] IOptions<DashboardOptions> opts, [FromServices] DashboardLogger logger) =>
        {
            if (opts.Value.HideTrace)
            {
                return Results.StatusCode(StatusCodes.Status403Forbidden);
            }

            await StreamTraceAsync(context, logger);
            return Results.Empty;
        });

        return group;
    }

    private static async Task<IResult> GetRemindersPage(int page, IDashboardClient client, JsonSerializerOptions jsonOptions)
    {
        try
        {
            var result = await client.GetReminders(page, 50);
            return Results.Json(result.Value, jsonOptions);
        }
        catch (SiloUnavailableException)
        {
            return CreateUnavailableResult(true);
        }
        catch
        {
            // If reminders are not configured, return empty response
            return Results.Json(new ReminderResponse { Reminders = [], Count = 0 }, jsonOptions);
        }
    }

    private static async Task StreamTraceAsync(HttpContext context, DashboardLogger logger)
    {
        var token = context.RequestAborted;

        try
        {
            await using var writer = new TraceWriter(logger, context);
            await writer.WriteAsync("""
                   ____       _                        _____            _     _                         _
                  / __ \     | |                      |  __ \          | |   | |                       | |
                 | |  | |_ __| | ___  __ _ _ __  ___  | |  | | __ _ ___| |__ | |__   ___   __ _ _ __ __| |
                 | |  | | '__| |/ _ \/ _` | '_ \/ __| | |  | |/ _` / __| '_ \| '_ \ / _ \ / _` | '__/ _` |
                 | |__| | |  | |  __/ (_| | | | \__ \ | |__| | (_| \__ \ | | | |_) | (_) | (_| | | | (_| |
                  \____/|_|  |_|\___|\__,_|_| |_|___/ |_____/ \__,_|___/_| |_|_.__/ \___/ \__,_|_|  \__,_|

                You are connected to the Scynapse Dashboard log streaming service
                """);

            await Task.Delay(TimeSpan.FromMinutes(60), token);
            await writer.WriteAsync("Disconnecting after 60 minutes\r\n");
        }
        catch (OperationCanceledException)
        {
        }
    }

    private static IResult CreateUnavailableResult(bool lostConnectivity)
    {
        var message = lostConnectivity
            ? "The dashboard has lost connectivity with the Scynapse cluster"
            : "The dashboard is still trying to connect to the Scynapse cluster";

        return Results.Text(message, "text/plain", statusCode: 503);
    }

    /// <summary>
    /// Adds Scynapse Dashboard services to an Scynapse client builder.
    /// This allows you to host the Scynapse Dashboard application on an Scynapse client, so long as the silos also have the dashboard added.
    /// </summary>
    /// <param name="clientBuilder">The client builder.</param>
    /// <param name="configureOptions">Optional configuration action for <see cref="DashboardOptions"/>.</param>
    /// <returns>The service collection for method chaining.</returns>
    public static IClientBuilder AddScynapseDashboard(this IClientBuilder clientBuilder, Action<DashboardOptions>? configureOptions = null)
    {
        clientBuilder.Services.AddSingleton<DashboardLogger>();
        clientBuilder.Services.AddFromExisting<ILoggerProvider, DashboardLogger>();
        clientBuilder.Services.AddSingleton<IDashboardClient, DashboardClient>();
        clientBuilder.Services.AddSingleton<EmbeddedAssetProvider>();

        return clientBuilder;
    }
}
