using ElBruno.MAF.FoundryLocal;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables(prefix: "ELBRUNO_")
    .AddCommandLine(args);

builder.Services.Configure<FoundryLocalOptions>(builder.Configuration.GetSection("FoundryLocal"));
builder.Services.Configure<LocalAgentOptions>(builder.Configuration.GetSection("Agent"));
builder.Services.Configure<ChatRuntimeOptions>(builder.Configuration.GetSection("Chat"));
builder.Services.Configure<DiagnosticsOptions>(builder.Configuration.GetSection("Diagnostics"));

builder.Services.AddLogging(logging =>
{
    logging.ClearProviders();
    logging.AddSimpleConsole(options =>
    {
        options.SingleLine = true;
        options.TimestampFormat = "HH:mm:ss ";
    });
});

builder.Services.AddSingleton<FoundryLocalModelLifecycleService>();
builder.Services.AddSingleton<IChatClient, FoundryLocalChatClientAdapter>();
builder.Services.AddSingleton<FoundryLocalEmbeddingAdapter>();
builder.Services.AddSingleton<ChatClientAgent>(sp =>
{
    var chatClient = sp.GetRequiredService<IChatClient>();
    var agentOptions = sp.GetRequiredService<IOptions<LocalAgentOptions>>().Value;
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var tools = new[]
    {
        AIFunctionFactory.Create(
            (Func<string>)ConsoleTools.GetCurrentTime,
            "get_current_time",
            "Gets the current UTC date and time in ISO-8601 format.",
            serializerOptions: null)
    };

    return new ChatClientAgent(
        chatClient,
        instructions: agentOptions.Instructions,
        name: agentOptions.Name,
        description: "Foundry Local Agent Framework console bridge",
        tools: tools,
        loggerFactory: loggerFactory,
        services: sp);
});
builder.Services.AddSingleton<ConsoleRunner>();

using var host = builder.Build();
await host.Services.GetRequiredService<ConsoleRunner>().RunAsync(args);

