using SupportAgent.Infrastructure.Copilot;

namespace SupportAgent.Tests;

public class CopilotSupportResponseParserTests
{
    [Fact]
    public void Parses_advisory_actions_without_executing_them()
    {
        var result = CopilotSupportResponseParser.Parse("""
            {"answer":"Review the order.","suggestedActions":["Check carrier status","Escalate after 24 hours"],"confidence":0.8}
            """);
        Assert.Equal("Review the order.", result.Answer);
        Assert.Equal(2, result.SuggestedActions.Count);
        Assert.Equal(0.8, result.Confidence);
    }

    [Fact]
    public void Preserves_plain_text_for_backward_compatibility()
    {
        var result = CopilotSupportResponseParser.Parse("Customer 101 is John Smith.");
        Assert.Equal("Customer 101 is John Smith.", result.Answer);
        Assert.Empty(result.SuggestedActions);
    }
}
