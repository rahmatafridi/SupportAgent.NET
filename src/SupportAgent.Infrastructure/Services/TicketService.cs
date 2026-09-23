using Microsoft.EntityFrameworkCore;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data;

namespace SupportAgent.Infrastructure.Services;

/// <summary>
/// Support ticket business service. Reads tickets and ticket messages through EF Core.
/// </summary>
public class TicketService : ITicketService
{
    private readonly SupportAgentDbContext _dbContext;

    /// <summary>
    /// Creates a new ticket service instance.
    /// </summary>
    /// <param name="dbContext">Database context used to query tickets and messages.</param>
    public TicketService(SupportAgentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TicketListItem>> GetTicketsAsync(
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.Customer)
            .OrderByDescending(ticket => ticket.CreatedAt)
            .Select(ticket => new TicketListItem
            {
                Id = ticket.Id,
                Subject = ticket.Subject,
                Status = ticket.Status,
                Priority = ticket.Priority,
                CustomerId = ticket.CustomerId,
                CustomerName = ticket.Customer.FirstName + " " + ticket.Customer.LastName
            })
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Ticket?> GetTicketAsync(
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Tickets
            .AsNoTracking()
            .FirstOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<TicketMessage>> GetTicketMessagesAsync(
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.TicketMessages
            .AsNoTracking()
            .Where(message => message.TicketId == ticketId)
            .OrderBy(message => message.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