internal sealed class ConsoleRunner(
    ChatClientAgent agent,
    FoundryLocalEmbeddingAdapter embeddingAdapter,
    FoundryLocalModelLifecycleService lifecycleService,
    IOptions<FoundryLocalOptions> foundryOptions,
    IOptions<ChatRuntimeOptions> chatOptions,
    ILogger<ConsoleRunner> logger)
{
    private readonly ChatClientAgent _agent = agent;
    private readonly FoundryLocalEmbeddingAdapter _embeddingAdapter = embeddingAdapter;
    private readonly FoundryLocalModelLifecycleService _lifecycleService = lifecycleService;
    private readonly FoundryLocalOptions _foundryOptions = foundryOptions.Value;
    private readonly ChatRuntimeOptions _chatOptions = chatOptions.Value;
    private readonly ILogger<ConsoleRunner> _logger = logger;
    private readonly LocalRagSample _ragSample = new();

    public async Task RunAsync(string[] args)
    {
        var commands = CommandArguments.Parse(args);

        if (commands.ListModels)
        {
            await ListModelsAsync();
            return;
        }

        if (!string.IsNullOrWhiteSpace(commands.DownloadModelAlias))
        {
            await _lifecycleService.DownloadModelAsync(commands.DownloadModelAlias);
            Console.WriteLine($"Model '{commands.DownloadModelAlias}' downloaded or already available in cache.");
            return;
        }

        if (commands.Diagnostics)
        {
            PrintDiagnostics();
        }

        if (!string.IsNullOrWhiteSpace(commands.ModelAlias))
        {
            _lifecycleService.SetModelAliasOverride(commands.ModelAlias);
        }

        var streaming = commands.Stream ?? _chatOptions.Streaming;
        var ragMode = commands.RagMode;
        var workflowMode = commands.WorkflowMode;

        if (ragMode && workflowMode)
        {
            Console.WriteLine("Workflow mode cannot be combined with --rag in this sample.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(commands.Prompt))
        {
            if (ragMode)
            {
                await RunSinglePromptWithRagAsync(commands.Prompt, streaming, commands.RagTopK);
                return;
            }

            if (workflowMode)
            {
                await RunSinglePromptWithWorkflowAsync(commands.Prompt, streaming);
                return;
            }

            await RunSinglePromptAsync(commands.Prompt, streaming);
            return;
        }

        await RunInteractiveAsync(streaming, ragMode, workflowMode, commands.RagTopK);
    }

    private async Task ListModelsAsync()
    {
        var models = await _lifecycleService.ListModelsAsync();
        foreach (var model in models)
        {
            Console.WriteLine(model);
        }
    }

    private async Task RunSinglePromptAsync(string prompt, bool streaming)
    {
        if (streaming)
        {
            await foreach (var update in _agent.RunStreamingAsync(prompt, null, null))
            {
                var text = TryGetProperty(update, "Text");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    Console.Write(text);
                }
            }

            Console.WriteLine();
            return;
        }

        var response = await _agent.RunAsync(prompt, null, null);
        Console.WriteLine(TryGetProperty(response, "Text") ?? string.Empty);
    }

    private async Task RunInteractiveAsync(bool streaming, bool ragMode, bool workflowMode, int ragTopK)
    {
        Console.WriteLine($"Model: {_foundryOptions.ModelAlias}");
        Console.WriteLine(ragMode
            ? $"Enter a prompt in local RAG mode (top-k={ragTopK}). Type 'exit' to quit."
            : workflowMode
                ? "Enter a prompt in workflow mode (planner + responder). Type 'exit' to quit."
            : "Enter a prompt. Type 'exit' to quit.");

        AgentSession? session = null;
        while (true)
        {
            Console.Write("> ");
            var prompt = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(prompt))
            {
                continue;
            }

            if (string.Equals(prompt, "exit", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (ragMode)
            {
                await RunSinglePromptWithRagAsync(prompt, streaming, ragTopK);
                continue;
            }

            if (workflowMode)
            {
                await RunSinglePromptWithWorkflowAsync(prompt, streaming);
                continue;
            }

            if (streaming)
            {
                await foreach (var update in _agent.RunStreamingAsync(prompt, session, null))
                {
                    var text = TryGetProperty(update, "Text");
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        Console.Write(text);
                    }
                }

                Console.WriteLine();
                continue;
            }

            var response = await _agent.RunAsync(prompt, session, null);
            Console.WriteLine(TryGetProperty(response, "Text") ?? string.Empty);
        }
    }

    private async Task RunSinglePromptWithRagAsync(string prompt, bool streaming, int ragTopK)
    {
        var queryEmbedding = await _embeddingAdapter.TryGenerateEmbeddingsAsync([prompt]);
        if (!queryEmbedding.Success)
        {
            Console.WriteLine($"RAG mode is unavailable: {queryEmbedding.FailureReason}");
            return;
        }

        var corpusIndex = await _ragSample.GetOrBuildIndexAsync(_embeddingAdapter);
        if (!corpusIndex.Success)
        {
            Console.WriteLine($"RAG mode is unavailable: {corpusIndex.FailureReason}");
            return;
        }

        var hits = _ragSample.Search(corpusIndex.Embeddings, queryEmbedding.Embeddings[0], ragTopK);
        var groundedPrompt = _ragSample.BuildGroundedPrompt(prompt, hits);

        if (streaming)
        {
            await foreach (var update in _agent.RunStreamingAsync(groundedPrompt, null, null))
            {
                var text = TryGetProperty(update, "Text");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    Console.Write(text);
                }
            }

            Console.WriteLine();
            return;
        }

        var response = await _agent.RunAsync(groundedPrompt, null, null);
        Console.WriteLine(TryGetProperty(response, "Text") ?? string.Empty);
    }

    private async Task RunSinglePromptWithWorkflowAsync(string prompt, bool streaming)
    {
        var plannerPrompt = BuildPlannerPrompt(prompt);
        var plannerResponse = await _agent.RunAsync(plannerPrompt, null, null);
        var plannerOutput = NormalizePlannerOutput(TryGetProperty(plannerResponse, "Text"));

        Console.WriteLine("Plan:");
        Console.WriteLine(plannerOutput);
        Console.WriteLine();

        var responderPrompt = BuildResponderPrompt(prompt, plannerOutput);

        if (streaming)
        {
            await foreach (var update in _agent.RunStreamingAsync(responderPrompt, null, null))
            {
                var text = TryGetProperty(update, "Text");
                if (!string.IsNullOrWhiteSpace(text))
                {
                    Console.Write(text);
                }
            }

            Console.WriteLine();
            return;
        }

        var response = await _agent.RunAsync(responderPrompt, null, null);
        Console.WriteLine(TryGetProperty(response, "Text") ?? string.Empty);
    }

    private static string BuildPlannerPrompt(string userQuery) =>
        $$"""
          Create a concise plan to answer the user query.
          Return 3-5 short bullet points only.
          No preamble.

          User query:
          {{userQuery}}
          """;

    private static string BuildResponderPrompt(string userQuery, string plannerOutput) =>
        $$"""
          You are a helpful assistant.
          Use this plan to produce the final answer.
          Keep the response concise and practical.
          Do not repeat the plan unless needed.

          Plan:
          {{plannerOutput}}

          User query:
          {{userQuery}}
          """;

    private static string NormalizePlannerOutput(string? plannerOutput)
    {
        if (string.IsNullOrWhiteSpace(plannerOutput))
        {
            return "- Clarify intent\n- Gather key facts\n- Provide concise answer";
        }

        return plannerOutput.Trim();
    }

    private void PrintDiagnostics()
    {
        var snapshot = _lifecycleService.GetDiagnosticsSnapshot();

        _logger.LogInformation("Diagnostics:");
        _logger.LogInformation("ModelAlias: {ModelAlias}", snapshot.ModelAlias);
        _logger.LogInformation("DownloadedThisSession: {DownloadedThisSession}", snapshot.DownloadedThisSession);
        _logger.LogInformation("ModelLoaded: {ModelLoaded}", snapshot.ModelLoaded);
        _logger.LogInformation("StreamingEnabled: {StreamingEnabled}", snapshot.StreamingEnabled);
        _logger.LogInformation("UsingRestServer: {UsingRestServer}", snapshot.UsingRestServer);
        _logger.LogInformation("Warnings: {Warnings}", snapshot.Warnings.Count == 0 ? "(none)" : string.Join(", ", snapshot.Warnings));
        _logger.LogInformation("LastModelLoadDuration: {LastModelLoadDuration}", snapshot.LastModelLoadDuration);
        _logger.LogInformation("LastResponseDuration: {LastResponseDuration}", snapshot.LastResponseDuration);
    }

    private static string? TryGetProperty(object target, string propertyName)
    {
        var property = target.GetType().GetProperty(propertyName);
        return property?.GetValue(target) as string;
    }
}

