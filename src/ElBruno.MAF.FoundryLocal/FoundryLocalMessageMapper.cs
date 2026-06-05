using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace ElBruno.MAF.FoundryLocal;

public static class FoundryLocalMessageMapper
{
    public static IReadOnlyList<Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ChatMessage> Map(IEnumerable<Microsoft.Extensions.AI.ChatMessage> messages)
    {
        ArgumentNullException.ThrowIfNull(messages);

        return messages
            .Select(MapSingle)
            .ToArray();
    }

    private static Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ChatMessage MapSingle(Microsoft.Extensions.AI.ChatMessage message)
    {
        var mapped = new Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ChatMessage(
            role: MapRole(message.Role),
            content: message.Text ?? string.Empty,
            name: message.AuthorName);

        if (TryGetFunctionResult(message, out var result))
        {
            mapped.ToolCallId = result.CallId;
            mapped.Content = SerializeValue(result.Result);
        }

        var functionCalls = message.Contents.OfType<FunctionCallContent>().ToArray();
        if (functionCalls.Length > 0)
        {
            mapped.ToolCalls = functionCalls.Select(fc => new ToolCall
            {
                Id = fc.CallId,
                Type = "function",
                FunctionCall = new FunctionCall
                {
                    Name = fc.Name,
                    Arguments = JsonSerializer.Serialize(fc.Arguments)
                }
            }).ToArray();
        }

        return mapped;
    }

    private static string MapRole(ChatRole role) =>
        role.Value?.ToLowerInvariant() switch
        {
            "system" => "system",
            "assistant" => "assistant",
            "tool" => "tool",
            _ => "user"
        };

    private static bool TryGetFunctionResult(Microsoft.Extensions.AI.ChatMessage message, out FunctionResultContent result)
    {
        result = message.Contents.OfType<FunctionResultContent>().FirstOrDefault()!;
        return result is not null;
    }

    private static string SerializeValue(object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is string text)
        {
            return text;
        }

        return JsonSerializer.Serialize(value);
    }
}
