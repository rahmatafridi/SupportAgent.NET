using System.Text;
using System.Text.RegularExpressions;
using SupportAgent.Core.Interfaces;

namespace SupportAgent.Infrastructure.Services;

/// <summary>
/// Simple sentence-aware text chunker for knowledge base documents.
/// </summary>
public partial class TextChunker : ITextChunker
{
    private const int TargetChunkSize = 700;
    private const int OverlapSize = 100;
    private const int MinimumChunkSize = 200;

    /// <inheritdoc />
    public IReadOnlyList<string> Chunk(string text)
    {
        var normalized = NormalizeText(text);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return [];
        }

        if (normalized.Length <= TargetChunkSize)
        {
            return [normalized];
        }

        var sentences = SplitIntoSentences(normalized);
        if (sentences.Count == 0)
        {
            return [normalized];
        }

        var chunks = new List<string>();
        var current = new StringBuilder();

        foreach (var sentence in sentences)
        {
            if (current.Length == 0)
            {
                current.Append(sentence);
                continue;
            }

            if (current.Length + sentence.Length + 1 <= TargetChunkSize)
            {
                current.Append(' ').Append(sentence);
                continue;
            }

            chunks.Add(current.ToString());

            var overlap = GetOverlapText(current.ToString());
            current.Clear();

            if (!string.IsNullOrWhiteSpace(overlap))
            {
                current.Append(overlap).Append(' ');
            }

            current.Append(sentence);
        }

        if (current.Length > 0)
        {
            var finalChunk = current.ToString();
            if (chunks.Count > 0 && finalChunk.Length < MinimumChunkSize)
            {
                chunks[^1] = MergeWithOverlap(chunks[^1], finalChunk);
            }
            else
            {
                chunks.Add(finalChunk);
            }
        }

        return chunks;
    }

    /// <summary>
    /// Collapses whitespace and trims the input text.
    /// </summary>
    private static string NormalizeText(string text) =>
        WhitespaceRegex().Replace(text, " ").Trim();

    /// <summary>
    /// Splits text into sentence-like segments without cutting mid-word unnecessarily.
    /// </summary>
    private static List<string> SplitIntoSentences(string text)
    {
        var parts = SentenceSplitRegex().Split(text);
        return parts
            .Select(part => part.Trim())
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();
    }

    /// <summary>
    /// Returns the trailing overlap text from the previous chunk.
    /// </summary>
    private static string GetOverlapText(string chunk)
    {
        if (chunk.Length <= OverlapSize)
        {
            return chunk;
        }

        var overlapStart = chunk.Length - OverlapSize;
        var slice = chunk[overlapStart..];

        var firstSpace = slice.IndexOf(' ');
        return firstSpace >= 0 ? slice[(firstSpace + 1)..] : slice;
    }

    /// <summary>
    /// Merges a small trailing chunk into the previous chunk when practical.
    /// </summary>
    private static string MergeWithOverlap(string previousChunk, string trailingChunk)
    {
        if (previousChunk.Contains(trailingChunk, StringComparison.Ordinal))
        {
            return previousChunk;
        }

        return $"{previousChunk} {trailingChunk}".Trim();
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    [GeneratedRegex(@"(?<=[.!?])\s+")]
    private static partial Regex SentenceSplitRegex();
}
