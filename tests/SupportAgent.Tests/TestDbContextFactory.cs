using Microsoft.EntityFrameworkCore;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data;

namespace SupportAgent.Tests;

public static class TestDbContextFactory
{
    public static SupportAgentDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<SupportAgentDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;

        var context = new SupportAgentDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }

    public static async Task SeedSampleDataAsync(SupportAgentDbContext context)
    {
        var createdAt = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        context.Customers.Add(new Customer
        {
            Id = 101,
            FirstName = "John",
            LastName = "Smith",
            Email = "john@example.com",
            CreatedAt = createdAt
        });

        context.Orders.AddRange(
            new Order
            {
                Id = 1,
                CustomerId = 101,
                OrderNumber = "ORD-1001",
                Status = "Processing",
                TotalAmount = 129.99m,
                CreatedAt = createdAt
            },
            new Order
            {
                Id = 2,
                CustomerId = 101,
                OrderNumber = "ORD-1002",
                Status = "Shipped",
                TotalAmount = 89.50m,
                CreatedAt = createdAt
            });

        context.Tickets.Add(new Ticket
        {
            Id = 1001,
            CustomerId = 101,
            Subject = "Where is my order?",
            Status = "Open",
            Priority = "High",
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        });

        context.TicketMessages.AddRange(
            new TicketMessage
            {
                Id = 1,
                TicketId = 1001,
                SenderType = "Customer",
                Message = "Where is my order?",
                CreatedAt = createdAt
            },
            new TicketMessage
            {
                Id = 2,
                TicketId = 1001,
                SenderType = "Agent",
                Message = "Checking now.",
                CreatedAt = createdAt.AddMinutes(5)
            });

        await context.SaveChangesAsync();
    }
}
