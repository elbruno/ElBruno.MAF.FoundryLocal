# Why I built `ElBruno.MAF.FoundryLocal` (and how to use it in minutes)

If you are building local AI apps in C#, you quickly hit a practical gap:

- **Foundry Local SDK** is great for local/on-device inference.
- **Microsoft.Extensions.AI** and **Microsoft Agent Framework** expect an `IChatClient`.
- There is no first-party Foundry Local -> `IChatClient` bridge yet.

That is exactly why I created this library.

---

## Why I created this

I wanted a **clean, non-REST, in-process** integration where:

1. Foundry Local stays local and private.
2. My app code uses standard MEAI abstractions (`IChatClient`).
3. The same `IChatClient` can be reused by Microsoft Agent Framework.

So the library provides a thin adapter:

`Foundry Local SDK` -> `FoundryLocalChatClientAdapter` -> `IChatClient`

This lets you write provider-agnostic app code while still running local inference.

---

## Minimal usage

Install:

```bash
dotnet add package ElBruno.MAF.FoundryLocal.Adapter
dotnet add package Microsoft.Extensions.Hosting
dotnet add package Microsoft.Agents.AI
```

Use the wrapper as `IChatClient`:

```csharp
using ElBruno.MAF.FoundryLocal;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<FoundryLocalOptions>(o =>
{
    o.ModelAlias = "qwen2.5-0.5b";
    o.DownloadIfMissing = true;
    o.UnloadOnExit = true;
});

builder.Services.Configure<ChatRuntimeOptions>(_ => { });
builder.Services.AddSingleton<FoundryLocalModelLifecycleService>();
builder.Services.AddSingleton<IChatClient, FoundryLocalChatClientAdapter>();

using var host = builder.Build();
var chatClient = host.Services.GetRequiredService<IChatClient>();

var response = await chatClient.GetResponseAsync(
[
    new(ChatRole.User, "Explain local-first AI in one paragraph.")
]);

Console.WriteLine(response.Text);
```

Reuse the same wrapper with Microsoft Agent Framework:

```csharp
using Microsoft.Agents.AI;

var agent = new ChatClientAgent(
    chatClient: chatClient,
    instructions: "You are a concise local assistant.",
    name: "LocalFoundryAgent",
    description: "Foundry Local + IChatClient adapter",
    tools: null,
    loggerFactory: null,
    services: null);

var agentResponse = await agent.RunAsync(
    "Give me 3 bullet points about local AI in .NET.",
    session: null,
    options: null);

Console.WriteLine(agentResponse);
```

---

## What this gives you

- Local inference through Foundry Local SDK
- No local REST server required
- Standard MEAI `IChatClient` programming model
- Easy path to Agent Framework agents

In short: **local model runtime + standard .NET AI abstractions + minimal glue code**.
