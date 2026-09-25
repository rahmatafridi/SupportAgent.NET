using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.EntityFrameworkCore;
using SupportAgent.Core.Enums;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Core.Models.AI;
using SupportAgent.Infrastructure.AI.Tools;
using SupportAgent.Infrastructure.Copilot;
using SupportAgent.Infrastructure.Data;
using System.Text.Json;

namespace SupportAgent.Infrastructure.Services;

/// <summary>
/// Ticket-aware AI copilot service with persisted conversation state.
/// </summary>
public class CopilotService : ICopilotService
{
    private readonly ITicketService _ticketService;
    private readonly IAIConversationService _conversationService;
    private readonly IAIGateway _aiGateway;
    private readonly CopilotOptions _options;
    private readonly ILogger<CopilotService> _logger;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly SupportAgentDbContext _dbContext;

    /// <summary>
    /// Creates a new copilot service instance.
    /// </summary>
    public CopilotService(
        ITicketService ticketService,
        IAIConversationService conversationService,
        IAIGateway aiGateway,
        IOptions<CopilotOptions> options,
        ILogger<CopilotService> logger,
        IHostEnvironment hostEnvironment,
        SupportAgentDbContext dbContext)
    {
        _ticketService = ticketService;
        _conversationService = conversationService;
        _aiGateway = aiGateway;
        _options = options.Value;
        _logger = logger;
        _hostEnvironment = hostEnvironment;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<CopilotAskResult> AskAsync(
        int ticketId,
        Guid? conversationId,
        string message,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            throw new InvalidOperationException("Message is required.");
        }

        var ticket = await _ticketService.GetTicketAsync(ticketId, cancellationToken)
            ?? throw new InvalidOperationException($"Ticket '{ticketId}' was not found.");

        var ticketMessages = await _ticketService.GetTicketMessagesAsync(ticketId, cancellationToken);
        var conversation = await ResolveConversationAsync(conversationId, ticketId, cancellationToken);

        await _conversationService.AddMessageAsync(
            conversation.Id,
            "user",
            message.Trim(),
            cancellationToken);

        var history = await _conversationService.GetRecentMessagesAsync(
            conversation.Id,
            _options.MaxConversationMessages,
            cancellationToken);

        var aiRequest = BuildAskRequest(ticket, ticketMessages, history);
        var response = await _aiGateway.GenerateAsync(aiRequest, cancellationToken);
        var structured = CopilotSupportResponseParser.Parse(response.Text ?? string.Empty);
        if (structured.StructuredOutputFailed && _hostEnvironment.IsDevelopment())
        {
            _logger.LogWarning(
                "Failed to parse structured copilot response for ticket {TicketId}; a safe fallback was returned.",
                ticketId);
        }
        var answer = structured.Answer;
        var suggestedActions = await PersistSuggestedActionsAsync(
            conversation.Id, ticket, structured.SuggestedActions, cancellationToken);

        await _conversationService.AddMessageAsync(
            conversation.Id,
            "assistant",
            answer,
            cancellationToken);

        await PersistToolAuditsAsync(conversation.Id, response.ToolExecutions, cancellationToken);

        return new CopilotAskResult
        {
            Usage = response,
            ConversationId = conversation.Id,
            Answer = answer,
            SuggestedActions = suggestedActions,
            Confidence = structured.Confidence,
            ToolsUsed = response.ToolsUsed,
            Sources = response.Sources
        };
    }

