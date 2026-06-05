namespace ElBruno.MAF.FoundryLocal;

public sealed class ChatRuntimeOptions
{
    public double Temperature { get; set; } = 0.7;

    public int MaxOutputTokens { get; set; } = 512;

    public bool Streaming { get; set; } = true;
}
