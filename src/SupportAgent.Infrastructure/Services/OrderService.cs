using Microsoft.EntityFrameworkCore;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data;

namespace SupportAgent.Infrastructure.Services;

/// <summary>
/// Order business service. Reads order records through EF Core.
/// </summary>
public class OrderService : IOrderService
{
    private readonly SupportAgentDbContext _dbContext;

    /// <summary>
    /// Creates a new order service instance.
    /// </summary>
    /// <param name="dbContext">Database context used to query orders.</param>
    public OrderService(SupportAgentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<Order?> GetOrderAsync(
        int orderId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(order => order.Id == orderId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Order>> GetOrdersByCustomerAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .Where(order => order.CustomerId == customerId)
            .OrderBy(order => order.CreatedAt)
            .ToListAsync(cancellationToken);
    }
}
