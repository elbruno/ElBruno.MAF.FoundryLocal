# Adapter packaging and migration guide

This repository now includes a NuGet-ready baseline for the adapter project (`src\ElBruno.MAF.FoundryLocal`).

## Version 0.4.0 compatibility

Version 0.4.0 adds typed decision support while retaining the .NET 10 and
.NET 11 Windows x64 targets and dependencies introduced in 0.3.0:
`Microsoft.AI.Foundry.Local` 2.4.1 and `Microsoft.Extensions.AI` 11.0.0.
Applications upgrading from 0.2.x may need to update direct references to
Microsoft.Extensions packages to avoid dependency downgrades.

Foundry Local 2.x makes execution-provider downloads explicit. This adapter
does not automatically register providers, so applications requiring hardware
acceleration must configure them through the SDK.

See the [Foundry Local SDK migration guide](https://learn.microsoft.com/azure/foundry-local/reference/reference-sdk-migration)
and [current SDK reference](https://learn.microsoft.com/azure/foundry-local/reference/reference-sdk-current).

## Native chat and embedding sessions

`FoundryLocalChatClientAdapter` and `FoundryLocalEmbeddingAdapter` use
`ChatSession` and `EmbeddingsSession` in-process. Each chat request creates a
fresh session and supplies the full Microsoft.Extensions.AI message history.
Streaming emits text incrementally and returns complete function calls with the
terminal update. Native response handles are disposed by the adapters;
`RawRepresentation` is not populated by the native adapter. Use
`ChatResponse.Usage` or the terminal streaming update's `UsageContent` for
token counts reported by the SDK. Native responses and their item views cannot
outlive the adapter's disposal of the underlying handles.

Temperature, top-p and output token limits map to native request options.
Custom stop sequences truncate visible text (including across streaming chunks);
they do not stop the native generator early. SDK 2.4.1 documents request
cancellation rather than a successful custom stop, so the adapter lets the
request finish and retains its final usage. Tool choice supports `Auto`,
`None`, and `RequireAny` on compatible models. `RequireSpecific` throws
`NotSupportedException` before model preparation. Tool calls in `Auto` mode depend on the
selected model; ordinary text is never interpreted as a tool call. Embeddings
require a model with the `embeddings` task, such as `qwen3-embedding-0.6b`.

The public `GetChatClientAsync`, `TryGetEmbeddingClientAsync`, and
`FoundryLocalOptionMapper.Apply` signatures remain available on SDK 2.4.1 for
source and binary compatibility. Callers should migrate to
`FoundryLocalChatClientAdapter` (`IChatClient`) or
`FoundryLocalEmbeddingAdapter.TryGenerateEmbeddingsAsync` before upgrading to
an SDK that removes those legacy types. The adapter does not promise that
the legacy signatures will work on such an SDK.

## Typed decision models

`FoundryLocalDecisionAdapter` wraps the SDK's `DecisionSession` API and can
select a default model with `FoundryLocalOptions.DecisionModelAlias` or an
alias per `DecideAsync` call. Set `FoundryLocalOptions.ModelCacheDir` to reuse
the Foundry CLI model cache; otherwise, the SDK uses its default cache.

The current public KEV catalog reports task `decision`, while
`DecisionSession` requires `typed-decision`. The adapter resolves the public
model, downloads it when allowed, and registers its local path with
`typed-decision` metadata before running inference. This is a compatibility
workaround for the current SDK/catalog mismatch, not a change to the public
model's catalog metadata. Registration references the existing model assets
without copying them. An existing registration must point to the same model
path and have the expected task. The local ID is namespaced and keeps the
public model version; public/local identity collisions and package metadata
that overrides the registration alias are rejected.

## Package-ready metadata

The adapter project includes:

- Package identity (`PackageId`, `Version`)
- Author + description
- License expression
- Package readme
- Repository metadata

These values are intentionally practical defaults for internal/external package feeds.

## Create package locally

From repository root:

```powershell
dotnet pack src\ElBruno.MAF.FoundryLocal\ElBruno.MAF.FoundryLocal.csproj -c Release -o .\artifacts\packages
```

This command only builds `.nupkg` output locally. It does **not** publish.

## Suggested release flow (manual)

1. Update the package version in `src\ElBruno.MAF.FoundryLocal\ElBruno.MAF.FoundryLocal.csproj`.
2. Run tests:
   ```powershell
   dotnet test --nologo
   ```
3. Run pack:
   ```powershell
   dotnet pack src\ElBruno.MAF.FoundryLocal\ElBruno.MAF.FoundryLocal.csproj -c Release -o .\artifacts\packages
   ```
4. Validate `.nupkg` contents (readme, metadata, dependencies).
5. Publish via your feed workflow when ready (outside this repository baseline task).

## Migration path from in-repo usage

### Current in-repo setup

The console project currently uses:

```xml
<ProjectReference Include="..\ElBruno.MAF.FoundryLocal\ElBruno.MAF.FoundryLocal.csproj" />
```

This remains unchanged to keep local development fast and non-breaking.

### Consumer migration to package reference

For external consumers, replace the project reference with:

```xml
<ItemGroup>
  <PackageReference Include="ElBruno.MAF.FoundryLocal.Adapter" Version="0.5.0" />
</ItemGroup>
```

The adapter surface remains the same, but the dependency upgrades above can
require changes to an application's other package references.
