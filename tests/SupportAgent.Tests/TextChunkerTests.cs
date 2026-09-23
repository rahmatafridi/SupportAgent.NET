using SupportAgent.Infrastructure.Services;

namespace SupportAgent.Tests;

public class TextChunkerTests
{
    [Fact]
    public void Chunk_ReturnsSingleChunk_WhenTextIsShort()
    {
        var chunker = new TextChunker();

        var chunks = chunker.Chunk("Refund requests must be submitted within 30 days.");

        Assert.Single(chunks);
        Assert.Contains("30 days", chunks[0], StringComparison.Ordinal);
    }

    [Fact]
    public void Chunk_CreatesMultipleChunks_ForLongDocument()
    {
        var chunker = new TextChunker();
        var content = string.Join(
            ' ',
            Enumerable.Repeat(
                "Customers may request a refund within 30 days if the order has not been fully consumed.",
                20));

        var chunks = chunker.Chunk(content);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, chunk => Assert.InRange(chunk.Length, 100, 900));
    }

    [Fact]
    public void Chunk_UsesSentenceBoundaries_WhenPossible()
    {
        var chunker = new TextChunker();
        var paragraph = string.Join(
            ' ',
            Enumerable.Repeat(
                "Customers may request a refund within 30 days if the order has not been fully consumed.",
                12));
        var content = paragraph + " " + string.Join(
            ' ',
            Enumerable.Repeat(
                "Standard orders usually ship within 1 to 2 business days after payment confirmation.",
                12));

        var chunks = chunker.Chunk(content);

        Assert.True(chunks.Count >= 2);
        Assert.Contains(chunks, chunk => chunk.Contains("refund", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(chunks, chunk => chunk.Contains("ship", StringComparison.OrdinalIgnoreCase));
    }
}
