using SupportAgent.Core.Interfaces;

namespace SupportAgent.Api.Endpoints;

/// <summary>
/// Order API endpoints. These call <see cref="IOrderService"/> and do not access the database directly.
/// </summary>
public static class OrderEndpoints
{
    /// <summary>
    /// Maps order-related routes to the application pipeline.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same route builder so calls can be chained.</returns>
    public static IEndpointRouteBuilder MapOrderEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/orders/{id}
        // Looks up one order by numeric ID through the business service layer.
        app.MapGet("/api/orders/{id:int}", async (
            int id,
            IOrderService orderService,
            CancellationToken cancellationToken) =>
        {
            var order = await orderService.GetOrderAsync(id, cancellationToken);

            return order is null
                ? Results.NotFound()
                : Results.Ok(order);
        })
        .WithName("GetOrder")
        .RequireAuthorization()
        .WithSummary("Gets an order by ID.")
        .WithDescription("Returns order details from SQL Server via IOrderService.GetOrderAsync.");

        // GET /api/customers/{customerId}/orders
        // Returns all orders belonging to the specified customer.
        app.MapGet("/api/customers/{customerId:int}/orders", async (
            int customerId,
            IOrderService orderService,
            CancellationToken cancellationToken) =>
        {
            var orders = await orderService.GetOrdersByCustomerAsync(customerId, cancellationToken);
            return Results.Ok(orders);
        })
        .WithName("GetOrdersByCustomer")
        .RequireAuthorization()
        .WithSummary("Gets all orders for a customer.")
        .WithDescription("Returns a list of orders for the given customer ID via IOrderService.GetOrdersByCustomerAsync.");

        return app;
    }
}
