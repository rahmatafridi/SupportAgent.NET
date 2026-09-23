using System.Text.RegularExpressions;
using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.Knowledge;

/// <summary>
/// Lexical term-matching scorer used as part of hybrid knowledge retrieval.
/// </summary>
public static partial class LexicalKnowledgeScorer
{
    /// <summary>
    /// Scores one chunk against extracted query terms.
    /// </summary>
    /// <param name="chunk">The candidate knowledge chunk.</param>
    /// <param name="terms">Distinct lowercase query terms.</param>
    /// <returns>Lexical relevance score between 0 and 1.</returns>
    public static double Score(KnowledgeChunk chunk, IReadOnlyList<string> terms)
    {
        if (terms.Count == 0)
        {
            return 0;
        }

        var content = chunk.Content.ToLowerInvariant();
        var title = chunk.KnowledgeDocument.Title.ToLowerInvariant();

        var contentMatches = terms.Count(term => content.Contains(term, StringComparison.Ordinal));
        var titleMatches = terms.Count(term => title.Contains(term, StringComparison.Ordinal));

        if (contentMatches == 0 && titleMatches == 0)
        {
            return 0;
        }

        var rawScore = contentMatches + (titleMatches * 2.0);
        var maxScore = terms.Count * 3.0;
        return Math.Min(1.0, rawScore / maxScore);
    }

    /// <summary>
    /// Extracts distinct lowercase search terms from the query.
    /// </summary>
    /// <param name="query">The user or LLM query.</param>
    /// <returns>Search terms used for lexical scoring.</returns>
    public static IReadOnlyList<string> ExtractSearchTerms(string query) =>
        TermSplitRegex()
            .Split(query.ToLowerInvariant())
            .Select(term => term.Trim())
            .Where(term => term.Length >= 2)
            .Distinct(StringComparer.Ordinal)
            .ToList();

    [GeneratedRegex(@"[^\p{L}\p{N}]+")]
    private static partial Regex TermSplitRegex();
}
