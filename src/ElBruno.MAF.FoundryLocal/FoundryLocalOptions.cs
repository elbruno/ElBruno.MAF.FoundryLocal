namespace ElBruno.MAF.FoundryLocal;

public sealed class FoundryLocalOptions
{
    public string ModelAlias { get; set; } = "qwen2.5-0.5b";

    public string DecisionModelAlias { get; set; } = "kev-0.8b";

    public string? ModelCacheDir { get; set; }

    public bool DownloadIfMissing { get; set; } = true;

    public bool UnloadOnExit { get; set; } = true;
}
