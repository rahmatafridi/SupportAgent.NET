using SupportAgent.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data;

namespace SupportAgent.Tests;

public class TicketServiceTests
{
    [Fact]
    public async Task Create_update_and_reply_workflow_is_tenant_scoped()
    {
        var tenant = Guid.NewGuid(); var user = Guid.NewGuid(); var database = $"tickets-{Guid.NewGuid()}";
        await using var context = CreateContext(database, tenant, user);
        context.Customers.Add(new Customer { Id = 501, OrganizationId = tenant, FirstName = "Test", LastName = "Customer", Email = "customer@example.com", CreatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        var service = new TicketService(context, new UserContext(tenant, user));

        var ticket = await service.CreateTicketAsync(501, "Order not received", "high", "My package has not arrived.");

        Assert.NotNull(ticket); Assert.Equal(tenant, ticket.OrganizationId); Assert.Equal("High", ticket.Priority); Assert.Equal("Open", ticket.Status);
        var initial = await context.TicketMessages.SingleAsync(); Assert.Equal("Customer", initial.SenderType); Assert.Equal(tenant, initial.OrganizationId);
        Assert.Equal("Closed", (await service.UpdateStatusAsync(ticket.Id, "closed"))!.Status);
        Assert.Equal("Low", (await service.UpdatePriorityAsync(ticket.Id, "low"))!.Priority);
        Assert.Equal("Agent", (await service.AddAgentMessageAsync(ticket.Id, "We are checking with the carrier."))!.SenderType);
        Assert.Equal("Customer", (await service.AddCustomerMessageAsync(ticket.Id, "Thank you."))!.SenderType);
    }

    [Fact]
    public async Task Cross_tenant_customer_and_ticket_mutations_return_not_found()
    {
        var database = $"cross-ticket-{Guid.NewGuid()}"; var tenantA = Guid.NewGuid(); var tenantB = Guid.NewGuid();
        await using (var a = CreateContext(database, tenantA, Guid.NewGuid()))
        {
            a.Customers.Add(new Customer { Id = 601, OrganizationId = tenantA, FirstName = "A", LastName = "Customer", Email = "a@example.com" });
            a.Tickets.Add(new Ticket { Id = 701, OrganizationId = tenantA, CustomerId = 601, Subject = "Private", Status = "Open", Priority = "Medium", CreatedAt = DateTime.UtcNow });
            await a.SaveChangesAsync();
        }
        await using var b = CreateContext(database, tenantB, Guid.NewGuid()); var service = new TicketService(b, new UserContext(tenantB, Guid.NewGuid()));
        Assert.Null(await service.CreateTicketAsync(601, "Subject", "Medium", "Message"));
        Assert.Null(await service.UpdateStatusAsync(701, "Closed"));
        Assert.Null(await service.AddAgentMessageAsync(701, "Reply"));
    }

    [Fact]
    public async Task GetTicketMessagesAsync_ReturnsMessagesInCreatedOrder()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(GetTicketMessagesAsync_ReturnsMessagesInCreatedOrder));
        await TestDbContextFactory.SeedSampleDataAsync(context);

        var service = new TicketService(context);
        var messages = await service.GetTicketMessagesAsync(1001);

        Assert.Equal(2, messages.Count);
        Assert.Equal("Customer", messages[0].SenderType);
        Assert.Equal("Agent", messages[1].SenderType);
    }

    [Fact]
    public async Task GetTicketAsync_ReturnsNull_WhenTicketDoesNotExist()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(GetTicketAsync_ReturnsNull_WhenTicketDoesNotExist));

        var service = new TicketService(context);
        var ticket = await service.GetTicketAsync(9999);

        Assert.Null(ticket);
    }

    private static SupportAgentDbContext CreateContext(string name, Guid tenant, Guid user) =>
        new(new DbContextOptionsBuilder<SupportAgentDbContext>().UseInMemoryDatabase(name).Options, new UserContext(tenant, user));
    private sealed class UserContext(Guid tenant, Guid user) : ICurrentUserContext
    {
        public Guid UserId => user; public Guid OrganizationId => tenant; public string Email => "agent@example.com";
        public IReadOnlyCollection<string> Roles => ["SupportAgent"]; public bool IsAuthenticated => true;
    }
}
