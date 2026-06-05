# PRD: .NET 10 Console Agent with Microsoft Agent Framework + Foundry Local SDK + Microsoft.Extensions.AI

## 1. Product Name

**ElBruno.LocalAgent.Console**

Working repo name options:

- `ElBruno.LocalAgent.Console`
- `ElBruno.MAF.FoundryLocal`
- `ElBruno.AgentFramework.FoundryLocal`
- `ElBruno.FoundryLocalAgent`

Recommended: **`ElBruno.MAF.FoundryLocal`**

## 2. Problem Statement

Developers are asking whether it is possible to build an AI agent in C# using:

- .NET 10
- Microsoft Agent Framework
- Microsoft.Extensions.AI
- Foundry Local SDK
- A non-REST, local SDK approach

The current friction point is that Microsoft Agent Framework works best when the model provider exposes a `Microsoft.Extensions.AI.IChatClient`, while Foundry Local SDK provides its own native lifecycle and OpenAI-compatible client APIs. This means a direct “plug Foundry Local SDK into Agent Framework” experience is not currently obvious.

The application should answer this practical question:

> Can we create a .NET console application that uses Microsoft Agent Framework and Foundry Local SDK together, without using the Foundry Local REST server, by adding a clean adapter layer around Foundry Local?

## 3. Product Goal

Create a working .NET 10 console sample that demonstrates a local AI agent using Microsoft Agent Framework and Foundry Local SDK through a custom adapter that exposes Foundry Local as a `Microsoft.Extensions.AI.IChatClient`.

The app should be small, readable, and useful as a reference implementation for .NET developers who want local, private, on-device agents without spinning up a REST service.

## 4. Non-Goals

This PRD does not aim to:

- Build a production-grade agent runtime.
- Replace official Foundry Local SDK support for Microsoft.Extensions.AI, if/when that becomes available.
- Use the optional Foundry Local REST server as the primary integration path.
- Create a UI, web app, Aspire app, or multi-agent workflow in the first version.
- Implement full OpenAI parity.
- Implement advanced tool-calling if the underlying Foundry Local model/client path does not support it cleanly.
- Hide current SDK limitations.

## 5. Background and Current State

### 5.1 Microsoft.Extensions.AI

`Microsoft.Extensions.AI` provides common abstractions for AI clients in .NET. The key abstraction for chat is `IChatClient`, and the package also supports higher-level utilities such as telemetry, caching, and automatic function invocation through familiar dependency injection and middleware patterns.

Reference:
https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai  
https://www.nuget.org/packages/Microsoft.Extensions.AI

### 5.2 Microsoft Agent Framework

Microsoft Agent Framework supports building agents and workflows in .NET and Python. The framework supports agent providers that expose chat clients, and its provider documentation states that any inference service with a `Microsoft.Extensions.AI.IChatClient` implementation can be used to build simple agents.

Reference:
https://learn.microsoft.com/en-us/agent-framework/overview/  
https://learn.microsoft.com/en-us/agent-framework/agents/providers/  
https://www.nuget.org/packages/Microsoft.Agents.AI/

### 5.3 Foundry Local SDK

Foundry Local is designed for local/on-device AI. It provides native SDKs, including C#, and can download, cache, load, and run optimized local models in-process. It also has an optional OpenAI-compatible REST server, but this project intentionally avoids that path.

Reference:
https://learn.microsoft.com/en-us/azure/foundry-local/what-is-foundry-local  
https://learn.microsoft.com/en-us/azure/foundry-local/get-started  
https://learn.microsoft.com/en-us/azure/foundry-local/reference/reference-sdk-current  
https://www.nuget.org/packages/Microsoft.AI.Foundry.Local

### 5.4 Known Compatibility Gap

A public Foundry Local GitHub discussion highlights the same issue: Microsoft Agent Framework is based on `IChatClient` from Microsoft.Extensions.AI, while Foundry Local SDK uses OpenAI-compatible client models that do not directly map to Agent Framework / MEAI types without possible information loss.

Reference:
https://github.com/microsoft/Foundry-Local/discussions/434

## 6. Proposed Solution

Build a .NET 10 console app with the following architecture:

```text
Console App
   |
   |-- Microsoft Agent Framework Agent
   |       |
   |       |-- Microsoft.Extensions.AI.IChatClient
   |               |
   |               |-- Custom FoundryLocalChatClientAdapter
   |                       |
   |                       |-- Foundry Local SDK Manager
   |                       |-- Foundry Local model lifecycle
   |                       |-- Foundry Local native/OpenAI-compatible chat client
```

The key product deliverable is the custom adapter:

