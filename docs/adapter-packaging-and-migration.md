# Adapter packaging and migration guide

This repository now includes a NuGet-ready baseline for the adapter project (`src\ElBruno.MAF.FoundryLocal`).

## Version 0.3.0 compatibility

The package targets .NET 10 and .NET 11 on Windows x64. It upgrades
`Microsoft.AI.Foundry.Local` from 1.2.1 to 2.4.1 and
`Microsoft.Extensions.AI` from 10.6.0 to 11.0.0. Applications may need to
update direct references to Microsoft.Extensions packages to avoid dependency
downgrades.

The adapter still uses the SDK's in-process OpenAI-compatible chat and embedding
clients, which Foundry Local 2.x marks obsolete and plans to remove at the end
of 2026. They remain available in 2.4.1; a future adapter release must migrate
to `ChatSession` and `EmbeddingsSession`. Foundry Local 2.x also makes execution
provider downloads explicit. This adapter does not automatically register
providers, so applications requiring hardware acceleration must configure them
through the SDK.

See the [Foundry Local SDK migration guide](https://learn.microsoft.com/azure/foundry-local/reference/reference-sdk-migration)
and [current SDK reference](https://learn.microsoft.com/azure/foundry-local/reference/reference-sdk-current).

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

For external consumers (or when repo consumers decide to switch), replace the project reference with:

```xml
<ItemGroup>
  <PackageReference Include="ElBruno.MAF.FoundryLocal.Adapter" Version="0.3.0" />
</ItemGroup>
```

The adapter surface remains the same, but the dependency upgrades above can
require changes to an application's other package references.
