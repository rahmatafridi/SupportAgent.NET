using SupportAgent.Core.Models;

namespace SupportAgent.Core.Interfaces;

/// <summary>
/// Reads order data from the business layer. Used by API endpoints and AI tools.
/// </summary>
public interface IOrderService
{
    /// <summary>
    /// Gets one order by ID from the database.
    /// </summary>
    /// <param name="orderId">The order ID to look up.</param>
    /// <param name="cancellationToken">Token used to cancel the database query.</param>
    /// <returns>The order if found; otherwise <c>null</c>.</returns>
    Task<Order?> GetOrderAsync(
        int orderId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all orders belonging to a customer.
    /// </summary>
    /// <param name="customerId">The customer ID whose orders should be returned.</param>
    /// <param name="cancellationToken">Token used to cancel the database query.</param>
    /// <returns>All orders for the customer ordered by created date.</returns>
    Task<IReadOnlyList<Order>> GetOrdersByCustomerAsync(
        int customerId,
        CancellationToken cancellationToken = default);
}
