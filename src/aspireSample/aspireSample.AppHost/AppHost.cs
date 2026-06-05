var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.aspireSample_ApiService>("apiservice");

builder.Build().Run();
