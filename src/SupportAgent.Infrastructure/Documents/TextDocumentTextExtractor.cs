using System.Text;
using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.Documents;

public sealed class TextDocumentTextExtractor
{
    public async Task<DocumentExtractionResult> ExtractAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true), detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        string text;
        try { text = await reader.ReadToEndAsync(cancellationToken); }
        catch (DecoderFallbackException) { throw new DocumentIngestionException("The text file is not valid UTF-8 text."); }
        if (text.Contains('\0')) throw new DocumentIngestionException("The uploaded file does not appear to be plain text.");
        return new DocumentExtractionResult { FileName = fileName, ContentType = contentType, Text = text };
    }
}
