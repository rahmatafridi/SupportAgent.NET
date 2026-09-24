using Microsoft.Extensions.Options;
using SupportAgent.Core.Enums;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Core.Models.AI;
using SupportAgent.Infrastructure.AI.Tools;
using SupportAgent.Infrastructure.Copilot;

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

    /// <summary>
    /// Creates a new copilot service instance.
    /// </summary>
    public CopilotService(
        ITicketService ticketService,
        IAIConversationService conversationService,
        IAIGateway aiGateway,
        IOptions<CopilotOptions> options)
    {
        _ticketService = ticketService;
        _conversationService = conversationService;
        _aiGateway = aiGateway;
        _options = options.Value;
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
        var answer = structured.Answer;

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
            SuggestedActions = structured.SuggestedActions,
            Confidence = structured.Confidence,
            ToolsUsed = response.ToolsUsed,
            Sources = response.Sources
        };
    }

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

        var aiRequest = new AIRequest
        {
            SystemPrompt = BuildDraftSystemPrompt(ticketContext),
            Prompt = "Draft a professional suggested reply for the customer on this ticket.",
            Tools = SupportToolDefinitions.GetAll()
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
            Tools = SupportToolDefinitions.GetAll()
        };
    }

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
