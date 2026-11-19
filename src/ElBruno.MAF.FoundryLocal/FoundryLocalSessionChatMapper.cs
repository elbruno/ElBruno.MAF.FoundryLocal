using System.Text.Json;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ElBruno.MAF.FoundryLocal;

internal static class FoundryLocalSessionChatMapper
{
    internal static void AddMessages(Request request, IEnumerable<ChatMessage> messages)
    {
        foreach (var message in messages)
        {
            var role = message.Role.Value?.ToLowerInvariant() switch
            {
                "system" => MessageRole.System,
                "assistant" => MessageRole.Assistant,
                "tool" => MessageRole.Tool,
                _ => MessageRole.User
            };

            if (!string.IsNullOrEmpty(message.Text) &&
                (role != MessageRole.Tool || !message.Contents.OfType<FunctionResultContent>().Any()))
            {
                request.AddItem(new MessageItem(role, message.Text, message.AuthorName));
            }

            foreach (var content in message.Contents)
            {
                if (content is FunctionCallContent call)
                {
                    request.AddItem(new ToolCallItem(call.CallId, call.Name,
                        call.Arguments is null ? "{}" : JsonSerializer.Serialize(call.Arguments)));
                }
                else if (content is FunctionResultContent result)
                {
                    request.AddItem(new ToolResultItem(result.CallId, result.Result switch
                    {
                        null => string.Empty,
                        string text => text,
                        _ => JsonSerializer.Serialize(result.Result)
                    }));
                }
            }
        }
    }

    internal static RequestOptions MapOptions(ChatOptions? options) => new()
    {
        ToolChoice = options?.ToolMode switch
        {
            AutoChatToolMode => ToolChoice.Auto,
            NoneChatToolMode => ToolChoice.None,
            RequiredChatToolMode { RequiredFunctionName: null } => ToolChoice.Required,
            RequiredChatToolMode required => throw new NotSupportedException(
                $"Foundry Local native sessions cannot require the named tool '{required.RequiredFunctionName}'. Use RequireAny or omit the requirement."),
            _ => null
        },
        Search = new SearchOptions
        {
            Temperature = options?.Temperature,
            TopP = options?.TopP,
            MaxOutputTokens = options?.MaxOutputTokens
        }
    };

    internal static void Configure(ChatSession session, Request request, RequestOptions nativeOptions, ChatOptions? options, ILogger logger)
    {
        if (options is null)
        {
            return;
        }

        request.SetOptions(nativeOptions);
        List<string> unsupported = [];
        if (options.ResponseFormat is not null) unsupported.Add(nameof(options.ResponseFormat));
        if (options.FrequencyPenalty is not null) unsupported.Add(nameof(options.FrequencyPenalty));
        if (options.PresencePenalty is not null) unsupported.Add(nameof(options.PresencePenalty));

        foreach (var tool in options.Tools ?? [])
        {
            if (tool is AIFunctionDeclaration function && !string.IsNullOrWhiteSpace(function.Name))
            {
                session.AddToolDefinition(function.Name, function.Description ?? string.Empty,
                    function.JsonSchema.GetRawText());
            }
            else
            {
                unsupported.Add(tool is null ? "NullTool" : $"Tool '{tool.Name}'");
            }
        }

        if (unsupported.Count > 0)
        {
            logger.LogWarning("The following ChatOptions are not supported by the current Foundry Local adapter path: {UnsupportedOptions}",
                string.Join(", ", unsupported));
        }
    }

    internal static IEnumerable<AIContent> MapItem(Item item)
    {
        switch (item)
        {
            case TextItem { Type: TextItemType.Default } text:
                yield return new TextContent(text.Text);
                break;
            case ToolCallItem call:
                yield return new FunctionCallContent(call.CallId, call.Name,
                    FoundryLocalResponseMapper.ParseArguments(call.Arguments));
                break;
            case MessageItem message:
                foreach (var part in message.Parts)
                {
                    foreach (var content in MapItem(part))
                    {
                        yield return content;
                    }
                }
                break;
        }
    }

    internal static ChatFinishReason? MapFinishReason(FinishReason reason) => reason switch
    {
        FinishReason.Stop => ChatFinishReason.Stop,
        FinishReason.Length => ChatFinishReason.Length,
        FinishReason.ToolCalls => ChatFinishReason.ToolCalls,
        _ => null
    };

    internal static UsageDetails MapUsage(TokenUsage usage) => new()
    {
        InputTokenCount = usage.PromptTokens,
        OutputTokenCount = usage.CompletionTokens,
        TotalTokenCount = usage.TotalTokens
    };
}

// SDK 2.4.1 has no custom stop-sequence setting. Hold back any suffix that
// could start a stop sequence so a match can span streamed tokens.
internal sealed class FoundryLocalStopSequenceFilter(IEnumerable<string>? sequences)
{
    private readonly string[] _sequences = sequences?.Where(static value => !string.IsNullOrEmpty(value)).ToArray() ?? [];
    private string _pending = string.Empty;

    internal bool Stopped { get; private set; }

    internal string Append(string text)
    {
        if (Stopped || _sequences.Length == 0)
        {
            return Stopped ? string.Empty : text;
        }

        var buffer = _pending + text;
        var firstMatch = buffer.Length;
        var firstEnd = buffer.Length;
        var suffix = 0;
        foreach (var sequence in _sequences)
        {
            var index = buffer.IndexOf(sequence, StringComparison.Ordinal);
            if (index >= 0)
            {
                var end = index + sequence.Length;
                if (!Stopped || end < firstEnd || (end == firstEnd && index < firstMatch))
                {
                    firstMatch = index;
                    firstEnd = end;
                }
                Stopped = true;
            }
            else
            {
                for (var length = Math.Min(sequence.Length - 1, buffer.Length); length > suffix; length--)
                {
                    if (buffer.AsSpan().EndsWith(sequence.AsSpan(0, length), StringComparison.Ordinal))
                    {
                        suffix = length;
                        break;
                    }
                }
            }
        }

        var visibleLength = Stopped ? firstMatch : buffer.Length - suffix;
        _pending = Stopped ? string.Empty : buffer[visibleLength..];
        return buffer[..visibleLength];
    }

    internal string Complete()
    {
        var result = _pending;
        _pending = string.Empty;
        return result;
    }
}
