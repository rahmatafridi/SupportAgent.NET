using Microsoft.EntityFrameworkCore;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data;
using SupportAgent.Infrastructure.Services;

namespace SupportAgent.Tests;

public class TenantIsolationTests
{
    [Fact]
    public async Task Global_filters_isolate_all_tenant_owned_support_and_AI_data()
    {
        var database = $"tenant-{Guid.NewGuid()}";
        var tenantA = Guid.NewGuid(); var tenantB = Guid.NewGuid();
        await using (var db = Create(database, tenantA))
        {
            db.Organizations.AddRange(Organization(tenantA, "a"), Organization(tenantB, "b"));
            db.Customers.Add(new Customer { Id = 11, OrganizationId = tenantA, FirstName = "A", LastName = "Customer", Email = "a@example.com", CreatedAt = DateTime.UtcNow });
            db.Orders.Add(new Order { Id = 12, OrganizationId = tenantA, CustomerId = 11, OrderNumber = "A-1", Status = "Open", CreatedAt = DateTime.UtcNow });
            db.Tickets.Add(new Ticket { Id = 13, OrganizationId = tenantA, CustomerId = 11, Subject = "A", Status = "Open", Priority = "Normal", CreatedAt = DateTime.UtcNow });
            db.TicketMessages.Add(new TicketMessage { Id = 14, OrganizationId = tenantA, TicketId = 13, SenderType = "Customer", Message = "A", CreatedAt = DateTime.UtcNow });
            db.KnowledgeDocuments.Add(new KnowledgeDocument { Id = 15, OrganizationId = tenantA, Title = "A", CreatedAt = DateTime.UtcNow });
            db.KnowledgeChunks.Add(new KnowledgeChunk { Id = 16, OrganizationId = tenantA, KnowledgeDocumentId = 15, Content = "tenant a secret", CreatedAt = DateTime.UtcNow });
            var conversationId = Guid.NewGuid();
            db.AIConversations.Add(new AIConversation { Id = conversationId, OrganizationId = tenantA, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
            db.AIConversationMessages.Add(new AIConversationMessage { Id = Guid.NewGuid(), OrganizationId = tenantA, AIConversationId = conversationId, Role = "user", Content = "A", CreatedAt = DateTime.UtcNow });
            db.AIConversationToolAudits.Add(new AIConversationToolAudit { Id = Guid.NewGuid(), OrganizationId = tenantA, AIConversationId = conversationId, ToolName = "GetCustomer", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }

        await using var tenantBDb = Create(database, tenantB);
        Assert.Empty(await tenantBDb.Customers.ToListAsync());
        Assert.Empty(await tenantBDb.Orders.ToListAsync());
        Assert.Empty(await tenantBDb.Tickets.ToListAsync());
        Assert.Empty(await tenantBDb.TicketMessages.ToListAsync());
        Assert.Empty(await tenantBDb.KnowledgeDocuments.ToListAsync());
        Assert.Empty(await tenantBDb.KnowledgeChunks.ToListAsync());
        Assert.Empty(await tenantBDb.AIConversations.ToListAsync());
        Assert.Empty(await tenantBDb.AIConversationMessages.ToListAsync());
        Assert.Empty(await tenantBDb.AIConversationToolAudits.ToListAsync());
    }

    [Fact]
    public async Task Business_services_return_not_found_across_tenants()
    {
        var database = $"services-{Guid.NewGuid()}"; var tenantA = Guid.NewGuid(); var tenantB = Guid.NewGuid();
        await using (var db = Create(database, tenantA))
        {
            db.Organizations.AddRange(Organization(tenantA, "a"), Organization(tenantB, "b"));
            db.Customers.Add(new Customer { Id = 21, OrganizationId = tenantA, FirstName = "A", LastName = "Customer", Email = "service-a@example.com", CreatedAt = DateTime.UtcNow });
            db.Orders.Add(new Order { Id = 22, OrganizationId = tenantA, CustomerId = 21, OrderNumber = "SERVICE-A", Status = "Open", CreatedAt = DateTime.UtcNow });
            db.Tickets.Add(new Ticket { Id = 23, OrganizationId = tenantA, CustomerId = 21, Subject = "A", Status = "Open", Priority = "Normal", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
        }
        await using var tenantBDb = Create(database, tenantB);
        Assert.Null(await new CustomerService(tenantBDb).GetCustomerAsync(21));
        Assert.Null(await new OrderService(tenantBDb).GetOrderAsync(22));
        Assert.Null(await new TicketService(tenantBDb).GetTicketAsync(23));
    }

    [Fact]
    public async Task Usage_is_written_for_the_current_server_side_user_and_tenant()
    {
        var database = $"usage-{Guid.NewGuid()}"; var tenant = Guid.NewGuid(); var user = Guid.NewGuid();
        var context = new TestCurrentUser(tenant, user);
        await using var db = Create(database, tenant, context);
        db.Organizations.Add(Organization(tenant, "usage")); await db.SaveChangesAsync();
        var service = new AIUsageService(db, context);
        await service.RecordUsageAsync(new AIResponse { Provider = "Test", Model = "model", InputTokens = 2, OutputTokens = 3, TotalTokens = 5 }, "Chat");
        var record = await db.AIUsageRecords.SingleAsync();
        Assert.Equal(tenant, record.OrganizationId); Assert.Equal(user, record.UserId); Assert.Equal(5, record.TotalTokens);
    }

    private static Organization Organization(Guid id, string slug) => new() { Id = id, Name = slug, Slug = slug, IsActive = true, CreatedAt = DateTime.UtcNow };
    private static SupportAgentDbContext Create(string name, Guid tenant, TestCurrentUser? user = null) =>
        new(new DbContextOptionsBuilder<SupportAgentDbContext>().UseInMemoryDatabase(name).Options, user ?? new TestCurrentUser(tenant, Guid.NewGuid()));

    private sealed class TestCurrentUser(Guid organizationId, Guid userId) : ICurrentUserContext
    {
        public Guid UserId => userId; public Guid OrganizationId => organizationId; public string Email => "test@example.com";
        public IReadOnlyCollection<string> Roles => ["Admin"]; public bool IsAuthenticated => true;
    }
}
