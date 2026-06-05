# Aspire sample

This sample follows the current Aspire app model from [aspire.dev](https://aspire.dev):

- `DistributedApplication.CreateBuilder(args)` in the AppHost
- `builder.AddProject<...>("apiservice")` to orchestrate a service
- `builder.Build().Run()` to launch the Aspire dashboard and resources

## Run

```powershell
cd C:\src\ElBruno.MAF.FoundryLocal\src\aspireSample
dotnet run --project .\aspireSample.AppHost\aspireSample.AppHost.csproj
```

When it starts, open the Aspire Dashboard URL shown in the console and use the login URL/token emitted by AppHost.
