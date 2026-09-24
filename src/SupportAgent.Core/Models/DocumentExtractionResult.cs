namespace SupportAgent.Core.Models;

public sealed class DocumentExtractionResult
{
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public int? PageCount { get; set; }
    public IReadOnlyDictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();
}