```csharp
public sealed class FoundryLocalChatClientAdapter : IChatClient
{
    // Converts Microsoft.Extensions.AI chat messages/options
    // into Foundry Local compatible calls.
}
```

This adapter should make Foundry Local usable by Agent Framework as a normal chat provider, while documenting all mapping decisions and limitations.

## 7. Target User

Primary users:

- .NET developers exploring local AI agents.
- Cloud advocates and demo builders.
- Enterprise developers evaluating private/local inference.
- Developers already using Microsoft.Extensions.AI and wanting Foundry Local as another provider.
- Developers using Microsoft Agent Framework who want a local model option without REST.

Secondary users:

- SDK teams looking for concrete feedback on where official integration could improve.
- Community contributors interested in a MEAI provider for Foundry Local.

## 8. User Stories

### US1: Run a local agent from the console

As a .NET developer, I want to run a console app that starts a local model and sends prompts through an Agent Framework agent, so I can validate the end-to-end integration.

Acceptance criteria:

- `dotnet run` starts the console app.
- The app loads a configured Foundry Local model.
- The user can type a prompt.
- The agent returns a response.
- The app supports streaming output if the SDK path supports it.

### US2: Avoid the REST server

As a developer, I want the app to use the Foundry Local SDK directly, so that I can keep the integration in-process and avoid running a local REST service.

Acceptance criteria:

- The app does not start or depend on the optional Foundry Local REST server.
- Foundry Local SDK is used directly.
- Documentation explains why REST was intentionally avoided.

### US3: Expose Foundry Local through Microsoft.Extensions.AI

As a developer, I want a small adapter that implements `IChatClient`, so that Microsoft Agent Framework can consume Foundry Local as a normal chat provider.

Acceptance criteria:

- Adapter implements the required `IChatClient` members.
- Adapter supports non-streaming chat completions.
- Adapter supports streaming chat completions if the underlying Foundry Local SDK supports the required operation.
- Adapter maps MEAI chat messages to Foundry Local-compatible message structures.
- Adapter maps basic options such as temperature, max tokens, and stop sequences where supported.
- Unsupported options are ignored safely and logged.

### US4: Make limitations explicit

As a developer, I want clear documentation of what works and what does not, so I can avoid over-promising Foundry Local + Agent Framework capabilities.

Acceptance criteria:

- README includes a compatibility matrix.
- Tool-calling support is explicitly marked as supported, partial, or not supported.
- Function invocation behavior is documented.
- Multimodal support is documented as out of scope for v1 unless proven.
- Any message conversion loss is documented.

### US5: Provide a clean path to official support

As a developer, I want the sample structured so that the custom adapter can be replaced later by an official Foundry Local MEAI provider.

Acceptance criteria:

- Adapter is isolated behind DI.
- No Agent Framework code directly depends on Foundry Local SDK types.
- README includes a “Future official provider replacement” section.

## 9. Functional Requirements

### FR1: Console Host

The app must be a .NET 10 console application.

Required commands:

```bash
dotnet run
dotnet run -- --model <model-alias>
dotnet run -- --prompt "Explain local agents in one paragraph"
dotnet run -- --stream
dotnet run -- --diagnostics
```

Optional commands:

```bash
dotnet run -- --list-models
dotnet run -- --download-model <model-alias>
dotnet run -- --unload-on-exit
```

### FR2: Configuration

The app must support configuration from:

1. Command-line arguments.
2. `appsettings.json`.
3. Environment variables.

Configuration values:

```json
{
  "FoundryLocal": {
    "ModelAlias": "qwen2.5-0.5b",
    "DownloadIfMissing": true,
    "UnloadOnExit": true
  },
  "Agent": {
    "Name": "LocalFoundryAgent",
    "Instructions": "You are a helpful local AI assistant. Keep answers concise."
  },
  "Chat": {
    "Temperature": 0.7,
    "MaxOutputTokens": 512,
    "Streaming": true
  },
  "Diagnostics": {
    "EnableOpenTelemetry": true,
    "LogMessageMapping": false
  }
}
```

### FR3: Foundry Local Model Lifecycle

The app must:

- Initialize Foundry Local SDK.
- Resolve the configured model alias.
- Download the model if missing and if configured to do so.
- Load the model.
- Create a chat client.
- Dispose/unload resources on exit when configured.

### FR4: MEAI Adapter

The adapter must implement `Microsoft.Extensions.AI.IChatClient`.

Minimum supported behavior:

- Non-streaming chat.
- Streaming chat if supported by the underlying Foundry Local SDK.
- Cancellation tokens.
- Basic chat options mapping.
- Safe disposal.

Message mapping:

