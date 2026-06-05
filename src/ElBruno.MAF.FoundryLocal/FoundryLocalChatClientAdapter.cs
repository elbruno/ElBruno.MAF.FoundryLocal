using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ElBruno.MAF.FoundryLocal;

public sealed class FoundryLocalChatClientAdapter(
    FoundryLocalModelLifecycleService modelLifecycleService,
    ILogger<FoundryLocalChatClientAdapter> logger) : IChatClient
{
    private readonly FoundryLocalModelLifecycleService _modelLifecycleService = modelLifecycleService;
    private readonly ILogger<FoundryLocalChatClientAdapter> _logger = logger;

    public async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var client = await _modelLifecycleService.GetChatClientAsync(cancellationToken);
        var optionMapping = FoundryLocalOptionMapper.Apply(client, options, _logger);
        var mappedMessages = FoundryLocalMessageMapper.Map(messages);

        var started = DateTimeOffset.UtcNow;
        var completion = optionMapping.Tools.Count > 0
            ? await client.CompleteChatAsync(mappedMessages, optionMapping.Tools, cancellationToken)
            : await client.CompleteChatAsync(mappedMessages, cancellationToken);
        _modelLifecycleService.SetLastResponseDuration(DateTimeOffset.UtcNow - started);

        return FoundryLocalResponseMapper.Map(completion);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);

        var client = await _modelLifecycleService.GetChatClientAsync(cancellationToken);
        var optionMapping = FoundryLocalOptionMapper.Apply(client, options, _logger);
        var mappedMessages = FoundryLocalMessageMapper.Map(messages);

        var started = DateTimeOffset.UtcNow;
        var streamingResponse = optionMapping.Tools.Count > 0
            ? client.CompleteChatStreamingAsync(mappedMessages, optionMapping.Tools, cancellationToken)
            : client.CompleteChatStreamingAsync(mappedMessages, cancellationToken);

        await foreach (var update in streamingResponse)
        {
            yield return FoundryLocalResponseMapper.MapStreamingUpdate(update);
        }

        _modelLifecycleService.SetLastResponseDuration(DateTimeOffset.UtcNow - started);
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceType == typeof(FoundryLocalChatClientAdapter) ? this : null;
    }

    public void Dispose()
    {
    }
}
