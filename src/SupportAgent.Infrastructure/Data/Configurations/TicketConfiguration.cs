using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core mapping for the <see cref="Ticket"/> table and its messages.
/// </summary>
public class TicketConfiguration : IEntityTypeConfiguration<Ticket>
{
    /// <summary>
    /// Configures columns and the one-to-many relationship to ticket messages.
    /// </summary>
    /// <param name="builder">The entity builder for <see cref="Ticket"/>.</param>
    public void Configure(EntityTypeBuilder<Ticket> builder)
    {
        builder.ToTable("Tickets");

        builder.HasKey(ticket => ticket.Id);

        builder.Property(ticket => ticket.Subject)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(ticket => ticket.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(ticket => ticket.Priority)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(ticket => ticket.CreatedAt)
            .IsRequired();

        builder.HasMany(ticket => ticket.Messages)
            .WithOne(message => message.Ticket)
            .HasForeignKey(message => message.TicketId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
