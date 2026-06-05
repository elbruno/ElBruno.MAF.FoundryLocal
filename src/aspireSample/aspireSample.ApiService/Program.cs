var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Ok(new
{
    message = "Hello from Aspire sample API",
    utcNow = DateTime.UtcNow
}))
.WithName("Root");

app.MapDefaultEndpoints();

app.Run();
