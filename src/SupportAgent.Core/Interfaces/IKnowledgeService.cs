using SupportAgent.Core.Models;

namespace SupportAgent.Core.Interfaces;

/// <summary>
/// Company knowledge base operations used by API endpoints and AI tools.
/// </summary>
public interface IKnowledgeService
{
    /// <summary>
    /// Adds a knowledge document and stores it as searchable chunks with embeddings.
    /// </summary>
    /// <param name="title">Document title.</param>
    /// <param name="content">Full document content to chunk and store.</param>
    /// <param name="source">Optional source label.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns>The created knowledge document.</returns>
    Task<KnowledgeDocument> AddDocumentAsync(
        string title,
        string content,
        string? source = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Searches stored knowledge chunks using hybrid semantic and lexical retrieval.
    /// </summary>
    /// <param name="query">Search query provided by a user or the LLM.</param>
    /// <param name="maxResults">Maximum number of ranked chunks to return.</param>
    /// <param name="cancellationToken">Token used to cancel the search.</param>
    /// <returns>Ranked knowledge chunks matching the query.</returns>
    Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates and stores embeddings for chunks that do not have them yet.
    /// </summary>
    /// <param name="cancellationToken">Token used to cancel the rebuild operation.</param>
    /// <returns>The number of chunks updated with new embeddings.</returns>
    Task<int> GenerateMissingEmbeddingsAsync(CancellationToken cancellationToken = default);
}
