# ElBruno.MAF.FoundryLocal.Adapter

`ElBruno.MAF.FoundryLocal.Adapter` provides a reusable bridge between Foundry Local SDK and `Microsoft.Extensions.AI` by exposing a production-friendly `IChatClient` adapter.

## What is included

- `FoundryLocalChatClientAdapter` for `IChatClient` integration
- Foundry Local lifecycle helpers (`FoundryLocalModelLifecycleService`)
- Option/message/response mapping helpers for MEAI compatibility
- Optional embedding helper path (`FoundryLocalEmbeddingAdapter`)

## Intended usage

Use this package when you want to:

- Keep model execution local with Foundry Local SDK
- Consume it via `IChatClient` in Agent Framework or MEAI-based apps
- Avoid running a local OpenAI-compatible REST server

For packaging and migration guidance, see `docs\adapter-packaging-and-migration.md` in this repository.
