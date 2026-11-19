# Foundry Local 0.5.0: native sessions behind your .NET agent

![A laptop's processor connects chat messages and embedding vectors to an AI agent.](images/foundry-local-native-sessions-hero.png)

`ElBruno.MAF.FoundryLocal.Adapter` 0.5.0 moves local chat, streaming, and embeddings to native Foundry Local SDK sessions. If your app already uses `IChatClient`, keep your agent code. The adapter now runs inference in-process through `ChatSession` instead of the deprecated OpenAI-compatible client, without starting a REST server.

![An application or agent calls IChatClient, which routes chat and streaming to ChatSession; the embedding adapter routes text batches to EmbeddingsSession. Both use local models.](images/foundry-local-native-sessions.svg)

## Run an agent with a local chat model

On Windows x64, create a console app. These commands use .NET 11; use `net10.0` if you are on .NET 10.

```shell
dotnet new console -n LocalChat -f net11.0
cd LocalChat
dotnet add package ElBruno.MAF.FoundryLocal.Adapter --version 0.5.0
dotnet add package Microsoft.Agents.AI --version 1.28.0
```

Replace `Program.cs` with the code below and run `dotnet run -r win-x64`. `ListModelsAsync` initializes Foundry Local in the current process and queries its catalog. `DownloadModelAsync` downloads the selected model if it is not cached and loads it before the agent runs. The first run may need an internet connection to fetch model files.

```csharp
using ElBruno.MAF.FoundryLocal;
using Microsoft.Agents.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

const string modelAlias = "qwen2.5-0.5b";
await using var foundry = new FoundryLocalModelLifecycleService(
    Options.Create(new FoundryLocalOptions
    {
        ModelAlias = modelAlias,
        DownloadIfMissing = true,
        UnloadOnExit = true
    }),
    Options.Create(new ChatRuntimeOptions()),
    NullLogger<FoundryLocalModelLifecycleService>.Instance);

var availableModels = await foundry.ListModelsAsync();
if (!availableModels.Contains(modelAlias, StringComparer.Ordinal))
    throw new InvalidOperationException($"Chat model '{modelAlias}' is not in the catalog.");

await foundry.DownloadModelAsync(modelAlias);

using var chatClient = new FoundryLocalChatClientAdapter(
    foundry, NullLogger<FoundryLocalChatClientAdapter>.Instance);
var agent = new ChatClientAgent(
    chatClient: chatClient, instructions: "Answer in one concise paragraph.");
var response = await agent.RunAsync("What does it mean to run an AI model on-device?");
Console.WriteLine(response.Text);
```

The agent still uses `IChatClient`. The adapter opens a native `ChatSession` for each call and also supports `IChatClient.GetStreamingResponseAsync`. If your app uses dependency injection, see the [README's registration example](../README.md#quick-start-foundry-local-as-ichatclient).

## Batch embeddings on-device

Create a separate console app for the embedding model. This one only needs the adapter package.

```shell
dotnet new console -n LocalEmbeddings -f net11.0
cd LocalEmbeddings
dotnet add package ElBruno.MAF.FoundryLocal.Adapter --version 0.5.0
```

Replace `Program.cs` and run `dotnet run -r win-x64`. The setup is the same as for chat, but the model must support embeddings. The program sends two inputs in one batch and prints the number of dimensions in each vector.

```csharp
using ElBruno.MAF.FoundryLocal;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

const string modelAlias = "qwen3-embedding-0.6b";
await using var foundry = new FoundryLocalModelLifecycleService(
    Options.Create(new FoundryLocalOptions
    {
        ModelAlias = modelAlias,
        DownloadIfMissing = true,
        UnloadOnExit = true
    }),
    Options.Create(new ChatRuntimeOptions()),
    NullLogger<FoundryLocalModelLifecycleService>.Instance);

var availableModels = await foundry.ListModelsAsync();
if (!availableModels.Contains(modelAlias, StringComparer.Ordinal))
    throw new InvalidOperationException($"Embedding model '{modelAlias}' is not in the catalog.");

await foundry.DownloadModelAsync(modelAlias);

var embeddingAdapter = new FoundryLocalEmbeddingAdapter(
    foundry, NullLogger<FoundryLocalEmbeddingAdapter>.Instance);
string[] inputs = ["Foundry Local runs models on-device.", "Embeddings are useful for search."];
var batch = await embeddingAdapter.TryGenerateEmbeddingsAsync(inputs);
if (!batch.Success)
    throw new InvalidOperationException(batch.FailureReason);

for (var i = 0; i < inputs.Length; i++)
    Console.WriteLine($"{inputs[i]} -> {batch.Embeddings[i].Count} dimensions");
```

The embedding adapter uses `EmbeddingsSession`; it does not initialize a chat client. Typed decision support, including the KEV compatibility path introduced in 0.4.0, is unchanged.

**Upgrade notes:** Use `ChatResponse.Usage` or terminal streaming `UsageContent` for native token counts, not `RawRepresentation`, which is unset. `RequireSpecific` fails explicitly, so use `RequireAny` only if any available function is acceptable. Custom stop sequences hide the matching text across chunks, but the SDK still finishes generating. Legacy accessors remain on SDK 2.4.1 for migration; see the [migration guide](adapter-packaging-and-migration.md) for details.

**Suggested tags:** Foundry Local, .NET, Microsoft Agent Framework, Local AI, On-device inference, Embeddings
