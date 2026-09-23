namespace SupportAgent.Core.Models;

/// <summary>
/// One ranked knowledge chunk returned by a knowledge base search.
/// </summary>
public class KnowledgeSearchResult
{
    /// <summary>Parent document identifier.</summary>
    public int DocumentId { get; set; }

    /// <summary>Parent document title.</summary>
    public string DocumentTitle { get; set; } = string.Empty;

    /// <summary>Optional parent document source label.</summary>
    public string? DocumentSource { get; set; }

    /// <summary>Matching chunk identifier.</summary>
    public int ChunkId { get; set; }

    /// <summary>Matching chunk content.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Relevance score between 0 and 1.</summary>
    public double Score { get; set; }
}
