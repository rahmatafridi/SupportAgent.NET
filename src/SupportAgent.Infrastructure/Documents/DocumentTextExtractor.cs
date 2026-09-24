using Microsoft.Extensions.Options;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.Documents;

public sealed class DocumentTextExtractor(IOptions<KnowledgeUploadOptions> options) : IDocumentTextExtractor
{
    private static readonly IReadOnlyDictionary<string, string[]> ContentTypes = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ["application/pdf"],
        [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document", "application/octet-stream"],
        [".txt"] = ["text/plain", "application/octet-stream"]
    };

    public async Task<DocumentExtractionResult> ExtractAsync(Stream fileStream, string fileName, string contentType, CancellationToken cancellationToken = default)
    {
        var safeName = Path.GetFileName(fileName);
        if (string.IsNullOrWhiteSpace(safeName) || safeName != fileName || safeName.Length > 255)
            throw new DocumentIngestionException("The file name is invalid.");
        var extension = Path.GetExtension(safeName).ToLowerInvariant();
        if (!options.Value.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase) || !ContentTypes.ContainsKey(extension))
            throw new DocumentIngestionException("Only PDF, DOCX, and TXT files are supported.");
        if (!ContentTypes[extension].Contains(contentType, StringComparer.OrdinalIgnoreCase))
            throw new DocumentIngestionException("The file content type does not match a supported format.");

        using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, cancellationToken);
        if (buffer.Length == 0) throw new DocumentIngestionException("The uploaded file is empty.");
        buffer.Position = 0;
        var header = new byte[Math.Min(8, (int)buffer.Length)];
        await buffer.ReadExactlyAsync(header, cancellationToken);
        buffer.Position = 0;
        if (extension == ".pdf" && !header.AsSpan().StartsWith("%PDF"u8))
            throw new DocumentIngestionException("The uploaded file is not a valid PDF.");
        if (extension == ".docx" && (header.Length < 2 || !(header[0] == (byte)'P' && header[1] == (byte)'K')))
            throw new DocumentIngestionException("The uploaded file is not a valid DOCX package.");

        var result = extension switch
        {
            ".pdf" => await new PdfDocumentTextExtractor().ExtractAsync(buffer, safeName, contentType, cancellationToken),
            ".docx" => await new DocxDocumentTextExtractor().ExtractAsync(buffer, safeName, contentType, cancellationToken),
            ".txt" => await new TextDocumentTextExtractor().ExtractAsync(buffer, safeName, contentType, cancellationToken),
            _ => throw new DocumentIngestionException("Unsupported document format.")
        };
        if (string.IsNullOrWhiteSpace(result.Text))
            throw new DocumentIngestionException("The document contains no extractable text.");
        return result;
    }
}
