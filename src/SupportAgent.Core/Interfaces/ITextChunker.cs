namespace SupportAgent.Core.Interfaces;

/// <summary>
/// Splits document text into overlapping chunks for knowledge base storage.
/// </summary>
public interface ITextChunker
{
    /// <summary>
    /// Splits normalized text into retrieval-friendly chunks.
    /// </summary>
    /// <param name="text">The full document text to chunk.</param>
    /// <returns>Ordered chunks ready to store in the knowledge base.</returns>
    IReadOnlyList<string> Chunk(string text);
}