    private async Task<IReadOnlyList<AISuggestedActionView>> PersistSuggestedActionsAsync(
        Guid conversationId,
        Ticket ticket,
        IReadOnlyList<ParsedSuggestedAction> suggestions,
        CancellationToken cancellationToken)
    {
        var existing = await _dbContext.AISuggestedActions
            .Where(action => action.AIConversationId == conversationId)
            .ToListAsync(cancellationToken);
        var completedActionTypes = existing
            .Where(action => action.Status == AISuggestedActionStatus.Completed && action.ExecutedAt.HasValue)
            .Select(action => action.ActionType)
            .ToHashSet();
        foreach (var action in existing.Where(action => action.Status == AISuggestedActionStatus.Pending))
            action.Status = AISuggestedActionStatus.Completed;

        var created = new List<AISuggestedAction>();
        foreach (var suggestion in suggestions)
        {
            var actionType = suggestion.ActionType == AISuggestedActionType.GetOrderStatus && !ticket.OrderId.HasValue
                ? AISuggestedActionType.GetCustomerOrders
                : suggestion.ActionType;
            if (completedActionTypes.Contains(actionType))
                continue;
            var label = GetActionLabel(actionType);

            string? trustedArguments = actionType switch
            {
                AISuggestedActionType.GetOrderStatus when ticket.OrderId.HasValue =>
                    JsonSerializer.Serialize(new { orderId = ticket.OrderId.Value }),
                AISuggestedActionType.GetOrderStatus => null,
                AISuggestedActionType.SearchKnowledgeBase =>
                    JsonSerializer.Serialize(new { query = ticket.Subject }),
                AISuggestedActionType.LinkOrder => null,
                _ => "{}"
            };
            if ((actionType is AISuggestedActionType.GetOrderStatus or AISuggestedActionType.LinkOrder) &&
                trustedArguments is null) continue;
            created.Add(new AISuggestedAction
            {
                Id = Guid.NewGuid(),
                AIConversationId = conversationId,
                TicketId = ticket.Id,
                ActionType = actionType,
                Label = label,
                Description = suggestion.Description,
                RequiresConfirmation = actionType == AISuggestedActionType.LinkOrder,
                Status = AISuggestedActionStatus.Pending,
                TrustedArgumentsJson = trustedArguments,
                CreatedAt = DateTime.UtcNow
            });
        }

        // A ticket without a linked order must not leave the human agent at a
        // dead end merely because the model omitted the safe discovery action.
        // This does not select or guess an order; it only exposes a human-clicked
        // lookup using the ticket's trusted CustomerId.
        if (!ticket.OrderId.HasValue &&
            !completedActionTypes.Contains(AISuggestedActionType.GetCustomerOrders) &&
            created.All(action => action.ActionType != AISuggestedActionType.GetCustomerOrders))
        {
            created.Add(new AISuggestedAction
            {
                Id = Guid.NewGuid(),
                AIConversationId = conversationId,
                TicketId = ticket.Id,
                ActionType = AISuggestedActionType.GetCustomerOrders,
                Label = "Check customer's recent orders",
                Description = "Review the customer's validated orders and choose which order belongs to this ticket.",
                RequiresConfirmation = false,
                Status = AISuggestedActionStatus.Pending,
                TrustedArgumentsJson = "{}",
                CreatedAt = DateTime.UtcNow
            });
        }

        _dbContext.AISuggestedActions.AddRange(created);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return created.Select(ToView).ToList();
    }

    private static AISuggestedActionView ToView(AISuggestedAction action) =>
        new(action.Id, action.Label, action.Description, action.ActionType, action.RequiresConfirmation, action.Status);

    private static string GetActionLabel(AISuggestedActionType actionType) => actionType switch
    {
        AISuggestedActionType.GetCustomerOrders => "Check customer's recent orders",
        AISuggestedActionType.GetOrderStatus => "Check current order status",
        AISuggestedActionType.SearchKnowledgeBase => "Search the knowledge base",
        AISuggestedActionType.ViewRecentTickets => "View customer's recent tickets",
        AISuggestedActionType.LinkOrder => "Link this order",
        _ => "Review suggested action"
    };

    /// <inheritdoc />
    public async Task<CopilotDraftResult> DraftReplyAsync(
        int ticketId,
        CancellationToken cancellationToken = default)
    {
        var ticket = await _ticketService.GetTicketAsync(ticketId, cancellationToken)
            ?? throw new InvalidOperationException($"Ticket '{ticketId}' was not found.");

        var ticketMessages = await _ticketService.GetTicketMessagesAsync(ticketId, cancellationToken);
        var ticketContext = TicketContextBuilder.Build(
            ticket,
            ticketMessages,
            _options.MaxTicketMessagesInContext);
        var investigationMessages = await GetLatestInvestigationMessagesAsync(ticketId, cancellationToken);

        var aiRequest = new AIRequest
        {
            SystemPrompt = BuildDraftSystemPrompt(ticketContext),
            Prompt = "Draft a professional suggested reply for the customer on this ticket.",
            Messages = investigationMessages.ToList(),
            Tools = GetToolsForTicket(ticket)
        };

        var response = await _aiGateway.GenerateAsync(aiRequest, cancellationToken);
        var draft = CopilotDraftReplyParser.Parse(response.Text ?? string.Empty);

        return new CopilotDraftResult
        {
            Usage = response,
            Draft = draft,
            ToolsUsed = response.ToolsUsed,
            Sources = response.Sources
        };
    }

