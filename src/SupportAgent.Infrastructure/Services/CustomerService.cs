using Microsoft.EntityFrameworkCore;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data;

namespace SupportAgent.Infrastructure.Services;

/// <summary>
/// Customer business service. Reads customer records through EF Core.
/// </summary>
public class CustomerService : ICustomerService
{
    private readonly SupportAgentDbContext _dbContext;

    /// <summary>
    /// Creates a new customer service instance.
    /// </summary>
    /// <param name="dbContext">Database context used to query customers.</param>
    public CustomerService(SupportAgentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<Customer?> GetCustomerAsync(
        int customerId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(customer => customer.Id == customerId, cancellationToken);
    }
}
