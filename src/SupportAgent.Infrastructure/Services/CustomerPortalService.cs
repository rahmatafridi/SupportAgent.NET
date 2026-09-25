using Microsoft.EntityFrameworkCore;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data;

namespace SupportAgent.Infrastructure.Services;

public class CustomerPortalService(SupportAgentDbContext db, ICurrentUserContext current) : ICustomerPortalService
{
    private Task<Customer?> CustomerAsync(CancellationToken token) => db.Customers
        .FirstOrDefaultAsync(x => x.ApplicationUserId == current.UserId, token);

    public async Task<PortalProfile?> GetProfileAsync(CancellationToken token = default)
    {
        var customer = await CustomerAsync(token); if (customer is null) return null;
        var organization = await db.Organizations.Where(x => x.Id == customer.OrganizationId).Select(x => x.Name).FirstOrDefaultAsync(token);
        return new(customer.Id, $"{customer.FirstName} {customer.LastName}".Trim(), customer.Email, customer.Phone, organization ?? string.Empty);
    }

    public async Task<PortalTicketList?> GetTicketsAsync(CancellationToken token = default)
    {
        var customer = await CustomerAsync(token); if (customer is null) return null;
        var tickets = await db.Tickets.AsNoTracking().Where(x => x.CustomerId == customer.Id).OrderByDescending(x => x.UpdatedAt)
            .Select(x => new PortalTicket(x.Id, x.Subject, x.Status, x.Priority, x.CreatedAt, x.UpdatedAt, x.OrderId,
                db.TicketMessages.Any(message => message.TicketId == x.Id && message.SenderType == "Agent" &&
                    (!x.CustomerLastReadAt.HasValue || message.CreatedAt > x.CustomerLastReadAt.Value)))).ToListAsync(token);
        return new(tickets, new(tickets.Count(x => x.Status == "Open"), tickets.Count(x => x.Status == "InProgress"), tickets.Count(x => x.Status == "Closed")));
    }

    public async Task<PortalTicket?> GetTicketAsync(int ticketId, CancellationToken token = default)
    {
        var customer = await CustomerAsync(token); if (customer is null) return null;
        return await db.Tickets.AsNoTracking().Where(x => x.Id == ticketId && x.CustomerId == customer.Id)
            .Select(x => new PortalTicket(x.Id, x.Subject, x.Status, x.Priority, x.CreatedAt, x.UpdatedAt, x.OrderId,
                db.TicketMessages.Any(message => message.TicketId == x.Id && message.SenderType == "Agent" &&
                    (!x.CustomerLastReadAt.HasValue || message.CreatedAt > x.CustomerLastReadAt.Value)))).FirstOrDefaultAsync(token);
    }

    public async Task<IReadOnlyList<PortalMessage>?> GetMessagesAsync(int ticketId, CancellationToken token = default)
    {
        if (await GetTicketAsync(ticketId, token) is null) return null;
        return await db.TicketMessages.AsNoTracking().Where(x => x.TicketId == ticketId).OrderBy(x => x.CreatedAt)
            .Select(x => new PortalMessage(x.Id, x.SenderType, x.Message, x.CreatedAt)).ToListAsync(token);
    }

    public async Task<bool> MarkTicketReadAsync(int ticketId, CancellationToken token = default)
    {
        var customer = await CustomerAsync(token); if (customer is null) return false;
        var ticket = await db.Tickets.FirstOrDefaultAsync(
            x => x.Id == ticketId && x.CustomerId == customer.Id, token);
        if (ticket is null) return false;
        ticket.CustomerLastReadAt = DateTime.UtcNow;
        await db.SaveChangesAsync(token);
        return true;
    }

    public async Task<PortalTicket?> CreateTicketAsync(string subject, string priority, string message, int? orderId, CancellationToken token = default)
    {
        var customer = await CustomerAsync(token); if (customer is null) return null;
        subject = Validate(subject, "Subject", 200); message = Validate(message, "Message", 4000);
        var allowed = new[] { "Low", "Medium", "High" }; priority = allowed.FirstOrDefault(x => x.Equals(priority?.Trim(), StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException("Priority must be Low, Medium, or High.");
        if (orderId.HasValue && !await db.Orders.AnyAsync(x => x.Id == orderId && x.CustomerId == customer.Id, token))
            throw new ArgumentException("Related order was not found.");
        var now = DateTime.UtcNow;
        var ticket = new Ticket { OrganizationId = current.OrganizationId, CustomerId = customer.Id, OrderId = orderId, Subject = subject, Priority = priority, Status = "Open", CreatedAt = now, UpdatedAt = now, CustomerLastReadAt = now };
        ticket.Messages.Add(new TicketMessage { OrganizationId = current.OrganizationId, SenderType = "Customer", Message = message, CreatedAt = now });
        db.Tickets.Add(ticket); await db.SaveChangesAsync(token);
        return new(ticket.Id, ticket.Subject, ticket.Status, ticket.Priority, ticket.CreatedAt, ticket.UpdatedAt, ticket.OrderId, false);
    }

    public async Task<PortalMessage?> AddMessageAsync(int ticketId, string message, CancellationToken token = default)
    {
        var customer = await CustomerAsync(token); if (customer is null) return null;
        var ticket = await db.Tickets.FirstOrDefaultAsync(x => x.Id == ticketId && x.CustomerId == customer.Id, token); if (ticket is null) return null;
        message = Validate(message, "Message", 4000); var now = DateTime.UtcNow;
        var item = new TicketMessage { OrganizationId = current.OrganizationId, TicketId = ticketId, SenderType = "Customer", Message = message, CreatedAt = now };
        ticket.UpdatedAt = now; ticket.CustomerLastReadAt = now; db.TicketMessages.Add(item); await db.SaveChangesAsync(token);
        return new(item.Id, item.SenderType, item.Message, item.CreatedAt);
    }

    public async Task<IReadOnlyList<PortalOrder>?> GetOrdersAsync(CancellationToken token = default)
    {
        var customer = await CustomerAsync(token); if (customer is null) return null;
        return await db.Orders.AsNoTracking().Where(x => x.CustomerId == customer.Id).OrderByDescending(x => x.CreatedAt)
            .Select(x => new PortalOrder(x.Id, x.OrderNumber, x.Status, x.TotalAmount, x.CreatedAt)).ToListAsync(token);
    }

    private static string Validate(string value, string name, int max) { if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException($"{name} is required."); value = value.Trim(); if (value.Length > max) throw new ArgumentException($"{name} must be {max} characters or fewer."); return value; }
}
