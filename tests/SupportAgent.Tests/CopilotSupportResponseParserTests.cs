using SupportAgent.Infrastructure.Copilot;

namespace SupportAgent.Tests;

public class CopilotSupportResponseParserTests
{
    [Fact]
    public void Parses_advisory_actions_without_executing_them()
    {
        var result = CopilotSupportResponseParser.Parse("""
            {"answer":"Review the order.","suggestedActions":[{"label":"Check order status","description":"Retrieve current status","actionType":"GetOrderStatus"},{"label":"Search policy","actionType":"SearchKnowledgeBase"}],"confidence":0.8}
            """);
        Assert.Equal("Review the order.", result.Answer);
        Assert.Equal(2, result.SuggestedActions.Count);
        Assert.Equal(0.8, result.Confidence);
        Assert.False(result.StructuredOutputFailed);
    }

    [Fact]
    public void Parses_fenced_structured_response_without_exposing_raw_json()
    {
        var result = CopilotSupportResponseParser.Parse("""
            ```json
            {"answer":"Order ORD-1002 is shipped.","suggestedActions":[{"label":"View recent tickets","actionType":"ViewRecentTickets"}],"confidence":0.9}
            ```
            """);

        Assert.Equal("Order ORD-1002 is shipped.", result.Answer);
        Assert.DoesNotContain("{\"answer\"", result.Answer, StringComparison.Ordinal);
        Assert.Equal("View recent tickets", Assert.Single(result.SuggestedActions).Label);
        Assert.Equal(0.9, result.Confidence);
    }

    [Fact]
    public void Malformed_structured_response_returns_safe_text_without_raw_json()
    {
        var result = CopilotSupportResponseParser.Parse(
            "{\"answer\":\"Order status\",\"suggestedActions\":[}");

        Assert.Equal("The AI response could not be formatted safely. Please try again.", result.Answer);
        Assert.DoesNotContain("{", result.Answer, StringComparison.Ordinal);
        Assert.Empty(result.SuggestedActions);
        Assert.Equal(0, result.Confidence);
        Assert.True(result.StructuredOutputFailed);
    }

    [Fact]
    public void Json_inside_answer_is_rejected_instead_of_rendered()
    {
        var result = CopilotSupportResponseParser.Parse("""
            {"answer":"{\"answer\":\"nested raw JSON\"}","suggestedActions":[],"confidence":0.9}
            """);

        Assert.Equal("The AI response could not be formatted safely. Please try again.", result.Answer);
        Assert.True(result.StructuredOutputFailed);
    }

    [Fact]
    public void Preserves_plain_text_for_backward_compatibility()
    {
        var result = CopilotSupportResponseParser.Parse("Customer 101 is John Smith.");
        Assert.Equal("Customer 101 is John Smith.", result.Answer);
        Assert.Empty(result.SuggestedActions);
    }
}
