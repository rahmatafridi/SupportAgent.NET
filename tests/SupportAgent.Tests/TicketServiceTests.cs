using SupportAgent.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data;
using SupportAgent.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

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

    [Fact]
    public async Task Search_filters_and_sorting_are_combined()
    {
        await using var context = TestDbContextFactory.CreateContext(nameof(Search_filters_and_sorting_are_combined));
        await TestDbContextFactory.SeedSampleDataAsync(context);
        context.Tickets.Add(new Ticket { Id = 1002, CustomerId = 101, Subject = "Delivery delayed", Status = "Closed", Priority = "Low", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        var service = new TicketService(context);
        Assert.Single(await service.GetTicketsAsync(new TicketQuery { Search = "order", Status = "Open", Priority = "High" }));
        Assert.Equal(2, (await service.GetTicketsAsync(new TicketQuery { Search = "John Smith" })).Count);
        Assert.Equal(1002, (await service.GetTicketsAsync(new TicketQuery { Sort = "newest" }))[0].Id);
    }

    [Fact]
    public async Task Assignment_requires_same_tenant_support_user_and_updates_timestamp()
    {
        var tenant = Guid.NewGuid(); var otherTenant = Guid.NewGuid(); var current = Guid.NewGuid(); var assignee = Guid.NewGuid(); var outsider = Guid.NewGuid();
        await using var context = CreateContext(nameof(Assignment_requires_same_tenant_support_user_and_updates_timestamp), tenant, current);
        var role = new IdentityRole<Guid>(ApplicationRoles.SupportAgent) { Id = Guid.NewGuid(), NormalizedName = ApplicationRoles.SupportAgent.ToUpperInvariant() };
        context.Roles.Add(role);
        context.Users.AddRange(
            new ApplicationUser { Id = current, OrganizationId = tenant, DisplayName = "Current Agent", UserName = "current", IsActive = true },
            new ApplicationUser { Id = assignee, OrganizationId = tenant, DisplayName = "Other Agent", UserName = "other", IsActive = true },
            new ApplicationUser { Id = outsider, OrganizationId = otherTenant, DisplayName = "Outsider", UserName = "outside", IsActive = true });
        context.UserRoles.AddRange(new IdentityUserRole<Guid> { UserId = current, RoleId = role.Id }, new IdentityUserRole<Guid> { UserId = assignee, RoleId = role.Id }, new IdentityUserRole<Guid> { UserId = outsider, RoleId = role.Id });
        context.Customers.Add(new Customer { Id = 1, OrganizationId = tenant, FirstName = "A", LastName = "B", Email = "a@b.com" });
        context.Tickets.Add(new Ticket { Id = 1, OrganizationId = tenant, CustomerId = 1, Subject = "Test", Status = "Open", Priority = "Medium", CreatedAt = DateTime.UtcNow.AddDays(-1), UpdatedAt = DateTime.UtcNow.AddDays(-1) });
        await context.SaveChangesAsync(); var service = new TicketService(context, new UserContext(tenant, current));
        Assert.Equal(assignee, (await service.UpdateAssigneeAsync(1, assignee))!.AssignedToUserId);
        Assert.Equal(current, (await service.AssignToCurrentUserAsync(1))!.AssignedToUserId);
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateAssigneeAsync(1, outsider));
    }

    [Fact]
    public async Task Related_order_must_belong_to_ticket_customer()
    {
        var tenant = Guid.NewGuid();
        await using var context = CreateContext(nameof(Related_order_must_belong_to_ticket_customer), tenant, Guid.NewGuid());
        context.Customers.AddRange(
            new Customer { Id = 1, OrganizationId = tenant, FirstName = "Ticket", LastName = "Customer", Email = "one@example.com" },
            new Customer { Id = 2, OrganizationId = tenant, FirstName = "Other", LastName = "Customer", Email = "two@example.com" });
        context.Orders.AddRange(
            new Order { Id = 1, OrganizationId = tenant, CustomerId = 1, OrderNumber = "ORD-1", Status = "Processing" },
            new Order { Id = 2, OrganizationId = tenant, CustomerId = 2, OrderNumber = "ORD-2", Status = "Shipped" });
        context.Tickets.Add(new Ticket { Id = 1, OrganizationId = tenant, CustomerId = 1, Subject = "Missing", Status = "Open", Priority = "Medium", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
        var service = new TicketService(context, new UserContext(tenant, Guid.NewGuid()));

        Assert.Equal(1, (await service.UpdateOrderAsync(1, 1))!.OrderId);
        await Assert.ThrowsAsync<ArgumentException>(() => service.UpdateOrderAsync(1, 2));
        Assert.Equal(1, (await service.GetTicketAsync(1))!.OrderId);
    }

    [Fact]
    public async Task Notes_context_summary_and_updated_time_are_tenant_scoped()
    {
        var tenant = Guid.NewGuid(); var user = Guid.NewGuid();
        await using var context = CreateContext(nameof(Notes_context_summary_and_updated_time_are_tenant_scoped), tenant, user);
        var created = DateTime.UtcNow.AddDays(-2);
        context.Customers.Add(new Customer { Id = 10, OrganizationId = tenant, FirstName = "Jane", LastName = "Doe", Email = "jane@example.com", Phone = "123" });
        context.Orders.Add(new Order { Id = 10, OrganizationId = tenant, CustomerId = 10, OrderNumber = "ORD-10", Status = "Shipped", TotalAmount = 50, CreatedAt = created });
        context.Tickets.AddRange(
            new Ticket { Id = 10, OrganizationId = tenant, CustomerId = 10, Subject = "Current", Status = "Open", Priority = "High", CreatedAt = created, UpdatedAt = created },
            new Ticket { Id = 11, OrganizationId = tenant, CustomerId = 10, Subject = "Previous", Status = "Closed", Priority = "Low", CreatedAt = created, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync(); var service = new TicketService(context, new UserContext(tenant, user));
        var note = await service.AddInternalNoteAsync(10, "Waiting for carrier confirmation.");
        Assert.NotNull(note); Assert.Equal(user, note.AuthorUserId); Assert.True((await service.GetTicketAsync(10))!.UpdatedAt > created);
        Assert.Single((await service.GetCustomerContextAsync(10))!.RecentOrders); Assert.Single((await service.GetCustomerContextAsync(10))!.RecentTickets);
        var summary = await service.GetSummaryAsync(); Assert.Equal(1, summary.Open); Assert.Equal(1, summary.HighPriority); Assert.Equal(1, summary.Unassigned);
    }

    private static SupportAgentDbContext CreateContext(string name, Guid tenant, Guid user) =>
        new(new DbContextOptionsBuilder<SupportAgentDbContext>().UseInMemoryDatabase(name).Options, new UserContext(tenant, user));
    private sealed class UserContext(Guid tenant, Guid user) : ICurrentUserContext
    {
        public Guid UserId => user; public Guid OrganizationId => tenant; public string Email => "agent@example.com";
        public IReadOnlyCollection<string> Roles => ["SupportAgent"]; public bool IsAuthenticated => true;
    }
}
