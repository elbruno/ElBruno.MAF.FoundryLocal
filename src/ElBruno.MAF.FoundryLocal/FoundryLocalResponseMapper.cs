using System.Text.Json;
using Microsoft.Extensions.AI;

namespace ElBruno.MAF.FoundryLocal;

public static class FoundryLocalResponseMapper
{
    public static ChatResponse Map(object? completion)
    {
        var text = TryExtractText(completion) ?? string.Empty;
        var functionCalls = TryExtractFunctionCalls(completion);
        var message = new ChatMessage(ChatRole.Assistant, text)
        {
            RawRepresentation = completion
        };

        foreach (var functionCall in functionCalls)
        {
            message.Contents.Add(functionCall);
        }

        return new ChatResponse(message)
        {
            RawRepresentation = completion
        };
    }

    public static ChatResponseUpdate MapStreamingUpdate(object? streamingChunk)
    {
        var text = TryExtractStreamingText(streamingChunk) ?? string.Empty;

        return new ChatResponseUpdate(ChatRole.Assistant, text)
        {
            RawRepresentation = streamingChunk
        };
    }

    private static string? TryExtractText(object? completion)
    {
        if (completion is null)
        {
            return null;
        }

        if (!TryGetPropertyValue(completion, "Choices", out var choicesObj) || choicesObj is not System.Collections.IEnumerable choices)
        {
            return null;
        }

        foreach (var choice in choices)
        {
            if (choice is null || !TryGetPropertyValue(choice, "Message", out var message) || message is null)
            {
                continue;
            }

            if (TryGetPropertyValue(message, "ContentCalculated", out var contentCalculatedObj) &&
                contentCalculatedObj is string contentCalculated &&
                !string.IsNullOrWhiteSpace(contentCalculated))
            {
                return contentCalculated;
            }

            if (TryGetPropertyValue(message, "Content", out var contentObj) &&
                contentObj is string content &&
                !string.IsNullOrWhiteSpace(content))
            {
                return content;
            }
        }

        return null;
    }

    private static string? TryExtractStreamingText(object? chunk)
    {
        if (chunk is null)
        {
            return null;
        }

        if (!TryGetPropertyValue(chunk, "Choices", out var choicesObj) || choicesObj is not System.Collections.IEnumerable choices)
        {
            return null;
        }

        foreach (var choice in choices)
        {
            if (choice is null || !TryGetPropertyValue(choice, "Delta", out var delta) || delta is null)
            {
                continue;
            }

            if (TryGetPropertyValue(delta, "Content", out var contentObj) &&
                contentObj is string content &&
                !string.IsNullOrWhiteSpace(content))
            {
                return content;
            }
        }

        return null;
    }

    private static bool TryGetPropertyValue(object source, string propertyName, out object? value)
    {
        var property = source.GetType().GetProperty(propertyName);
        if (property is null)
        {
            value = null;
            return false;
        }

        value = property.GetValue(source);
        return true;
    }

    private static IReadOnlyList<FunctionCallContent> TryExtractFunctionCalls(object? completion)
    {
        if (completion is null ||
            !TryGetPropertyValue(completion, "Choices", out var choicesObj) ||
            choicesObj is not System.Collections.IEnumerable choices)
        {
            return [];
        }

        List<FunctionCallContent> functionCalls = [];
        foreach (var choice in choices)
        {
            if (choice is null ||
                !TryGetPropertyValue(choice, "Message", out var messageObj) ||
                messageObj is null ||
                !TryGetPropertyValue(messageObj, "ToolCalls", out var toolCallsObj) ||
                toolCallsObj is not System.Collections.IEnumerable toolCalls)
            {
                continue;
            }

            foreach (var toolCall in toolCalls)
            {
                if (toolCall is null ||
                    !TryGetPropertyValue(toolCall, "Type", out var typeObj) ||
                    typeObj is not string type ||
                    !string.Equals(type, "function", StringComparison.OrdinalIgnoreCase) ||
                    !TryGetPropertyValue(toolCall, "Id", out var idObj) ||
                    idObj is not string callId ||
                    !TryGetPropertyValue(toolCall, "FunctionCall", out var functionObj) ||
                    functionObj is null ||
                    !TryGetPropertyValue(functionObj, "Name", out var nameObj) ||
                    nameObj is not string functionName)
                {
                    continue;
                }

                var arguments = TryGetPropertyValue(functionObj, "Arguments", out var argumentsObj) && argumentsObj is string argumentsJson
                    ? ParseArguments(argumentsJson)
                    : new Dictionary<string, object?>();

                functionCalls.Add(new FunctionCallContent(callId, functionName, arguments)
                {
                    RawRepresentation = toolCall
                });
            }
        }

        return functionCalls;
    }

    private static Dictionary<string, object?> ParseArguments(string argumentsJson)
    {
        if (string.IsNullOrWhiteSpace(argumentsJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, object?> { ["_raw"] = argumentsJson };
            }

            Dictionary<string, object?> values = [];
            foreach (var property in document.RootElement.EnumerateObject())
            {
                values[property.Name] = ToValue(property.Value);
            }

            return values;
        }
        catch (JsonException)
        {
            return new Dictionary<string, object?> { ["_raw"] = argumentsJson };
        }
    }

    private static object? ToValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Null => null,
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.Number when element.TryGetInt64(out var intValue) => intValue,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Array => element.EnumerateArray().Select(ToValue).ToArray(),
            JsonValueKind.Object => element.EnumerateObject().ToDictionary(p => p.Name, p => ToValue(p.Value)),
            _ => element.GetRawText()
        };
    }
}
