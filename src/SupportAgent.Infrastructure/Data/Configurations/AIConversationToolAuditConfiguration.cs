using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core mapping for <see cref="AIConversationToolAudit"/>.
/// </summary>
public class AIConversationToolAuditConfiguration : IEntityTypeConfiguration<AIConversationToolAudit>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AIConversationToolAudit> builder)
    {
        builder.ToTable("AIConversationToolAudits");

        builder.HasKey(audit => audit.Id);

        builder.Property(audit => audit.ToolName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(audit => audit.ArgumentsJson)
            .IsRequired();

        builder.Property(audit => audit.CreatedAt).IsRequired();
    }
}
