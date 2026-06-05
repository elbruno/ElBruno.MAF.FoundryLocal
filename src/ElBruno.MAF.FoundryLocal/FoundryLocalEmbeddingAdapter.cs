using Microsoft.Extensions.Logging;

namespace ElBruno.MAF.FoundryLocal;

public sealed class FoundryLocalEmbeddingAdapter(
    FoundryLocalModelLifecycleService modelLifecycleService,
    ILogger<FoundryLocalEmbeddingAdapter> logger)
{
    private readonly FoundryLocalModelLifecycleService _modelLifecycleService = modelLifecycleService;
    private readonly ILogger<FoundryLocalEmbeddingAdapter> _logger = logger;

    public async Task<FoundryLocalEmbeddingResult> TryGenerateEmbeddingsAsync(
        IEnumerable<string> inputs,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(inputs);

        var source = inputs.Where(static value => !string.IsNullOrWhiteSpace(value)).ToArray();
        if (source.Length == 0)
        {
            return FoundryLocalEmbeddingResult.Failure("At least one non-empty input is required.");
        }

        var embeddingClient = await _modelLifecycleService.TryGetEmbeddingClientAsync(cancellationToken);
        if (embeddingClient is null)
        {
            return FoundryLocalEmbeddingResult.Unsupported("Embedding client is not available for the current Foundry Local runtime/model.");
        }

        try
        {
            var response = await embeddingClient.GenerateEmbeddingsAsync(source, cancellationToken);
            if (!response.Successful || response.Data is null || response.Data.Count == 0)
            {
                var errorMessage = response.Error?.Message ?? "Embedding generation returned an empty response.";
                _logger.LogWarning("Embedding generation failed. Reason: {Reason}", errorMessage);
                return FoundryLocalEmbeddingResult.Failure(errorMessage);
            }

            var embeddings = response.Data
                .OrderBy(static item => item.Index ?? int.MaxValue)
                .Select(static item => (IReadOnlyList<float>)item.Embedding.Select(static value => (float)value).ToArray())
                .ToArray();

            return new FoundryLocalEmbeddingResult(true, true, embeddings, null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Embedding generation is unavailable for the current Foundry Local model/runtime.");
            return FoundryLocalEmbeddingResult.Unsupported(ex.Message);
        }
    }
}

public sealed record FoundryLocalEmbeddingResult(
    bool Success,
    bool IsSupported,
    IReadOnlyList<IReadOnlyList<float>> Embeddings,
    string? FailureReason)
{
    public static FoundryLocalEmbeddingResult Unsupported(string reason)
        => new(false, false, [], reason);

    public static FoundryLocalEmbeddingResult Failure(string reason)
        => new(false, true, [], reason);
}
