namespace ElBruno.MAF.FoundryLocal;

public sealed class FoundryLocalOptions
{
    public string ModelAlias { get; set; } = "qwen2.5-0.5b";

    public bool DownloadIfMissing { get; set; } = true;

    public bool UnloadOnExit { get; set; } = true;
}
