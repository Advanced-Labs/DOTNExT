using Aspire.Hosting;

var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("scynapse-redis");

var scynapse = builder.AddScynapse("cluster")
    .WithClustering(redis);

var backend = builder.AddProject<Projects.ActivationRebalancing_Cluster>("backend")
    .WithReference(scynapse)
    .WaitFor(redis)
    .WithReplicas(5);

builder.AddProject<Projects.ActivationRebalancing_Frontend>("frontend")
    .WithReference(scynapse.AsClient())
    .WaitFor(backend)
    .WithReplicas(1);

builder.Build().Run();
