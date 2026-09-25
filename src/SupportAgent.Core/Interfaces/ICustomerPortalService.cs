using SupportAgent.Core.Models;

namespace SupportAgent.Core.Interfaces;

public interface ICustomerPortalService
{
    Task<PortalProfile?> GetProfileAsync(CancellationToken cancellationToken = default);
    Task<PortalTicketList?> GetTicketsAsync(CancellationToken cancellationToken = default);
    Task<PortalTicket?> GetTicketAsync(int ticketId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PortalMessage>?> GetMessagesAsync(int ticketId, CancellationToken cancellationToken = default);
    Task<bool> MarkTicketReadAsync(int ticketId, CancellationToken cancellationToken = default);
    Task<PortalTicket?> CreateTicketAsync(string subject, string priority, string message, int? orderId, CancellationToken cancellationToken = default);
    Task<PortalMessage?> AddMessageAsync(int ticketId, string message, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PortalOrder>?> GetOrdersAsync(CancellationToken cancellationToken = default);
}
