using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.AI.Tools;

namespace SupportAgent.Tests;

public class KnowledgeSourceParserTests
{
    [Fact]
    public void TryAddSourcesFromToolResult_AddsUniqueSources()
    {
        var sources = new List<KnowledgeSource>();
        var content = """
            {
              "success": true,
              "results": [
                {
                  "documentTitle": "Refund Policy",
                  "documentSource": "internal-policy",
                  "content": "Customers may request a refund within 30 days.",
                  "score": 0.91
                },
                {
                  "documentTitle": "Refund Policy",
                  "documentSource": "internal-policy",
                  "content": "Approved refunds are processed within 5 to 7 business days.",
                  "score": 0.75
                }
              ]
            }
            """;

        KnowledgeSourceParser.TryAddSourcesFromToolResult(
            SupportToolDefinitions.SearchKnowledgeBaseToolName,
            content,
            sources);

        Assert.Single(sources);
        Assert.Equal("Refund Policy", sources[0].Title);
        Assert.Equal("internal-policy", sources[0].Source);
    }
}
