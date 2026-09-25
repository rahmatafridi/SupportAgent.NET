using Microsoft.EntityFrameworkCore;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data;
using SupportAgent.Infrastructure.Identity;

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
        var ticket = new Ticket { OrganizationId = _currentUser.OrganizationId, CustomerId = customerId, Subject = subject, Status = "Open", Priority = priority, CreatedAt = now, UpdatedAt = now };
        ticket.Messages.Add(new TicketMessage { OrganizationId = _currentUser.OrganizationId, SenderType = "Customer", Message = message, CreatedAt = now });
        _dbContext.Tickets.Add(ticket);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public Task<Ticket?> UpdateStatusAsync(int ticketId, string status, CancellationToken cancellationToken = default) =>
        UpdateTicketAsync(ticketId, Normalize(status, Statuses, nameof(status)), true, cancellationToken);

    public Task<Ticket?> UpdatePriorityAsync(int ticketId, string priority, CancellationToken cancellationToken = default) =>
        UpdateTicketAsync(ticketId, Normalize(priority, Priorities, nameof(priority)), false, cancellationToken);

    public async Task<Ticket?> UpdateOrderAsync(int ticketId, int? orderId, CancellationToken cancellationToken = default)
    {
        var ticket = await _dbContext.Tickets.FirstOrDefaultAsync(item => item.Id == ticketId, cancellationToken);
        if (ticket is null) return null;
        if (orderId.HasValue && !await _dbContext.Orders.AnyAsync(
                order => order.Id == orderId.Value && order.CustomerId == ticket.CustomerId,
                cancellationToken))
        {
            throw new ArgumentException("Related order must belong to the ticket customer.", nameof(orderId));
        }

        ticket.OrderId = orderId;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public Task<TicketMessage?> AddAgentMessageAsync(int ticketId, string message, CancellationToken cancellationToken = default) =>
        AddMessageAsync(ticketId, message, "Agent", cancellationToken);

    public Task<TicketMessage?> AddCustomerMessageAsync(int ticketId, string message, CancellationToken cancellationToken = default) =>
        AddMessageAsync(ticketId, message, "Customer", cancellationToken);

    private async Task<Ticket?> UpdateTicketAsync(int ticketId, string value, bool status, CancellationToken cancellationToken)
    {
        var ticket = await _dbContext.Tickets.FirstOrDefaultAsync(item => item.Id == ticketId, cancellationToken);
        if (ticket is null) return null;
        if (status) ticket.Status = value; else ticket.Priority = value;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    private async Task<TicketMessage?> AddMessageAsync(int ticketId, string text, string senderType, CancellationToken cancellationToken)
    {
        text = ValidateText(text, "message", 4000);
        var ticket = await _dbContext.Tickets.FirstOrDefaultAsync(ticket => ticket.Id == ticketId, cancellationToken);
        if (ticket is null) return null;
        var now = DateTime.UtcNow;
        var message = new TicketMessage { OrganizationId = _currentUser.OrganizationId, TicketId = ticketId, SenderType = senderType, Message = text, CreatedAt = now };
        ticket.UpdatedAt = now;
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
        TicketQuery? query = null,
        CancellationToken cancellationToken = default)
    {
        query ??= new TicketQuery();
        var tickets = _dbContext.Tickets
            .AsNoTracking()
            .Include(ticket => ticket.Customer)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            var hasId = int.TryParse(search.TrimStart('#'), out var ticketId);
            tickets = tickets.Where(ticket =>
                (hasId && ticket.Id == ticketId) ||
                ticket.Subject.Contains(search) ||
                ticket.Customer.FirstName.Contains(search) ||
                ticket.Customer.LastName.Contains(search) ||
                (ticket.Customer.FirstName + " " + ticket.Customer.LastName).Contains(search) ||
                ticket.Customer.Email.Contains(search));
        }

        if (!string.IsNullOrWhiteSpace(query.Status) && !query.Status.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var status = Normalize(query.Status, Statuses, nameof(query.Status));
            tickets = tickets.Where(ticket => ticket.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Priority) && !query.Priority.Equals("All", StringComparison.OrdinalIgnoreCase))
        {
            var priority = Normalize(query.Priority, Priorities, nameof(query.Priority));
            tickets = tickets.Where(ticket => ticket.Priority == priority);
        }

        if (query.AssignedToMe) tickets = tickets.Where(ticket => ticket.AssignedToUserId == _currentUser.UserId);

        tickets = query.Sort?.ToLowerInvariant() switch
        {
            "oldest" => tickets.OrderBy(ticket => ticket.CreatedAt),
            "priority" => tickets.OrderBy(ticket => ticket.Priority == "High" ? 0 : ticket.Priority == "Medium" ? 1 : 2)
                .ThenByDescending(ticket => ticket.UpdatedAt),
            "newest" => tickets.OrderByDescending(ticket => ticket.CreatedAt),
            _ => tickets.OrderByDescending(ticket => ticket.UpdatedAt)
        };

        return await tickets
            .Select(ticket => new TicketListItem
            {
                Id = ticket.Id,
                Subject = ticket.Subject,
                Status = ticket.Status,
                Priority = ticket.Priority,
                CustomerId = ticket.CustomerId,
                CustomerName = ticket.Customer.FirstName + " " + ticket.Customer.LastName,
                CustomerEmail = ticket.Customer.Email,
                AssignedToUserId = ticket.AssignedToUserId,
                AssignedToName = ticket.AssignedToUserId == null ? null : _dbContext.Users
                    .Where(user => user.Id == ticket.AssignedToUserId).Select(user => user.DisplayName).FirstOrDefault(),
                CreatedAt = ticket.CreatedAt,
                UpdatedAt = ticket.UpdatedAt
            })
            .ToListAsync(cancellationToken);
    }

    public Task<Ticket?> AssignToCurrentUserAsync(int ticketId, CancellationToken cancellationToken = default) =>
        UpdateAssigneeAsync(ticketId, _currentUser.UserId, cancellationToken);

    public async Task<Ticket?> UpdateAssigneeAsync(int ticketId, Guid? assignedToUserId, CancellationToken cancellationToken = default)
    {
        var ticket = await _dbContext.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId, cancellationToken);
        if (ticket is null) return null;
        if (assignedToUserId.HasValue)
        {
            var permittedRoleIds = _dbContext.Roles
                .Where(role => role.Name == ApplicationRoles.Admin || role.Name == ApplicationRoles.SupportAgent)
                .Select(role => role.Id);
            var valid = await _dbContext.Users.AnyAsync(user => user.Id == assignedToUserId &&
                user.OrganizationId == _currentUser.OrganizationId && user.IsActive &&
                _dbContext.UserRoles.Any(role => role.UserId == user.Id && permittedRoleIds.Contains(role.RoleId)), cancellationToken);
            if (!valid) throw new ArgumentException("Assignee must be an active Admin or SupportAgent in the current organization.", nameof(assignedToUserId));
        }
        ticket.AssignedToUserId = assignedToUserId;
        ticket.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public async Task<IReadOnlyList<TicketAssignee>> GetAssigneesAsync(CancellationToken cancellationToken = default)
    {
        var roleIds = _dbContext.Roles.Where(role => role.Name == ApplicationRoles.Admin || role.Name == ApplicationRoles.SupportAgent).Select(role => role.Id);
        return await _dbContext.Users.AsNoTracking()
            .Where(user => user.OrganizationId == _currentUser.OrganizationId && user.IsActive &&
                _dbContext.UserRoles.Any(role => role.UserId == user.Id && roleIds.Contains(role.RoleId)))
            .OrderBy(user => user.DisplayName)
            .Select(user => new TicketAssignee(user.Id, user.DisplayName, user.Email ?? string.Empty))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<TicketInternalNote>> GetInternalNotesAsync(int ticketId, CancellationToken cancellationToken = default) =>
        await _dbContext.TicketInternalNotes.AsNoTracking().Where(note => note.TicketId == ticketId)
            .OrderByDescending(note => note.CreatedAt).ToListAsync(cancellationToken);

    public async Task<TicketInternalNote?> AddInternalNoteAsync(int ticketId, string content, CancellationToken cancellationToken = default)
    {
        content = ValidateText(content, nameof(content), 4000);
        var ticket = await _dbContext.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId, cancellationToken);
        if (ticket is null) return null;
        var now = DateTime.UtcNow;
        var note = new TicketInternalNote { OrganizationId = _currentUser.OrganizationId, TicketId = ticketId, AuthorUserId = _currentUser.UserId, Content = content, CreatedAt = now };
        ticket.UpdatedAt = now;
        _dbContext.TicketInternalNotes.Add(note);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return note;
    }

    public async Task<TicketCustomerContext?> GetCustomerContextAsync(int ticketId, CancellationToken cancellationToken = default)
    {
        var ticket = await _dbContext.Tickets.AsNoTracking().Include(x => x.Customer).FirstOrDefaultAsync(x => x.Id == ticketId, cancellationToken);
        if (ticket is null) return null;
        var orders = await _dbContext.Orders.AsNoTracking().Where(x => x.CustomerId == ticket.CustomerId)
            .OrderByDescending(x => x.CreatedAt).Take(3)
            .Select(x => new RecentOrder(x.Id, x.OrderNumber, x.Status, x.TotalAmount, x.CreatedAt)).ToListAsync(cancellationToken);
        var recentTickets = await _dbContext.Tickets.AsNoTracking().Where(x => x.CustomerId == ticket.CustomerId && x.Id != ticketId)
            .OrderByDescending(x => x.UpdatedAt).Take(3)
            .Select(x => new RecentTicket(x.Id, x.Subject, x.Status, x.Priority, x.UpdatedAt)).ToListAsync(cancellationToken);
        return new TicketCustomerContext(ticket.Customer.Id, $"{ticket.Customer.FirstName} {ticket.Customer.LastName}".Trim(),
            ticket.Customer.Email, ticket.Customer.Phone, orders, recentTickets);
    }

    public async Task<TicketSummary> GetSummaryAsync(CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var tickets = _dbContext.Tickets.AsNoTracking();
        return new TicketSummary(
            await tickets.CountAsync(x => x.Status == "Open", cancellationToken),
            await tickets.CountAsync(x => x.Status == "InProgress", cancellationToken),
            await tickets.CountAsync(x => x.Priority == "High" && x.Status != "Closed", cancellationToken),
            await tickets.CountAsync(x => x.AssignedToUserId == null && x.Status != "Closed", cancellationToken),
            await tickets.CountAsync(x => x.Status == "Closed" && x.UpdatedAt >= today, cancellationToken));
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
