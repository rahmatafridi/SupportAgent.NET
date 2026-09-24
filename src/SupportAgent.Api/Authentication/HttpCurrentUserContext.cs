using System.Security.Claims;
using SupportAgent.Core.Interfaces;

namespace SupportAgent.Api.Authentication;

public sealed class HttpCurrentUserContext(IHttpContextAccessor accessor) : ICurrentUserContext
{
    private ClaimsPrincipal User => accessor.HttpContext?.User ?? new ClaimsPrincipal();
    public Guid UserId => ReadGuid(ClaimTypes.NameIdentifier);
    public Guid OrganizationId => ReadGuid("organization_id");
    public string Email => User.FindFirstValue(ClaimTypes.Email) ?? string.Empty;
    public IReadOnlyCollection<string> Roles => User.FindAll(ClaimTypes.Role).Select(x => x.Value).ToArray();
    public bool IsAuthenticated => User.Identity?.IsAuthenticated == true;
    private Guid ReadGuid(string type) => Guid.TryParse(User.FindFirstValue(type), out var value) ? value : Guid.Empty;
}
