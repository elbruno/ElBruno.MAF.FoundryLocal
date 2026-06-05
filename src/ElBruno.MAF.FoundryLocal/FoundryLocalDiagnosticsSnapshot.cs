namespace ElBruno.MAF.FoundryLocal;

public sealed record FoundryLocalDiagnosticsSnapshot(
    string ModelAlias,
    bool DownloadedThisSession,
    bool ModelLoaded,
    bool StreamingEnabled,
    bool UsingRestServer,
    IReadOnlyList<string> Warnings,
    TimeSpan? LastModelLoadDuration,
    TimeSpan? LastResponseDuration);
