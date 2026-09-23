using Microsoft.EntityFrameworkCore;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data.Configurations;

namespace SupportAgent.Infrastructure.Data;

/// <summary>
/// Entity Framework Core database context for customers, orders, tickets, and ticket messages.
/// </summary>
public class SupportAgentDbContext : DbContext
{
    /// <summary>
    /// Creates a new database context instance.
    /// </summary>
    /// <param name="options">EF Core options configured with the SQL Server connection string.</param>
    public SupportAgentDbContext(DbContextOptions<SupportAgentDbContext> options)
        : base(options)
    {
    }

    /// <summary>Customer records used by support tools and API endpoints.</summary>
    public DbSet<Customer> Customers => Set<Customer>();

    /// <summary>Order records linked to customers.</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <summary>Support tickets opened by customers.</summary>
    public DbSet<Ticket> Tickets => Set<Ticket>();

    /// <summary>Messages posted inside support tickets.</summary>
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();

    /// <summary>Company knowledge documents used for grounded support answers.</summary>
    public DbSet<KnowledgeDocument> KnowledgeDocuments => Set<KnowledgeDocument>();

    /// <summary>Searchable chunks derived from company knowledge documents.</summary>
    public DbSet<KnowledgeChunk> KnowledgeChunks => Set<KnowledgeChunk>();

    /// <summary>Persisted AI copilot conversations.</summary>
    public DbSet<AIConversation> AIConversations => Set<AIConversation>();

    /// <summary>Messages inside AI copilot conversations.</summary>
    public DbSet<AIConversationMessage> AIConversationMessages => Set<AIConversationMessage>();

    /// <summary>Tool audit records for AI copilot conversations.</summary>
    public DbSet<AIConversationToolAudit> AIConversationToolAudits => Set<AIConversationToolAudit>();

    /// <summary>
    /// Applies entity mappings and relationships for all support tables.
    /// </summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CustomerConfiguration());
        modelBuilder.ApplyConfiguration(new OrderConfiguration());
        modelBuilder.ApplyConfiguration(new TicketConfiguration());
        modelBuilder.ApplyConfiguration(new TicketMessageConfiguration());
        modelBuilder.ApplyConfiguration(new KnowledgeDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new KnowledgeChunkConfiguration());
        modelBuilder.ApplyConfiguration(new AIConversationConfiguration());
        modelBuilder.ApplyConfiguration(new AIConversationMessageConfiguration());
        modelBuilder.ApplyConfiguration(new AIConversationToolAuditConfiguration());
    }
}