    private async Task<AIConversation> ResolveConversationAsync(
        Guid? conversationId,
        int ticketId,
        CancellationToken cancellationToken)
    {
        if (conversationId is null)
        {
            return await _conversationService.CreateConversationAsync(ticketId, cancellationToken);
        }

        var conversation = await _conversationService.GetConversationAsync(conversationId.Value, cancellationToken);
        if (conversation is null)
        {
            throw new InvalidOperationException($"AI conversation '{conversationId}' was not found.");
        }

        if (conversation.TicketId.HasValue && conversation.TicketId.Value != ticketId)
        {
            throw new InvalidOperationException(
                $"AI conversation '{conversationId}' does not belong to ticket '{ticketId}'.");
        }

        return conversation;
    }

    private AIRequest BuildAskRequest(
        Ticket ticket,
        IReadOnlyList<TicketMessage> ticketMessages,
        IReadOnlyList<AIConversationMessage> history)
    {
        var limitedHistory = ConversationHistoryPolicy.ApplyLimit(history, _options);
        var ticketContext = TicketContextBuilder.Build(
            ticket,
            ticketMessages,
            _options.MaxTicketMessagesInContext);

        return new AIRequest
        {
            SystemPrompt = BuildAskSystemPrompt(ticketContext),
            Messages = limitedHistory
                .Select(message => new AIMessage
                {
                    Role = MapRole(message.Role),
                    Content = message.Content
                })
                .ToList(),
            Tools = GetToolsForTicket(ticket),
            ResponseFormatJsonSchema = SupportAgentCopilotPrompts.SupportResponseJsonSchema
        };
    }

    private async Task<IReadOnlyList<AIMessage>> GetLatestInvestigationMessagesAsync(
        int ticketId,
        CancellationToken cancellationToken)
    {
        var conversationId = await _dbContext.AIConversations
            .Where(conversation => conversation.TicketId == ticketId)
            .OrderByDescending(conversation => conversation.UpdatedAt)
            .Select(conversation => (Guid?)conversation.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (!conversationId.HasValue)
            return [];

        var messages = await _conversationService.GetRecentMessagesAsync(
            conversationId.Value,
            _options.MaxConversationMessages,
            cancellationToken);
        return ConversationHistoryPolicy.ApplyLimit(messages, _options)
            .Select(message => new AIMessage
            {
                Role = MapRole(message.Role),
                Content = message.Content
            })
            .ToList();
    }

    private static IReadOnlyList<AIToolDefinition> GetToolsForTicket(Ticket ticket) =>
        ticket.OrderId.HasValue
            ? SupportToolDefinitions.GetAll()
            : SupportToolDefinitions.GetAll()
                .Where(tool => tool.Name != SupportToolDefinitions.GetOrderStatusToolName)
                .ToList();

    private static string BuildAskSystemPrompt(string ticketContext) =>
        $"{SupportAgentCopilotPrompts.AskAssistant}\n\n{ticketContext}";

    private static string BuildDraftSystemPrompt(string ticketContext) =>
        $"{SupportAgentCopilotPrompts.DraftReplyAssistant}\n\n{ticketContext}";

    private static AIMessageRole MapRole(string role) =>
        role.Equals("assistant", StringComparison.OrdinalIgnoreCase)
            ? AIMessageRole.Assistant
            : AIMessageRole.User;

    private async Task PersistToolAuditsAsync(
        Guid conversationId,
        IReadOnlyList<AIToolExecutionRecord> executions,
        CancellationToken cancellationToken)
    {
        if (executions.Count == 0)
        {
            return;
        }

        var audits = executions.Select(execution => new AIConversationToolAudit
        {
            Id = Guid.NewGuid(),
            AIConversationId = conversationId,
            ToolName = execution.Name,
            ArgumentsJson = execution.ArgumentsJson,
            Success = execution.Success,
            CreatedAt = execution.Timestamp
        });

        await _conversationService.AddToolAuditsAsync(conversationId, audits, cancellationToken);
    }
}
