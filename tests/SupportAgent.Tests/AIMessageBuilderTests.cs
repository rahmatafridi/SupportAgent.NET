using SupportAgent.Core.Enums;
using SupportAgent.Core.Models;
using SupportAgent.Core.Models.AI;
using SupportAgent.Infrastructure.AI;

namespace SupportAgent.Tests;

public class AIMessageBuilderTests
{
    [Fact]
    public void BuildMessages_IncludesSystemPromptAndUserPrompt()
    {
        var request = new AIRequest
        {
            SystemPrompt = "You are helpful.",
            Prompt = "Explain RAG in one sentence."
        };

        var messages = AIMessageBuilder.BuildMessages(request);

        Assert.Equal(2, messages.Count);
        Assert.Equal("system", messages[0].Role);
        Assert.Equal("You are helpful.", messages[0].Content);
        Assert.Equal("user", messages[1].Role);
        Assert.Equal("Explain RAG in one sentence.", messages[1].Content);
    }

    [Fact]
    public void BuildMessages_IncludesAssistantToolCallsAndToolResults()
    {
        var request = new AIRequest
        {
            Messages =
            [
                new AIMessage
                {
                    Role = AIMessageRole.Assistant,
                    Content = string.Empty,
                    ToolCalls =
                    [
                        new AIToolCall
                        {
                            Id = "call-1",
                            Name = "GetCustomer",
                            ArgumentsJson = "{\"customerId\":101}"
                        }
                    ]
                },
                new AIMessage
                {
                    Role = AIMessageRole.Tool,
                    ToolCallId = "call-1",
                    ToolName = "GetCustomer",
                    Content = "{\"success\":true}"
                }
            ]
        };

        var messages = AIMessageBuilder.BuildMessages(request);

        Assert.Equal(2, messages.Count);
        Assert.Equal("assistant", messages[0].Role);
        Assert.NotNull(messages[0].ToolCalls);
        Assert.Single(messages[0].ToolCalls!);
        Assert.Equal("tool", messages[1].Role);
        Assert.Equal("call-1", messages[1].ToolCallId);
        Assert.Equal("GetCustomer", messages[1].ToolName);
    }

    [Fact]
    public void BuildMessages_Throws_WhenRequestIsEmpty()
    {
        var request = new AIRequest();

        Assert.Throws<InvalidOperationException>(() => AIMessageBuilder.BuildMessages(request));
    }
}
