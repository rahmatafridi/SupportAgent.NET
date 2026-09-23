namespace SupportAgent.Core.Models;

/// <summary>
/// Represents a customer order.
/// </summary>
public class Order
{
    /// <summary>Unique order identifier.</summary>
    public int Id { get; set; }

    /// <summary>Customer who placed the order.</summary>
    public int CustomerId { get; set; }

    /// <summary>Human-readable order number such as ORD-1001.</summary>
    public string OrderNumber { get; set; } = string.Empty;

    /// <summary>Current order status such as Processing or Shipped.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Total order amount.</summary>
    public decimal TotalAmount { get; set; }

    /// <summary>Date and time when the order was created.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Navigation property for the related customer.</summary>
    public Customer Customer { get; set; } = null!;
}
