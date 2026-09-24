namespace SupportAgent.Core.Models;

/// <summary>
/// A company knowledge document stored in the internal knowledge base.
/// </summary>
public class KnowledgeDocument
{
    /// <summary>Unique document identifier.</summary>
    public int Id { get; set; }

    public Guid OrganizationId { get; set; }

    /// <summary>Human-readable document title.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional source label, such as internal-policy or help-center.</summary>
    public string? Source { get; set; }

    /// <summary>Optional content type label, such as policy or procedure.</summary>
    public string? ContentType { get; set; }

    /// <summary>Date and time when the document was added.</summary>
    public DateTime CreatedAt { get; set; }

    public string? OriginalFileName { get; set; }
    public long? FileSizeBytes { get; set; }
    public Guid? UploadedByUserId { get; set; }
    public DateTime? UploadedAt { get; set; }
    public string ProcessingStatus { get; set; } = "Completed";
    public string? ProcessingError { get; set; }

    /// <summary>Searchable chunks derived from the original document content.</summary>
    public ICollection<KnowledgeChunk> Chunks { get; set; } = new List<KnowledgeChunk>();
}
