using SupportAgent.Core.Interfaces;
using SupportAgent.Infrastructure.Data;

namespace SupportAgent.Infrastructure.Identity;

internal sealed class DefaultCurrentUserContext : ICurrentUserContext
{
    public Guid UserId => Guid.Empty;
    public Guid OrganizationId => TenantDefaults.DemoOrganizationId;
    public string Email => string.Empty;
    public IReadOnlyCollection<string> Roles => [];
    public bool IsAuthenticated => false;
}
