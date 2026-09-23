using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.Knowledge.Internal;

/// <summary>
/// Internal hybrid ranking result used before mapping to public search DTOs.
/// </summary>
/// <param name="Chunk">The ranked knowledge chunk.</param>
/// <param name="FinalScore">Combined hybrid relevance score.</param>
/// <param name="SemanticScore">Semantic cosine similarity score.</param>
/// <param name="LexicalScore">Lexical term-match score.</param>
internal sealed record RankedKnowledgeChunk(
    KnowledgeChunk Chunk,
    double FinalScore,
    double SemanticScore,
    double LexicalScore);
