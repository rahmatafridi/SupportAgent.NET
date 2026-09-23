using SupportAgent.Core.Models;

namespace SupportAgent.Core.Interfaces;

/// <summary>
/// Persists and loads AI copilot conversation state in SQL Server.
/// </summary>
public interface IAIConversationService
{
    /// <summary>
    /// Creates a new AI conversation optionally linked to a ticket.
    /// </summary>
    Task<AIConversation> CreateConversationAsync(
        int? ticketId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads one conversation by identifier.
    /// </summary>
    Task<AIConversation?> GetConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds one message to a conversation and updates the conversation timestamp.
    /// </summary>
    Task<AIConversationMessage> AddMessageAsync(
        Guid conversationId,
        string role,
        string content,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the most recent messages for a conversation in chronological order.
    /// </summary>
    Task<IReadOnlyList<AIConversationMessage>> GetRecentMessagesAsync(
        Guid conversationId,
        int maxMessages,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Records tool execution audit entries for a conversation.
    /// </summary>
    Task AddToolAuditsAsync(
        Guid conversationId,
        IEnumerable<AIConversationToolAudit> audits,
        CancellationToken cancellationToken = default);
}
