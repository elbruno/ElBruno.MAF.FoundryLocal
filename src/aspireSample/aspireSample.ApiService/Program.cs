using ElBruno.MAF.FoundryLocal;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.Configure<FoundryLocalOptions>(builder.Configuration.GetSection("FoundryLocal"));
builder.Services.Configure<ChatRuntimeOptions>(builder.Configuration.GetSection("Chat"));
builder.Services.AddSingleton<FoundryLocalModelLifecycleService>();
builder.Services.AddSingleton<IChatClient, FoundryLocalChatClientAdapter>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGet("/", () => Results.Ok(new
{
    message = "Aspire sample wired to ElBruno.MAF.FoundryLocal adapter",
    endpoints = new[]
    {
        "GET /models",
        "POST /chat { \"prompt\": \"Your question\" }"
    }
}))
.WithName("Root");

app.MapGet("/models", async (FoundryLocalModelLifecycleService lifecycleService, CancellationToken cancellationToken) =>
{
    var models = await lifecycleService.ListModelsAsync(cancellationToken);
    return Results.Ok(new { count = models.Count, models });
})
.WithName("Models");

app.MapPost("/chat", async (
    ChatRequest request,
    IChatClient chatClient,
    IOptions<FoundryLocalOptions> foundryOptions,
    IOptions<ChatRuntimeOptions> chatRuntimeOptions,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Prompt))
    {
        return Results.BadRequest(new { error = "Prompt is required." });
    }

    var chatOptions = new ChatOptions
    {
        Temperature = (float)chatRuntimeOptions.Value.Temperature,
        MaxOutputTokens = chatRuntimeOptions.Value.MaxOutputTokens
    };

    var response = await chatClient.GetResponseAsync(
        [new ChatMessage(ChatRole.User, request.Prompt)],
        chatOptions,
        cancellationToken);

    return Results.Ok(new
    {
        model = foundryOptions.Value.ModelAlias,
        response = response.Text
    });
})
.WithName("Chat");

app.MapDefaultEndpoints();

app.Run();

internal sealed record ChatRequest(string Prompt);
