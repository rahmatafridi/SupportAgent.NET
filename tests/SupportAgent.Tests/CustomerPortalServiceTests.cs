using Microsoft.EntityFrameworkCore;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data;
using SupportAgent.Infrastructure.Services;

namespace SupportAgent.Tests;

public class CustomerPortalServiceTests
{
    [Fact]
    public async Task Customer_can_create_and_reply_only_to_own_ticket_and_order()
    {
        var tenant = Guid.NewGuid(); var user = Guid.NewGuid(); var otherUser = Guid.NewGuid();
        await using var db = new SupportAgentDbContext(new DbContextOptionsBuilder<SupportAgentDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, new Context(tenant, user));
        db.Organizations.Add(new Organization { Id = tenant, Name = "Portal Org", Slug = "portal", IsActive = true });
        db.Customers.AddRange(new Customer { Id = 1, OrganizationId = tenant, ApplicationUserId = user, FirstName = "Jane", LastName = "Doe", Email = "jane@example.com" }, new Customer { Id = 2, OrganizationId = tenant, ApplicationUserId = otherUser, FirstName = "Other", LastName = "Customer", Email = "other@example.com" });
        db.Orders.AddRange(new Order { Id = 1, OrganizationId = tenant, CustomerId = 1, OrderNumber = "ORD-1", Status = "Processing" }, new Order { Id = 2, OrganizationId = tenant, CustomerId = 2, OrderNumber = "ORD-2", Status = "Processing" });
        db.Tickets.Add(new Ticket { Id = 20, OrganizationId = tenant, CustomerId = 2, Subject = "Private", Status = "Open", Priority = "Medium", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(); var service = new CustomerPortalService(db, new Context(tenant, user));
        var created = await service.CreateTicketAsync("My order", "Medium", "Please help", 1);
        Assert.NotNull(created); Assert.Equal(1, created.OrderId); Assert.Equal(1, (await db.Tickets.FindAsync(created.Id))!.CustomerId); Assert.Equal(1, (await db.Tickets.FindAsync(created.Id))!.OrderId); Assert.Equal("Customer", (await db.TicketMessages.SingleAsync()).SenderType);
        Assert.Null(await service.GetTicketAsync(20)); Assert.Null(await service.AddMessageAsync(20, "No access"));
        await Assert.ThrowsAsync<ArgumentException>(() => service.CreateTicketAsync("Wrong order", "Medium", "No", 2));
        Assert.NotNull(await service.AddMessageAsync(created.Id, "Any update?"));
        Assert.Single((await service.GetOrdersAsync())!);
    }

    private sealed class Context(Guid tenant, Guid user) : ICurrentUserContext
    { public Guid UserId => user; public Guid OrganizationId => tenant; public string Email => "jane@example.com"; public IReadOnlyCollection<string> Roles => ["Customer"]; public bool IsAuthenticated => true; }
}
