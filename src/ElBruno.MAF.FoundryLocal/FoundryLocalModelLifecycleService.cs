using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ElBruno.MAF.FoundryLocal;

public sealed class FoundryLocalModelLifecycleService : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly FoundryLocalOptions _options;
    private readonly ChatRuntimeOptions _chatOptions;
    private readonly ILogger<FoundryLocalModelLifecycleService> _logger;
    private readonly List<string> _warnings = [];
    private readonly object _leaseLock = new();

    private FoundryLocalManager? _manager;
    private IModel? _model;
    private readonly Dictionary<string, IModel> _loadedModels = new(StringComparer.Ordinal);
    private TaskCompletionSource? _leasesDrained;
    private int _activeLeases;
    private bool _disposed;
    private string? _modelAliasOverride;
    private bool _downloadedThisSession;
    private TimeSpan? _lastModelLoadDuration;
    private TimeSpan? _lastResponseDuration;

    public FoundryLocalModelLifecycleService(
        IOptions<FoundryLocalOptions> options,
        IOptions<ChatRuntimeOptions> chatOptions,
        ILogger<FoundryLocalModelLifecycleService> logger)
    {
        _options = options.Value;
        _chatOptions = chatOptions.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<string>> ListModelsAsync(CancellationToken cancellationToken = default)
    {
        await EnsureManagerAsync(cancellationToken);
        var catalog = await _manager!.GetCatalogAsync(cancellationToken);
        var models = await catalog.ListModelsAsync(cancellationToken);
        return models.Select(m => m.Alias).OrderBy(v => v).ToArray();
    }

    public async Task DownloadModelAsync(string modelAlias, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelAlias);
        using var lease = await AcquireModelAsync(modelAlias, cancellationToken);
    }

    public void SetModelAliasOverride(string modelAlias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelAlias);
        _modelAliasOverride = modelAlias;
    }

