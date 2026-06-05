# Adapter packaging and migration guide

This repository now includes a NuGet-ready baseline for the adapter project (`src\ElBruno.MAF.FoundryLocal`).

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
  <PackageReference Include="ElBruno.MAF.FoundryLocal.Adapter" Version="1.0.0" />
</ItemGroup>
```

No API-level migration is expected because the adapter surface remains the same; only the dependency acquisition mechanism changes.
