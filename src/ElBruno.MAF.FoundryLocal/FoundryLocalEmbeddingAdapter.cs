using Microsoft.AI.Foundry.Local;
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

        using var lease = await _modelLifecycleService.AcquireModelAsync(cancellationToken);
        if (lease.Model.Info.Task is not "embeddings")
        {
            return FoundryLocalEmbeddingResult.Unsupported("The current Foundry Local model does not support embeddings.");
        }

        try
        {
            using var session = new EmbeddingsSession(lease.Model);
            using var request = new Request();
            foreach (var input in source)
            {
                request.AddItem(new TextItem(input));
            }

            using var response = await session.ProcessRequestAsync(request, cancellationToken);
            if (response.FinishReason == FinishReason.Error || response.ItemCount != source.Length)
            {
                const string reason = "Embedding generation did not return one vector per input.";
                _logger.LogWarning("{Reason}", reason);
                return FoundryLocalEmbeddingResult.Failure(reason);
            }

            List<IReadOnlyList<float>> embeddings = [];
            foreach (var item in response)
            {
                if (item is not TensorItem { ElementCount: > 0 } tensor)
                {
                    const string reason = "Embedding generation returned an invalid vector.";
                    _logger.LogWarning("{Reason}", reason);
                    return FoundryLocalEmbeddingResult.Failure(reason);
                }

                embeddings.Add(tensor.ToArray<float>());
            }
            return new FoundryLocalEmbeddingResult(true, true, embeddings.ToArray(), null);
        }
        catch (Exception ex) when (ex is InvalidOperationException or NotSupportedException)
        {
            _logger.LogWarning(ex, "Embedding generation is unavailable for the current Foundry Local model/runtime.");
            return FoundryLocalEmbeddingResult.Unsupported(ex.Message);
        }
        catch (FoundryLocalException ex)
        {
            _logger.LogWarning(ex, "Foundry Local embedding generation failed.");
            return FoundryLocalEmbeddingResult.Failure(ex.Message);
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
