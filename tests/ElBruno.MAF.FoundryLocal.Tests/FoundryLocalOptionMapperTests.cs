using ElBruno.MAF.FoundryLocal;
using Microsoft.Extensions.AI;

namespace ElBruno.MAF.FoundryLocal.Tests;

public class FoundryLocalOptionMapperTests
{
    [Fact]
    public void CreateOptionMapping_MapsCoreValues()
    {
        var options = new ChatOptions
        {
            Temperature = 0.3f,
            MaxOutputTokens = 256,
            TopP = 0.8f,
            StopSequences = ["END"]
        };

        var result = FoundryLocalOptionMapper.CreateOptionMapping(options);

        Assert.Equal(0.3f, result.Temperature);
        Assert.Equal(256, result.MaxOutputTokens);
        Assert.Equal(0.8f, result.TopP);
        Assert.Single(result.StopSequences);
        Assert.Equal("END", result.StopSequences[0]);
    }

    [Fact]
    public void CreateOptionMapping_TracksUnsupportedOptions()
    {
        var options = new ChatOptions
        {
            Tools = [AIFunctionFactory.Create(() => "ok", "demo_tool")],
            PresencePenalty = 0.4f,
            FrequencyPenalty = 0.2f
        };

        var result = FoundryLocalOptionMapper.CreateOptionMapping(options);

        var tool = Assert.Single(result.Tools);
        Assert.Equal("demo_tool", tool.Function!.Name);
        Assert.DoesNotContain(result.UnsupportedOptions, value => value.Contains("Tool", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("PresencePenalty", result.UnsupportedOptions);
        Assert.Contains("FrequencyPenalty", result.UnsupportedOptions);
    }
}
