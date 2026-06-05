using System.Text.Json;
using Betalgo.Ranul.OpenAI.ObjectModels.RequestModels;
using Betalgo.Ranul.OpenAI.ObjectModels.SharedModels;
using Microsoft.Extensions.AI;

namespace ElBruno.MAF.FoundryLocal;

public static class FoundryLocalToolMapper
{
    public static ToolMappingResult MapTools(IList<AITool>? tools)
    {
        if (tools is null || tools.Count == 0)
        {
            return new ToolMappingResult([], []);
        }

        List<ToolDefinition> mapped = [];
        List<string> unsupported = [];

        foreach (var tool in tools)
        {
            if (!TryMapTool(tool, out var mappedTool, out var reason))
            {
                unsupported.Add(reason);
                continue;
            }

            mapped.Add(mappedTool!);
        }

        return new ToolMappingResult(mapped, unsupported);
    }

    private static bool TryMapTool(AITool tool, out ToolDefinition? mapped, out string reason)
    {
        mapped = null;
        reason = string.Empty;

        if (tool is null)
        {
            reason = "NullTool";
            return false;
        }

        if (string.IsNullOrWhiteSpace(tool.Name))
        {
            reason = "ToolWithoutName";
            return false;
        }

        var schemaProperty = tool.GetType().GetProperty("JsonSchema");
        if (schemaProperty is null || schemaProperty.PropertyType != typeof(JsonElement))
        {
            reason = $"Tool '{tool.Name}' is not an AIFunction-created tool.";
            return false;
        }

        var schema = (JsonElement)schemaProperty.GetValue(tool)!;
        var function = new FunctionDefinition
        {
            Name = tool.Name,
            Description = tool.Description,
            Parameters = MapSchemaToPropertyDefinition(schema)
        };

        mapped = new ToolDefinition
        {
            Type = "function",
            Function = function
        };

        return true;
    }

    private static PropertyDefinition MapSchemaToPropertyDefinition(JsonElement schema)
    {
        if (schema.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidOperationException("Function schema must be a JSON object.");
        }

        var description = schema.TryGetProperty("description", out var descriptionElement) &&
                          descriptionElement.ValueKind == JsonValueKind.String
            ? descriptionElement.GetString()
            : null;

        var enumValues = TryGetEnumValues(schema);
        if (enumValues is not null)
        {
            return PropertyDefinition.DefineEnum(enumValues, description);
        }

        if (!schema.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("Function schema is missing a string 'type' property.");
        }

        var type = typeElement.GetString();
        return type switch
        {
            "string" => PropertyDefinition.DefineString(description),
            "integer" => PropertyDefinition.DefineInteger(description),
            "number" => PropertyDefinition.DefineNumber(description),
            "boolean" => PropertyDefinition.DefineBoolean(description),
            "null" => PropertyDefinition.DefineNull(description),
            "array" => MapArraySchema(schema),
            "object" => MapObjectSchema(schema, description),
            _ => throw new NotSupportedException($"Unsupported schema type '{type}'.")
        };
    }

    private static PropertyDefinition MapArraySchema(JsonElement schema)
    {
        var itemsDefinition = PropertyDefinition.DefineString(null);
        if (schema.TryGetProperty("items", out var itemsSchema))
        {
            itemsDefinition = MapSchemaToPropertyDefinition(itemsSchema);
        }

        return PropertyDefinition.DefineArray(itemsDefinition);
    }

    private static PropertyDefinition MapObjectSchema(JsonElement schema, string? description)
    {
        Dictionary<string, PropertyDefinition> properties = [];
        if (schema.TryGetProperty("properties", out var propertiesElement) &&
            propertiesElement.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in propertiesElement.EnumerateObject())
            {
                properties[property.Name] = MapSchemaToPropertyDefinition(property.Value);
            }
        }

        List<string> required = [];
        if (schema.TryGetProperty("required", out var requiredElement) &&
            requiredElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in requiredElement.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.String)
                {
                    required.Add(entry.GetString()!);
                }
            }
        }

        bool? additionalProperties = null;
        if (schema.TryGetProperty("additionalProperties", out var additionalPropertiesElement) &&
            (additionalPropertiesElement.ValueKind == JsonValueKind.True ||
             additionalPropertiesElement.ValueKind == JsonValueKind.False))
        {
            additionalProperties = additionalPropertiesElement.GetBoolean();
        }

        return PropertyDefinition.DefineObject(properties, required, additionalProperties, description, null);
    }

    private static List<string>? TryGetEnumValues(JsonElement schema)
    {
        if (!schema.TryGetProperty("enum", out var enumElement) || enumElement.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        List<string> values = [];
        foreach (var item in enumElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                throw new NotSupportedException("Only string enums are supported.");
            }

            values.Add(item.GetString()!);
        }

        return values;
    }
}

public sealed record ToolMappingResult(
    IReadOnlyList<ToolDefinition> Tools,
    IReadOnlyList<string> UnsupportedTools);