| MEAI Role | Foundry Local/OpenAI-Compatible Role | Notes |
|---|---|---|
| System | system/developer if supported | Preserve instruction text. |
| User | user | Required. |
| Assistant | assistant | Required for chat history. |
| Tool | tool | Only if supported. Otherwise document limitation. |

Options mapping:

| MEAI Option | Foundry Local Mapping | Required |
|---|---|---|
| Temperature | Temperature | Yes, if available |
| MaxOutputTokens | Max tokens / max completion tokens | Yes, if available |
| StopSequences | Stop | Best effort |
| Tools | Tool definitions | Only if supported |
| ResponseFormat | Response format | Optional |
| TopP | TopP | Optional |
| FrequencyPenalty | Frequency penalty | Optional |
| PresencePenalty | Presence penalty | Optional |

### FR5: Agent Framework Integration

The app must create a Microsoft Agent Framework agent that uses the adapter-backed `IChatClient`.

The agent must:

- Have a configurable name.
- Have configurable instructions.
- Support single-turn prompt mode.
- Support interactive chat mode.
- Support streaming mode where available.

### FR6: Diagnostics

The app must expose:

- Model alias used.
- Whether model was downloaded or already cached.
- Whether model was loaded successfully.
- Whether streaming is enabled.
- Which adapter features are supported.
- Warnings for unsupported options.
- Basic timings for model load and response generation.

### FR7: README

The README must include:

- Why this sample exists.
- Current state of Foundry Local + MEAI + Agent Framework.
- How to run.
- How the adapter works.
- Compatibility matrix.
- Limitations.
- Troubleshooting.
- Links to official docs.
- Future improvements.

## 10. Non-Functional Requirements

### NFR1: Developer Experience

The sample must be easy to run:

```bash
git clone <repo>
cd <repo>
dotnet run
```

The first run may download a model.

### NFR2: Local-First Behavior

The app must not require:

- Azure subscription.
- Cloud endpoint.
- Foundry Local REST server.
- API key for local model inference.

### NFR3: Privacy

The app must run inference locally. The README must explain that prompts and responses are handled locally by Foundry Local, while also noting that model download and SDK update behavior may require network access.

### NFR4: Reliability

The app must handle:

- Missing model.
- Failed model download.
- Unsupported model.
- Unsupported options.
- Cancellation.
- User exit.
- Model unload/disposal errors.

### NFR5: Observability

Use structured logging through `Microsoft.Extensions.Logging`.

Optional:

- Add OpenTelemetry instrumentation through MEAI middleware if the adapter supports the required pipeline.

### NFR6: Maintainability

The project must keep adapter code isolated.

Recommended project structure:

```text
src/
  ElBruno.MAF.FoundryLocal.Console/
    Program.cs
    appsettings.json

  ElBruno.MAF.FoundryLocal/
    FoundryLocalChatClientAdapter.cs
    FoundryLocalChatClientAdapterOptions.cs
    FoundryLocalMessageMapper.cs
    FoundryLocalOptionMapper.cs
    FoundryLocalModelLifecycleService.cs

tests/
  ElBruno.MAF.FoundryLocal.Tests/
    FoundryLocalMessageMapperTests.cs
    FoundryLocalOptionMapperTests.cs
    FoundryLocalChatClientAdapterTests.cs
```

## 11. Architecture

### 11.1 Main Components

#### Console Shell

Responsibilities:

- Parse CLI args.
- Load configuration.
- Configure DI.
- Start interactive or single-prompt mode.

#### Agent Factory

Responsibilities:

- Create the Agent Framework agent.
- Inject `IChatClient`.
- Apply instructions and name.

#### Foundry Local Model Lifecycle Service

Responsibilities:

- Find model by alias.
- Download model if needed.
- Load model.
- Create native chat client.
- Dispose/unload model.

#### FoundryLocalChatClientAdapter

Responsibilities:

- Implement MEAI `IChatClient`.
- Map MEAI messages to Foundry Local/OpenAI-compatible messages.
- Map MEAI options to Foundry Local/OpenAI-compatible options.
- Return MEAI-compatible responses.
- Stream response updates where possible.

#### Mapper Classes

Responsibilities:

- Keep mapping logic testable.
- Document unsupported features.
- Prevent silent behavior surprises.

## 12. Technical Design

### 12.1 Dependency Injection

Pseudo-code:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<FoundryLocalOptions>(
    builder.Configuration.GetSection("FoundryLocal"));

builder.Services.Configure<LocalAgentOptions>(
    builder.Configuration.GetSection("Agent"));

