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

    private FoundryLocalManager? _manager;
    private IModel? _model;
    private OpenAIChatClient? _chatClient;
    private OpenAIEmbeddingClient? _embeddingClient;
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
        await PrepareModelCoreAsync(modelAlias, cancellationToken);
    }

    public void SetModelAliasOverride(string modelAlias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(modelAlias);
        _modelAliasOverride = modelAlias;
    }

    public async Task<OpenAIChatClient> GetChatClientAsync(CancellationToken cancellationToken = default)
    {
        await PrepareModelCoreAsync(_modelAliasOverride ?? _options.ModelAlias, cancellationToken);
        return _chatClient!;
    }

    public async Task<OpenAIEmbeddingClient?> TryGetEmbeddingClientAsync(CancellationToken cancellationToken = default)
    {
        await PrepareModelCoreAsync(_modelAliasOverride ?? _options.ModelAlias, cancellationToken);

        if (_embeddingClient is not null)
        {
            return _embeddingClient;
        }

        try
        {
            _embeddingClient = await _model!.GetEmbeddingClientAsync(cancellationToken);
            return _embeddingClient;
        }
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
        if (_model is not null && _options.UnloadOnExit)
        {
            await _model.UnloadAsync(CancellationToken.None);
        }

        _gate.Dispose();
    }

    private async Task PrepareModelCoreAsync(string modelAlias, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await EnsureManagerAsync(cancellationToken);

            var catalog = await _manager!.GetCatalogAsync(cancellationToken);
            var model = await catalog.GetModelAsync(modelAlias, cancellationToken);
            if (model is null)
            {
                throw new InvalidOperationException($"Model alias '{modelAlias}' was not found in the Foundry Local catalog.");
            }

            var modelPath = await model.GetPathAsync(cancellationToken);
            var missingFromCache = string.IsNullOrWhiteSpace(modelPath);

            if (missingFromCache)
            {
                if (!_options.DownloadIfMissing)
                {
                    throw new InvalidOperationException(
                        $"Model '{modelAlias}' is not available in cache and DownloadIfMissing is disabled.");
                }

                _logger.LogInformation("Downloading model {ModelAlias} ...", modelAlias);
                await model.DownloadAsync(null, cancellationToken);
                _downloadedThisSession = true;
            }

            var start = DateTimeOffset.UtcNow;
            await model.LoadAsync(cancellationToken);
            _lastModelLoadDuration = DateTimeOffset.UtcNow - start;

            _model = model;
            _chatClient = await model.GetChatClientAsync(cancellationToken);
            _embeddingClient = null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task EnsureManagerAsync(CancellationToken cancellationToken)
    {
        if (_manager is not null)
        {
            return;
        }

        var config = new Configuration
        {
            AppName = "ElBruno.MAF.FoundryLocal"
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
