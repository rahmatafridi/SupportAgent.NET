using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Identity;

namespace SupportAgent.Infrastructure.Data;

public static class IdentityDevelopmentSeeder
{
    public const string DevelopmentPassword = "SupportAgent123!";

    public static async Task SeedAsync(
        SupportAgentDbContext db,
        RoleManager<IdentityRole<Guid>> roleManager,
        UserManager<ApplicationUser> userManager,
        CancellationToken cancellationToken = default)
    {
        var organization = await db.Organizations.IgnoreQueryFilters()
            .FirstOrDefaultAsync(x => x.Id == TenantDefaults.DemoOrganizationId, cancellationToken);
        if (organization is null)
        {
            organization = new Organization
            {
                Id = TenantDefaults.DemoOrganizationId,
                Name = "SupportAgent Demo",
                Slug = "supportagent-demo",
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };
            db.Organizations.Add(organization);
            await db.SaveChangesAsync(cancellationToken);
        }

        foreach (var role in new[] { ApplicationRoles.Admin, ApplicationRoles.SupportAgent, ApplicationRoles.Viewer })
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole<Guid>(role));

        await EnsureUserAsync("admin@supportagent.local", "Demo Admin", ApplicationRoles.Admin);
        await EnsureUserAsync("agent@supportagent.local", "Demo Agent", ApplicationRoles.SupportAgent);
        await EnsureUserAsync("viewer@supportagent.local", "Demo Viewer", ApplicationRoles.Viewer);

        async Task EnsureUserAsync(string email, string displayName, string role)
        {
            var user = await userManager.FindByEmailAsync(email);
            if (user is null)
            {
                user = new ApplicationUser
                {
                    Id = Guid.NewGuid(), UserName = email, Email = email, DisplayName = displayName,
                    OrganizationId = organization.Id, IsActive = true, CreatedAt = DateTime.UtcNow
                };
                var created = await userManager.CreateAsync(user, DevelopmentPassword);
                if (!created.Succeeded)
                    throw new InvalidOperationException(string.Join("; ", created.Errors.Select(x => x.Description)));
            }
            if (!await userManager.IsInRoleAsync(user, role)) await userManager.AddToRoleAsync(user, role);
        }
    }
}
