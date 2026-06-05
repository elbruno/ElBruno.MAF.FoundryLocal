# Aspire sample

This sample follows the current Aspire app model from [aspire.dev](https://aspire.dev) and includes:

- API backend (`aspireSample.ApiService`) that uses:
  - `FoundryLocalModelLifecycleService`
  - `FoundryLocalChatClientAdapter` as `IChatClient`
  - MEAI OpenTelemetry instrumentation (`UseOpenTelemetry`) for GenAI traces
- Blazor web app (`aspireSample.Web`) with a simple chat UI
- Aspire AppHost orchestration for both projects (web -> api)

## Run

```powershell
cd C:\src\ElBruno.MAF.FoundryLocal\src\aspireSample
dotnet run --project .\aspireSample.AppHost\aspireSample.AppHost.csproj
```

When it starts, open the Aspire Dashboard URL shown in the console and use the login URL/token emitted by AppHost. Open the **web** resource, enter a prompt, then click either **Send with MEAI** or **Send with Agent**.

## Endpoints

- `GET /models`  
  Lists model aliases from Foundry Local catalog.
- `POST /chat` (direct MEAI + FoundryLocal adapter) with JSON body:

```json
{ "prompt": "Explain local AI agents in one short paragraph." }
```

- `POST /chat-agent` (Agent Framework `ChatClientAgent` over same adapter) with JSON body:

```json
{ "prompt": "Explain local AI agents in one short paragraph." }
```

Both return:
- `backend` (`meai` or `agent-framework`)
- `model`
- `response`

## Quick test flow

1. Start AppHost (command above).
2. In Aspire Dashboard, open the `web` endpoint.
3. Use the chat textbox and click **Send with MEAI** or **Send with Agent**.
4. The UI calls:
   - `/chat` for direct MEAI path
   - `/chat-agent` for Agent Framework path
5. In Aspire Dashboard traces, inspect the API request spans and GenAI spans emitted from MEAI OpenTelemetry instrumentation (`Microsoft.Extensions.AI` source).
