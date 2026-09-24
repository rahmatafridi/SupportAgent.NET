using SupportAgent.Core.Interfaces;

namespace SupportAgent.Api.Endpoints;

/// <summary>
/// Customer API endpoints. These call <see cref="ICustomerService"/> and do not access the database directly.
/// </summary>
public static class CustomerEndpoints
{
    /// <summary>
    /// Maps customer-related routes to the application pipeline.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same route builder so calls can be chained.</returns>
    public static IEndpointRouteBuilder MapCustomerEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/customers", async (ICustomerService customerService, CancellationToken cancellationToken) =>
            Results.Ok(await customerService.GetCustomersAsync(cancellationToken)))
            .WithName("GetCustomers")
            .RequireAuthorization()
            .WithSummary("Lists customers in the current organization.");

        // GET /api/customers/{id}
        // Looks up one customer by numeric ID through the business service layer.
        app.MapGet("/api/customers/{id:int}", async (
            int id,
            ICustomerService customerService,
            CancellationToken cancellationToken) =>
        {
            var customer = await customerService.GetCustomerAsync(id, cancellationToken);

            return customer is null
                ? Results.NotFound()
                : Results.Ok(customer);
        })
        .WithName("GetCustomer")
        .RequireAuthorization()
        .WithSummary("Gets a customer by ID.")
        .WithDescription("Returns customer details from SQL Server via ICustomerService.GetCustomerAsync.");

        return app;
    }
}
