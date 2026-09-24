using SupportAgent.Core.Models;

namespace SupportAgent.Core.Interfaces;

public interface IDocumentTextExtractor
{
    Task<DocumentExtractionResult> ExtractAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default);
}
