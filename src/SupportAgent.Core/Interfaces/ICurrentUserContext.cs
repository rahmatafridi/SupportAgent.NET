namespace SupportAgent.Core.Interfaces;

/// <summary>Authenticated user and tenant values resolved by the server.</summary>
public interface ICurrentUserContext
{
    Guid UserId { get; }
    Guid OrganizationId { get; }
    string Email { get; }
    IReadOnlyCollection<string> Roles { get; }
    bool IsAuthenticated { get; }
}