builder.Services.AddSingleton<FoundryLocalModelLifecycleService>();
builder.Services.AddSingleton<IChatClient, FoundryLocalChatClientAdapter>();
builder.Services.AddSingleton<LocalAgentFactory>();

using var host = builder.Build();
await host.RunAsync();
```

### 12.2 Agent Creation

Pseudo-code:

```csharp
public sealed class LocalAgentFactory
{
    private readonly IChatClient _chatClient;
    private readonly IOptions<LocalAgentOptions> _options;

    public LocalAgentFactory(
        IChatClient chatClient,
        IOptions<LocalAgentOptions> options)
    {
        _chatClient = chatClient;
        _options = options;
    }

    public AIAgent Create()
    {
        return _chatClient.AsAIAgent(
            name: _options.Value.Name,
            instructions: _options.Value.Instructions);
    }
}
```

Exact API shape must be verified against the current Microsoft Agent Framework package version used in the implementation.

### 12.3 Adapter Shape

Pseudo-code:

```csharp
public sealed class FoundryLocalChatClientAdapter : IChatClient
{
    private readonly FoundryLocalModelLifecycleService _modelService;
    private readonly ILogger<FoundryLocalChatClientAdapter> _logger;

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var client = await _modelService.GetChatClientAsync(cancellationToken);

        var foundryMessages = FoundryLocalMessageMapper.Map(messages);
        var foundryOptions = FoundryLocalOptionMapper.Map(options);

        var response = await client.CompleteChatAsync(
            foundryMessages,
            foundryOptions,
            cancellationToken);

        return FoundryLocalResponseMapper.Map(response);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var client = await _modelService.GetChatClientAsync(cancellationToken);

        var foundryMessages = FoundryLocalMessageMapper.Map(messages);
        var foundryOptions = FoundryLocalOptionMapper.Map(options);

