var builder = DistributedApplication.CreateBuilder(args);

var ollama = builder.AddOllama("ollama")
    .WithDataVolume()
    .AddModel("llama3.2");

var apiService = builder.AddProject<Projects.TripPlanner_ApiService>("apiservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.TripPlanner_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService)
    .WithReference(ollama)
    .WaitFor(ollama);

builder.Build().Run();
