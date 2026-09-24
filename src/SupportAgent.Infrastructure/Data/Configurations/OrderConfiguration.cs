using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core mapping for the <see cref="Order"/> table.
/// </summary>
public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    /// <summary>
    /// Configures columns and indexes for orders.
    /// </summary>
    /// <param name="builder">The entity builder for <see cref="Order"/>.</param>
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");

        builder.HasKey(order => order.Id);

        builder.Property(order => order.OrderNumber)
            .IsRequired()
            .HasMaxLength(50);

        builder.HasIndex(order => new { order.OrganizationId, order.OrderNumber })
            .IsUnique();

        builder.Property(order => order.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(order => order.TotalAmount)
            .HasPrecision(18, 2);

        builder.Property(order => order.CreatedAt)
            .IsRequired();
    }
}
