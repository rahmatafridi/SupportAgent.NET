namespace SupportAgent.Infrastructure.Documents;

public sealed class KnowledgeUploadOptions
{
    public const string SectionName = "KnowledgeUpload";
    public int MaxFileSizeMb { get; set; } = 10;
    public string[] AllowedExtensions { get; set; } = [".pdf", ".docx", ".txt"];
}
