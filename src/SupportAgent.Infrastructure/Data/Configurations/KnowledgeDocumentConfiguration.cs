using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core mapping for the <see cref="KnowledgeDocument"/> table.
/// </summary>
public class KnowledgeDocumentConfiguration : IEntityTypeConfiguration<KnowledgeDocument>
{
    /// <summary>
    /// Configures columns and the one-to-many relationship to knowledge chunks.
    /// </summary>
    /// <param name="builder">The entity builder for <see cref="KnowledgeDocument"/>.</param>
    public void Configure(EntityTypeBuilder<KnowledgeDocument> builder)
    {
        builder.ToTable("KnowledgeDocuments");

        builder.HasKey(document => document.Id);

        builder.Property(document => document.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(document => document.Source)
            .HasMaxLength(100);

        builder.Property(document => document.ContentType)
            .HasMaxLength(100);

        builder.Property(document => document.CreatedAt)
            .IsRequired();

        builder.HasMany(document => document.Chunks)
            .WithOne(chunk => chunk.KnowledgeDocument)
            .HasForeignKey(chunk => chunk.KnowledgeDocumentId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
