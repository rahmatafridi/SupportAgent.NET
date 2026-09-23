using Microsoft.EntityFrameworkCore;
using SupportAgent.Core.Interfaces;
using SupportAgent.Core.Models;
using SupportAgent.Infrastructure.Data;

namespace SupportAgent.Infrastructure.Services;

/// <summary>
/// SQL Server-backed AI conversation persistence service.
/// </summary>
public class AIConversationService : IAIConversationService
{
    private readonly SupportAgentDbContext _dbContext;

    /// <summary>
    /// Creates a new conversation service instance.
    /// </summary>
    public AIConversationService(SupportAgentDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<AIConversation> CreateConversationAsync(
        int? ticketId = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var conversation = new AIConversation
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            CreatedAt = now,
            UpdatedAt = now
        };

        _dbContext.AIConversations.Add(conversation);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return conversation;
    }

    /// <inheritdoc />
    public async Task<AIConversation?> GetConversationAsync(
        Guid conversationId,
        CancellationToken cancellationToken = default)
    {
        return await _dbContext.AIConversations
            .AsNoTracking()
            .FirstOrDefaultAsync(conversation => conversation.Id == conversationId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AIConversationMessage> AddMessageAsync(
        Guid conversationId,
        string role,
        string content,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _dbContext.AIConversations
            .FirstOrDefaultAsync(item => item.Id == conversationId, cancellationToken);

        if (conversation is null)
        {
            throw new InvalidOperationException($"AI conversation '{conversationId}' was not found.");
        }

        var message = new AIConversationMessage
        {
            Id = Guid.NewGuid(),
            AIConversationId = conversationId,
            Role = role,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        conversation.UpdatedAt = message.CreatedAt;
        _dbContext.AIConversationMessages.Add(message);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return message;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AIConversationMessage>> GetRecentMessagesAsync(
        Guid conversationId,
        int maxMessages,
        CancellationToken cancellationToken = default)
    {
        var messages = await _dbContext.AIConversationMessages
            .AsNoTracking()
            .Where(message => message.AIConversationId == conversationId)
            .OrderBy(message => message.CreatedAt)
            .ToListAsync(cancellationToken);

        return messages.TakeLast(maxMessages).ToList();
    }

    /// <inheritdoc />
    public async Task AddToolAuditsAsync(
        Guid conversationId,
        IEnumerable<AIConversationToolAudit> audits,
        CancellationToken cancellationToken = default)
    {
        var auditList = audits.ToList();
        if (auditList.Count == 0)
        {
            return;
        }

        var conversation = await _dbContext.AIConversations
            .FirstOrDefaultAsync(item => item.Id == conversationId, cancellationToken);

        if (conversation is null)
        {
            throw new InvalidOperationException($"AI conversation '{conversationId}' was not found.");
        }

        foreach (var audit in auditList)
        {
            audit.Id = audit.Id == Guid.Empty ? Guid.NewGuid() : audit.Id;
            audit.AIConversationId = conversationId;
        }

        conversation.UpdatedAt = DateTime.UtcNow;
        _dbContext.AIConversationToolAudits.AddRange(auditList);
        await _dbContext.SaveChangesAsync(cancellationToken);
    }
}
