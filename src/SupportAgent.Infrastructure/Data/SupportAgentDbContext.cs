using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data.Configurations;
using SupportAgent.Infrastructure.Identity;

namespace SupportAgent.Infrastructure.Data;

/// <summary>
/// Entity Framework Core database context for customers, orders, tickets, and ticket messages.
/// </summary>
public class SupportAgentDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    private readonly ICurrentUserContext _currentUser;
    /// <summary>
    /// Creates a new database context instance.
    /// </summary>
    /// <param name="options">EF Core options configured with the SQL Server connection string.</param>
    public SupportAgentDbContext(
        DbContextOptions<SupportAgentDbContext> options,
        ICurrentUserContext currentUser)
        : base(options)
    {
        _currentUser = currentUser;
    }

    public SupportAgentDbContext(DbContextOptions<SupportAgentDbContext> options)
        : this(options, new DefaultCurrentUserContext())
    {
    }

    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<AIUsageRecord> AIUsageRecords => Set<AIUsageRecord>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        AssignTenantToAddedEntities();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        AssignTenantToAddedEntities();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void AssignTenantToAddedEntities()
    {
        var organizationId = _currentUser.OrganizationId == Guid.Empty
            ? TenantDefaults.DemoOrganizationId
            : _currentUser.OrganizationId;
        foreach (var entry in ChangeTracker.Entries().Where(x => x.State == EntityState.Added))
        {
            var property = entry.Metadata.FindProperty("OrganizationId");
            if (property is not null && (Guid)entry.Property("OrganizationId").CurrentValue! == Guid.Empty)
                entry.Property("OrganizationId").CurrentValue = organizationId;
        }
    }

    /// <summary>Customer records used by support tools and API endpoints.</summary>
    public DbSet<Customer> Customers => Set<Customer>();

    /// <summary>Order records linked to customers.</summary>
    public DbSet<Order> Orders => Set<Order>();

    /// <summary>Support tickets opened by customers.</summary>
    public DbSet<Ticket> Tickets => Set<Ticket>();

    /// <summary>Messages posted inside support tickets.</summary>
    public DbSet<TicketMessage> TicketMessages => Set<TicketMessage>();
    public DbSet<TicketInternalNote> TicketInternalNotes => Set<TicketInternalNote>();

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
    public DbSet<AISuggestedAction> AISuggestedActions => Set<AISuggestedAction>();

    /// <summary>
    /// Applies entity mappings and relationships for all support tables.
    /// </summary>
    /// <param name="modelBuilder">The EF Core model builder.</param>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Organization>(builder =>
        {
            builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
            builder.Property(x => x.Slug).HasMaxLength(200).IsRequired();
            builder.HasIndex(x => x.Slug).IsUnique();
        });
        modelBuilder.Entity<ApplicationUser>(builder =>
        {
            builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
            builder.HasOne(x => x.Organization).WithMany().HasForeignKey(x => x.OrganizationId)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(x => new { x.OrganizationId, x.NormalizedEmail });
        });
        modelBuilder.Entity<AIUsageRecord>(builder =>
        {
            builder.Property(x => x.Provider).HasMaxLength(100).IsRequired();
            builder.Property(x => x.Model).HasMaxLength(200).IsRequired();
            builder.Property(x => x.RequestType).HasMaxLength(50).IsRequired();
            builder.HasIndex(x => new { x.OrganizationId, x.CreatedAt });
            builder.HasQueryFilter(x => x.OrganizationId == _currentUser.OrganizationId);
        });
        foreach (var entityType in new[]
        {
            typeof(Customer), typeof(Order), typeof(Ticket), typeof(TicketMessage), typeof(TicketInternalNote),
            typeof(KnowledgeDocument), typeof(KnowledgeChunk), typeof(AIConversation),
            typeof(AIConversationMessage), typeof(AIConversationToolAudit), typeof(AISuggestedAction), typeof(AIUsageRecord)
        })
        {
            modelBuilder.Entity(entityType).HasOne(typeof(Organization)).WithMany()
                .HasForeignKey("OrganizationId").OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity(entityType).HasIndex("OrganizationId");
        }
        modelBuilder.ApplyConfiguration(new CustomerConfiguration());
        modelBuilder.ApplyConfiguration(new OrderConfiguration());
        modelBuilder.ApplyConfiguration(new TicketConfiguration());
        modelBuilder.ApplyConfiguration(new TicketMessageConfiguration());
        modelBuilder.Entity<TicketInternalNote>(builder =>
        {
            builder.ToTable("TicketInternalNotes");
            builder.Property(x => x.Content).HasMaxLength(4000).IsRequired();
            builder.HasIndex(x => new { x.OrganizationId, x.TicketId, x.CreatedAt });
            builder.HasOne(x => x.Ticket).WithMany(x => x.InternalNotes).HasForeignKey(x => x.TicketId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.ApplyConfiguration(new KnowledgeDocumentConfiguration());
        modelBuilder.ApplyConfiguration(new KnowledgeChunkConfiguration());
        modelBuilder.ApplyConfiguration(new AIConversationConfiguration());
        modelBuilder.ApplyConfiguration(new AIConversationMessageConfiguration());
        modelBuilder.ApplyConfiguration(new AIConversationToolAuditConfiguration());
        modelBuilder.ApplyConfiguration(new AISuggestedActionConfiguration());

        modelBuilder.Entity<Customer>().HasQueryFilter(x => x.OrganizationId == _currentUser.OrganizationId);
        modelBuilder.Entity<Order>().HasQueryFilter(x => x.OrganizationId == _currentUser.OrganizationId);
        modelBuilder.Entity<Ticket>().HasQueryFilter(x => x.OrganizationId == _currentUser.OrganizationId);
        modelBuilder.Entity<TicketMessage>().HasQueryFilter(x => x.OrganizationId == _currentUser.OrganizationId);
        modelBuilder.Entity<TicketInternalNote>().HasQueryFilter(x => x.OrganizationId == _currentUser.OrganizationId);
        modelBuilder.Entity<KnowledgeDocument>().HasQueryFilter(x => x.OrganizationId == _currentUser.OrganizationId);
        modelBuilder.Entity<KnowledgeChunk>().HasQueryFilter(x => x.OrganizationId == _currentUser.OrganizationId);
        modelBuilder.Entity<AIConversation>().HasQueryFilter(x => x.OrganizationId == _currentUser.OrganizationId);
        modelBuilder.Entity<AIConversationMessage>().HasQueryFilter(x => x.OrganizationId == _currentUser.OrganizationId);
        modelBuilder.Entity<AIConversationToolAudit>().HasQueryFilter(x => x.OrganizationId == _currentUser.OrganizationId);
        modelBuilder.Entity<AISuggestedAction>().HasQueryFilter(x => x.OrganizationId == _currentUser.OrganizationId);
    }
}
