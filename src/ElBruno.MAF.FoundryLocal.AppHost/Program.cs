var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.ElBruno_MAF_FoundryLocal_Console>("console");

builder.Build().Run();
