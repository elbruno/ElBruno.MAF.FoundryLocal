using System.Text.Json;
using Microsoft.AI.Foundry.Local;

namespace ElBruno.MAF.FoundryLocal;

internal static class FoundryLocalDecisionModelResolver
{
    internal static async Task<IModel> ResolveAsync(
        ICatalog publicCatalog,
        ICatalog localCatalog,
        string modelAlias,
        bool downloadIfMissing,
        Action onDownloaded,
        CancellationToken cancellationToken)
    {
        var model = await publicCatalog.GetModelAsync(modelAlias, cancellationToken)
            ?? throw new InvalidOperationException(
                $"Decision model alias '{modelAlias}' was not found in the Foundry Local catalog.");

        if (model.Info.Task is not ("decision" or "typed-decision"))
        {
            throw new NotSupportedException(
                $"Model '{modelAlias}' has task '{model.Info.Task}', not a supported decision task.");
        }

        if (!await model.IsCachedAsync(cancellationToken))
        {
            if (!downloadIfMissing)
            {
                throw new InvalidOperationException(
                    $"Decision model '{modelAlias}' is not available in cache and DownloadIfMissing is disabled.");
            }

            await model.DownloadAsync(null, cancellationToken);
            onDownloaded();
        }

        var modelPath = await model.GetPathAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(modelPath))
        {
            throw new InvalidOperationException(
                $"Foundry Local did not return a cache path for decision model '{modelAlias}'.");
        }

        var idParts = model.Id.Split(':', 2);
        if (idParts.Length != 2 || string.IsNullOrWhiteSpace(idParts[0]) || string.IsNullOrWhiteSpace(idParts[1]))
        {
            throw new InvalidOperationException(
                $"Foundry Local returned invalid model ID '{model.Id}' for decision model '{modelAlias}'.");
        }

        var localAlias = $"elbruno-maf-foundrylocal-{idParts[0]}-typed-decision";
        var localId = $"{localAlias}:{idParts[1]}";
        if (await publicCatalog.GetModelAsync(localAlias, cancellationToken) is not null ||
            await publicCatalog.GetModelVariantAsync(localId, cancellationToken) is not null)
        {
            throw new InvalidOperationException(
                $"Foundry Local decision compatibility identity '{localId}' already exists in the public catalog.");
        }

        var inferenceMetadataPath = Path.Combine(modelPath, "inference_model.json");
        if (File.Exists(inferenceMetadataPath))
        {
            await using var stream = File.OpenRead(inferenceMetadataPath);
            using var inferenceMetadata = await JsonDocument.ParseAsync(
                stream, cancellationToken: cancellationToken);
            EnsureCompatiblePackageAlias(inferenceMetadata.RootElement, localAlias);
        }

        var decisionModel = await localCatalog.GetModelVariantAsync(localId, cancellationToken);
        if (decisionModel is null &&
            await localCatalog.GetModelAsync(localAlias, cancellationToken) is not null)
        {
            throw new InvalidOperationException(
                $"Foundry Local decision compatibility alias '{localAlias}' is already registered locally.");
        }

        if (decisionModel is null)
        {
            var runtime = model.Info.Runtime
                ?? throw new InvalidOperationException(
                    $"Foundry Local did not provide runtime metadata for decision model '{modelAlias}'.");
            var metadata = new ModelInfoBuilder()
                .SetStringProperty(ModelInfoPropertyKeys.Task, "typed-decision")
                .SetStringProperty(ModelInfoPropertyKeys.DisplayName, $"{modelAlias} typed decision");
            if (runtime.DeviceType != DeviceType.Invalid)
            {
                metadata.SetStringProperty(ModelInfoPropertyKeys.DeviceType, runtime.DeviceType.ToString());
            }

            if (!string.IsNullOrWhiteSpace(runtime.ExecutionProvider))
            {
                metadata.SetStringProperty(ModelInfoPropertyKeys.ExecutionProvider, runtime.ExecutionProvider);
            }

            if (model.Info.ContextLength is long contextLength && contextLength > 0)
            {
                metadata.SetIntProperty(ModelInfoPropertyKeys.ContextLength, contextLength);
            }

            try
            {
                decisionModel = await localCatalog.RegisterModelAsync(
                    modelPath, localId, metadata, cancellationToken);
            }
            catch (FoundryLocalException)
            {
                decisionModel = await localCatalog.GetModelVariantAsync(localId, cancellationToken);
                if (decisionModel is null)
                {
                    throw;
                }
            }
        }

        if (decisionModel.Id != localId ||
            decisionModel.Alias != localAlias ||
            decisionModel.Info.Task != "typed-decision")
        {
            throw new InvalidOperationException(
                $"Local decision model alias '{localAlias}' is already registered with task '{decisionModel.Info.Task}'.");
        }

        var registeredPath = await decisionModel.GetPathAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(registeredPath) ||
            !string.Equals(Path.GetFullPath(modelPath), Path.GetFullPath(registeredPath),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Local decision model alias '{localAlias}' is registered to a different model path.");
        }

        return decisionModel;
    }

    internal static void EnsureCompatiblePackageAlias(JsonElement metadata, string localAlias)
    {
        if (metadata.ValueKind == JsonValueKind.Object &&
            (!metadata.TryGetProperty("Alias", out var alias) ||
             (alias.ValueKind == JsonValueKind.String &&
              (string.IsNullOrEmpty(alias.GetString()) || alias.GetString() == localAlias))))
        {
            return;
        }

        throw new InvalidOperationException(
            "The Foundry Local package has incompatible alias metadata. " +
            "Automatic decision-model registration was skipped to avoid a catalog collision.");
    }
}
