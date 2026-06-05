using ElBruno.MAF.FoundryLocal;
using System.Diagnostics;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);
var genAiActivitySource = new ActivitySource(builder.Environment.ApplicationName);

builder.AddServiceDefaults();
builder.Services.AddOpenApi();
builder.Services.Configure<FoundryLocalOptions>(builder.Configuration.GetSection("FoundryLocal"));
builder.Services.Configure<ChatRuntimeOptions>(builder.Configuration.GetSection("Chat"));
builder.Services.AddSingleton<FoundryLocalModelLifecycleService>();
builder.Services.AddSingleton<IChatClient>(sp =>
{
    var lifecycleService = sp.GetRequiredService<FoundryLocalModelLifecycleService>();
    var adapterLogger = sp.GetRequiredService<ILogger<FoundryLocalChatClientAdapter>>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

    return new FoundryLocalChatClientAdapter(lifecycleService, adapterLogger)
        .AsBuilder()
        .UseOpenTelemetry(loggerFactory, sourceName: "Microsoft.Extensions.AI", configure: telemetry =>
        {
            telemetry.EnableSensitiveData = false;
        })
        .Build();
});
builder.Services.AddSingleton<ChatClientAgent>(sp =>
{
    var chatClient = sp.GetRequiredService<IChatClient>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

    return new ChatClientAgent(
        chatClient,
        instructions: "You are a concise local assistant. Keep responses short and useful.",
        name: "LocalFoundryAgent",
        description: "Agent Framework over FoundryLocal MEAI adapter",
        tools: null,
        loggerFactory: loggerFactory,
        services: sp);
});

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
        "POST /chat { \"prompt\": \"Your question\" }",
        "POST /chat-agent { \"prompt\": \"Your question\" }"
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

    using var activity = genAiActivitySource.StartActivity("gen_ai.chat.meai", ActivityKind.Internal);
    activity?.SetTag("gen_ai.system", "foundry_local");
    activity?.SetTag("gen_ai.operation.name", "chat");
    activity?.SetTag("gen_ai.request.model", foundryOptions.Value.ModelAlias);
    activity?.SetTag("chat.backend", "meai");

    var response = await chatClient.GetResponseAsync(
        [new ChatMessage(ChatRole.User, request.Prompt)],
        chatOptions,
        cancellationToken);
    activity?.SetTag("gen_ai.response.model", foundryOptions.Value.ModelAlias);
    activity?.SetTag("gen_ai.response.finish_reasons", "stop");

    return Results.Ok(new ChatResponsePayload(
        Backend: "meai",
        Model: foundryOptions.Value.ModelAlias,
        Response: response.Text));
})
.WithName("ChatMeai");

app.MapPost("/chat-agent", async (
    ChatRequest request,
    ChatClientAgent agent,
    IOptions<FoundryLocalOptions> foundryOptions,
    CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(request.Prompt))
    {
        return Results.BadRequest(new { error = "Prompt is required." });
    }

    using var activity = genAiActivitySource.StartActivity("gen_ai.chat.agent", ActivityKind.Internal);
    activity?.SetTag("gen_ai.system", "foundry_local");
    activity?.SetTag("gen_ai.operation.name", "chat");
    activity?.SetTag("gen_ai.request.model", foundryOptions.Value.ModelAlias);
    activity?.SetTag("chat.backend", "agent-framework");

    var agentResponse = await agent.RunAsync(request.Prompt, session: null, options: null, cancellationToken: cancellationToken);
    var responseText = TryGetProperty(agentResponse, "Text") ?? string.Empty;
    activity?.SetTag("gen_ai.response.model", foundryOptions.Value.ModelAlias);
    activity?.SetTag("gen_ai.response.finish_reasons", "stop");

    return Results.Ok(new ChatResponsePayload(
        Backend: "agent-framework",
        Model: foundryOptions.Value.ModelAlias,
        Response: responseText));
})
.WithName("ChatAgent");

app.MapDefaultEndpoints();

app.Run();

static string? TryGetProperty(object target, string propertyName)
{
    var property = target.GetType().GetProperty(propertyName);
    return property?.GetValue(target) as string;
}

internal sealed record ChatRequest(string Prompt);
internal sealed record ChatResponsePayload(string Backend, string Model, string Response);