internal sealed class CommandArguments
{
    public string? ModelAlias { get; init; }
    public string? Prompt { get; init; }
    public bool? Stream { get; init; }
    public bool Diagnostics { get; init; }
    public bool ListModels { get; init; }
    public string? DownloadModelAlias { get; init; }
    public bool RagMode { get; init; }
    public int RagTopK { get; init; } = 2;
    public bool WorkflowMode { get; init; }

    public static CommandArguments Parse(string[] args)
    {
        string? modelAlias = null;
        string? prompt = null;
        bool? stream = null;
        var diagnostics = false;
        var listModels = false;
        string? downloadModelAlias = null;
        var ragMode = false;
        var ragTopK = 2;
        var workflowMode = false;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--model" when i + 1 < args.Length:
                    modelAlias = args[++i];
                    break;

                case "--prompt" when i + 1 < args.Length:
                    prompt = args[++i];
                    break;

                case "--stream":
                    stream = true;
                    break;

                case "--diagnostics":
                    diagnostics = true;
                    break;

                case "--list-models":
                    listModels = true;
                    break;

                case "--download-model" when i + 1 < args.Length:
                    downloadModelAlias = args[++i];
                    break;

                case "--rag":
                    ragMode = true;
                    break;

                case "--rag-top-k" when i + 1 < args.Length && int.TryParse(args[i + 1], out var parsedTopK):
                    ragTopK = Math.Max(1, parsedTopK);
                    i++;
                    break;

                case "--workflow":
                    workflowMode = true;
                    break;
            }

        }

        return new CommandArguments
        {
            ModelAlias = modelAlias,
            Prompt = prompt,
            Stream = stream,
            Diagnostics = diagnostics,
            ListModels = listModels,
            DownloadModelAlias = downloadModelAlias,
            RagMode = ragMode,
            RagTopK = ragTopK,
            WorkflowMode = workflowMode
        };
    }
}

