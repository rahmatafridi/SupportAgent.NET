using Microsoft.AspNetCore.Identity;
using SupportAgent.Core.Models;

namespace SupportAgent.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public string DisplayName { get; set; } = string.Empty;
    public Guid OrganizationId { get; set; }
    public Organization Organization { get; set; } = null!;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
