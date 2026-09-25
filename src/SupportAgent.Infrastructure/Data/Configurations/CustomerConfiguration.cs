using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Identity;

namespace SupportAgent.Infrastructure.Data.Configurations;

/// <summary>
/// EF Core mapping for the <see cref="Customer"/> table and its relationships.
/// </summary>
public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    /// <summary>
    /// Configures columns, indexes, and relationships for customers.
    /// </summary>
    /// <param name="builder">The entity builder for <see cref="Customer"/>.</param>
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("Customers");

        builder.HasKey(customer => customer.Id);

        builder.Property(customer => customer.FirstName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(customer => customer.LastName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(customer => customer.Email)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(customer => new { customer.OrganizationId, customer.Email })
            .IsUnique();
        builder.HasIndex(customer => customer.ApplicationUserId).IsUnique().HasFilter("[ApplicationUserId] IS NOT NULL");
        builder.HasOne<ApplicationUser>().WithOne().HasForeignKey<Customer>(customer => customer.ApplicationUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Property(customer => customer.Phone)
            .HasMaxLength(30);

        builder.Property(customer => customer.CreatedAt)
            .IsRequired();

        builder.HasMany(customer => customer.Orders)
            .WithOne(order => order.Customer)
            .HasForeignKey(order => order.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(customer => customer.Tickets)
            .WithOne(ticket => ticket.Customer)
            .HasForeignKey(ticket => ticket.CustomerId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
