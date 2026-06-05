namespace ElBruno.MAF.FoundryLocal;

public sealed class LocalAgentOptions
{
    public string Name { get; set; } = "LocalFoundryAgent";

    public string Instructions { get; set; } = "You are a helpful local AI assistant. Keep answers concise.";
}
