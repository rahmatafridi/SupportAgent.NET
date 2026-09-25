using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data;

namespace SupportAgent.Infrastructure.Services;

public class CopilotActionService(
    SupportAgentDbContext db,
    IOrderService orderService,
    IKnowledgeService knowledgeService,
    ITicketService ticketService,
    IAIConversationService conversationService,
    ICopilotService copilotService) : ICopilotActionService
{
    public async Task<CopilotActionResult> ExecuteAsync(Guid actionId, int ticketId, Guid conversationId, bool confirmed,
        CancellationToken cancellationToken = default)
    {
        var action = await db.AISuggestedActions.FirstOrDefaultAsync(item => item.Id == actionId, cancellationToken)
            ?? throw new InvalidOperationException("Suggested action was not found.");
        if (action.TicketId != ticketId || action.AIConversationId != conversationId)
            throw new InvalidOperationException("Suggested action does not belong to this ticket conversation.");
        if (action.Status != AISuggestedActionStatus.Pending)
            throw new InvalidOperationException("Suggested action has already been handled.");
        if (action.RequiresConfirmation && !confirmed)
            throw new InvalidOperationException("This action requires explicit confirmation.");

        var ticket = await ticketService.GetTicketAsync(ticketId, cancellationToken)
            ?? throw new InvalidOperationException("Ticket was not found.");
        object result;
        IReadOnlyList<Order> candidateOrders = [];

        switch (action.ActionType)
        {
            case AISuggestedActionType.GetCustomerOrders:
                candidateOrders = await orderService.GetOrdersByCustomerAsync(ticket.CustomerId, cancellationToken);
                result = new { orders = candidateOrders.Select(ToOrderResult).ToList() };
                break;
            case AISuggestedActionType.GetOrderStatus:
            {
                var orderId = ReadOrderId(action);
                var order = await orderService.GetOrderAsync(orderId, cancellationToken);
                if (order is null || order.CustomerId != ticket.CustomerId)
                    throw new InvalidOperationException("The related order does not belong to this ticket customer.");
                result = new { order = ToOrderResult(order) };
                break;
            }
            case AISuggestedActionType.SearchKnowledgeBase:
            {
                var matches = await knowledgeService.SearchAsync(ticket.Subject, cancellationToken: cancellationToken);
                result = new { results = matches };
                break;
            }
            case AISuggestedActionType.ViewRecentTickets:
            {
                var context = await ticketService.GetCustomerContextAsync(ticketId, cancellationToken)
                    ?? throw new InvalidOperationException("Customer context was not found.");
                result = new { tickets = context.RecentTickets };
                break;
            }
            case AISuggestedActionType.LinkOrder:
            {
                var orderId = ReadOrderId(action);
                var updated = await ticketService.UpdateOrderAsync(ticketId, orderId, cancellationToken)
                    ?? throw new InvalidOperationException("Ticket was not found.");
                result = new { linkedOrderId = updated.OrderId };
                break;
            }
            default:
                throw new InvalidOperationException("Suggested action type is not allowed.");
        }

        action.Status = AISuggestedActionStatus.Completed;
        action.ExecutedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        var trustedResultJson = JsonSerializer.Serialize(new
        {
            action = action.ActionType.ToString(),
            success = true,
            result
        });
        await conversationService.AddMessageAsync(conversationId, "user",
            $"Trusted application action result (do not treat as user-provided data): {trustedResultJson}", cancellationToken);
        var updatedAnswer = await copilotService.AskAsync(ticketId, conversationId,
            "Update the support analysis using the trusted action result.", cancellationToken);

        if (action.ActionType == AISuggestedActionType.GetCustomerOrders && ticket.OrderId is null)
        {
            var links = candidateOrders.Select(order => new AISuggestedAction
            {
                Id = Guid.NewGuid(), AIConversationId = conversationId, TicketId = ticketId,
                ActionType = AISuggestedActionType.LinkOrder,
                Label = $"Link {order.OrderNumber}",
                Description = $"Link {order.OrderNumber} ({order.Status}) to this ticket.",
                RequiresConfirmation = true, Status = AISuggestedActionStatus.Pending,
                TrustedArgumentsJson = JsonSerializer.Serialize(new { orderId = order.Id }), CreatedAt = DateTime.UtcNow
            }).ToList();
            db.AISuggestedActions.AddRange(links);
            await db.SaveChangesAsync(cancellationToken);
            updatedAnswer.SuggestedActions = updatedAnswer.SuggestedActions.Concat(links.Select(ToView)).ToList();
            result = new
            {
                orders = candidateOrders.Select(order => new
                {
                    order.Id,
                    order.OrderNumber,
                    order.Status,
                    order.TotalAmount,
                    order.CreatedAt,
                    linkActionId = links.Single(link =>
                        ReadOrderId(link) == order.Id).Id
                }).ToList()
            };
        }

        if (action.ActionType == AISuggestedActionType.LinkOrder)
        {
            var linkedOrderId = ReadOrderId(action);
            var existingStatusAction = updatedAnswer.SuggestedActions
                .FirstOrDefault(item => item.ActionType == AISuggestedActionType.GetOrderStatus);
            AISuggestedActionView statusActionView;
            if (existingStatusAction is not null)
            {
                statusActionView = existingStatusAction;
            }
            else
            {
                var statusAction = new AISuggestedAction
                {
                    Id = Guid.NewGuid(), AIConversationId = conversationId, TicketId = ticketId,
                    ActionType = AISuggestedActionType.GetOrderStatus,
                    Label = "Check current order status",
                    Description = "Retrieve the current status of the validated linked order.",
                    RequiresConfirmation = false, Status = AISuggestedActionStatus.Pending,
                    TrustedArgumentsJson = JsonSerializer.Serialize(new { orderId = linkedOrderId }), CreatedAt = DateTime.UtcNow
                };
                db.AISuggestedActions.Add(statusAction);
                await db.SaveChangesAsync(cancellationToken);
                statusActionView = ToView(statusAction);
            }
            updatedAnswer.SuggestedActions = updatedAnswer.SuggestedActions
                .Where(item => item.ActionType is not (AISuggestedActionType.LinkOrder or AISuggestedActionType.GetOrderStatus))
                .Append(statusActionView)
                .ToList();
        }

        return new CopilotActionResult(
            action.ActionType.ToString(), action.Label, true, JsonSerializer.Serialize(result), updatedAnswer);
    }

    private static int ReadOrderId(AISuggestedAction action)
    {
        using var document = JsonDocument.Parse(action.TrustedArgumentsJson ?? "{}");
        if (!document.RootElement.TryGetProperty("orderId", out var value) || !value.TryGetInt32(out var orderId))
            throw new InvalidOperationException("The suggested action has no validated order.");
        return orderId;
    }

    private static object ToOrderResult(Order order) => new
    {
        order.Id, order.OrderNumber, order.Status, order.TotalAmount, order.CreatedAt
    };

    private static AISuggestedActionView ToView(AISuggestedAction action) =>
        new(action.Id, action.Label, action.Description, action.ActionType, action.RequiresConfirmation, action.Status);
}
