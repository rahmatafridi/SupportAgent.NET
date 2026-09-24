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
    private readonly ICurrentUserContext _currentUser;
    private static readonly HashSet<string> Statuses = new(StringComparer.OrdinalIgnoreCase) { "Open", "InProgress", "Closed" };
    private static readonly HashSet<string> Priorities = new(StringComparer.OrdinalIgnoreCase) { "Low", "Medium", "High" };

    /// <summary>
    /// Creates a new ticket service instance.
    /// </summary>
    /// <param name="dbContext">Database context used to query tickets and messages.</param>
    public TicketService(SupportAgentDbContext dbContext, ICurrentUserContext? currentUser = null)
    {
        _dbContext = dbContext;
        _currentUser = currentUser ?? new SupportAgent.Infrastructure.Identity.DefaultCurrentUserContext();
    }

    public async Task<Ticket?> CreateTicketAsync(int customerId, string subject, string priority, string message, CancellationToken cancellationToken = default)
    {
        subject = ValidateText(subject, nameof(subject), 200);
        message = ValidateText(message, nameof(message), 4000);
        priority = Normalize(priority, Priorities, nameof(priority));
        if (!await _dbContext.Customers.AnyAsync(customer => customer.Id == customerId, cancellationToken)) return null;
        var now = DateTime.UtcNow;
        var ticket = new Ticket { OrganizationId = _currentUser.OrganizationId, CustomerId = customerId, Subject = subject, Status = "Open", Priority = priority, CreatedAt = now };
        ticket.Messages.Add(new TicketMessage { OrganizationId = _currentUser.OrganizationId, SenderType = "Customer", Message = message, CreatedAt = now });
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public Task<Ticket?> UpdateStatusAsync(int ticketId, string status, CancellationToken cancellationToken = default) =>
        UpdateTicketAsync(ticketId, Normalize(status, Statuses, nameof(status)), true, cancellationToken);

    public Task<Ticket?> UpdatePriorityAsync(int ticketId, string priority, CancellationToken cancellationToken = default) =>
        UpdateTicketAsync(ticketId, Normalize(priority, Priorities, nameof(priority)), false, cancellationToken);

    public Task<TicketMessage?> AddAgentMessageAsync(int ticketId, string message, CancellationToken cancellationToken = default) =>
        AddMessageAsync(ticketId, message, "Agent", cancellationToken);

    public Task<TicketMessage?> AddCustomerMessageAsync(int ticketId, string message, CancellationToken cancellationToken = default) =>
        AddMessageAsync(ticketId, message, "Customer", cancellationToken);

    private async Task<Ticket?> UpdateTicketAsync(int ticketId, string value, bool status, CancellationToken cancellationToken)
    {
        var ticket = await _dbContext.Tickets.FirstOrDefaultAsync(item => item.Id == ticketId, cancellationToken);
        if (ticket is null) return null;
        if (status) ticket.Status = value; else ticket.Priority = value;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    private async Task<TicketMessage?> AddMessageAsync(int ticketId, string text, string senderType, CancellationToken cancellationToken)
    {
        text = ValidateText(text, "message", 4000);
        if (!await _dbContext.Tickets.AnyAsync(ticket => ticket.Id == ticketId, cancellationToken)) return null;
        var message = new TicketMessage { OrganizationId = _currentUser.OrganizationId, TicketId = ticketId, SenderType = senderType, Message = text, CreatedAt = DateTime.UtcNow };
        _dbContext.TicketMessages.Add(message);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return message;
    }

    private static string Normalize(string value, HashSet<string> allowed, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || !allowed.TryGetValue(value.Trim(), out var normalized))
            throw new ArgumentException($"{name} must be one of: {string.Join(", ", allowed)}.", name);
        return normalized;
    }

    private static string ValidateText(string value, string name, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{name} is required.", name);
        value = value.Trim();
        if (value.Length > maxLength) throw new ArgumentException($"{name} must be {maxLength} characters or fewer.", name);
        return value;
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
