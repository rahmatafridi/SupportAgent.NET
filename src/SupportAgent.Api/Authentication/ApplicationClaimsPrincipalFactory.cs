using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using SupportAgent.Infrastructure.Identity;

namespace SupportAgent.Api.Authentication;

public sealed class ApplicationClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole<Guid>> roleManager,
    IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole<Guid>>(userManager, roleManager, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("organization_id", user.OrganizationId.ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Email, user.Email ?? string.Empty));
        return identity;
    }
}
