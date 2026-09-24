namespace SupportAgent.Core.Models;

/// <summary>An agent-only note attached to a support ticket.</summary>
public class TicketInternalNote
{
    public int Id { get; set; }
    public Guid OrganizationId { get; set; }
    public int TicketId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Ticket Ticket { get; set; } = null!;
}
