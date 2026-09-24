using SupportAgent.Core.Interfaces;
using SupportAgent.Api.Authorization;
using SupportAgent.Api.Security;

namespace SupportAgent.Api.Endpoints;

public record CreateTicketRequest(int CustomerId, string Subject, string Priority, string Message);
public record UpdateTicketStatusRequest(string Status);
public record UpdateTicketPriorityRequest(string Priority);
public record AddTicketMessageRequest(string Message);
public record UpdateTicketAssigneeRequest(Guid? AssignedToUserId);
public record AddInternalNoteRequest(string Content);
public record TicketResponse(int Id, int CustomerId, string Subject, string Status, string Priority, DateTime CreatedAt, DateTime UpdatedAt, Guid? AssignedToUserId);
public record TicketMessageResponse(int Id, int TicketId, string SenderType, string Message, DateTime CreatedAt);

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
        app.MapPost("/api/tickets", async (CreateTicketRequest request, ITicketService ticketService, CancellationToken cancellationToken) =>
        {
            try
            {
                var ticket = await ticketService.CreateTicketAsync(request.CustomerId, request.Subject, request.Priority, request.Message, cancellationToken);
                return ticket is null ? Results.NotFound(new { error = "Customer not found." }) : Results.Created($"/api/tickets/{ticket.Id}", ToResponse(ticket));
            }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).RequireAuthorization(AuthorizationPolicies.TicketManagement).AddEndpointFilter<AntiforgeryEndpointFilter>()
          .WithName("CreateTicket").WithSummary("Creates a tenant-scoped support ticket.");

        app.MapPatch("/api/tickets/{id:int}/status", async (int id, UpdateTicketStatusRequest request, ITicketService service, CancellationToken cancellationToken) =>
        {
            try { var ticket = await service.UpdateStatusAsync(id, request.Status, cancellationToken); return ticket is null ? Results.NotFound() : Results.Ok(ToResponse(ticket)); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).RequireAuthorization(AuthorizationPolicies.TicketManagement).AddEndpointFilter<AntiforgeryEndpointFilter>()
          .WithName("UpdateTicketStatus");

        app.MapPatch("/api/tickets/{id:int}/priority", async (int id, UpdateTicketPriorityRequest request, ITicketService service, CancellationToken cancellationToken) =>
        {
            try { var ticket = await service.UpdatePriorityAsync(id, request.Priority, cancellationToken); return ticket is null ? Results.NotFound() : Results.Ok(ToResponse(ticket)); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).RequireAuthorization(AuthorizationPolicies.TicketManagement).AddEndpointFilter<AntiforgeryEndpointFilter>()
          .WithName("UpdateTicketPriority");

        app.MapPost("/api/tickets/{id:int}/messages", async (int id, AddTicketMessageRequest request, ITicketService service, CancellationToken cancellationToken) =>
        {
            try { var message = await service.AddAgentMessageAsync(id, request.Message, cancellationToken); return message is null ? Results.NotFound() : Results.Created($"/api/tickets/{id}/messages/{message.Id}", new TicketMessageResponse(message.Id, message.TicketId, message.SenderType, message.Message, message.CreatedAt)); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).RequireAuthorization(AuthorizationPolicies.TicketManagement).AddEndpointFilter<AntiforgeryEndpointFilter>()
          .WithName("AddTicketMessage");

        // GET /api/tickets
        app.MapGet("/api/tickets", async (
            string? search,
            string? status,
            string? priority,
            string? sort,
            bool? assignedToMe,
            ITicketService ticketService,
            CancellationToken cancellationToken) =>
        {
            try
            {
                var tickets = await ticketService.GetTicketsAsync(new SupportAgent.Core.Models.TicketQuery
                { Search = search, Status = status, Priority = priority, Sort = sort, AssignedToMe = assignedToMe ?? false }, cancellationToken);
                return Results.Ok(tickets);
            }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        })
        .WithName("GetTickets")
        .RequireAuthorization()
        .WithSummary("Lists support tickets.")
        .WithDescription("Returns ticket summaries with customer information for the support workspace.");

        app.MapGet("/api/tickets/summary", async (ITicketService service, CancellationToken token) =>
            Results.Ok(await service.GetSummaryAsync(token)))
            .RequireAuthorization().WithName("GetTicketSummary");

        app.MapGet("/api/tickets/assignees", async (ITicketService service, CancellationToken token) =>
            Results.Ok(await service.GetAssigneesAsync(token)))
            .RequireAuthorization().WithName("GetTicketAssignees");

        app.MapPatch("/api/tickets/{id:int}/assignee", async (int id, UpdateTicketAssigneeRequest request, ITicketService service, CancellationToken token) =>
        {
            try { var ticket = await service.UpdateAssigneeAsync(id, request.AssignedToUserId, token); return ticket is null ? Results.NotFound() : Results.Ok(ToResponse(ticket)); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).RequireAuthorization(AuthorizationPolicies.TicketManagement).AddEndpointFilter<AntiforgeryEndpointFilter>().WithName("UpdateTicketAssignee");

        app.MapPatch("/api/tickets/{id:int}/assign-to-me", async (int id, ITicketService service, CancellationToken token) =>
        {
            try { var ticket = await service.AssignToCurrentUserAsync(id, token); return ticket is null ? Results.NotFound() : Results.Ok(ToResponse(ticket)); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).RequireAuthorization(AuthorizationPolicies.TicketManagement).AddEndpointFilter<AntiforgeryEndpointFilter>().WithName("AssignTicketToMe");

        app.MapGet("/api/tickets/{id:int}/notes", async (int id, ITicketService service, CancellationToken token) =>
        {
            if (await service.GetTicketAsync(id, token) is null) return Results.NotFound();
            return Results.Ok(await service.GetInternalNotesAsync(id, token));
        }).RequireAuthorization(AuthorizationPolicies.TicketManagement).WithName("GetTicketNotes");

        app.MapPost("/api/tickets/{id:int}/notes", async (int id, AddInternalNoteRequest request, ITicketService service, CancellationToken token) =>
        {
            try { var note = await service.AddInternalNoteAsync(id, request.Content, token); return note is null ? Results.NotFound() : Results.Created($"/api/tickets/{id}/notes/{note.Id}", note); }
            catch (ArgumentException exception) { return Results.BadRequest(new { error = exception.Message }); }
        }).RequireAuthorization(AuthorizationPolicies.TicketManagement).AddEndpointFilter<AntiforgeryEndpointFilter>().WithName("AddTicketNote");

        app.MapGet("/api/tickets/{id:int}/customer-context", async (int id, ITicketService service, CancellationToken token) =>
        {
            var context = await service.GetCustomerContextAsync(id, token);
            return context is null ? Results.NotFound() : Results.Ok(context);
        }).RequireAuthorization().WithName("GetTicketCustomerContext");

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
        .RequireAuthorization()
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
        .RequireAuthorization()
        .WithSummary("Gets messages for a support ticket.")
        .WithDescription("Returns ticket messages from SQL Server via ITicketService.GetTicketMessagesAsync.");

        return app;
    }

    private static TicketResponse ToResponse(SupportAgent.Core.Models.Ticket ticket) =>
        new(ticket.Id, ticket.CustomerId, ticket.Subject, ticket.Status, ticket.Priority, ticket.CreatedAt,
            ticket.UpdatedAt, ticket.AssignedToUserId);
}
