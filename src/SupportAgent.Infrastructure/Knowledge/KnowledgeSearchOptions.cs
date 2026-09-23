namespace SupportAgent.Infrastructure.Knowledge;

/// <summary>
/// Configurable hybrid knowledge search settings.
/// </summary>
public class KnowledgeSearchOptions
{
    /// <summary>Configuration section name in appsettings.</summary>
    public const string SectionName = "KnowledgeSearch";

    /// <summary>Weight applied to semantic cosine similarity scores.</summary>
    public double SemanticWeight { get; set; } = 0.75;

    /// <summary>Weight applied to lexical term-match scores.</summary>
    public double LexicalWeight { get; set; } = 0.25;

    /// <summary>Default maximum number of ranked chunks to return.</summary>
    public int MaxResults { get; set; } = 5;

    /// <summary>Minimum hybrid score required for a chunk to be returned.</summary>
    public double MinimumScore { get; set; } = 0.25;
}
