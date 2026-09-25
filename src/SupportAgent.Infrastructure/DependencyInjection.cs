using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SupportAgent.Core.Interfaces;
using SupportAgent.Infrastructure.Data;
using SupportAgent.Infrastructure.Knowledge;
using SupportAgent.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using SupportAgent.Infrastructure.Identity;
using SupportAgent.Infrastructure.Documents;

namespace SupportAgent.Infrastructure;

/// <summary>
/// Dependency injection helpers for database access and business services.
/// </summary>
public static class DependencyInjection
{
    /// <summary>Applies pending EF Core migrations before database-backed seeders run.</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupportAgentDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public static async Task SeedIdentityRolesAsync(this IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        foreach (var role in new[] { ApplicationRoles.Admin, ApplicationRoles.SupportAgent, ApplicationRoles.Viewer, ApplicationRoles.Customer })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));
    }
    /// <summary>
    /// Registers EF Core, SQL Server, and the customer/order/ticket business services.
    /// </summary>
    /// <param name="services">The application service collection.</param>
    /// <param name="configuration">Application configuration containing the database connection string.</param>
    /// <returns>The same service collection so calls can be chained.</returns>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "Connection string 'DefaultConnection' was not found.");

        services.AddDbContext<SupportAgentDbContext>(options =>
            options.UseSqlServer(connectionString));

        services.Configure<KnowledgeSearchOptions>(
            configuration.GetSection(KnowledgeSearchOptions.SectionName));

        services.Configure<Copilot.CopilotOptions>(
            configuration.GetSection(Copilot.CopilotOptions.SectionName));
        services.Configure<KnowledgeUploadOptions>(configuration.GetSection(KnowledgeUploadOptions.SectionName));

        services.AddScoped<ICustomerService, CustomerService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ITicketService, TicketService>();
        services.AddScoped<ICustomerPortalService, CustomerPortalService>();
        services.AddScoped<IAIConversationService, AIConversationService>();
        services.AddScoped<IAIUsageService, AIUsageService>();
        services.AddScoped<ICopilotService, CopilotService>();
        services.AddScoped<ICopilotActionService, CopilotActionService>();
        services.AddSingleton<ITextChunker, TextChunker>();
        services.AddScoped<IKnowledgeService, KnowledgeService>();
        services.AddScoped<IDocumentTextExtractor, DocumentTextExtractor>();
        services.AddScoped<IKnowledgeIngestionService, KnowledgeIngestionService>();

        return services;
    }

    /// <summary>
    /// Seeds development-only sample data when the database is empty.
    /// </summary>
    /// <param name="serviceProvider">The application service provider.</param>
    /// <param name="cancellationToken">Token used to cancel the seed operation.</param>
    /// <returns>A task that completes when seeding has finished.</returns>
    public static async Task SeedDevelopmentDataAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SupportAgentDbContext>();
        await IdentityDevelopmentSeeder.SeedAsync(
            dbContext,
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>(),
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            cancellationToken);
        await DevelopmentDataSeeder.SeedAsync(dbContext, cancellationToken);
        // A second pass links the seeded customer profile to its customer Identity account.
        await IdentityDevelopmentSeeder.SeedAsync(
            dbContext,
            scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole<Guid>>>(),
            scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>(),
            cancellationToken);

        var knowledgeService = scope.ServiceProvider.GetRequiredService<IKnowledgeService>();
        await KnowledgeDevelopmentSeeder.SeedAsync(dbContext, knowledgeService, cancellationToken);
    }
}
