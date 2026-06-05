# ElBruno.MAF.FoundryLocal

[![NuGet](https://img.shields.io/nuget/v/ElBruno.MAF.FoundryLocal.Adapter.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.MAF.FoundryLocal.Adapter)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ElBruno.MAF.FoundryLocal.Adapter.svg?style=flat-square&logo=nuget)](https://www.nuget.org/packages/ElBruno.MAF.FoundryLocal.Adapter)
[![Publish Status](https://github.com/elbruno/ElBruno.MAF.FoundryLocal/actions/workflows/publish.yml/badge.svg)](https://github.com/elbruno/ElBruno.MAF.FoundryLocal/actions/workflows/publish.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg?style=flat-square)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![GitHub stars](https://img.shields.io/github/stars/elbruno/ElBruno.MAF.FoundryLocal?style=social)](https://github.com/elbruno/ElBruno.MAF.FoundryLocal)
[![Twitter Follow](https://img.shields.io/twitter/follow/elbruno?style=social)](https://twitter.com/elbruno)

This repository demonstrates a local-first AI agent in C# using:

- .NET 10
- Microsoft Agent Framework
- Microsoft.Extensions.AI (`IChatClient`)
- Foundry Local SDK (in-process, no REST server)

## Repository assets and documentation rules

- Documentation location rule: all docs live under `docs\`, except `README.md` and `LICENSE` at root.
- Image generation prompts are tracked in `docs\image-prompts.md`.

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

## Quick start: Foundry Local as `IChatClient`

```csharp
using ElBruno.MAF.FoundryLocal;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

var builder = Host.CreateApplicationBuilder(args);

// ElBruno.MAF.FoundryLocal options for model lifecycle (alias, auto-download, unload behavior).
builder.Services.Configure<FoundryLocalOptions>(o =>
{
    o.ModelAlias = "qwen2.5-0.5b";
    o.DownloadIfMissing = true;
    o.UnloadOnExit = true;
});
builder.Services.Configure<ChatRuntimeOptions>(_ => { });

// ElBruno.MAF.FoundryLocal service that handles Foundry Local manager/model/chat client lifecycle.
builder.Services.AddSingleton<FoundryLocalModelLifecycleService>();

// Core value of this library:
// register FoundryLocalChatClientAdapter so Foundry Local is exposed as MEAI IChatClient.
builder.Services.AddSingleton<IChatClient, FoundryLocalChatClientAdapter>();

using var host = builder.Build();
// From here on you code against standard IChatClient, not Foundry Local-specific SDK calls.
var chatClient = host.Services.GetRequiredService<IChatClient>();

var response = await chatClient.GetResponseAsync(
[
    new(ChatRole.User, "Explain local AI agents in one short paragraph.")
]);

Console.WriteLine(response.Text);
```

## Quick start: use the same wrapper with Microsoft Agent Framework

This is the key benefit of this library: once Foundry Local is exposed as `IChatClient`,
you can pass that same `IChatClient` to Agent Framework and use local inference through
standard agent APIs.

```csharp
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

// Reuse the IChatClient instance created in the previous snippet.
// No Foundry Local-specific types are needed here.
var agent = new ChatClientAgent(
    chatClient: chatClient,
    // Agent Framework sends these instructions through the same IChatClient pipeline.
    instructions: "You are a concise local assistant.",
    name: "LocalFoundryAgent",
    description: "Foundry Local + MEAI adapter",
    tools: null,
    loggerFactory: null,
    services: null);

var agentResponse = await agent.RunAsync(
    "Give me 3 bullet points about local-first AI in .NET.",
    session: null,
    options: null);

// The call above is routed to Foundry Local through ElBruno.MAF.FoundryLocal adapter.
Console.WriteLine(agentResponse);
```

## Minimal package install

```bash
dotnet add package ElBruno.MAF.FoundryLocal.Adapter
dotnet add package Microsoft.Agents.AI
dotnet add package Microsoft.Extensions.Hosting
```

## Scope of this README

This README is intentionally focused on the simplest adoption path:

1. Foundry Local SDK wrapped as `IChatClient`.
2. Reusing that same `IChatClient` to create agents with Microsoft Agent Framework.

Advanced scenarios (console modes, Aspire demo, RAG/workflow extras) can be expanded later in `docs\`.

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
