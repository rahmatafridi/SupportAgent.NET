using SupportAgent.Core.Models;

namespace SupportAgent.Core.Interfaces;

/// <summary>
/// Reads support ticket data from the business layer. Used by API endpoints.
/// </summary>
public interface ITicketService
{
    Task<Ticket?> CreateTicketAsync(int customerId, string subject, string priority, string message, CancellationToken cancellationToken = default);
    Task<Ticket?> UpdateStatusAsync(int ticketId, string status, CancellationToken cancellationToken = default);
    Task<Ticket?> UpdatePriorityAsync(int ticketId, string priority, CancellationToken cancellationToken = default);
    Task<TicketMessage?> AddAgentMessageAsync(int ticketId, string message, CancellationToken cancellationToken = default);
    Task<TicketMessage?> AddCustomerMessageAsync(int ticketId, string message, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all support tickets with basic customer information for list views.
    /// </summary>
    Task<IReadOnlyList<TicketListItem>> GetTicketsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets one support ticket by ID from the database.
    /// </summary>
    /// <param name="ticketId">The ticket ID to look up.</param>
    /// <param name="cancellationToken">Token used to cancel the database query.</param>
    /// <returns>The ticket if found; otherwise <c>null</c>.</returns>
    Task<Ticket?> GetTicketAsync(
        int ticketId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all messages for a support ticket in created-date order.
    /// </summary>
    /// <param name="ticketId">The ticket ID whose messages should be returned.</param>
    /// <param name="cancellationToken">Token used to cancel the database query.</param>
    /// <returns>All messages for the ticket.</returns>
    Task<IReadOnlyList<TicketMessage>> GetTicketMessagesAsync(
        int ticketId,
        CancellationToken cancellationToken = default);
}
