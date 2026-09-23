using SupportAgent.Core.Interfaces;

namespace SupportAgent.Api.Endpoints;

/// <summary>
/// Support ticket API endpoints. These call <see cref="ITicketService"/> and do not access the database directly.
/// </summary>
public static class TicketEndpoints
{
    /// <summary>
    /// Maps ticket-related routes to the application pipeline.
    /// </summary>
    /// <param name="app">The endpoint route builder.</param>
    /// <returns>The same route builder so calls can be chained.</returns>
    public static IEndpointRouteBuilder MapTicketEndpoints(this IEndpointRouteBuilder app)
    {
        // GET /api/tickets
        app.MapGet("/api/tickets", async (
            ITicketService ticketService,
            CancellationToken cancellationToken) =>
        {
            var tickets = await ticketService.GetTicketsAsync(cancellationToken);
            return Results.Ok(tickets);
        })
        .WithName("GetTickets")
        .WithSummary("Lists support tickets.")
        .WithDescription("Returns ticket summaries with customer information for the support workspace.");

        // GET /api/tickets/{id}
        // Looks up one support ticket by numeric ID.
        app.MapGet("/api/tickets/{id:int}", async (
            int id,
            ITicketService ticketService,
            CancellationToken cancellationToken) =>
        {
            var ticket = await ticketService.GetTicketAsync(id, cancellationToken);

            return ticket is null
                ? Results.NotFound()
                : Results.Ok(ticket);
        })
        .WithName("GetTicket")
        .WithSummary("Gets a support ticket by ID.")
        .WithDescription("Returns ticket details from SQL Server via ITicketService.GetTicketAsync.");

        // GET /api/tickets/{id}/messages
        // Returns the conversation messages for a ticket in created-date order.
        app.MapGet("/api/tickets/{id:int}/messages", async (
            int id,
            ITicketService ticketService,
            CancellationToken cancellationToken) =>
        {
            var ticket = await ticketService.GetTicketAsync(id, cancellationToken);

            if (ticket is null)
            {
                return Results.NotFound();
            }

            var messages = await ticketService.GetTicketMessagesAsync(id, cancellationToken);
            return Results.Ok(messages);
        })
        .WithName("GetTicketMessages")
        .WithSummary("Gets messages for a support ticket.")
        .WithDescription("Returns ticket messages from SQL Server via ITicketService.GetTicketMessagesAsync.");

        return app;
    }
}
