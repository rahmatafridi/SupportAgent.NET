using System.Text;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.Extensions.Options;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Documents;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace SupportAgent.Tests;

public class DocumentExtractionTests
{
    private readonly DocumentTextExtractor _extractor = new(Options.Create(new KnowledgeUploadOptions()));

    [Fact]
    public async Task Extracts_utf8_text()
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes("Refunds are available within thirty days."));
        var result = await _extractor.ExtractAsync(stream, "refunds.txt", "text/plain");
        Assert.Contains("thirty days", result.Text);
    }

    [Fact]
    public async Task Extracts_docx_text()
    {
        await using var stream = CreateDocx("DOCX return policy text");
        var result = await _extractor.ExtractAsync(stream, "policy.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        Assert.Contains("DOCX return policy", result.Text);
    }

    [Fact]
    public async Task Extracts_pdf_text()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4);
        page.AddText("PDF shipping policy text", 12, new PdfPoint(50, 750), font);
        await using var stream = new MemoryStream(builder.Build());
        var result = await _extractor.ExtractAsync(stream, "shipping.pdf", "application/pdf");
        Assert.Contains("PDF shipping policy", result.Text); Assert.Equal(1, result.PageCount);
    }

    [Theory]
    [InlineData("malware.exe", "application/octet-stream")]
    [InlineData("fake.pdf", "text/plain")]
    public async Task Rejects_unsupported_or_mismatched_files(string name, string contentType)
    {
        await using var stream = new MemoryStream("not a document"u8.ToArray());
        await Assert.ThrowsAsync<DocumentIngestionException>(() => _extractor.ExtractAsync(stream, name, contentType));
    }

    [Fact]
    public async Task Rejects_empty_file_and_empty_extracted_text()
    {
        await Assert.ThrowsAsync<DocumentIngestionException>(() => _extractor.ExtractAsync(new MemoryStream(), "empty.txt", "text/plain"));
        await Assert.ThrowsAsync<DocumentIngestionException>(() => _extractor.ExtractAsync(new MemoryStream("   "u8.ToArray()), "blank.txt", "text/plain"));
    }

    internal static MemoryStream CreateDocx(string text)
    {
        var stream = new MemoryStream();
        using (var document = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document, true))
        {
            var main = document.AddMainDocumentPart();
            main.Document = new Document(new Body(new Paragraph(new Run(new Text(text)))));
        }
        stream.Position = 0; return stream;
    }
}