        await foreach (var update in client.CompleteChatStreamingAsync(
            foundryMessages,
            foundryOptions,
            cancellationToken))
        {
            yield return FoundryLocalResponseMapper.MapStreamingUpdate(update);
        }
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        return serviceType == typeof(FoundryLocalChatClientAdapter)
            ? this
            : null;
    }

    public void Dispose()
    {
        // Dispose model/native resources if owned here.
    }
}
```

Exact method names must be adjusted to the current `IChatClient` interface and Foundry Local SDK APIs.

## 13. Compatibility Matrix

| Capability | v1 Target | Notes |
|---|---:|---|
| Local model download | Yes | Through Foundry Local SDK |
| Local model load/unload | Yes | Through Foundry Local SDK |
| Non-streaming chat | Yes | Required |
| Streaming chat | Yes / Best effort | Required if SDK path supports it cleanly |
| Agent Framework single agent | Yes | Required |
| Agent Framework workflows | No | Out of scope for v1 |
| MEAI `IChatClient` | Yes | Through custom adapter |
| MEAI telemetry middleware | Best effort | Depends on adapter compatibility |
| Tool calling | Partial / TBD | Must be verified per Foundry Local model/client support |
| Function invocation | Partial / TBD | Must be documented honestly |
| Embeddings | No | Future milestone |
| Audio transcription | No | Future milestone |
| REST server | No | Explicitly avoided |
| Aspire orchestration | No | Future milestone |

## 14. Risks and Mitigations

### Risk 1: Message conversion loses information

Mitigation:

- Keep mapping logic isolated.
- Add unit tests for all supported roles.
- Log unsupported message content.
- Document known losses.

### Risk 2: Tool-calling expectations are too high

Mitigation:

- Make v1 a chat-first agent.
- Add tool-calling only after validating Foundry Local support path.
- Keep README compatibility matrix clear.

### Risk 3: SDK APIs change quickly

Mitigation:

- Pin package versions.
- Add a package version table to README.
- Keep adapter small and easy to update.

### Risk 4: Local model behavior differs from cloud models

Mitigation:

- Use short, simple default prompts.
- Document recommended local models.
- Add prompt examples that work well with small local models.

### Risk 5: First run is slow because model download/load takes time

Mitigation:

- Show progress where possible.
- Add diagnostics mode.
- Cache models through Foundry Local SDK behavior.
- Explain first-run behavior in README.

## 15. Success Metrics

The project is successful when:

- A developer can clone and run the sample.
- The console app loads a Foundry Local model.
- The Agent Framework agent returns a response.
- The app does not use the REST server.
- The adapter exposes Foundry Local as `IChatClient`.
- The README clearly explains current limitations.
- The sample can be used as a credible answer to the original community question.

## 16. MVP Scope

The MVP must include:

- .NET 10 console app.
- Foundry Local SDK model lifecycle.
- `IChatClient` adapter.
- Microsoft Agent Framework single-agent integration.
- Non-streaming prompt mode.
- Interactive console mode.
- Basic streaming if available.
- README with compatibility matrix.
- Unit tests for message and option mapping.

## 17. Post-MVP Roadmap

### Milestone 2: Tool Calling Investigation

- Validate whether Foundry Local models and OpenAI-compatible clients support tool-calling.
- Map MEAI tool definitions where possible.
- Add one demo tool, such as `get_current_time`.

### Milestone 3: Embeddings

- Add `IEmbeddingGenerator<string, Embedding<float>>` adapter if Foundry Local embedding APIs support the required shape.
- Add a small local RAG sample.

### Milestone 4: Agent Framework Workflows

- Add a two-step workflow:
  - Planner agent/function.
  - Responder agent.
- Keep inference local.

### Milestone 5: Aspire Demo

- Add Aspire only after the console sample works.
- Use Aspire for orchestration and observability, not as the core integration proof.

### Milestone 6: Package the Adapter

- Extract adapter into a reusable package.
- Consider naming:
  - `ElBruno.Extensions.AI.FoundryLocal`
  - `Microsoft.Extensions.AI.FoundryLocal.Experimental` if contributed upstream or aligned with Microsoft guidance.

## 18. Testing Strategy

### Unit Tests

- Map user/system/assistant messages.
- Map empty chat history.
- Map multiple messages.
- Map temperature.
- Map max tokens.
- Ignore unsupported options safely.
- Verify warnings for unsupported features.

### Integration Tests

- Optional and disabled by default if they download or load real models.
- Use a small configured model.
- Validate non-streaming response.
- Validate streaming response if supported.

### Manual Test Checklist

- Run app first time.
- Confirm model download.
- Confirm model load.
- Ask a simple question.
- Ask a multi-turn follow-up.
- Run with `--prompt`.
- Run with `--stream`.
- Run with invalid model alias.
- Run with `--diagnostics`.
- Confirm no REST server is started.

## 19. Open Questions

1. Does the current Foundry Local C# SDK expose enough metadata to map response usage/tokens into MEAI response metadata?
2. Does the native SDK path expose tool-calling for chat completions?
3. Are streaming deltas rich enough to preserve role, content parts, and finish reason?
4. Which Foundry Local model alias should be the default for a reliable first-run demo?
5. Should the adapter expose Foundry Local native client through `GetService()` for advanced callers?
6. Should the adapter live in the sample repo only, or should it become a reusable NuGet package?

## 20. Recommended Answer to the Original Question

A good solution is to build a thin `Microsoft.Extensions.AI.IChatClient` adapter around the Foundry Local SDK, then pass that adapter-backed chat client into Microsoft Agent Framework.

This keeps the architecture aligned with Agent Framework and MEAI, while still using Foundry Local in-process through the SDK instead of the optional REST server.

The main caveat is that this should be treated as an experimental bridge until Foundry Local provides an official MEAI provider. The first version should focus on chat and streaming, document any mapping limitations, and avoid over-promising tool-calling until it is validated with the selected local model and SDK version.

## 21. Initial README Pitch

> This repo explores how to build a local AI agent in C# using .NET 10, Microsoft Agent Framework, Microsoft.Extensions.AI, and Foundry Local SDK.
>
> The key idea: Foundry Local does not currently expose a first-party MEAI provider, so this sample adds a small `IChatClient` adapter that lets Microsoft Agent Framework consume Foundry Local as a local chat provider — without using the optional REST server.
>
> This is an experimental bridge and a learning sample, not an official SDK integration.

## 22. Source Links

- Microsoft.Extensions.AI docs: https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai
- Microsoft.Extensions.AI NuGet: https://www.nuget.org/packages/Microsoft.Extensions.AI/
- Microsoft Agent Framework overview: https://learn.microsoft.com/en-us/agent-framework/overview/
- Microsoft Agent Framework providers: https://learn.microsoft.com/en-us/agent-framework/agents/providers/
- Microsoft Agent Framework NuGet: https://www.nuget.org/packages/Microsoft.Agents.AI/
- Foundry Local overview: https://learn.microsoft.com/en-us/azure/foundry-local/what-is-foundry-local
- Foundry Local get started: https://learn.microsoft.com/en-us/azure/foundry-local/get-started
- Foundry Local SDK reference: https://learn.microsoft.com/en-us/azure/foundry-local/reference/reference-sdk-current
- Foundry Local NuGet: https://www.nuget.org/packages/Microsoft.AI.Foundry.Local
- Foundry Local GitHub discussion about MEAI compatibility: https://github.com/microsoft/Foundry-Local/discussions/434
