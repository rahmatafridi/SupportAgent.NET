namespace SupportAgent.Core.Models;

/// <summary>
/// Represents one message in a support ticket conversation.
/// </summary>
public class TicketMessage
{
    /// <summary>Unique message identifier.</summary>
    public int Id { get; set; }

    public Guid OrganizationId { get; set; }

    /// <summary>Ticket this message belongs to.</summary>
    public int TicketId { get; set; }

    /// <summary>Who sent the message, such as Customer or Agent.</summary>
    public string SenderType { get; set; } = string.Empty;

    /// <summary>Message body text.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Date and time when the message was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Navigation property for the related ticket.</summary>
    public Ticket Ticket { get; set; } = null!;
}
