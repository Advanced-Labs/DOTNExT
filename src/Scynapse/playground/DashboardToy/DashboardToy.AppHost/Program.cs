using Aspire.Hosting;
using Projects;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("scynapse-redis");

var scynapse = builder.AddScynapse("cluster")
    .WithClustering(redis);

builder.AddProject<DashboardToy_Frontend>("frontend")
    .WithReference(scynapse)
    .WaitFor(redis)
    .WithReplicas(5);

builder.Build().Run();
