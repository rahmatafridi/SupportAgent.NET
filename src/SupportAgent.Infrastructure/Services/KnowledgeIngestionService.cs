using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data;
using SupportAgent.Infrastructure.Documents;

namespace SupportAgent.Infrastructure.Services;

public sealed class KnowledgeIngestionService(
    SupportAgentDbContext db,
    IDocumentTextExtractor extractor,
    IKnowledgeService knowledgeService,
    ICurrentUserContext currentUser,
    IOptions<KnowledgeUploadOptions> options) : IKnowledgeIngestionService
{
    public async Task<KnowledgeIngestionResult> UploadDocumentAsync(Stream fileStream, string fileName, string contentType, long fileSize, string? title = null, CancellationToken cancellationToken = default)
    {
        var maxBytes = options.Value.MaxFileSizeMb * 1024L * 1024L;
        if (fileSize <= 0) throw new DocumentIngestionException("The uploaded file is empty.");
        if (fileSize > maxBytes) throw new DocumentIngestionException($"The file exceeds the {options.Value.MaxFileSizeMb} MB upload limit.");
        var safeName = Path.GetFileName(fileName);
        if (safeName != fileName || string.IsNullOrWhiteSpace(safeName)) throw new DocumentIngestionException("The file name is invalid.");
        var documentTitle = string.IsNullOrWhiteSpace(title) ? MakeTitle(safeName) : title.Trim();
        if (documentTitle.Length > 200) throw new DocumentIngestionException("The document title must be 200 characters or fewer.");

        var now = DateTime.UtcNow;
        var document = new KnowledgeDocument
        {
            OrganizationId = currentUser.OrganizationId,
            Title = documentTitle,
            Source = safeName,
            ContentType = contentType,
            OriginalFileName = safeName,
            FileSizeBytes = fileSize,
            UploadedByUserId = currentUser.UserId,
            UploadedAt = now,
            ProcessingStatus = "Processing",
            CreatedAt = now
        };
        db.KnowledgeDocuments.Add(document);
        await db.SaveChangesAsync(cancellationToken);

        try
        {
            var extraction = await extractor.ExtractAsync(fileStream, safeName, contentType, cancellationToken);
            var chunks = await knowledgeService.ProcessDocumentAsync(document.Id, extraction.Text, cancellationToken);
            document.ProcessingStatus = "Completed";
            document.ProcessingError = null;
            await db.SaveChangesAsync(cancellationToken);
            return new KnowledgeIngestionResult(document.Id, document.Title, safeName, document.ProcessingStatus, chunks);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            var safeError = exception switch
            {
                DocumentIngestionException ingestionException => ingestionException.Message,
                InvalidOperationException when exception.Message.StartsWith("Failed to generate embeddings", StringComparison.Ordinal) =>
                    "Embedding generation failed. Ensure Ollama is running and the configured embedding model is installed.",
                _ => "Document processing failed."
            };
            document.ProcessingStatus = "Failed";
            document.ProcessingError = safeError;
            await db.SaveChangesAsync(CancellationToken.None);
            throw exception is DocumentIngestionException
                ? exception
                : new DocumentIngestionException(safeError);
        }
    }

    public async Task<IReadOnlyList<KnowledgeDocumentSummary>> GetDocumentsAsync(CancellationToken cancellationToken = default) =>
        await db.KnowledgeDocuments.AsNoTracking().OrderByDescending(x => x.UploadedAt ?? x.CreatedAt)
            .Select(x => new KnowledgeDocumentSummary(x.Id, x.Title, x.OriginalFileName, x.ContentType, x.FileSizeBytes, x.UploadedAt, x.ProcessingStatus, x.Chunks.Count, x.Source, x.UploadedByUserId, x.CreatedAt, x.ProcessingError)).ToListAsync(cancellationToken);

    public async Task<KnowledgeDocumentSummary?> GetDocumentAsync(int id, CancellationToken cancellationToken = default) =>
        await db.KnowledgeDocuments.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new KnowledgeDocumentSummary(x.Id, x.Title, x.OriginalFileName, x.ContentType, x.FileSizeBytes, x.UploadedAt, x.ProcessingStatus, x.Chunks.Count, x.Source, x.UploadedByUserId, x.CreatedAt, x.ProcessingError)).FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> DeleteDocumentAsync(int id, CancellationToken cancellationToken = default)
    {
        var document = await db.KnowledgeDocuments.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (document is null) return false;
        db.KnowledgeDocuments.Remove(document);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string MakeTitle(string fileName) =>
        string.Join(' ', Path.GetFileNameWithoutExtension(fileName).Split(['-', '_'], StringSplitOptions.RemoveEmptyEntries)) switch
        {
            { Length: > 0 } value => char.ToUpperInvariant(value[0]) + value[1..],
            _ => "Uploaded document"
        };
}
