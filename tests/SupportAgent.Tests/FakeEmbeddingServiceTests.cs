using SupportAgent.Core.Interfaces;

namespace SupportAgent.Tests;

public class FakeEmbeddingServiceTests
{
    [Fact]
    public async Task GenerateEmbeddingAsync_ReturnsNormalizedVector()
    {
        var service = new FakeEmbeddingService();

        var embedding = await service.GenerateEmbeddingAsync("refund policy");

        Assert.Equal(3, embedding.Length);
        var magnitude = MathF.Sqrt(embedding.Sum(value => value * value));
        Assert.Equal(1f, magnitude, precision: 3);
    }

    [Fact]
    public async Task GenerateEmbeddingsAsync_ReturnsOneVectorPerInput()
    {
        var service = new FakeEmbeddingService();

        var embeddings = await service.GenerateEmbeddingsAsync([
            "shipping policy",
            "refund policy"
        ]);

        Assert.Equal(2, embeddings.Count);
        Assert.Equal(3, embeddings[0].Length);
    }
}
