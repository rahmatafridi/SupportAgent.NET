using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;

namespace SupportAgent.Api.Endpoints;

/// <summary>
/// Request body for adding a knowledge document.
/// </summary>
/// <param name="Title">Document title.</param>
/// <param name="Content">Full document content to chunk and store.</param>
/// <param name="Source">Optional source label.</param>
public record AddKnowledgeDocumentRequest(string Title, string Content, string? Source);

/// <summary>
/// Knowledge base endpoints used to add documents and verify retrieval directly.
/// </summary>
public static class KnowledgeEndpoints
{
    /// <summary>
    /// Maps knowledge base routes to the application pipeline.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same route builder so calls can be chained.</returns>
    public static IEndpointRouteBuilder MapKnowledgeEndpoints(this IEndpointRouteBuilder app)
    {
        // POST /api/knowledge/documents
        // Adds a company knowledge document and stores searchable chunks through IKnowledgeService.
        app.MapPost("/api/knowledge/documents", async (
            AddKnowledgeDocumentRequest request,
            IKnowledgeService knowledgeService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Title))
            {
                return Results.BadRequest(new { error = "Title is required." });
            }

            if (string.IsNullOrWhiteSpace(request.Content))
            {
                return Results.BadRequest(new { error = "Content is required." });
            }

            try
            {
                var document = await knowledgeService.AddDocumentAsync(
                    request.Title,
                    request.Content,
                    request.Source,
                    cancellationToken);

                return Results.Created($"/api/knowledge/search?query={Uri.EscapeDataString(document.Title)}", document);
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        })
        .WithName("PostKnowledgeDocument")
        .WithSummary("Adds a company knowledge document.")
        .WithDescription("Stores the document and creates searchable chunks through IKnowledgeService.AddDocumentAsync.");

        // GET /api/knowledge/search?query=refund
        // Verifies lexical retrieval directly without going through the LLM.
        app.MapGet("/api/knowledge/search", async (
            string query,
            IKnowledgeService knowledgeService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Results.BadRequest(new { error = "Query is required." });
            }

            try
            {
                var results = await knowledgeService.SearchAsync(query, cancellationToken: cancellationToken);
                return Results.Ok(results);
            }
            catch (ArgumentException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        })
        .WithName("SearchKnowledge")
        .WithSummary("Searches the company knowledge base.")
        .WithDescription("Returns ranked knowledge chunks from IKnowledgeService.SearchAsync for direct hybrid retrieval testing.");

        // POST /api/knowledge/embeddings/rebuild
        // Generates embeddings for chunks created before Phase 6 or missing embeddings.
        app.MapPost("/api/knowledge/embeddings/rebuild", async (
            IKnowledgeService knowledgeService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var updatedChunks = await knowledgeService.GenerateMissingEmbeddingsAsync(cancellationToken);
                return Results.Ok(new { updatedChunks });
            }
            catch (InvalidOperationException exception)
            {
                return Results.BadRequest(new { error = exception.Message });
            }
        })
        .WithName("RebuildKnowledgeEmbeddings")
        .WithSummary("Generates missing knowledge chunk embeddings.")
        .WithDescription("Temporary admin-oriented endpoint that backfills embeddings for chunks missing EmbeddingJson.");

        return app;
    }
}
