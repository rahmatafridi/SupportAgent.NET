namespace SupportAgent.Core.Models;

/// <summary>
/// Represents a customer in the support system.
/// </summary>
public class Customer
{
    /// <summary>Unique customer identifier.</summary>
    public int Id { get; set; }

    public Guid OrganizationId { get; set; }

    /// <summary>Customer first name.</summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>Customer last name.</summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>Customer email address.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Customer phone number.</summary>
    public string? Phone { get; set; }

    /// <summary>Date and time when the customer record was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Orders belonging to this customer.</summary>
    public ICollection<Order> Orders { get; set; } = new List<Order>();

    /// <summary>Support tickets opened by this customer.</summary>
    public ICollection<Ticket> Tickets { get; set; } = new List<Ticket>();
}
