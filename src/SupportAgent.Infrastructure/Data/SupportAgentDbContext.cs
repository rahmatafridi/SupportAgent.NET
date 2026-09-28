using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data.Configurations;
using SupportAgent.Infrastructure.Identity;
using SupportAgent.Core.BusinessQuery;
using SupportAgent.Core.ExternalData;

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
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();

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
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<BusinessQueryAudit> BusinessQueryAudits => Set<BusinessQueryAudit>();
    public DbSet<ExternalDataSource> ExternalDataSources => Set<ExternalDataSource>();
    public DbSet<ExternalEntityMapping> ExternalEntityMappings => Set<ExternalEntityMapping>();
    public DbSet<ExternalFieldMapping> ExternalFieldMappings => Set<ExternalFieldMapping>();
    public DbSet<ExternalRelationshipMapping> ExternalRelationshipMappings => Set<ExternalRelationshipMapping>();
    public DbSet<ExternalDataScope> ExternalDataScopes => Set<ExternalDataScope>();
    public DbSet<ExternalDataScopeEntity> ExternalDataScopeEntities => Set<ExternalDataScopeEntity>();

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
            , typeof(Product), typeof(Invoice), typeof(InvoiceItem), typeof(Payment), typeof(InventoryItem),
            typeof(OrderItem), typeof(BusinessQueryAudit), typeof(ExternalDataSource), typeof(ExternalEntityMapping),
            typeof(ExternalFieldMapping), typeof(ExternalRelationshipMapping), typeof(ExternalDataScope)
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
        modelBuilder.Entity<Product>(b => { b.Property(x => x.Sku).HasMaxLength(50).IsRequired(); b.Property(x => x.Name).HasMaxLength(200).IsRequired(); b.Property(x => x.UnitPrice).HasPrecision(18, 2); b.HasIndex(x => new { x.OrganizationId, x.Sku }).IsUnique(); });
        modelBuilder.Entity<Invoice>(b => { b.Property(x => x.InvoiceNumber).HasMaxLength(50).IsRequired(); b.Property(x => x.Status).HasMaxLength(30); foreach (var p in new[] { nameof(Invoice.Subtotal), nameof(Invoice.TaxAmount), nameof(Invoice.DiscountAmount), nameof(Invoice.TotalAmount) }) b.Property(p).HasPrecision(18, 2); b.HasIndex(x => new { x.OrganizationId, x.InvoiceNumber }).IsUnique(); b.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict); });
        modelBuilder.Entity<InvoiceItem>(b => { foreach (var p in new[] { nameof(InvoiceItem.Quantity), nameof(InvoiceItem.UnitPrice), nameof(InvoiceItem.DiscountAmount), nameof(InvoiceItem.TaxAmount), nameof(InvoiceItem.LineTotal) }) b.Property(p).HasPrecision(18, 2); b.Property(x => x.Description).HasMaxLength(500); b.HasOne(x => x.Invoice).WithMany(x => x.Items).HasForeignKey(x => x.InvoiceId); b.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict); });
        modelBuilder.Entity<Payment>(b => { b.Property(x => x.Amount).HasPrecision(18, 2); b.Property(x => x.PaymentReference).HasMaxLength(100); b.HasOne(x => x.Invoice).WithMany(x => x.Payments).HasForeignKey(x => x.InvoiceId); });
        modelBuilder.Entity<InventoryItem>(b => { b.Property(x => x.QuantityOnHand).HasPrecision(18, 2); b.Property(x => x.ReorderLevel).HasPrecision(18, 2); b.HasIndex(x => new { x.OrganizationId, x.ProductId }).IsUnique(); b.HasOne(x => x.Product).WithOne(x => x.Inventory).HasForeignKey<InventoryItem>(x => x.ProductId); });
        modelBuilder.Entity<OrderItem>(b => { b.Property(x => x.Description).HasMaxLength(500); b.Property(x => x.Quantity).HasPrecision(18,2); b.Property(x => x.UnitPrice).HasPrecision(18,2); b.Property(x => x.LineTotal).HasPrecision(18,2); b.HasOne(x=>x.Order).WithMany().HasForeignKey(x=>x.OrderId); b.HasOne(x=>x.Product).WithMany().HasForeignKey(x=>x.ProductId).OnDelete(DeleteBehavior.Restrict); });
        modelBuilder.Entity<BusinessQueryAudit>(b => { b.Property(x=>x.Question).HasMaxLength(1000).IsRequired(); b.Property(x=>x.QueryPlanJson).HasMaxLength(8000).IsRequired(); b.Property(x=>x.ErrorCode).HasMaxLength(100); b.Property(x=>x.DataSourceName).HasMaxLength(200); b.Property(x=>x.DataSourceProvider).HasMaxLength(30); b.HasIndex(x=>new{x.OrganizationId,x.CreatedAt}); });
        modelBuilder.Entity<ExternalDataSource>(b=>{b.Property(x=>x.Name).HasMaxLength(200).IsRequired();b.Property(x=>x.SecretKey).HasMaxLength(200).IsRequired();b.Property(x=>x.Description).HasMaxLength(1000);b.HasIndex(x=>new{x.OrganizationId,x.Name}).IsUnique();});
        modelBuilder.Entity<ExternalEntityMapping>(b=>{b.Property(x=>x.ExternalSchemaName).HasMaxLength(128).IsRequired();b.Property(x=>x.ExternalTableName).HasMaxLength(128).IsRequired();b.Property(x=>x.BusinessEntityName).HasMaxLength(128).IsRequired();b.Property(x=>x.ObjectType).HasMaxLength(20);b.Property(x=>x.Concepts).HasMaxLength(1000);b.Property(x=>x.DefaultRecencyField).HasMaxLength(128);b.HasOne(x=>x.DataSource).WithMany(x=>x.Entities).HasForeignKey(x=>x.ExternalDataSourceId).OnDelete(DeleteBehavior.Cascade);});
        modelBuilder.Entity<ExternalFieldMapping>(b=>{foreach(var p in new[]{nameof(ExternalFieldMapping.ExternalColumnName),nameof(ExternalFieldMapping.BusinessFieldName),nameof(ExternalFieldMapping.DisplayName),nameof(ExternalFieldMapping.DataType)})b.Property(p).HasMaxLength(128).IsRequired();b.HasOne(x=>x.Entity).WithMany(x=>x.Fields).HasForeignKey(x=>x.ExternalEntityMappingId).OnDelete(DeleteBehavior.Cascade);});
        modelBuilder.Entity<ExternalRelationshipMapping>(b=>{foreach(var p in new[]{nameof(ExternalRelationshipMapping.Name),nameof(ExternalRelationshipMapping.FromEntity),nameof(ExternalRelationshipMapping.FromField),nameof(ExternalRelationshipMapping.ToEntity),nameof(ExternalRelationshipMapping.ToField)})b.Property(p).HasMaxLength(128).IsRequired();b.Property(x=>x.RelationshipType).HasMaxLength(20).HasDefaultValue("ManyToOne").IsRequired();b.Property(x=>x.DiscoveredForeignKeyName).HasMaxLength(128);b.Property(x=>x.Description).HasMaxLength(1000);b.Property(x=>x.Concepts).HasMaxLength(1000);b.HasIndex(x=>new{x.OrganizationId,x.ExternalDataSourceId,x.FromEntity,x.Name}).IsUnique();});
        modelBuilder.Entity<ExternalDataScope>(b=>{b.Property(x=>x.Name).HasMaxLength(200).IsRequired();b.Property(x=>x.Description).HasMaxLength(1000);b.HasIndex(x=>new{x.OrganizationId,x.ExternalDataSourceId,x.Name}).IsUnique();b.HasOne(x=>x.DataSource).WithMany(x=>x.Scopes).HasForeignKey(x=>x.ExternalDataSourceId).OnDelete(DeleteBehavior.Cascade);});
        modelBuilder.Entity<ExternalDataScopeEntity>(b=>{b.HasKey(x=>new{x.ExternalDataScopeId,x.ExternalEntityMappingId});b.HasOne(x=>x.Scope).WithMany(x=>x.Entities).HasForeignKey(x=>x.ExternalDataScopeId).OnDelete(DeleteBehavior.Cascade);b.HasOne(x=>x.Entity).WithMany().HasForeignKey(x=>x.ExternalEntityMappingId).OnDelete(DeleteBehavior.Restrict);});
        modelBuilder.Entity<Product>().HasQueryFilter(x => x.OrganizationId == _currentUser.OrganizationId);
        modelBuilder.Entity<Invoice>().HasQueryFilter(x => x.OrganizationId == _currentUser.OrganizationId);
        modelBuilder.Entity<InvoiceItem>().HasQueryFilter(x => x.OrganizationId == _currentUser.OrganizationId);
        modelBuilder.Entity<Payment>().HasQueryFilter(x => x.OrganizationId == _currentUser.OrganizationId);
        modelBuilder.Entity<InventoryItem>().HasQueryFilter(x => x.OrganizationId == _currentUser.OrganizationId);
        modelBuilder.Entity<OrderItem>().HasQueryFilter(x => x.OrganizationId == _currentUser.OrganizationId);
        modelBuilder.Entity<BusinessQueryAudit>().HasQueryFilter(x => x.OrganizationId == _currentUser.OrganizationId);
        modelBuilder.Entity<ExternalDataSource>().HasQueryFilter(x=>x.OrganizationId==_currentUser.OrganizationId);
        modelBuilder.Entity<ExternalEntityMapping>().HasQueryFilter(x=>x.OrganizationId==_currentUser.OrganizationId);
        modelBuilder.Entity<ExternalFieldMapping>().HasQueryFilter(x=>x.OrganizationId==_currentUser.OrganizationId);
        modelBuilder.Entity<ExternalRelationshipMapping>().HasQueryFilter(x=>x.OrganizationId==_currentUser.OrganizationId);
        modelBuilder.Entity<ExternalDataScope>().HasQueryFilter(x=>x.OrganizationId==_currentUser.OrganizationId);
        modelBuilder.Entity<ExternalDataScopeEntity>().HasQueryFilter(x=>x.Scope.OrganizationId==_currentUser.OrganizationId&&x.Entity.OrganizationId==_currentUser.OrganizationId);
    }
}
