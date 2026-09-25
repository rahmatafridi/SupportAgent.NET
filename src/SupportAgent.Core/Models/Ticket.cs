namespace SupportAgent.Core.Models;

/// <summary>
/// Represents a customer support ticket.
/// </summary>
public class Ticket
{
    /// <summary>Unique ticket identifier.</summary>
    public int Id { get; set; }

    public Guid OrganizationId { get; set; }

    /// <summary>Customer who opened the ticket.</summary>
    public int CustomerId { get; set; }

    /// <summary>Short summary of the support issue.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Current ticket status such as Open or Closed.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Ticket priority such as High or Medium.</summary>
    public string Priority { get; set; } = string.Empty;

    /// <summary>Date and time when the ticket was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Date and time when the ticket was last changed.</summary>
    public DateTime UpdatedAt { get; set; }

    /// <summary>When the customer last opened this ticket conversation.</summary>
    public DateTime? CustomerLastReadAt { get; set; }

    /// <summary>Optional application user currently responsible for the ticket.</summary>
    public Guid? AssignedToUserId { get; set; }

    /// <summary>Optional order related to the support request.</summary>
    public int? OrderId { get; set; }

    /// <summary>Navigation property for the related customer.</summary>
    public Customer Customer { get; set; } = null!;

    /// <summary>Messages exchanged on this ticket.</summary>
    public ICollection<TicketMessage> Messages { get; set; } = new List<TicketMessage>();

    public ICollection<TicketInternalNote> InternalNotes { get; set; } = new List<TicketInternalNote>();
}
