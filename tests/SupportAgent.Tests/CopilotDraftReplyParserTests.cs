using SupportAgent.Infrastructure.Copilot;

namespace SupportAgent.Tests;

public class CopilotDraftReplyParserTests
{
    [Fact]
    public void Parse_ReadsStructuredJsonDraft()
    {
        const string json = """
            {
              "subject": "Update on your order",
              "body": "Thanks for contacting us. I am checking your order now.",
              "tone": "professional",
              "confidence": 0.82
            }
            """;

        var draft = CopilotDraftReplyParser.Parse(json);

        Assert.Equal("Update on your order", draft.Subject);
        Assert.Contains("checking your order", draft.Body, StringComparison.Ordinal);
        Assert.Equal("professional", draft.Tone);
        Assert.Equal(0.82, draft.Confidence);
    }

    [Fact]
    public void Parse_ExtractsJsonFromMarkdownCodeBlock()
    {
        const string text = """
            Here is the draft:
            ```json
            {
              "subject": "Refund request",
              "body": "We can help with your refund request.",
              "tone": "professional",
              "confidence": 0.7
            }
            ```
            """;

        var draft = CopilotDraftReplyParser.Parse(text);

        Assert.Equal("Refund request", draft.Subject);
        Assert.Contains("refund request", draft.Body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_FallsBackToPlainText_WhenJsonIsInvalid()
    {
        const string text = "Thanks for your patience. I am reviewing your order now.";

        var draft = CopilotDraftReplyParser.Parse(text);

        Assert.Equal(text, draft.Body);
        Assert.Equal("professional", draft.Tone);
        Assert.Equal(0.5, draft.Confidence);
    }
}
