using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core mapping for the <see cref="KnowledgeChunk"/> table.
/// </summary>
public class KnowledgeChunkConfiguration : IEntityTypeConfiguration<KnowledgeChunk>
{
    /// <summary>
    /// Configures columns and indexes for searchable knowledge chunks.
    /// </summary>
    /// <param name="builder">The entity builder for <see cref="KnowledgeChunk"/>.</param>
    public void Configure(EntityTypeBuilder<KnowledgeChunk> builder)
    {
        builder.ToTable("KnowledgeChunks");

        builder.HasKey(chunk => chunk.Id);

        builder.Property(chunk => chunk.Content)
            .IsRequired()
            .HasMaxLength(8000);

        builder.Property(chunk => chunk.ChunkIndex)
            .IsRequired();

        builder.Property(chunk => chunk.CreatedAt)
            .IsRequired();

        builder.Property(chunk => chunk.EmbeddingJson)
            .HasColumnType("nvarchar(max)");

        builder.HasIndex(chunk => chunk.KnowledgeDocumentId);
    }
}
