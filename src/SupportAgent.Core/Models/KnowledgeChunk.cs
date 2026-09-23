namespace SupportAgent.Core.Models;

/// <summary>
/// One searchable chunk of a company knowledge document.
/// </summary>
public class KnowledgeChunk
{
    /// <summary>Unique chunk identifier.</summary>
    public int Id { get; set; }

    /// <summary>Parent knowledge document identifier.</summary>
    public int KnowledgeDocumentId { get; set; }

    /// <summary>Parent knowledge document.</summary>
    public KnowledgeDocument KnowledgeDocument { get; set; } = null!;

    /// <summary>Chunk text used for lexical retrieval.</summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>Zero-based position of this chunk within the parent document.</summary>
    public int ChunkIndex { get; set; }

    /// <summary>Date and time when the chunk was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Serialized embedding vector used for semantic retrieval. Not exposed through public APIs.</summary>
    public string? EmbeddingJson { get; set; }
}
