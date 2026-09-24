using SupportAgent.Core.Models;

namespace SupportAgent.Core.Interfaces;

public interface IKnowledgeIngestionService
{
    Task<KnowledgeIngestionResult> UploadDocumentAsync(Stream fileStream, string fileName, string contentType, long fileSize, string? title = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<KnowledgeDocumentSummary>> GetDocumentsAsync(CancellationToken cancellationToken = default);
    Task<KnowledgeDocumentSummary?> GetDocumentAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> DeleteDocumentAsync(int id, CancellationToken cancellationToken = default);
}

public sealed record KnowledgeIngestionResult(int DocumentId, string Title, string FileName, string Status, int ChunksCreated);
public sealed record KnowledgeDocumentSummary(int Id, string Title, string? OriginalFileName, string? ContentType, long? FileSizeBytes, DateTime? UploadedAt, string ProcessingStatus, int ChunkCount, string? Source, Guid? UploadedByUserId, DateTime CreatedAt, string? ProcessingError);
