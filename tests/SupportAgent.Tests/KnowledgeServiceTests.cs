using SupportAgent.Infrastructure.Data;
using SupportAgent.Infrastructure.Knowledge;

namespace SupportAgent.Tests;

public class KnowledgeServiceTests
{
    [Fact]
    public async Task AddDocumentAsync_CreatesDocumentAndChunks()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(AddDocumentAsync_CreatesDocumentAndChunks));
        var service = KnowledgeServiceTestFactory.Create(context);

        var document = await service.AddDocumentAsync(
            "Refund Policy",
            """
            Customers may request a refund within 30 days of purchase if the order has not been fully consumed.
            Approved refunds are processed within 5 to 7 business days.
            """);

        Assert.True(document.Id > 0);
        Assert.Equal("Refund Policy", document.Title);

        var chunks = context.KnowledgeChunks.Where(chunk => chunk.KnowledgeDocumentId == document.Id).ToList();
        Assert.NotEmpty(chunks);
        Assert.Equal(0, chunks[0].ChunkIndex);
        Assert.All(chunks, chunk => Assert.False(string.IsNullOrWhiteSpace(chunk.EmbeddingJson)));
    }

    [Fact]
    public async Task SearchAsync_ReturnsRelevantRefundChunk()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(SearchAsync_ReturnsRelevantRefundChunk));
        var service = KnowledgeServiceTestFactory.Create(context);

        await service.AddDocumentAsync(
            "Refund Policy",
            "Customers may request a refund within 30 days of purchase if the order has not been fully consumed.");
        await service.AddDocumentAsync(
            "Shipping Policy",
            "Standard orders usually ship within 1 to 2 business days after payment confirmation.");

        var results = await service.SearchAsync("refund policy");

        Assert.NotEmpty(results);
        Assert.Contains(results, result => result.DocumentTitle == "Refund Policy");
        Assert.Contains("refund", results[0].Content, StringComparison.OrdinalIgnoreCase);
        Assert.True(results[0].Score > 0);
    }

    [Fact]
    public async Task SearchAsync_MatchesSemanticallyRelatedShippingQuestion()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(SearchAsync_MatchesSemanticallyRelatedShippingQuestion));
        var service = KnowledgeServiceTestFactory.Create(context);

        await service.AddDocumentAsync(
            "Shipping Policy",
            "Standard orders usually ship within 1 to 2 business days.");
        await service.AddDocumentAsync(
            "Refund Policy",
            "Customers may request a refund within 30 days of purchase.");

        var results = await service.SearchAsync("How long before my package leaves the warehouse?");

        Assert.NotEmpty(results);
        Assert.Equal("Shipping Policy", results[0].DocumentTitle);
    }

    [Fact]
    public async Task SearchAsync_MatchesSemanticallyRelatedRefundQuestion()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(SearchAsync_MatchesSemanticallyRelatedRefundQuestion));
        var service = KnowledgeServiceTestFactory.Create(context);

        await service.AddDocumentAsync(
            "Refund Policy",
            "Customers may request a refund within 30 days of purchase if the order has not been fully consumed.");

        var results = await service.SearchAsync("Can I get my money back after buying something?");

        Assert.NotEmpty(results);
        Assert.Equal("Refund Policy", results[0].DocumentTitle);
    }

    [Fact]
    public async Task SearchAsync_ReturnsLimitedResults()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(SearchAsync_ReturnsLimitedResults));
        var service = KnowledgeServiceTestFactory.Create(context);

        for (var index = 1; index <= 8; index++)
        {
            await service.AddDocumentAsync(
                $"Policy {index}",
                $"Support policy {index} mentions refunds, shipping, and escalation guidance for customers.");
        }

        var results = await service.SearchAsync("refund shipping escalation", maxResults: 5);

        Assert.Equal(5, results.Count);
    }

    [Fact]
    public async Task SearchAsync_Throws_WhenQueryIsEmpty()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(SearchAsync_Throws_WhenQueryIsEmpty));
        var service = KnowledgeServiceTestFactory.Create(context);

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.SearchAsync("   "));
        Assert.Equal("query", exception.ParamName);
    }

    [Fact]
    public async Task SearchAsync_ReturnsEmptyResults_WhenNothingMatches()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(SearchAsync_ReturnsEmptyResults_WhenNothingMatches));
        var service = KnowledgeServiceTestFactory.Create(context);

        await service.AddDocumentAsync(
            "Shipping Policy",
            "Standard orders usually ship within 1 to 2 business days.");

        var results = await service.SearchAsync("nonexistent topic");

        Assert.Empty(results);
    }

    [Fact]
    public async Task SearchAsync_HandlesMissingChunkEmbeddingsSafely()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(SearchAsync_HandlesMissingChunkEmbeddingsSafely));
        var service = KnowledgeServiceTestFactory.Create(context);

        await service.AddDocumentAsync(
            "Refund Policy",
            "Customers may request a refund within 30 days of purchase.");

        var chunk = context.KnowledgeChunks.Single();
        chunk.EmbeddingJson = null;
        await context.SaveChangesAsync();

        var results = await service.SearchAsync("refund policy");

        Assert.NotEmpty(results);
        Assert.Equal("Refund Policy", results[0].DocumentTitle);
    }

    [Fact]
    public async Task GenerateMissingEmbeddingsAsync_UpdatesOnlyMissingEmbeddings()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(GenerateMissingEmbeddingsAsync_UpdatesOnlyMissingEmbeddings));
        var service = KnowledgeServiceTestFactory.Create(context);

        await service.AddDocumentAsync(
            "Refund Policy",
            "Customers may request a refund within 30 days of purchase.");

        var chunk = context.KnowledgeChunks.Single();
        var existingEmbedding = chunk.EmbeddingJson;
        chunk.EmbeddingJson = null;
        await context.SaveChangesAsync();

        var updatedChunks = await service.GenerateMissingEmbeddingsAsync();

        Assert.Equal(1, updatedChunks);
        await context.Entry(chunk).ReloadAsync();
        Assert.False(string.IsNullOrWhiteSpace(chunk.EmbeddingJson));
    }

    [Fact]
    public async Task GenerateMissingEmbeddingsAsync_DoesNotRegenerateExistingEmbeddings()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(GenerateMissingEmbeddingsAsync_DoesNotRegenerateExistingEmbeddings));
        var service = KnowledgeServiceTestFactory.Create(context);

        await service.AddDocumentAsync(
            "Refund Policy",
            "Customers may request a refund within 30 days of purchase.");

        var chunk = context.KnowledgeChunks.Single();
        var existingEmbedding = chunk.EmbeddingJson;

        var updatedChunks = await service.GenerateMissingEmbeddingsAsync();

        Assert.Equal(0, updatedChunks);
        await context.Entry(chunk).ReloadAsync();
        Assert.Equal(existingEmbedding, chunk.EmbeddingJson);
    }
}
