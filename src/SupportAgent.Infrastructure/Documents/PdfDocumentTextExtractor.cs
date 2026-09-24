using System.Text;
using SupportAgent.Core.Models;
using UglyToad.PdfPig;

namespace SupportAgent.Infrastructure.Documents;

public sealed class PdfDocumentTextExtractor
{
    public Task<DocumentExtractionResult> ExtractAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken)
    {
        try
        {
            using var document = PdfDocument.Open(stream);
            var text = new StringBuilder();
            foreach (var page in document.GetPages())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (text.Length > 0) text.AppendLine().AppendLine();
                text.Append(page.Text);
            }
            if (string.IsNullOrWhiteSpace(text.ToString()))
                throw new DocumentIngestionException("The PDF contains no extractable text. Scanned PDFs require OCR, which is not supported yet.");
            return Task.FromResult(new DocumentExtractionResult { FileName = fileName, ContentType = contentType, Text = text.ToString(), PageCount = document.NumberOfPages });
        }
        catch (DocumentIngestionException) { throw; }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new DocumentIngestionException("The PDF could not be read.");
        }
    }
}
