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

## 👋 About the Author

Hi! I'm **ElBruno** 🧡, a passionate developer and content creator exploring AI, .NET, and modern development practices.

**Made with ❤️ by [ElBruno](https://github.com/elbruno)**

If you like this project, consider following my work across platforms:

- 🔗 **Blog**: [ElBruno.com](https://elbruno.com) — Deep dives on embeddings, RAG, .NET, and local AI
- 💻 **YouTube**: [youtube.com/elbruno](https://www.youtube.com/elbruno) — Demos, tutorials, and live coding
- 📺 **LinkedIn**: [@elbruno](https://www.linkedin.com/in/elbruno/) — Professional updates and insights
- 𝕏 **Twitter**: [@elbruno](https://www.x.com/elbruno/) — Quick tips, releases, and tech news
- 📻 **Podcast**: [No Tienen Nombre](https://notienenombre.com) — Spanish-language episodes on AI, development, and tech culture
