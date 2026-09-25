namespace SupportAgent.Core.Models;

public record PortalProfile(int CustomerId, string Name, string Email, string? Phone, string OrganizationName);
public record PortalTicket(int Id, string Subject, string Status, string Priority, DateTime CreatedAt, DateTime UpdatedAt, int? OrderId, bool HasUnreadAgentReply);
public record PortalTicketSummary(int Open, int InProgress, int Closed);
public record PortalTicketList(IReadOnlyList<PortalTicket> Tickets, PortalTicketSummary Summary);
public record PortalMessage(int Id, string SenderType, string Message, DateTime CreatedAt);
public record PortalOrder(int Id, string OrderNumber, string Status, decimal TotalAmount, DateTime CreatedAt);
