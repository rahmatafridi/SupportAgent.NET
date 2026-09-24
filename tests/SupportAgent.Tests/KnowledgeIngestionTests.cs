using Microsoft.EntityFrameworkCore;
using System.Text;
using Microsoft.Extensions.Options;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data;
using SupportAgent.Infrastructure.Documents;
using SupportAgent.Infrastructure.Services;

namespace SupportAgent.Tests;

public class KnowledgeIngestionTests
{
    [Fact]
    public async Task Upload_creates_tenant_scoped_embedded_searchable_chunks_and_delete_cascades()
    {
        var tenant = Guid.NewGuid(); var user = Guid.NewGuid();
        await using var db = CreateContext($"ingest-{Guid.NewGuid()}", tenant, user);
        db.Organizations.Add(new Organization { Id = tenant, Name = "Tenant", Slug = Guid.NewGuid().ToString("N"), IsActive = true, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var context = new UserContext(tenant, user);
        var knowledge = KnowledgeServiceTestFactory.Create(db, currentUser: context);
        var service = new KnowledgeIngestionService(db, new DocumentTextExtractor(Options.Create(new KnowledgeUploadOptions())), knowledge, context, Options.Create(new KnowledgeUploadOptions()));
        var text = "Our uploaded return policy allows a refund within thirty days of purchase.";
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var result = await service.UploadDocumentAsync(stream, "returns-policy.txt", "text/plain", stream.Length);

        var document = await db.KnowledgeDocuments.Include(x => x.Chunks).SingleAsync();
        Assert.Equal(tenant, document.OrganizationId); Assert.Equal(user, document.UploadedByUserId); Assert.Equal("Completed", document.ProcessingStatus);
        Assert.NotEmpty(document.Chunks); Assert.All(document.Chunks, chunk => Assert.False(string.IsNullOrWhiteSpace(chunk.EmbeddingJson)));
        Assert.Contains(await knowledge.SearchAsync("refund return purchase"), item => item.DocumentId == result.DocumentId);
        Assert.True(await service.DeleteDocumentAsync(result.DocumentId));
        Assert.Empty(await db.KnowledgeChunks.ToListAsync());
    }

    [Fact]
    public async Task Oversized_upload_is_rejected_and_failed_extraction_is_recorded()
    {
        var tenant = Guid.NewGuid(); var user = Guid.NewGuid();
        await using var db = CreateContext($"failed-{Guid.NewGuid()}", tenant, user);
        db.Organizations.Add(new Organization { Id = tenant, Name = "Tenant", Slug = Guid.NewGuid().ToString("N"), IsActive = true, CreatedAt = DateTime.UtcNow }); await db.SaveChangesAsync();
        var options = Options.Create(new KnowledgeUploadOptions { MaxFileSizeMb = 1 });
        var context = new UserContext(tenant, user);
        var service = new KnowledgeIngestionService(db, new DocumentTextExtractor(options), KnowledgeServiceTestFactory.Create(db, currentUser: context), context, options);
        await Assert.ThrowsAsync<DocumentIngestionException>(() => service.UploadDocumentAsync(new MemoryStream([1]), "big.txt", "text/plain", 2 * 1024 * 1024));
        await Assert.ThrowsAsync<DocumentIngestionException>(() => service.UploadDocumentAsync(new MemoryStream(" "u8.ToArray()), "blank.txt", "text/plain", 1));
        Assert.Equal("Failed", (await db.KnowledgeDocuments.SingleAsync()).ProcessingStatus);
    }

    [Fact]
    public async Task Another_tenant_cannot_read_or_delete_document()
    {
        var name = $"cross-doc-{Guid.NewGuid()}"; var a = Guid.NewGuid(); var b = Guid.NewGuid();
        await using (var db = CreateContext(name, a, Guid.NewGuid()))
        { db.Organizations.AddRange(new Organization { Id = a, Name = "A", Slug = "a", IsActive = true }, new Organization { Id = b, Name = "B", Slug = "b", IsActive = true }); db.KnowledgeDocuments.Add(new KnowledgeDocument { Id = 77, OrganizationId = a, Title = "A", ProcessingStatus = "Completed", CreatedAt = DateTime.UtcNow }); await db.SaveChangesAsync(); }
        await using var tenantB = CreateContext(name, b, Guid.NewGuid());
        var service = new KnowledgeIngestionService(tenantB, null!, null!, new UserContext(b, Guid.NewGuid()), Options.Create(new KnowledgeUploadOptions()));
        Assert.Null(await service.GetDocumentAsync(77)); Assert.False(await service.DeleteDocumentAsync(77));
    }

    [Fact]
    public async Task Embedding_failure_is_recorded_and_returns_safe_actionable_error()
    {
        var tenant = Guid.NewGuid(); var user = Guid.NewGuid();
        await using var db = CreateContext($"embedding-failed-{Guid.NewGuid()}", tenant, user);
        db.Organizations.Add(new Organization { Id = tenant, Name = "Tenant", Slug = Guid.NewGuid().ToString("N"), IsActive = true });
        await db.SaveChangesAsync();
        var context = new UserContext(tenant, user);
        var knowledge = KnowledgeServiceTestFactory.Create(db, new FailingEmbeddingService(), currentUser: context);
        var options = Options.Create(new KnowledgeUploadOptions());
        var service = new KnowledgeIngestionService(db, new DocumentTextExtractor(options), knowledge, context, options);
        await using var stream = new MemoryStream("Useful company documentation."u8.ToArray());

        var exception = await Assert.ThrowsAsync<DocumentIngestionException>(() =>
            service.UploadDocumentAsync(stream, "company.txt", "text/plain", stream.Length));

        Assert.Contains("Ensure Ollama is running", exception.Message);
        var document = await db.KnowledgeDocuments.SingleAsync();
        Assert.Equal("Failed", document.ProcessingStatus);
        Assert.Equal(exception.Message, document.ProcessingError);
        Assert.Empty(await db.KnowledgeChunks.ToListAsync());
    }

    private static SupportAgentDbContext CreateContext(string name, Guid tenant, Guid user) => new(new DbContextOptionsBuilder<SupportAgentDbContext>().UseInMemoryDatabase(name).Options, new UserContext(tenant, user));
    private sealed class FailingEmbeddingService : IEmbeddingService
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default) => throw new HttpRequestException("Connection refused");
        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default) => throw new HttpRequestException("Connection refused");
    }
    private sealed class UserContext(Guid tenant, Guid user) : ICurrentUserContext { public Guid UserId => user; public Guid OrganizationId => tenant; public string Email => "test@example.com"; public IReadOnlyCollection<string> Roles => ["Admin"]; public bool IsAuthenticated => true; }
}