internal static class ConsoleTools
{
    public static string GetCurrentTime() => DateTimeOffset.UtcNow.ToString("O");
}

internal sealed class LocalRagSample
{
    private readonly IReadOnlyList<LocalRagDocument> _documents =
    [
        new("doc-1", "Foundry Local runs models directly on-device without requiring a local REST inference server."),
        new("doc-2", "This sample uses a ChatClientAgent and a custom IChatClient adapter to bridge Agent Framework to Foundry Local."),
        new("doc-3", "Tool calling support is partial and currently focused on simple function-style tools mapped from ChatOptions.Tools."),
        new("doc-4", "Streaming responses are supported in best-effort mode through CompleteChatStreamingAsync.")
    ];

    private IReadOnlyList<LocalRagIndexedDocument>? _index;

    public async Task<FoundryLocalEmbeddingResult> GetOrBuildIndexAsync(FoundryLocalEmbeddingAdapter embeddingAdapter, CancellationToken cancellationToken = default)
    {
        if (_index is not null)
        {
            return new FoundryLocalEmbeddingResult(true, true, _index.Select(static doc => (IReadOnlyList<float>)doc.Embedding).ToArray(), null);
        }

        var response = await embeddingAdapter.TryGenerateEmbeddingsAsync(_documents.Select(static doc => doc.Content), cancellationToken);
        if (!response.Success)
        {
            return response;
        }

        _index = _documents
            .Zip(response.Embeddings, (document, embedding) => new LocalRagIndexedDocument(document.Id, document.Content, embedding))
            .ToArray();

        return new FoundryLocalEmbeddingResult(true, true, _index.Select(static doc => (IReadOnlyList<float>)doc.Embedding).ToArray(), null);
    }

    public IReadOnlyList<LocalRagSearchHit> Search(IReadOnlyList<IReadOnlyList<float>> corpusEmbeddings, IReadOnlyList<float> queryEmbedding, int topK)
    {
        if (_index is null)
        {
            return [];
        }

        var count = Math.Min(Math.Max(1, topK), _index.Count);
        return _index
            .Zip(corpusEmbeddings, (document, embedding) => new LocalRagSearchHit(
                document.Id,
                document.Content,
                VectorMath.CosineSimilarity(queryEmbedding, embedding)))
            .OrderByDescending(static hit => hit.Score)
            .Take(count)
            .ToArray();
    }

    public string BuildGroundedPrompt(string question, IReadOnlyList<LocalRagSearchHit> hits)
    {
        var context = string.Join(Environment.NewLine, hits.Select((hit, index) => $"[{index + 1}] {hit.Content}"));

        return $$"""
            You are a local assistant. Use only the provided context to answer.
            If the answer is not in the context, say that the context does not contain the answer.

            Context:
            {{context}}

            Question:
            {{question}}
            """;
    }
}

internal sealed record LocalRagDocument(string Id, string Content);
internal sealed record LocalRagIndexedDocument(string Id, string Content, IReadOnlyList<float> Embedding);
internal sealed record LocalRagSearchHit(string Id, string Content, double Score);
