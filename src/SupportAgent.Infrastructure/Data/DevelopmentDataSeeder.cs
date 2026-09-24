using Microsoft.EntityFrameworkCore;
using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.Data;

/// <summary>
/// Seeds development-only sample customers, orders, tickets, and messages when the database is empty.
/// </summary>
public static class DevelopmentDataSeeder
{
    /// <summary>
    /// Inserts sample support data if no customers exist yet.
    /// </summary>
    /// <param name="dbContext">The database context used to write seed data.</param>
    /// <param name="cancellationToken">Token used to cancel the seed operation.</param>
    /// <returns>A task that completes when seeding has finished or been skipped.</returns>
    public static async Task SeedAsync(
        SupportAgentDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        if (await dbContext.Customers.IgnoreQueryFilters().AnyAsync(cancellationToken))
        {
            return;
        }

        var seedCreatedAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        var customers = new[]
        {
            new Customer
            {
                Id = 101,
                FirstName = "John",
                LastName = "Smith",
                Email = "john@example.com",
                Phone = "555-0101",
                CreatedAt = seedCreatedAt
            },
            new Customer
            {
                Id = 102,
                FirstName = "Sarah",
                LastName = "Khan",
                Email = "sarah@example.com",
                Phone = "555-0102",
                CreatedAt = seedCreatedAt
            }
        };

        var orders = new[]
        {
            new Order
            {
                Id = 1,
                CustomerId = 101,
                OrderNumber = "ORD-1001",
                Status = "Processing",
                TotalAmount = 129.99m,
                CreatedAt = seedCreatedAt.AddDays(1)
            },
            new Order
            {
                Id = 2,
                CustomerId = 101,
                OrderNumber = "ORD-1002",
                Status = "Shipped",
                TotalAmount = 89.50m,
                CreatedAt = seedCreatedAt.AddDays(2)
            },
            new Order
            {
                Id = 3,
                CustomerId = 102,
                OrderNumber = "ORD-1003",
                Status = "Delivered",
                TotalAmount = 249.00m,
                CreatedAt = seedCreatedAt.AddDays(3)
            }
        };

        var tickets = new[]
        {
            new Ticket
            {
                Id = 1001,
                CustomerId = 101,
                Subject = "Where is my order?",
                Status = "Open",
                Priority = "High",
                CreatedAt = seedCreatedAt.AddDays(4)
            },
            new Ticket
            {
                Id = 1002,
                CustomerId = 102,
                Subject = "Need help with delivery",
                Status = "Open",
                Priority = "Medium",
                CreatedAt = seedCreatedAt.AddDays(5)
            }
        };

        var ticketMessages = new[]
        {
            new TicketMessage
            {
                Id = 1,
                TicketId = 1001,
                SenderType = "Customer",
                Message = "Hi, I placed order ORD-1001 last week and haven't received any shipping updates.",
                CreatedAt = seedCreatedAt.AddDays(4).AddHours(1)
            },
            new TicketMessage
            {
                Id = 2,
                TicketId = 1001,
                SenderType = "Agent",
                Message = "Thanks for reaching out. I can see ORD-1001 is currently processing and should ship within 24 hours.",
                CreatedAt = seedCreatedAt.AddDays(4).AddHours(2)
            },
            new TicketMessage
            {
                Id = 3,
                TicketId = 1001,
                SenderType = "Customer",
                Message = "Great, please let me know once it ships.",
                CreatedAt = seedCreatedAt.AddDays(4).AddHours(3)
            },
            new TicketMessage
            {
                Id = 4,
                TicketId = 1002,
                SenderType = "Customer",
                Message = "My order ORD-1003 shows delivered, but I did not receive the package.",
                CreatedAt = seedCreatedAt.AddDays(5).AddHours(1)
            },
            new TicketMessage
            {
                Id = 5,
                TicketId = 1002,
                SenderType = "Agent",
                Message = "I'm sorry to hear that. Can you confirm the delivery address on your account is correct?",
                CreatedAt = seedCreatedAt.AddDays(5).AddHours(2)
            },
            new TicketMessage
            {
                Id = 6,
                TicketId = 1002,
                SenderType = "Customer",
                Message = "Yes, the address is correct. Could you check with the carrier?",
                CreatedAt = seedCreatedAt.AddDays(5).AddHours(3)
            }
        };

        if (dbContext.Database.IsSqlServer())
        {
            await SeedWithIdentityInsertAsync(
                dbContext,
                customers,
                orders,
                tickets,
                ticketMessages,
                cancellationToken);
            return;
        }

        dbContext.Customers.AddRange(customers);
        dbContext.Orders.AddRange(orders);
        dbContext.Tickets.AddRange(tickets);
        dbContext.TicketMessages.AddRange(ticketMessages);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Seeds fixed IDs on SQL Server by temporarily enabling IDENTITY_INSERT for each table.
    /// </summary>
    private static async Task SeedWithIdentityInsertAsync(
        SupportAgentDbContext dbContext,
        IReadOnlyList<Customer> customers,
        IReadOnlyList<Order> orders,
        IReadOnlyList<Ticket> tickets,
        IReadOnlyList<TicketMessage> ticketMessages,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "SET IDENTITY_INSERT Customers ON",
            cancellationToken);
        dbContext.Customers.AddRange(customers);
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "SET IDENTITY_INSERT Customers OFF",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "SET IDENTITY_INSERT Orders ON",
            cancellationToken);
        dbContext.Orders.AddRange(orders);
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "SET IDENTITY_INSERT Orders OFF",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "SET IDENTITY_INSERT Tickets ON",
            cancellationToken);
        dbContext.Tickets.AddRange(tickets);
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "SET IDENTITY_INSERT Tickets OFF",
            cancellationToken);

        await dbContext.Database.ExecuteSqlRawAsync(
            "SET IDENTITY_INSERT TicketMessages ON",
            cancellationToken);
        dbContext.TicketMessages.AddRange(ticketMessages);
        await dbContext.SaveChangesAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync(
            "SET IDENTITY_INSERT TicketMessages OFF",
            cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
