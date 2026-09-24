using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data;
using SupportAgent.Infrastructure.Knowledge;
using SupportAgent.Infrastructure.Identity;

namespace SupportAgent.Infrastructure.Services;

/// <summary>
/// Stores company knowledge documents and performs hybrid semantic and lexical retrieval.
/// </summary>
public class KnowledgeService : IKnowledgeService
{
    private readonly SupportAgentDbContext _dbContext;
    private readonly ITextChunker _textChunker;
    private readonly IEmbeddingService _embeddingService;
    private readonly KnowledgeSearchOptions _searchOptions;
    private readonly ILogger<KnowledgeService> _logger;
    private readonly ICurrentUserContext _currentUser;

    /// <summary>
    /// Creates a new knowledge service instance.
    /// </summary>
    public KnowledgeService(
        SupportAgentDbContext dbContext,
        ITextChunker textChunker,
        IEmbeddingService embeddingService,
        IOptions<KnowledgeSearchOptions> searchOptions,
        ILogger<KnowledgeService> logger,
        ICurrentUserContext? currentUser = null)
    {
        _dbContext = dbContext;
        _textChunker = textChunker;
        _embeddingService = embeddingService;
        _searchOptions = searchOptions.Value;
        _logger = logger;
        _currentUser = currentUser ?? new DefaultCurrentUserContext();
    }

    /// <inheritdoc />
    public async Task<KnowledgeDocument> AddDocumentAsync(
        string title,
        string content,
        string? source = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ArgumentException("Title is required.", nameof(title));
        }

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content is required.", nameof(content));
        }

        var createdAt = DateTime.UtcNow;
        var document = new KnowledgeDocument
        {
            OrganizationId = _currentUser.OrganizationId,
            Title = title.Trim(),
            Source = source?.Trim(),
            ContentType = "text",
            CreatedAt = createdAt
        };

        _dbContext.KnowledgeDocuments.Add(document);
        await _dbContext.SaveChangesAsync(cancellationToken);

        var chunkTexts = _textChunker.Chunk(content);
        var chunkEntities = new List<KnowledgeChunk>();

        for (var index = 0; index < chunkTexts.Count; index++)
        {
            chunkEntities.Add(new KnowledgeChunk
            {
                OrganizationId = _currentUser.OrganizationId,
                KnowledgeDocumentId = document.Id,
                Content = chunkTexts[index],
                ChunkIndex = index,
                CreatedAt = createdAt
            });
        }

        if (chunkEntities.Count > 0)
        {
            try
            {
                var embeddings = await _embeddingService.GenerateEmbeddingsAsync(
                    chunkEntities.Select(chunk => chunk.Content).ToList(),
                    cancellationToken);

                for (var index = 0; index < chunkEntities.Count; index++)
                {
                    chunkEntities[index].EmbeddingJson =
                        EmbeddingJsonSerializer.Serialize(embeddings[index]);
                }
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "Failed to generate embeddings while ingesting document '{Title}'.", title);
                throw new InvalidOperationException(
                    "Failed to generate embeddings for the knowledge document. Ensure the embedding provider is configured and running.",
                    exception);
            }
        }

        _dbContext.KnowledgeChunks.AddRange(chunkEntities);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return document;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        string query,
        int maxResults = 5,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new ArgumentException("Query is required.", nameof(query));
        }

        if (maxResults <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxResults), "Max results must be greater than zero.");
        }

        var terms = LexicalKnowledgeScorer.ExtractSearchTerms(query);
        if (terms.Count == 0)
        {
            throw new ArgumentException("Query must contain at least one searchable term.", nameof(query));
        }

        var chunks = await _dbContext.KnowledgeChunks
            .AsNoTracking()
            .Include(chunk => chunk.KnowledgeDocument)
            .ToListAsync(cancellationToken);

        float[]? queryEmbedding = null;
        try
        {
            queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                exception,
                "Query embedding generation failed. Falling back to lexical-only hybrid scoring.");
        }

        var ranked = HybridKnowledgeRanker.Rank(
            chunks,
            terms,
            queryEmbedding,
            _searchOptions,
            maxResults);

        return ranked
            .Select(result => new KnowledgeSearchResult
            {
                DocumentId = result.Chunk.KnowledgeDocumentId,
                DocumentTitle = result.Chunk.KnowledgeDocument.Title,
                DocumentSource = result.Chunk.KnowledgeDocument.Source,
                ChunkId = result.Chunk.Id,
                Content = result.Chunk.Content,
                Score = Math.Round(result.FinalScore, 2)
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<int> GenerateMissingEmbeddingsAsync(CancellationToken cancellationToken = default)
    {
        var chunks = await _dbContext.KnowledgeChunks
            .Where(chunk => chunk.EmbeddingJson == null || chunk.EmbeddingJson == string.Empty)
            .OrderBy(chunk => chunk.Id)
            .ToListAsync(cancellationToken);

        if (chunks.Count == 0)
        {
            return 0;
        }

        var updatedCount = 0;

        foreach (var chunk in chunks)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var embedding = await _embeddingService.GenerateEmbeddingAsync(
                    chunk.Content,
                    cancellationToken);
                chunk.EmbeddingJson = EmbeddingJsonSerializer.Serialize(embedding);
                updatedCount++;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(
                    exception,
                    "Failed to generate embedding for knowledge chunk {ChunkId}.",
                    chunk.Id);
                throw new InvalidOperationException(
                    $"Failed to generate embedding for knowledge chunk {chunk.Id}.",
                    exception);
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return updatedCount;
    }
}
