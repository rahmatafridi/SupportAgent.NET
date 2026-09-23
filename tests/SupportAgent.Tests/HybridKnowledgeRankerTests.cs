using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Knowledge;

namespace SupportAgent.Tests;

public class HybridKnowledgeRankerTests
{
    [Fact]
    public void Rank_PrefersSemanticMatch_WhenLexicalMatchIsWeak()
    {
        var shippingChunk = CreateChunk(
            1,
            "Shipping Policy",
            "Standard orders usually ship within 1 to 2 business days.");
        var refundChunk = CreateChunk(
            2,
            "Refund Policy",
            "Customers may request a refund within 30 days of purchase.");

        var queryEmbedding = new FakeEmbeddingService().GenerateEmbeddingAsync(
            "How long before my package leaves the warehouse?").Result;
        var terms = LexicalKnowledgeScorer.ExtractSearchTerms(
            "How long before my package leaves the warehouse?");

        var ranked = HybridKnowledgeRanker.Rank(
            [shippingChunk, refundChunk],
            terms,
            queryEmbedding,
            new KnowledgeSearchOptions
            {
                SemanticWeight = 0.75,
                LexicalWeight = 0.25,
                MinimumScore = 0.1
            },
            maxResults: 5);

        Assert.NotEmpty(ranked);
        Assert.Equal("Shipping Policy", ranked[0].Chunk.KnowledgeDocument.Title);
        Assert.True(ranked[0].SemanticScore > 0.5);
    }

    [Fact]
    public void Rank_StillFindsExactLexicalMatches()
    {
        var chunk = CreateChunk(
            1,
            "Refund Policy",
            "Customers may request a refund within 30 days of purchase.");

        var terms = LexicalKnowledgeScorer.ExtractSearchTerms("refund policy");
        var queryEmbedding = new FakeEmbeddingService().GenerateEmbeddingAsync("refund policy").Result;

        var ranked = HybridKnowledgeRanker.Rank(
            [chunk],
            terms,
            queryEmbedding,
            new KnowledgeSearchOptions { MinimumScore = 0.1 },
            maxResults: 5);

        Assert.Single(ranked);
        Assert.True(ranked[0].LexicalScore > 0);
        Assert.True(ranked[0].FinalScore >= 0.1);
    }

    [Fact]
    public void Rank_ReturnsEmpty_WhenScoresAreBelowMinimumThreshold()
    {
        var chunk = CreateChunk(
            1,
            "Shipping Policy",
            "Standard orders usually ship within 1 to 2 business days.");

        var terms = LexicalKnowledgeScorer.ExtractSearchTerms("quantum physics");
        var queryEmbedding = new FakeEmbeddingService().GenerateEmbeddingAsync("quantum physics").Result;

        var ranked = HybridKnowledgeRanker.Rank(
            [chunk],
            terms,
            queryEmbedding,
            new KnowledgeSearchOptions { MinimumScore = 0.5 },
            maxResults: 5);

        Assert.Empty(ranked);
    }

    private static KnowledgeChunk CreateChunk(int id, string title, string content) =>
        new()
        {
            Id = id,
            KnowledgeDocumentId = id,
            Content = content,
            ChunkIndex = 0,
            CreatedAt = DateTime.UtcNow,
            KnowledgeDocument = new KnowledgeDocument
            {
                Id = id,
                Title = title,
                CreatedAt = DateTime.UtcNow
            },
            EmbeddingJson = EmbeddingJsonSerializer.Serialize(
                new FakeEmbeddingService().GenerateEmbeddingAsync(content).Result)
        };
}
