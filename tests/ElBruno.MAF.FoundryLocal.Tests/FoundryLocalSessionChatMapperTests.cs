using ElBruno.MAF.FoundryLocal;
using Microsoft.AI.Foundry.Local;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ElBruno.MAF.FoundryLocal.Tests;

public class FoundryLocalSessionChatMapperTests
{
    [Fact]
    public void MapOptions_UsesTypedSearchOptionsWithoutCarryingOverPriorValues()
    {
        var configured = FoundryLocalSessionChatMapper.MapOptions(new ChatOptions
        {
            Temperature = 0,
            TopP = 0.75f,
            MaxOutputTokens = 40
        });
        var defaults = FoundryLocalSessionChatMapper.MapOptions(null);

        Assert.Equal(0f, configured.Search!.Temperature);
        Assert.Equal(0.75f, configured.Search.TopP);
        Assert.Equal(40, configured.Search.MaxOutputTokens);
        Assert.Null(defaults.Search!.Temperature);
        Assert.Null(defaults.Search.TopP);
        Assert.Null(defaults.Search.MaxOutputTokens);
    }

    [Fact]
    public void MapOptions_UsesNativeToolChoiceOnlyForSupportedModes()
    {
        Assert.Equal(ToolChoice.Auto, FoundryLocalSessionChatMapper.MapOptions(new() { ToolMode = ChatToolMode.Auto }).ToolChoice);
        Assert.Equal(ToolChoice.None, FoundryLocalSessionChatMapper.MapOptions(new() { ToolMode = ChatToolMode.None }).ToolChoice);
        Assert.Equal(ToolChoice.Required, FoundryLocalSessionChatMapper.MapOptions(new() { ToolMode = ChatToolMode.RequireAny }).ToolChoice);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Chat_RejectsNamedToolBeforeModelPreparation(bool streaming)
    {
        await using var lifecycle = new FoundryLocalModelLifecycleService(
            Options.Create(new FoundryLocalOptions()), Options.Create(new ChatRuntimeOptions()),
            NullLogger<FoundryLocalModelLifecycleService>.Instance);
        await lifecycle.DisposeAsync();
        using var adapter = new FoundryLocalChatClientAdapter(lifecycle, NullLogger<FoundryLocalChatClientAdapter>.Instance);
        ChatMessage[] messages = [new(ChatRole.User, "Use the tool.")];
        var options = new ChatOptions { ToolMode = ChatToolMode.RequireSpecific("lookup") };

        var error = await Assert.ThrowsAsync<NotSupportedException>(async () =>
        {
            if (streaming)
            {
                await using var stream = adapter.GetStreamingResponseAsync(messages, options).GetAsyncEnumerator();
                await stream.MoveNextAsync();
            }
            else
            {
                await adapter.GetResponseAsync(messages, options);
            }
        });

        Assert.Contains("'lookup'", error.Message);
    }

    [Fact]
    public void StopFilter_DoesNotEmitStopSequenceAcrossChunks()
    {
        var filter = new FoundryLocalStopSequenceFilter(["<end>", "STOP"]);
        Assert.Equal("answer", filter.Append("answer<"));
        Assert.Empty(filter.Append("en"));
        Assert.Empty(filter.Append("d>STOP"));
        Assert.True(filter.Stopped);
        Assert.Empty(filter.Append("ignored"));
        Assert.Empty(filter.Complete());
    }

    [Fact]
    public void StopFilter_FlushesUnmatchedSuffix()
    {
        var filter = new FoundryLocalStopSequenceFilter(["STOP"]);
        Assert.Equal("one", filter.Append("oneST"));
        Assert.Equal("ST two", filter.Append(" twoSTO"));
        Assert.Equal("STO", filter.Complete());
        Assert.False(filter.Stopped);
    }

    [Fact]
    public void StopFilter_OverlappingSequencesMatchIndependentlyOfChunking()
    {
        var complete = new FoundryLocalStopSequenceFilter(["ABCD", "BC"]);
        var streamed = new FoundryLocalStopSequenceFilter(["ABCD", "BC"]);

        Assert.Equal("A", complete.Append("ABCDEF"));
        Assert.Empty(streamed.Append("AB"));
        Assert.Equal("A", streamed.Append("C"));
        Assert.Empty(streamed.Append("DEF"));
    }

    [Theory]
    [InlineData("before<stop>after", "before", true)]
    [InlineData("before<stop", "before<stop", false)]
    [InlineData("before<st<stop>after", "before<st", true)]
    public void StopFilter_ProducesSameOutputAtEveryPairOfChunkBoundaries(
        string text, string expected, bool stopped)
    {
        for (var first = 0; first <= text.Length; first++)
        {
            for (var second = first; second <= text.Length; second++)
            {
                var filter = new FoundryLocalStopSequenceFilter(["<stop>"]);
                var actual = filter.Append(text[..first]) + filter.Append(text[first..second]) +
                    filter.Append(text[second..]) + filter.Complete();
                Assert.Equal(expected, actual);
                Assert.Equal(stopped, filter.Stopped);
            }
        }
    }
}
