# Adapter packaging and migration guide

This repository now includes a NuGet-ready baseline for the adapter project (`src\ElBruno.MAF.FoundryLocal`).

## Version 0.4.0 compatibility

Version 0.4.0 adds typed decision support while retaining the .NET 10 and
.NET 11 Windows x64 targets and dependencies introduced in 0.3.0:
`Microsoft.AI.Foundry.Local` 2.4.1 and `Microsoft.Extensions.AI` 11.0.0.
Applications upgrading from 0.2.x may need to update direct references to
Microsoft.Extensions packages to avoid dependency downgrades.

The adapter still uses the SDK's in-process OpenAI-compatible chat and embedding
clients, which Foundry Local 2.x marks obsolete and plans to remove at the end
of 2026. They remain available in 2.4.1; a future adapter release must migrate
to `ChatSession` and `EmbeddingsSession`. Foundry Local 2.x also makes execution
provider downloads explicit. This adapter does not automatically register
providers, so applications requiring hardware acceleration must configure them
through the SDK.

See the [Foundry Local SDK migration guide](https://learn.microsoft.com/azure/foundry-local/reference/reference-sdk-migration)
and [current SDK reference](https://learn.microsoft.com/azure/foundry-local/reference/reference-sdk-current).

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

For external consumers, once version 0.4.0 is published, replace the project reference with:

```xml
<ItemGroup>
  <PackageReference Include="ElBruno.MAF.FoundryLocal.Adapter" Version="0.4.0" />
</ItemGroup>
```

The adapter surface remains the same, but the dependency upgrades above can
require changes to an application's other package references.
