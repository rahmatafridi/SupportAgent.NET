using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core mapping for the <see cref="TicketMessage"/> table.
/// </summary>
public class TicketMessageConfiguration : IEntityTypeConfiguration<TicketMessage>
{
    /// <summary>
    /// Configures columns for ticket conversation messages.
    /// </summary>
    /// <param name="builder">The entity builder for <see cref="TicketMessage"/>.</param>
    public void Configure(EntityTypeBuilder<TicketMessage> builder)
    {
        builder.ToTable("TicketMessages");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.SenderType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(message => message.Message)
            .IsRequired()
            .HasMaxLength(4000);

        builder.Property(message => message.CreatedAt)
            .IsRequired();
    }
}
