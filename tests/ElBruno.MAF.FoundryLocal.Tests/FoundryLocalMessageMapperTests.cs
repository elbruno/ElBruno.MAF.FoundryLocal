using ElBruno.MAF.FoundryLocal;
using Microsoft.Extensions.AI;

namespace ElBruno.MAF.FoundryLocal.Tests;

public class FoundryLocalMessageMapperTests
{
    [Fact]
    public void Map_MapsKnownRolesAndText()
    {
        var source = new[]
        {
            new ChatMessage(ChatRole.System, "system text"),
            new ChatMessage(ChatRole.User, "user text"),
            new ChatMessage(ChatRole.Assistant, "assistant text")
        };

        var mapped = FoundryLocalMessageMapper.Map(source);

        Assert.Collection(
            mapped,
            m =>
            {
                Assert.Equal("system", m.Role);
                Assert.Equal("system text", m.ContentCalculated);
            },
            m =>
            {
                Assert.Equal("user", m.Role);
                Assert.Equal("user text", m.ContentCalculated);
            },
            m =>
            {
                Assert.Equal("assistant", m.Role);
                Assert.Equal("assistant text", m.ContentCalculated);
            });
    }

    [Fact]
    public void Map_MapsUnknownRoleToUser()
    {
        var source = new[] { new ChatMessage(new ChatRole("custom"), "hello") };

        var mapped = FoundryLocalMessageMapper.Map(source);

        Assert.Single(mapped);
        Assert.Equal("user", mapped[0].Role);
    }

    [Fact]
    public void Map_MapsFunctionCallContentsToToolCalls()
    {
        var arguments = new Dictionary<string, object?> { ["value"] = "abc" };
        var source = new[]
        {
            new ChatMessage(ChatRole.Assistant, [new FunctionCallContent("call-1", "demo_tool", arguments)])
        };

        var mapped = FoundryLocalMessageMapper.Map(source);

        var message = Assert.Single(mapped);
        var toolCall = Assert.Single(message.ToolCalls!);

        Assert.Equal("call-1", toolCall.Id);
        Assert.Equal("function", toolCall.Type);
        Assert.Equal("demo_tool", toolCall.FunctionCall!.Name);
        Assert.Contains("\"value\":\"abc\"", toolCall.FunctionCall.Arguments);
    }

    [Fact]
    public void Map_MapsFunctionResultToToolRoleMessage()
    {
        var source = new[]
        {
            new ChatMessage(ChatRole.Tool, [new FunctionResultContent("call-2", new { ok = true })])
        };

        var mapped = FoundryLocalMessageMapper.Map(source);

        Assert.Single(mapped);
        Assert.Equal("tool", mapped[0].Role);
        Assert.Equal("call-2", mapped[0].ToolCallId);
        Assert.Contains("\"ok\":true", mapped[0].Content);
    }
}
