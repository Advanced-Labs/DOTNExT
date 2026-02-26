using Scynapse.Dashboard;

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

// Map dashboard endpoints at the root
app.MapScynapseDashboard();

app.Run();
