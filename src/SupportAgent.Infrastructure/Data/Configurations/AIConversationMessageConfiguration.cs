using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core mapping for <see cref="AIConversationMessage"/>.
/// </summary>
public class AIConversationMessageConfiguration : IEntityTypeConfiguration<AIConversationMessage>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AIConversationMessage> builder)
    {
        builder.ToTable("AIConversationMessages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Role)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(message => message.Content)
            .IsRequired();

        builder.Property(message => message.CreatedAt).IsRequired();
    }
}
