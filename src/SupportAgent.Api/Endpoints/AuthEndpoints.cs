using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data;
using SupportAgent.Infrastructure.Identity;
using SupportAgent.Api.Security;

namespace SupportAgent.Api.Endpoints;

public record RegisterRequest(string OrganizationName, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record CurrentUserResponse(Guid Id, string Email, string DisplayName, Guid OrganizationId, string OrganizationName, IReadOnlyCollection<string> Roles);

public static partial class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/auth/csrf", (IAntiforgery antiforgery, HttpContext context) =>
        {
            var tokens = antiforgery.GetAndStoreTokens(context);
            return Results.Ok(new { token = tokens.RequestToken });
        }).AllowAnonymous();

        app.MapPost("/api/auth/register", async (
            RegisterRequest request,
            IOptions<AuthenticationOptions> options,
            SupportAgentDbContext db,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole<Guid>> roleManager,
            CancellationToken cancellationToken) =>
        {
            if (!options.Value.AllowSelfRegistration)
                return Results.Json(new { error = "Self-registration is disabled." }, statusCode: StatusCodes.Status403Forbidden);
            if (string.IsNullOrWhiteSpace(request.OrganizationName) || string.IsNullOrWhiteSpace(request.Email))
                return Results.BadRequest(new { error = "Organization name and email are required." });

            await using var transaction = db.Database.IsRelational()
                ? await db.Database.BeginTransactionAsync(cancellationToken)
                : null;
            var slugBase = SlugRegex().Replace(request.OrganizationName.Trim().ToLowerInvariant(), "-").Trim('-');
            if (string.IsNullOrEmpty(slugBase)) slugBase = "organization";
            var slug = slugBase;
            for (var suffix = 2; await db.Organizations.AnyAsync(x => x.Slug == slug, cancellationToken); suffix++)
                slug = $"{slugBase}-{suffix}";

            var organization = new Organization
            {
                Id = Guid.NewGuid(), Name = request.OrganizationName.Trim(), Slug = slug,
                IsActive = true, CreatedAt = DateTime.UtcNow
            };
            db.Organizations.Add(organization);
            await db.SaveChangesAsync(cancellationToken);
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(), UserName = request.Email.Trim(), Email = request.Email.Trim(),
                DisplayName = request.Email.Trim().Split('@')[0], OrganizationId = organization.Id,
                IsActive = true, CreatedAt = DateTime.UtcNow
            };
            var result = await userManager.CreateAsync(user, request.Password);
            if (!result.Succeeded)
            {
                db.Organizations.Remove(organization);
                await db.SaveChangesAsync(cancellationToken);
                return Results.BadRequest(new { errors = result.Errors.Select(x => x.Description) });
            }
            if (!await roleManager.RoleExistsAsync(ApplicationRoles.Admin))
                await roleManager.CreateAsync(new IdentityRole<Guid>(ApplicationRoles.Admin));
            await userManager.AddToRoleAsync(user, ApplicationRoles.Admin);
            if (transaction is not null) await transaction.CommitAsync(cancellationToken);
            return Results.Created("/api/auth/me", new { organization.Id, organization.Name, user.Email });
        }).AllowAnonymous().AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/auth/login", async (
            LoginRequest request,
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager) =>
        {
            var user = await userManager.FindByEmailAsync(request.Email.Trim());
            if (user is null || !user.IsActive)
                return Results.Unauthorized();
            if (!await userManager.CheckPasswordAsync(user, request.Password))
            {
                await userManager.AccessFailedAsync(user);
                return Results.Unauthorized();
            }
            await userManager.ResetAccessFailedCountAsync(user);
            await signInManager.SignInAsync(user, isPersistent: false);
            return Results.NoContent();
        }).AllowAnonymous().AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapPost("/api/auth/logout", async (SignInManager<ApplicationUser> signInManager) =>
        {
            await signInManager.SignOutAsync();
            return Results.NoContent();
        }).RequireAuthorization().AddEndpointFilter<AntiforgeryEndpointFilter>();

        app.MapGet("/api/auth/me", async (
            ICurrentUserContext current,
            UserManager<ApplicationUser> userManager,
            SupportAgentDbContext db,
            CancellationToken cancellationToken) =>
        {
            var user = await userManager.FindByIdAsync(current.UserId.ToString());
            if (user is null) return Results.Unauthorized();
            var organization = await db.Organizations.FindAsync([user.OrganizationId], cancellationToken);
            var roles = await userManager.GetRolesAsync(user);
            return Results.Ok(new CurrentUserResponse(user.Id, user.Email ?? "", user.DisplayName,
                user.OrganizationId, organization?.Name ?? "", roles.ToArray()));
        }).RequireAuthorization();

        return app;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugRegex();
}
