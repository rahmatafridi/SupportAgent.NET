using SupportAgent.Core.Interfaces;

namespace SupportAgent.Tests;

/// <summary>
/// Deterministic embedding service for unit tests without Ollama.
/// </summary>
internal sealed class FakeEmbeddingService : IEmbeddingService
{
    public Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Embed(text));
    }

    public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        IReadOnlyList<float[]> embeddings = texts.Select(Embed).ToList();
        return Task.FromResult(embeddings);
    }

    private static float[] Embed(string text)
    {
        var lower = text.ToLowerInvariant();
        var shipping = ScoreKeywords(lower, "ship", "shipping", "warehouse", "package", "delivery", "carrier", "business day", "leaves");
        var refund = ScoreKeywords(lower, "refund", "money back", "return", "purchase", "eligible", "reimburse", "buying", "money");
        var escalation = ScoreKeywords(lower, "escalate", "priority", "tier", "unresolved", "troubleshooting");
        return Normalize([shipping, refund, escalation]);
    }

    private static float ScoreKeywords(string text, params string[] keywords) =>
        keywords.Sum(keyword => text.Contains(keyword, StringComparison.Ordinal) ? 1f : 0f);

    private static float[] Normalize(float[] values)
    {
        var magnitude = MathF.Sqrt(values.Sum(value => value * value));
        if (magnitude == 0)
        {
            return [0f, 0f, 0f];
        }

        return values.Select(value => value / magnitude).ToArray();
    }
}
