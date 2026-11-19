# Changelog

## 0.5.0

- Use native Foundry Local `ChatSession` for chat and streaming and `EmbeddingsSession` for ordered batch vectors. `IChatClient` and typed decisions remain available.
- Map native token counts to chat responses and terminal streaming updates. Native responses no longer populate `RawRepresentation`.
- Reject `RequireSpecific` explicitly; `Auto`, `None`, and `RequireAny` map to native tool choice. Custom stop sequences filter visible text without ending native generation early.
- Keep the legacy public accessors on Foundry Local SDK 2.4.1. Targets remain .NET 10 and .NET 11 on Windows x64.

See the [developer announcement](docs/blog-post-foundry-local-native-sessions.md) and [migration guide](docs/adapter-packaging-and-migration.md).
