using SupportAgent.Core.Models;

namespace SupportAgent.Core.Interfaces;

/// <summary>
/// Reads customer data from the business layer. Used by API endpoints and AI tools.
/// </summary>
public interface ICustomerService
{
    /// <summary>
    /// Gets one customer by ID from the database.
    /// </summary>
    /// <param name="customerId">The customer ID to look up.</param>
    /// <param name="cancellationToken">Token used to cancel the database query.</param>
    /// <returns>The customer if found; otherwise <c>null</c>.</returns>
    Task<Customer?> GetCustomerAsync(
        int customerId,
        CancellationToken cancellationToken = default);
}
