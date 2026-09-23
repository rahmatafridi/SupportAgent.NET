namespace SupportAgent.Core.Models;

/// <summary>
/// Summary row for ticket list views.
/// </summary>
public class TicketListItem
{
    /// <summary>Ticket identifier.</summary>
    public int Id { get; set; }

    /// <summary>Ticket subject.</summary>
    public string Subject { get; set; } = string.Empty;

    /// <summary>Ticket status.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Ticket priority.</summary>
    public string Priority { get; set; } = string.Empty;

    /// <summary>Related customer identifier.</summary>
    public int CustomerId { get; set; }

    /// <summary>Display name for the related customer.</summary>
    public string CustomerName { get; set; } = string.Empty;
}
