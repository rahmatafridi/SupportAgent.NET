namespace SupportAgent.Core.Interfaces;

/// <summary>
/// Provider-independent embedding generation used for semantic knowledge retrieval.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Generates an embedding vector for one text input.
    /// </summary>
    /// <param name="text">The text to embed.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>The embedding vector.</returns>
    Task<float[]> GenerateEmbeddingAsync(
        string text,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates embedding vectors for multiple text inputs.
    /// </summary>
    /// <param name="texts">The texts to embed.</param>
    /// <param name="cancellationToken">Token used to cancel the request.</param>
    /// <returns>Embedding vectors in the same order as the input texts.</returns>
    Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(
        IReadOnlyList<string> texts,
        CancellationToken cancellationToken = default);
}
