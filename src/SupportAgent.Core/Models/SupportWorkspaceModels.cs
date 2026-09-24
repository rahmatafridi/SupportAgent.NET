namespace SupportAgent.Core.Models;

public class TicketQuery
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? Sort { get; set; }
    public bool AssignedToMe { get; set; }
}

public record TicketAssignee(Guid Id, string DisplayName, string Email);
public record TicketSummary(int Open, int InProgress, int HighPriority, int Unassigned, int ClosedToday);
public record RecentOrder(int Id, string OrderNumber, string Status, decimal TotalAmount, DateTime CreatedAt);
public record RecentTicket(int Id, string Subject, string Status, string Priority, DateTime UpdatedAt);
public record TicketCustomerContext(
    int Id, string Name, string Email, string? Phone,
    IReadOnlyList<RecentOrder> RecentOrders,
    IReadOnlyList<RecentTicket> RecentTickets);
