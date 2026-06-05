# Aspire sample

This sample follows the current Aspire app model from [aspire.dev](https://aspire.dev) and includes:

- API backend (`aspireSample.ApiService`) that uses:
  - `FoundryLocalModelLifecycleService`
  - `FoundryLocalChatClientAdapter` as `IChatClient`
- Blazor web app (`aspireSample.Web`) with a simple chat UI
- Aspire AppHost orchestration for both projects (web -> api)

## Run

```powershell
cd C:\src\ElBruno.MAF.FoundryLocal\src\aspireSample
dotnet run --project .\aspireSample.AppHost\aspireSample.AppHost.csproj
```

When it starts, open the Aspire Dashboard URL shown in the console and use the login URL/token emitted by AppHost. Open the **web** resource, enter a prompt, and send it.

## Endpoints

- `GET /models`  
  Lists model aliases from Foundry Local catalog.
- `POST /chat` with JSON body:

```json
{ "prompt": "Explain local AI agents in one short paragraph." }
```

Returns the model alias and LLM response generated through `ElBruno.MAF.FoundryLocal`.

## Quick test flow

1. Start AppHost (command above).
2. In Aspire Dashboard, open the `web` endpoint.
3. Use the chat textbox and click **Send**.
4. The UI calls `apiservice /chat`, which executes local Foundry model inference through the adapter.
