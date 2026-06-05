var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.aspireSample_ApiService>("apiservice")
    .WithExternalHttpEndpoints()
    .WithEnvironment("FoundryLocal__ModelAlias", "qwen2.5-0.5b")
    .WithEnvironment("FoundryLocal__DownloadIfMissing", "true")
    .WithEnvironment("FoundryLocal__UnloadOnExit", "true")
    .WithEnvironment("Chat__Temperature", "0.7")
    .WithEnvironment("Chat__MaxOutputTokens", "512");

builder.AddProject<Projects.aspireSample_Web>("web")
    .WithExternalHttpEndpoints()
    .WithReference(apiService);

builder.Build().Run();