#pragma warning disable CS0618 // Keep the 0.4.0 public signatures while SDK 2.4.1 still provides them.
    public async Task<OpenAIChatClient> GetChatClientAsync(CancellationToken cancellationToken = default)
    {
        using var lease = await AcquireModelAsync(cancellationToken);
        return await lease.Model.GetChatClientAsync(cancellationToken);
    }

    public async Task<OpenAIEmbeddingClient?> TryGetEmbeddingClientAsync(CancellationToken cancellationToken = default)
    {
        using var lease = await AcquireModelAsync(cancellationToken);

        try
        {
            return await lease.Model.GetEmbeddingClientAsync(cancellationToken);
        }
#pragma warning restore CS0618
        catch (MissingMethodException ex)
        {
            AddWarning("Embedding client is not available in the current Foundry Local SDK runtime.");
            _logger.LogWarning(ex, "Embedding client path is unavailable in this SDK runtime.");
            return null;
        }
        catch (NotSupportedException ex)
        {
            AddWarning("Embedding generation is not supported by the current model/runtime.");
            _logger.LogWarning(ex, "Embedding generation is not supported for the current model/runtime.");
            return null;
        }
    }

    public Task<DecisionResult> DecideAsync(
        DecisionRequest request,
        CancellationToken cancellationToken = default)
        => DecideAsync(_options.DecisionModelAlias, request, cancellationToken);

    public async Task<DecisionResult> DecideAsync(
        string modelAlias,
        DecisionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelAlias);
        ArgumentNullException.ThrowIfNull(request);

        await _gate.WaitAsync(cancellationToken);
        IModel? decisionModel = null;
        var modelLoaded = false;
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await EnsureManagerAsync(cancellationToken);

            var publicCatalog = await _manager!.GetCatalogAsync(CatalogType.Public, cancellationToken);
            var localCatalog = await _manager.GetCatalogAsync(CatalogType.Local, cancellationToken);
            decisionModel = await FoundryLocalDecisionModelResolver.ResolveAsync(
                publicCatalog, localCatalog, modelAlias, _options.DownloadIfMissing,
                () => _downloadedThisSession = true, cancellationToken);

            await decisionModel.LoadAsync(cancellationToken);
            modelLoaded = true;

            using var session = new DecisionSession(decisionModel);
            return await session.DecideAsync(request, cancellationToken);
        }
        finally
        {
            try
            {
                if (modelLoaded)
                {
                    await decisionModel!.UnloadAsync(CancellationToken.None);
                }
            }
            finally
            {
                _gate.Release();
            }
        }
    }

    public void SetLastResponseDuration(TimeSpan elapsed) => _lastResponseDuration = elapsed;

    public FoundryLocalDiagnosticsSnapshot GetDiagnosticsSnapshot()
        => new(
            ModelAlias: _model?.Alias ?? _options.ModelAlias,
            DownloadedThisSession: _downloadedThisSession,
            ModelLoaded: _model is not null,
            StreamingEnabled: _chatOptions.Streaming,
            UsingRestServer: false,
            Warnings: _warnings.ToArray(),
            LastModelLoadDuration: _lastModelLoadDuration,
            LastResponseDuration: _lastResponseDuration);

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Task? pendingLeases;
            lock (_leaseLock)
            {
                pendingLeases = _leasesDrained?.Task;
            }

            if (pendingLeases is not null)
            {
                await pendingLeases;
            }

            if (_options.UnloadOnExit)
            {
                foreach (var model in _loadedModels.Values)
                {
                    await model.UnloadAsync(CancellationToken.None);
                }
            }
        }
        finally
        {
            _gate.Release();
        }
    }

    internal Task<ModelLease> AcquireModelAsync(CancellationToken cancellationToken)
        => AcquireModelAsync(_modelAliasOverride ?? _options.ModelAlias, cancellationToken);

    private async Task<ModelLease> AcquireModelAsync(string modelAlias, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await EnsureManagerAsync(cancellationToken);

            var catalog = await _manager!.GetCatalogAsync(cancellationToken);
            var model = await catalog.GetModelAsync(modelAlias, cancellationToken);
            if (model is null)
            {
                throw new InvalidOperationException($"Model alias '{modelAlias}' was not found in the Foundry Local catalog.");
            }

            var preparedModel = await PrepareModelOrVariantAsync(modelAlias, model, cancellationToken);

            _model = preparedModel;
            _loadedModels[preparedModel.Id] = preparedModel;
            lock (_leaseLock)
            {
                if (_activeLeases++ == 0)
                {
                    _leasesDrained = new(TaskCreationOptions.RunContinuationsAsynchronously);
                }
            }

            return new ModelLease(preparedModel, ReleaseLease);
        }
        finally
        {
            _gate.Release();
        }
    }

    private void ReleaseLease()
    {
        lock (_leaseLock)
        {
            if (--_activeLeases == 0)
            {
                _leasesDrained?.SetResult();
                _leasesDrained = null;
            }
        }
    }

    // Disposal waits for native sessions, without holding the preparation gate during inference.
    internal sealed class ModelLease(IModel model, Action release) : IDisposable
    {
        private Action? _release = release;
        internal IModel Model { get; } = model;

        public void Dispose() => Interlocked.Exchange(ref _release, null)?.Invoke();
    }

    private async Task<IModel> PrepareModelOrVariantAsync(
        string requestedAlias,
        IModel model,
        CancellationToken cancellationToken)
    {
        try
        {
            await EnsureModelReadyAsync(model, requestedAlias, cancellationToken);
            return model;
        }
        catch (FoundryLocalException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed preparing default model selection for alias {ModelAlias}. Trying variants.",
                requestedAlias);
        }

        if (model.Variants is null || model.Variants.Count == 0)
        {
            throw new InvalidOperationException(
                $"Model '{requestedAlias}' failed to initialize and does not expose fallback variants.");
        }

        foreach (var variant in model.Variants)
        {
            try
            {
                _logger.LogInformation(
                    "Trying model variant {VariantAlias} for alias {ModelAlias}.",
                    variant.Alias,
                    requestedAlias);

                await EnsureModelReadyAsync(variant, variant.Alias, cancellationToken);
                return variant;
            }
            catch (FoundryLocalException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Variant {VariantAlias} failed during initialization for alias {ModelAlias}.",
                    variant.Alias,
                    requestedAlias);
            }
        }

        throw new InvalidOperationException(
            $"No usable variant was found for model alias '{requestedAlias}'.");
    }

    private async Task EnsureModelReadyAsync(
        IModel candidate,
        string candidateAlias,
        CancellationToken cancellationToken)
    {
        bool missingFromCache;
        try
        {
            var modelPath = await candidate.GetPathAsync(cancellationToken);
            missingFromCache = string.IsNullOrWhiteSpace(modelPath);
        }
        catch (FoundryLocalException ex) when (IsModelMissingFromCache(ex))
        {
            // Foundry Local can throw instead of returning empty path when a model
            // has not been downloaded yet. Treat this as a cache miss.
            missingFromCache = true;
            _logger.LogInformation(
                "Model path lookup reported cache miss for {ModelAlias}. Continuing with download flow.",
                candidateAlias);
        }

        if (missingFromCache)
        {
            if (!_options.DownloadIfMissing)
            {
                throw new InvalidOperationException(
                    $"Model '{candidateAlias}' is not available in cache and DownloadIfMissing is disabled.");
            }

            _logger.LogInformation("Downloading model {ModelAlias} ...", candidateAlias);
            await candidate.DownloadAsync(null, cancellationToken);
            _downloadedThisSession = true;
        }

        var start = DateTimeOffset.UtcNow;
        await candidate.LoadAsync(cancellationToken);
        _lastModelLoadDuration = DateTimeOffset.UtcNow - start;
    }

    private static bool IsModelMissingFromCache(FoundryLocalException ex)
    {
        var message = ex.Message;
        return message.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("downloaded", StringComparison.OrdinalIgnoreCase);
    }

    private async Task EnsureManagerAsync(CancellationToken cancellationToken)
    {
        if (_manager is not null)
        {
            return;
        }

        var config = new Configuration
        {
            AppName = "ElBruno_MAF_FoundryLocal",
            ModelCacheDir = _options.ModelCacheDir
        };

        await FoundryLocalManager.CreateAsync(
            config,
            NullLogger.Instance,
            cancellationToken);

        _manager = FoundryLocalManager.Instance;
    }

    private void AddWarning(string warning)
    {
        if (_warnings.Contains(warning, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        _warnings.Add(warning);
    }
}
