using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.Documents;

public sealed class DocxDocumentTextExtractor
{
    public Task<DocumentExtractionResult> ExtractAsync(Stream stream, string fileName, string contentType, CancellationToken cancellationToken)
    {
        try
        {
            using var document = WordprocessingDocument.Open(stream, false);
            var mainDocument = document.MainDocumentPart;
            var body = mainDocument?.Document?.Body
                ?? throw new DocumentIngestionException("The DOCX file does not contain a readable document body.");
            var paragraphs = body.Descendants<Paragraph>()
                .Select(paragraph => string.Concat(paragraph.Descendants<Text>().Select(text => text.Text)))
                .Where(text => !string.IsNullOrWhiteSpace(text));
            return Task.FromResult(new DocumentExtractionResult { FileName = fileName, ContentType = contentType, Text = string.Join(Environment.NewLine, paragraphs) });
        }
        catch (DocumentIngestionException) { throw; }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new DocumentIngestionException("The DOCX file could not be read.");
        }
    }
}
