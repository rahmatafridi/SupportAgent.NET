namespace SupportAgent.Core.Models;

/// <summary>
/// A grounded knowledge source cited in an AI response.
/// </summary>
public class KnowledgeSource
{
    /// <summary>Document title used to answer the question.</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>Optional document source label.</summary>
    public string? Source { get; set; }
}
