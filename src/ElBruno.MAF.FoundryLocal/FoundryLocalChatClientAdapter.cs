using Microsoft.AI.Foundry.Local;
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
        var nativeOptions = FoundryLocalSessionChatMapper.MapOptions(options);

        using var lease = await _modelLifecycleService.AcquireModelAsync(cancellationToken);
        using var session = new ChatSession(lease.Model);
        using var request = new Request();
        FoundryLocalSessionChatMapper.AddMessages(request, messages);
        FoundryLocalSessionChatMapper.Configure(session, request, nativeOptions, options, _logger);

        var started = DateTimeOffset.UtcNow;
        using var completion = await session.ProcessRequestAsync(request, cancellationToken);
        EnsureSucceeded(completion);
        _modelLifecycleService.SetLastResponseDuration(DateTimeOffset.UtcNow - started);

        var filter = new FoundryLocalStopSequenceFilter(options?.StopSequences);
        List<AIContent> contents = [];
        foreach (var item in completion)
        {
            foreach (var content in FoundryLocalSessionChatMapper.MapItem(item))
            {
                if (content is TextContent text)
                {
                    var visible = filter.Append(text.Text);
                    if (visible.Length > 0) contents.Add(new TextContent(visible));
                }
                else if (!filter.Stopped)
                {
                    contents.Add(content);
                }
            }
        }

        var tail = filter.Complete();
        if (tail.Length > 0) contents.Add(new TextContent(tail));
        return new ChatResponse(new ChatMessage(ChatRole.Assistant, contents))
        {
            Usage = FoundryLocalSessionChatMapper.MapUsage(completion.GetUsage()),
            FinishReason = filter.Stopped ? ChatFinishReason.Stop : FoundryLocalSessionChatMapper.MapFinishReason(completion.FinishReason)
        };
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(messages);
        var nativeOptions = FoundryLocalSessionChatMapper.MapOptions(options);

        using var lease = await _modelLifecycleService.AcquireModelAsync(cancellationToken);
        using var session = new ChatSession(lease.Model);
        using var request = new Request();
        FoundryLocalSessionChatMapper.AddMessages(request, messages);
        FoundryLocalSessionChatMapper.Configure(session, request, nativeOptions, options, _logger);
        session.SetStreaming(true);

        var started = DateTimeOffset.UtcNow;
        await using var stream = session.ProcessStreamingRequestAsync(request, cancellationToken);
        var filter = new FoundryLocalStopSequenceFilter(options?.StopSequences);
        var sawText = false;
        Dictionary<string, FunctionCallContent> calls = [];

        await foreach (var item in stream.WithCancellation(cancellationToken))
        {
            List<AIContent> contents;
            using (item)
            {
                contents = FoundryLocalSessionChatMapper.MapItem(item).ToList();
            }

            foreach (var content in contents)
            {
                if (content is TextContent text)
                {
                    sawText |= text.Text.Length > 0;
                    var visible = filter.Append(text.Text);
                    if (visible.Length > 0) yield return new ChatResponseUpdate(ChatRole.Assistant, visible);
                }
                else if (content is FunctionCallContent call)
                {
                    calls[call.CallId] = call;
                }
            }
        }

        using var completion = await stream.FinalResponse;
        EnsureSucceeded(completion);
        foreach (var item in completion)
        {
            foreach (var content in FoundryLocalSessionChatMapper.MapItem(item))
            {
                if (content is TextContent text && !sawText)
                {
                    var visible = filter.Append(text.Text);
                    if (visible.Length > 0) yield return new ChatResponseUpdate(ChatRole.Assistant, visible);
                }
                else if (content is FunctionCallContent call)
                {
                    calls[call.CallId] = call;
                }
            }
        }

        var tail = filter.Complete();
        if (tail.Length > 0) yield return new ChatResponseUpdate(ChatRole.Assistant, tail);
        _modelLifecycleService.SetLastResponseDuration(DateTimeOffset.UtcNow - started);
        List<AIContent> terminalContents = filter.Stopped ? [] : [.. calls.Values];
        terminalContents.Add(new UsageContent(FoundryLocalSessionChatMapper.MapUsage(completion.GetUsage())));
        yield return new ChatResponseUpdate
        {
            Role = ChatRole.Assistant,
            Contents = terminalContents,
            FinishReason = filter.Stopped ? ChatFinishReason.Stop : FoundryLocalSessionChatMapper.MapFinishReason(completion.FinishReason)
        };
    }

    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceType == typeof(FoundryLocalChatClientAdapter) ? this : null;
    }

    public void Dispose()
    {
    }

    private static void EnsureSucceeded(Response response)
    {
        if (response.FinishReason == FinishReason.Error)
        {
            throw new FoundryLocalException("The native Foundry Local chat request failed.");
        }
    }
}
