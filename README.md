# ElBruno.MAF.FoundryLocal

This repository demonstrates a local-first AI agent in C# using:

- .NET 10
- Microsoft Agent Framework
- Microsoft.Extensions.AI (`IChatClient`)
- Foundry Local SDK (in-process, no REST server)

## Why this sample exists

Foundry Local does not currently provide a first-party MEAI provider, while Agent Framework expects an `IChatClient`-compatible provider.  
This sample bridges that gap with a custom `FoundryLocalChatClientAdapter`.

## Architecture

```text
Console Host (src/ElBruno.MAF.FoundryLocal.Console)
   -> ChatClientAgent (Microsoft Agent Framework)
      -> IChatClient (Microsoft.Extensions.AI)
         -> FoundryLocalChatClientAdapter (src/ElBruno.MAF.FoundryLocal)
            -> FoundryLocalModelLifecycleService
               -> Foundry Local SDK
```

## Run

```bash
dotnet run --project src/ElBruno.MAF.FoundryLocal.Console
dotnet run --project src/ElBruno.MAF.FoundryLocal.Console -- --prompt "Explain local agents in one paragraph"
dotnet run --project src/ElBruno.MAF.FoundryLocal.Console -- --stream --prompt "What is Foundry Local?"
dotnet run --project src/ElBruno.MAF.FoundryLocal.Console -- --list-models
dotnet run --project src/ElBruno.MAF.FoundryLocal.Console -- --download-model qwen2.5-0.5b
dotnet run --project src/ElBruno.MAF.FoundryLocal.Console -- --diagnostics
dotnet run --project src/ElBruno.MAF.FoundryLocal.Console -- --rag --prompt "How does this sample bridge Agent Framework and Foundry Local?"
dotnet run --project src/ElBruno.MAF.FoundryLocal.Console -- --rag --rag-top-k 3 --stream --prompt "What are the current tool calling limitations?"
dotnet run --project src/ElBruno.MAF.FoundryLocal.Console -- --workflow --prompt "Explain how this sample works in 3 steps"
```

## Optional Aspire demonstration layer

This repository also includes a minimal Aspire AppHost (`src/ElBruno.MAF.FoundryLocal.AppHost`) for observability/demo purposes.
It runs the existing console app as an Aspire resource, but does **not** replace the core console flow.

```bash
dotnet run --project src/ElBruno.MAF.FoundryLocal.AppHost
```

Caveats:
- Aspire is optional in this sample; the default usage remains `ElBruno.MAF.FoundryLocal.Console`.
- Console-first commands/flags continue to be the primary way to run scenarios.
- The AppHost layer is intentionally lightweight and focused on orchestration/observability.

### Workflow mode sample (`--workflow`)

`--workflow` runs a lightweight 2-step local flow:

1. **Planner**: generates concise bullet-point plan from the user query.
2. **Responder**: generates final answer using the planner output.

This stays on the same local adapter/agent path (`ChatClientAgent` -> `IChatClient` -> `FoundryLocalChatClientAdapter`).

## Configuration

Edit `src/ElBruno.MAF.FoundryLocal.Console/appsettings.json`:

- `FoundryLocal.ModelAlias`
- `FoundryLocal.DownloadIfMissing`
- `FoundryLocal.UnloadOnExit`
- `Agent.Name`
- `Agent.Instructions`
- `Chat.Temperature`
- `Chat.MaxOutputTokens`
- `Chat.Streaming`

## Compatibility matrix (v1)

| Capability | Status | Notes |
|---|---|---|
| Local model download/load/unload | Yes | Uses Foundry Local SDK lifecycle |
| Non-streaming chat | Yes | Via adapter `GetResponseAsync` |
| Streaming chat | Best effort | Via adapter `GetStreamingResponseAsync` |
| Agent Framework single-agent flow | Yes | `ChatClientAgent` based |
| MEAI `IChatClient` | Yes | Custom adapter |
| Tool calling (MEAI -> Foundry Local) | Partial | `ChatOptions.Tools` are mapped for AIFunction-based tools |
| Tool call response mapping | Partial | Function tool calls are mapped back to `FunctionCallContent` |
| Demo tool (`get_current_time`) | Yes | Registered in console `ChatClientAgent` |
| Embedding adapter path | Yes (best effort) | Uses `IModel.GetEmbeddingClientAsync` when available |
| Local RAG sample | Yes | In-memory corpus + embedding similarity + grounded prompt |
| REST server usage | No | Explicitly avoided |

## Packaging adapter for reuse

The adapter project (`src/ElBruno.MAF.FoundryLocal`) is prepared with NuGet package metadata so it can be reused without project-to-project coupling.

- Local pack command:
  - `dotnet pack src\ElBruno.MAF.FoundryLocal\ElBruno.MAF.FoundryLocal.csproj -c Release -o .\artifacts\packages`
- Packaging and migration guide:
  - `docs\adapter-packaging-and-migration.md`

The sample solution continues to use `ProjectReference` for in-repo development. Consumers can migrate to `PackageReference` using the guide above.

## Limitations

- Tool schema mapping currently supports JSON-schema object roots with common primitive/object/array shapes; complex schemas may be skipped.
- Tool calling/function invocation behavior remains model/path dependent and intentionally conservative.
- Some advanced chat options are logged as unsupported by the adapter.
- RAG mode is a minimal sample with a tiny in-memory corpus (no persistence, no vector DB, no chunking pipeline).
- Embeddings depend on model/runtime support; if unavailable, `--rag` returns a clear message and exits gracefully.
- Workflow mode performs two model calls per user prompt (planner + responder), so expect higher latency than default mode.
- `--workflow` and `--rag` are intentionally not combined in this sample flow.
- This is an experimental bridge, not official Foundry Local MEAI integration.

## Troubleshooting

- First run may take longer due to model download/load.
- If a model alias is invalid, run `--list-models` and choose an available alias.
- If model download is disabled and cache is empty, enable `DownloadIfMissing`.
