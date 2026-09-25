using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core mapping for <see cref="AIConversation"/>.
/// </summary>
public class AIConversationConfiguration : IEntityTypeConfiguration<AIConversation>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AIConversation> builder)
    {
        builder.ToTable("AIConversations");

        builder.HasKey(conversation => conversation.Id);

        builder.Property(conversation => conversation.CreatedAt).IsRequired();
        builder.Property(conversation => conversation.UpdatedAt).IsRequired();

        builder.HasMany(conversation => conversation.Messages)
            .WithOne(message => message.Conversation)
            .HasForeignKey(message => message.AIConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(conversation => conversation.ToolAudits)
            .WithOne(audit => audit.Conversation)
            .HasForeignKey(audit => audit.AIConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(conversation => conversation.SuggestedActions)
            .WithOne(action => action.Conversation)
            .HasForeignKey(action => action.AIConversationId)
            .OnDelete(DeleteBehavior.Cascade);

    }
}
