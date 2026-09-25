using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.Data.Configurations;

public class AISuggestedActionConfiguration : IEntityTypeConfiguration<AISuggestedAction>
{
    public void Configure(EntityTypeBuilder<AISuggestedAction> builder)
    {
        builder.ToTable("AISuggestedActions");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.Label).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Description).HasMaxLength(1000);
        builder.Property(item => item.ActionType).HasConversion<string>().HasMaxLength(50);
        builder.Property(item => item.Status).HasConversion<string>().HasMaxLength(30);
        builder.Property(item => item.TrustedArgumentsJson).HasMaxLength(2000);
        builder.HasIndex(item => new { item.OrganizationId, item.AIConversationId, item.Status });
    }
}
