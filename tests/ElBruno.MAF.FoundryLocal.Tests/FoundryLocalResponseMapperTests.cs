using ElBruno.MAF.FoundryLocal;
using Microsoft.Extensions.AI;

namespace ElBruno.MAF.FoundryLocal.Tests;

public class FoundryLocalResponseMapperTests
{
    [Fact]
    public void Map_MapsFunctionToolCallsToFunctionCallContent()
    {
        var completion = new FakeCompletion(
            [
                new FakeChoice(
                    new FakeAssistantMessage(
                        "call a tool",
                        [
                            new FakeToolCall(
                                "call-1",
                                "function",
                                new FakeFunctionCall("get_current_time", "{\"timezone\":\"utc\"}"))
                        ]))
            ]);

        var response = FoundryLocalResponseMapper.Map(completion);
        var message = Assert.Single(response.Messages);
        var functionCall = Assert.Single(message.Contents.OfType<FunctionCallContent>());
        var args = Assert.IsAssignableFrom<IDictionary<string, object?>>(functionCall.Arguments);

        Assert.Equal("call-1", functionCall.CallId);
        Assert.Equal("get_current_time", functionCall.Name);
        Assert.True(args.TryGetValue("timezone", out var timezone));
        Assert.Equal("utc", timezone as string);
    }

    [Fact]
    public void Map_HandlesInvalidFunctionArgumentsWithoutThrowing()
    {
        var completion = new FakeCompletion(
            [
                new FakeChoice(
                    new FakeAssistantMessage(
                        null,
                        [
                            new FakeToolCall(
                                "call-2",
                                "function",
                                new FakeFunctionCall("broken_tool", "{not-json"))
                        ]))
            ]);

        var response = FoundryLocalResponseMapper.Map(completion);
        var message = Assert.Single(response.Messages);
        var functionCall = Assert.Single(message.Contents.OfType<FunctionCallContent>());
        var args = Assert.IsAssignableFrom<IDictionary<string, object?>>(functionCall.Arguments);

        Assert.Equal("broken_tool", functionCall.Name);
        Assert.True(args.TryGetValue("_raw", out var raw));
        Assert.Equal("{not-json", raw as string);
    }

    private sealed record FakeCompletion(IReadOnlyList<FakeChoice> Choices);

    private sealed record FakeChoice(FakeAssistantMessage Message);

    private sealed record FakeAssistantMessage(string? ContentCalculated, IReadOnlyList<FakeToolCall> ToolCalls);

    private sealed record FakeToolCall(string Id, string Type, FakeFunctionCall FunctionCall);

    private sealed record FakeFunctionCall(string Name, string Arguments);
}
