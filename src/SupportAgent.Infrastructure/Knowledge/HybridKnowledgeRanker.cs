using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Knowledge.Internal;

namespace SupportAgent.Infrastructure.Knowledge;

/// <summary>
/// Combines semantic and lexical scores into hybrid knowledge rankings.
/// </summary>
internal static class HybridKnowledgeRanker
{
    /// <summary>
    /// Ranks knowledge chunks using hybrid semantic and lexical scoring.
    /// </summary>
    /// <param name="chunks">Candidate chunks loaded from storage.</param>
    /// <param name="terms">Lexical query terms.</param>
    /// <param name="queryEmbedding">Optional query embedding vector.</param>
    /// <param name="options">Hybrid search weight and threshold options.</param>
    /// <param name="maxResults">Maximum number of results to return for this call.</param>
    /// <returns>Ranked chunks that exceed the minimum hybrid score.</returns>
    public static IReadOnlyList<RankedKnowledgeChunk> Rank(
        IReadOnlyList<KnowledgeChunk> chunks,
        IReadOnlyList<string> terms,
        IReadOnlyList<float>? queryEmbedding,
        KnowledgeSearchOptions options,
        int maxResults)
    {
        var effectiveMaxResults = Math.Min(maxResults, options.MaxResults);
        var ranked = new List<RankedKnowledgeChunk>();

        foreach (var chunk in chunks)
        {
            var lexicalScore = LexicalKnowledgeScorer.Score(chunk, terms);
            var semanticScore = 0.0;

            if (queryEmbedding is not null)
            {
                var chunkEmbedding = EmbeddingJsonSerializer.Deserialize(chunk.EmbeddingJson);
                if (chunkEmbedding is not null)
                {
                    semanticScore = CosineSimilarity.Calculate(queryEmbedding, chunkEmbedding);
                }
            }

            var finalScore = chunk.EmbeddingJson is null or { Length: 0 }
                ? lexicalScore
                : (options.SemanticWeight * semanticScore) + (options.LexicalWeight * lexicalScore);

            if (finalScore < options.MinimumScore)
            {
                continue;
            }

            ranked.Add(new RankedKnowledgeChunk(
                chunk,
                Math.Round(finalScore, 4),
                Math.Round(semanticScore, 4),
                Math.Round(lexicalScore, 4)));
        }

        return ranked
            .OrderByDescending(result => result.FinalScore)
            .ThenBy(result => result.Chunk.ChunkIndex)
            .Take(effectiveMaxResults)
            .ToList();
    }
}
