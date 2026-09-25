using SupportAgent.Api.Authorization;
using SupportAgent.Api.Security;
using SupportAgent.Core.Interfaces;

namespace SupportAgent.Api.Endpoints;

public record CreatePortalTicketRequest(string Subject, string Priority, string Message, int? OrderId);
public record AddPortalMessageRequest(string Message);

public static class PortalEndpoints
{
    public static IEndpointRouteBuilder MapPortalEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/portal").RequireAuthorization(AuthorizationPolicies.CustomerPortal);
        group.MapGet("/me", async (ICustomerPortalService service, CancellationToken token) =>
            await service.GetProfileAsync(token) is { } profile ? Results.Ok(profile) : Results.NotFound());
        group.MapGet("/tickets", async (ICustomerPortalService service, CancellationToken token) =>
            await service.GetTicketsAsync(token) is { } tickets ? Results.Ok(tickets) : Results.NotFound());
        group.MapGet("/tickets/{id:int}", async (int id, ICustomerPortalService service, CancellationToken token) =>
            await service.GetTicketAsync(id, token) is { } ticket ? Results.Ok(ticket) : Results.NotFound());
        group.MapGet("/tickets/{id:int}/messages", async (int id, ICustomerPortalService service, CancellationToken token) =>
            await service.GetMessagesAsync(id, token) is { } messages ? Results.Ok(messages) : Results.NotFound());
        group.MapPost("/tickets/{id:int}/read", async (int id, ICustomerPortalService service, CancellationToken token) =>
            await service.MarkTicketReadAsync(id, token) ? Results.NoContent() : Results.NotFound())
            .AddEndpointFilter<AntiforgeryEndpointFilter>();
        group.MapPost("/tickets", async (CreatePortalTicketRequest request, ICustomerPortalService service, CancellationToken token) =>
        {
            try { var ticket = await service.CreateTicketAsync(request.Subject, request.Priority, request.Message, request.OrderId, token); return ticket is null ? Results.NotFound() : Results.Created($"/api/portal/tickets/{ticket.Id}", ticket); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).AddEndpointFilter<AntiforgeryEndpointFilter>();
        group.MapPost("/tickets/{id:int}/messages", async (int id, AddPortalMessageRequest request, ICustomerPortalService service, CancellationToken token) =>
        {
            try { var message = await service.AddMessageAsync(id, request.Message, token); return message is null ? Results.NotFound() : Results.Created($"/api/portal/tickets/{id}/messages/{message.Id}", message); }
            catch (ArgumentException ex) { return Results.BadRequest(new { error = ex.Message }); }
        }).AddEndpointFilter<AntiforgeryEndpointFilter>();
        group.MapGet("/orders", async (ICustomerPortalService service, CancellationToken token) =>
            await service.GetOrdersAsync(token) is { } orders ? Results.Ok(orders) : Results.NotFound());
        return app;
    }
}
