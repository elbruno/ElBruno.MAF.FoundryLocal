using System.Reflection;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace ElBruno.MAF.FoundryLocal;

public static class FoundryLocalOptionMapper
{
    public static FoundryLocalOptionMappingResult CreateOptionMapping(ChatOptions? options)
    {
        if (options is null)
        {
            return new FoundryLocalOptionMappingResult(null, null, null, [], [], []);
        }

        List<string> unsupported = [];
        var toolMapping = FoundryLocalToolMapper.MapTools(options.Tools);
        unsupported.AddRange(toolMapping.UnsupportedTools);

        if (options.ResponseFormat is not null)
        {
            unsupported.Add("ResponseFormat");
        }

        if (options.FrequencyPenalty is not null)
        {
            unsupported.Add("FrequencyPenalty");
        }

        if (options.PresencePenalty is not null)
        {
            unsupported.Add("PresencePenalty");
        }

        return new FoundryLocalOptionMappingResult(
            options.Temperature,
            options.MaxOutputTokens,
            options.TopP,
            options.StopSequences?.ToArray() ?? [],
            toolMapping.Tools,
            unsupported);
    }

    public static FoundryLocalOptionMappingResult Apply(OpenAIChatClient chatClient, ChatOptions? options, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(chatClient);
        ArgumentNullException.ThrowIfNull(logger);

        var mapping = CreateOptionMapping(options);

        if (mapping.UnsupportedOptions.Count > 0)
        {
            logger.LogWarning(
                "The following ChatOptions are not supported by the current Foundry Local adapter path: {UnsupportedOptions}",
                string.Join(", ", mapping.UnsupportedOptions));
        }

        var settings = chatClient.Settings;
        if (settings is null)
        {
            return mapping;
        }

        TrySet(settings, "Temperature", mapping.Temperature);
        TrySet(settings, "TopP", mapping.TopP);
        TrySet(settings, "MaxTokens", mapping.MaxOutputTokens);
        TrySet(settings, "MaxCompletionTokens", mapping.MaxOutputTokens);

        if (mapping.StopSequences.Count > 0)
        {
            TrySet(settings, "Stop", mapping.StopSequences);
            TrySet(settings, "StopSequences", mapping.StopSequences);
        }

        return mapping;
    }

    private static void TrySet(object target, string propertyName, object? value)
    {
        if (value is null)
        {
            return;
        }

        var property = target.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property is null || !property.CanWrite)
        {
            return;
        }

        object? convertedValue = value;

        if (property.PropertyType == typeof(float) && value is double d)
        {
            convertedValue = (float)d;
        }
        else if (property.PropertyType == typeof(float?) && value is double nullableDouble)
        {
            convertedValue = (float)nullableDouble;
        }
        else if (property.PropertyType == typeof(int?) && value is long longVal)
        {
            convertedValue = (int)longVal;
        }

        if (property.PropertyType.IsAssignableFrom(convertedValue.GetType()))
        {
            property.SetValue(target, convertedValue);
        }
    }
}

public sealed record FoundryLocalOptionMappingResult(
    float? Temperature,
    int? MaxOutputTokens,
    float? TopP,
    IReadOnlyList<string> StopSequences,
    IReadOnlyList<Betalgo.Ranul.OpenAI.ObjectModels.RequestModels.ToolDefinition> Tools,
    IReadOnlyList<string> UnsupportedOptions);
